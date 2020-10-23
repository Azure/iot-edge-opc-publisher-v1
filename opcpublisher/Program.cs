// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Opc.Ua;
using Opc.Ua.Configuration;
using Opc.Ua.Server;
using OpcPublisher.Configurations;
using Serilog;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace OpcPublisher
{
    public sealed class Program
    {
        /// <summary>
        /// Shutdown token source.
        /// </summary>
        public CancellationTokenSource ShutdownTokenSource { get; set; } = new CancellationTokenSource();

        /// <summary>
        /// Logging object.
        /// </summary>
        public Serilog.Core.Logger Logger { get; set; } = null;

        /// <summary>
        /// App instance
        /// </summary>
        public static Program Instance = new Program();

        /// <summary>
        /// App entry point
        /// </summary>
        public static void Main(string[] args) => Instance.MainAsync(args).Wait();

        /// <summary>
        /// Asynchronous part of the main method of the app.
        /// </summary>
        public async Task MainAsync(string[] args)
        {
            try
            {
                // init logging
                InitLogging();

                // show version
                Logger.Information($"OPC Publisher V{FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion} starting up...");
                
                // detect the runtime environment. either we run standalone (native or containerized) or as IoT Edge module (containerized)
                // check if we have an environment variable containing an IoT Edge connectionstring, we run as IoT Edge module
                if (SettingsConfiguration.RunningInIoTEdgeContext)
                {
                    Console.WriteLine("IoTEdge detected.");
                }

                // parse command line
                CommandLineArgumentsParser parser = new CommandLineArgumentsParser();
                parser.Parse(args);

                _application.LoadApplicationConfiguration(false).Wait();

                Utils.Tracing.TraceEventHandler += new EventHandler<TraceEventArgs>(LoggerOpcUaTraceHandler);
                Logger.Information($"opcstacktracemask set to: 0x{_application.ApplicationConfiguration.TraceConfiguration.TraceMasks:X}");

                foreach (var endpoint in _application.ApplicationConfiguration.ServerConfiguration.BaseAddresses)
                {
                    Logger.Information($"OPC UA server base address: {endpoint}");
                }

                // check the application certificate.
                bool certOK = _application.CheckApplicationInstanceCertificate(false, 0).Result;
                if (!certOK)
                {
                    throw new Exception("Application instance certificate invalid!");
                }

                Logger.Information($"Trusted Issuer store type is: {_application.ApplicationConfiguration.SecurityConfiguration.TrustedIssuerCertificates.StoreType}");
                Logger.Information($"Trusted Issuer Certificate store path is: {_application.ApplicationConfiguration.SecurityConfiguration.TrustedIssuerCertificates.StorePath}");

                Logger.Information($"Trusted Peer Certificate store type is: {_application.ApplicationConfiguration.SecurityConfiguration.TrustedPeerCertificates.StoreType}");
                Logger.Information($"Trusted Peer Certificate store path is: {_application.ApplicationConfiguration.SecurityConfiguration.TrustedPeerCertificates.StorePath}");

                Logger.Information($"Rejected certificate store type is: {_application.ApplicationConfiguration.SecurityConfiguration.RejectedCertificateStore.StoreType}");
                Logger.Information($"Rejected Certificate store path is: {_application.ApplicationConfiguration.SecurityConfiguration.RejectedCertificateStore.StorePath}");

                Logger.Information($"Rejection of SHA1 signed certificates is {(_application.ApplicationConfiguration.SecurityConfiguration.RejectSHA1SignedCertificates ? "enabled" : "disabled")}");
                Logger.Information($"Minimum certificate key size set to {_application.ApplicationConfiguration.SecurityConfiguration.MinimumCertificateKeySize}");

                Logger.Information($"Application Certificate store type is: {_application.ApplicationConfiguration.SecurityConfiguration.ApplicationCertificate.StoreType}");
                Logger.Information($"Application Certificate store path is: {_application.ApplicationConfiguration.SecurityConfiguration.ApplicationCertificate.StorePath}");
                Logger.Information($"Application Certificate subject name is: {_application.ApplicationConfiguration.SecurityConfiguration.ApplicationCertificate.SubjectName}");

                // handle cert validation
                if (SettingsConfiguration.AutoAcceptCerts)
                {
                    Logger.Warning("WARNING: Automatically accepting certificates. This is a security risk.");
                    _application.ApplicationConfiguration.SecurityConfiguration.AutoAcceptUntrustedCertificates = true;
                }
                
                // create cert validator
                _application.ApplicationConfiguration.CertificateValidator = new CertificateValidator();
                _application.ApplicationConfiguration.CertificateValidator.CertificateValidation += new CertificateValidationEventHandler(CertificateValidator_CertificateValidation);

                // init diagnostics
                _diag.Init();

                // init node config
                _nodeConfig.Init();

                // log shopfloor site setting
                if (string.IsNullOrEmpty(SettingsConfiguration.PublisherSite))
                {
                    Logger.Information("There is no site configured.");
                }
                else
                {
                    Logger.Information($"Publisher is in site '{SettingsConfiguration.PublisherSite}'.");
                }

                // start our server interface
                try
                {
                    Logger.Information($"Starting server on endpoint {_application.ApplicationConfiguration.ServerConfiguration.BaseAddresses[0].ToString(CultureInfo.InvariantCulture)} ...");
                    _publisherServer.Start(_application.ApplicationConfiguration);
                    Logger.Information("Server started.");
                }
                catch (Exception e)
                {
                    Logger.Fatal(e, "Failed to start OPC Publisher OPC UA server.");
                    Logger.Fatal("exiting...");
                    return;
                }

                // initialize and start EdgeHub communication
                _clientWrapper.InitHubCommunication(SettingsConfiguration.RunningInIoTEdgeContext, SettingsConfiguration.DeviceConnectionString);
                
                // initialize message processing
                _clientWrapper.InitMessageProcessing();

                // kick off OPC session creation and node monitoring
                await SessionStartAsync().ConfigureAwait(false);

                // Show notification on session events
                _publisherServer.CurrentInstance.SessionManager.SessionActivated += ServerEventStatus;
                _publisherServer.CurrentInstance.SessionManager.SessionClosing += ServerEventStatus;
                _publisherServer.CurrentInstance.SessionManager.SessionCreated += ServerEventStatus;

                // startup completed
                PublisherDiagnostics.StartupCompleted = true;

                // stop on user request
                if (SettingsConfiguration.NoShutdown)
                {
                    // wait forever if asked to do so
                    Logger.Information("Publisher is running forever...");
                    await Task.Delay(Timeout.Infinite).ConfigureAwait(false);
                }
                else
                {
                    Logger.Information("Publisher is running. Press CTRL-C to quit.");

                    // wait for Ctrl-C
                    await Task.Delay(Timeout.Infinite, ShutdownTokenSource.Token).ConfigureAwait(false);
                }

                Logger.Information("");
                Logger.Information("");
                ShutdownTokenSource.Cancel();
                Logger.Information("Publisher is shutting down...");

                // stop the server
                _publisherServer.Stop();

                // shutdown all OPC sessions
                _nodeConfig.Close();

                // shutdown the IoTHub messaging
                _clientWrapper.Close();

                // free resources
                ShutdownTokenSource = null;
            }
            catch (Exception e)
            {
                Logger.Fatal(e, e.StackTrace);
                e = e.InnerException ?? null;
                while (e != null)
                {
                    Logger.Fatal(e, e.StackTrace);
                    e = e.InnerException ?? null;
                }
                Logger.Fatal("Publisher exiting... ");
            }
        }

        /// <summary>
        /// Event handler to validate certificates.
        /// </summary>
        private static void CertificateValidator_CertificateValidation(Opc.Ua.CertificateValidator validator, Opc.Ua.CertificateValidationEventArgs e)
        {
            if (e.Error.StatusCode == StatusCodes.BadCertificateUntrusted)
            {
                // always accept all OPC UA server certificates for our OPC UA client
                Program.Instance.Logger.Information("Automatically trusting server certificate " + e.Certificate.Subject);
                e.Accept = true;
            }
        }

        /// <summary>
        /// Start all sessions.
        /// </summary>
        public async Task SessionStartAsync()
        {
            try
            {
                await _nodeConfig.OpcSessionsListSemaphore.WaitAsync().ConfigureAwait(false);
                _nodeConfig.OpcSessions.ForEach(s => s.ConnectAndMonitorSession.Set());
            }
            catch (Exception e)
            {
                Logger.Fatal(e, "Failed to start all sessions.");
            }
            finally
            {
                _nodeConfig.OpcSessionsListSemaphore.Release();
            }
        }

        /// <summary>
        /// Handler for server status changes.
        /// </summary>
        private void ServerEventStatus(Session session, SessionEventReason reason) => PrintSessionStatus(session, reason.ToString());

        /// <summary>
        /// Shows the session status.
        /// </summary>
        private void PrintSessionStatus(Session session, string reason)
        {
            lock (session.DiagnosticsLock)
            {
                string item = string.Format(CultureInfo.InvariantCulture, "{0,9}:{1,20}:", reason, session.SessionDiagnostics.SessionName);
                if (session.Identity != null)
                {
                    item += string.Format(CultureInfo.InvariantCulture, ":{0,20}", session.Identity.DisplayName);
                }
                item += string.Format(CultureInfo.InvariantCulture, ":{0}", session.Id);
                Logger.Information(item);
            }
        }
        
        /// <summary>
        /// Event handler to log OPC UA stack trace messages into own Program.Instance.Logger.
        /// </summary>
        private void LoggerOpcUaTraceHandler(object sender, TraceEventArgs e)
        {
            if ((e.TraceMask & _application.ApplicationConfiguration.TraceConfiguration.TraceMasks) != 0)
            {
                if (e.Arguments != null)
                {
                    Logger.Information("OPC UA Stack: " + string.Format(CultureInfo.InvariantCulture, e.Format, e.Arguments).Trim());
                }
                else
                {
                    Logger.Information("OPC UA Stack: " + e.Format.Trim());
                }
            }
        }

        /// <summary>
        /// Initialize logging.
        /// </summary>
        public void InitLogging()
        {
            LoggerConfiguration loggerConfiguration = new LoggerConfiguration();

            // set the log level
            switch (SettingsConfiguration.LogLevel)
            {
                case "fatal":
                    loggerConfiguration.MinimumLevel.Fatal();
                    break;
                case "error":
                    loggerConfiguration.MinimumLevel.Error();
                    break;
                case "warn":
                    loggerConfiguration.MinimumLevel.Warning();
                    break;
                case "info":
                    loggerConfiguration.MinimumLevel.Information();
                    break;
                case "debug":
                    loggerConfiguration.MinimumLevel.Debug();
                    break;
                case "verbose":
                    loggerConfiguration.MinimumLevel.Verbose();
                    break;
            }

            // set logging sinks
            loggerConfiguration.WriteTo.Console();

            // enable remote logging
            if (SettingsConfiguration.DiagnosticsInterval >= 0)
            {
                loggerConfiguration.WriteTo.DiagnosticLogSink();
            }

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("_GW_LOGP")))
            {
                SettingsConfiguration.LogFileName = Environment.GetEnvironmentVariable("_GW_LOGP");
            }

            if (!string.IsNullOrEmpty(SettingsConfiguration.LogFileName))
            {
                // configure rolling file sink
                const int MAX_LOGFILE_SIZE = 1024 * 1024;
                const int MAX_RETAINED_LOGFILES = 2;
                loggerConfiguration.WriteTo.File(SettingsConfiguration.LogFileName, fileSizeLimitBytes: MAX_LOGFILE_SIZE, flushToDiskInterval: SettingsConfiguration.LogFileFlushTimeSpanSec, rollOnFileSizeLimit: true, retainedFileCountLimit: MAX_RETAINED_LOGFILES);
            }

            Logger = loggerConfiguration.CreateLogger();
            Logger.Information($"Current directory is: {System.IO.Directory.GetCurrentDirectory()}");
            Logger.Information($"Log file is: {SettingsConfiguration.LogFileName}");
            Logger.Information($"Log level is: {SettingsConfiguration.LogLevel}");
        }
               
        private PublisherServer _publisherServer = new PublisherServer();

        public ApplicationInstance _application = new ApplicationInstance {
            ApplicationName = "OpcPublisher",
            ApplicationType = ApplicationType.ClientAndServer,
            ConfigSectionName = "Opc.Publisher"
        }; //TODO: make private

        private HubClientWrapper _clientWrapper = new HubClientWrapper();

        public PublisherDiagnostics _diag = new PublisherDiagnostics(); //TODO: make private
        public PublishedNodesConfiguration _nodeConfig = new PublishedNodesConfiguration(); //TODO: make private
    }
}
