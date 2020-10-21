// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Opc.Ua;
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

                CommandLineArgumentsParser.Parse(args);
                                
                // allow cancelling the application
                var quitEvent = new ManualResetEvent(false);
                try
                {
                    Console.CancelKeyPress += (sender, eArgs) =>
                    {
                        quitEvent.Set();
                        eArgs.Cancel = true;
                        ShutdownTokenSource.Cancel();
                    };
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "waiting for cancel key pressed causing error");
                }

                // init diagnostics
                _diag.Init();

                // init telemetry config
                _telemetryConfig.Init();

                // init node config
                _nodeConfig.Init();

                // init OPC configuration and tracing
                OpcApplicationConfiguration opcApplicationConfiguration = new OpcApplicationConfiguration();
                await opcApplicationConfiguration.ConfigureAsync().ConfigureAwait(false);

                // log shopfloor site setting
                if (string.IsNullOrEmpty(OpcUaSessionManager.PublisherSite))
                {
                    Logger.Information("There is no site configured.");
                }
                else
                {
                    Logger.Information($"Publisher is in site '{OpcUaSessionManager.PublisherSite}'.");
                }

                // start our server interface
                try
                {
                    Logger.Information($"Starting server on endpoint {OpcApplicationConfiguration.ApplicationConfiguration.ServerConfiguration.BaseAddresses[0].ToString(CultureInfo.InvariantCulture)} ...");
                    _publisherServer.Start(OpcApplicationConfiguration.ApplicationConfiguration);
                    Logger.Information("Server started.");
                }
                catch (Exception e)
                {
                    Logger.Fatal(e, "Failed to start Publisher OPC UA server.");
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
                Logger.Information("");
                Logger.Information("");
                if (SettingsConfiguration.NoShutdown)
                {
                    // wait forever if asked to do so
                    Logger.Information("Publisher is running infinite...");
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
                await SessionShutdownAsync().ConfigureAwait(false);

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

            // shutdown diagnostics
            _diag.Close();
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
        /// Shutdown all sessions.
        /// </summary>
        public async Task SessionShutdownAsync()
        {
            try
            {
                while (_nodeConfig.OpcSessions.Count > 0)
                {
                    OpcUaSessionManager opcSession = null;
                    try
                    {
                        await _nodeConfig.OpcSessionsListSemaphore.WaitAsync().ConfigureAwait(false);
                        opcSession = _nodeConfig.OpcSessions.ElementAt(0);
                        _nodeConfig.OpcSessions.RemoveAt(0);
                    }
                    finally
                    {
                        _nodeConfig.OpcSessionsListSemaphore.Release();
                    }
                    await (opcSession?.ShutdownAsync()).ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                Logger.Fatal(e, "Failed to shutdown all sessions.");
            }

            // Wait and continue after a while.
            uint maxTries = SettingsConfiguration.PublisherShutdownWaitPeriod;
            while (true)
            {
                int sessionCount = _nodeConfig.OpcSessions.Count;
                if (sessionCount == 0)
                {
                    return;
                }
                if (maxTries-- == 0)
                {
                    Logger.Information($"There are still {sessionCount} sessions alive. Ignore and continue shutdown.");
                    return;
                }
                Logger.Information($"Publisher is shutting down. Wait {OpcUaSessionManager.SessionConnectWaitSec} seconds, since there are stil {sessionCount} sessions alive...");
                await Task.Delay(OpcUaSessionManager.SessionConnectWaitSec * 1000).ConfigureAwait(false);
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
                    OpcApplicationConfiguration.OpcTraceToLoggerFatal = 0;
                    break;
                case "error":
                    loggerConfiguration.MinimumLevel.Error();
                    OpcApplicationConfiguration.OpcStackTraceMask = OpcApplicationConfiguration.OpcTraceToLoggerError = Utils.TraceMasks.Error;
                    break;
                case "warn":
                    loggerConfiguration.MinimumLevel.Warning();
                    OpcApplicationConfiguration.OpcTraceToLoggerWarning = 0;
                    break;
                case "info":
                    loggerConfiguration.MinimumLevel.Information();
                    OpcApplicationConfiguration.OpcStackTraceMask = OpcApplicationConfiguration.OpcTraceToLoggerInformation = 0;
                    break;
                case "debug":
                    loggerConfiguration.MinimumLevel.Debug();
                    OpcApplicationConfiguration.OpcStackTraceMask = OpcApplicationConfiguration.OpcTraceToLoggerDebug = Utils.TraceMasks.StackTrace | Utils.TraceMasks.Operation |
                        Utils.TraceMasks.StartStop | Utils.TraceMasks.ExternalSystem | Utils.TraceMasks.Security;
                    break;
                case "verbose":
                    loggerConfiguration.MinimumLevel.Verbose();
                    OpcApplicationConfiguration.OpcStackTraceMask = OpcApplicationConfiguration.OpcTraceToLoggerVerbose = Utils.TraceMasks.All;
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

        public HubClientWrapper _clientWrapper = new HubClientWrapper(); //TODO: make private
        public PublisherDiagnostics _diag = new PublisherDiagnostics(); //TODO: make private
        public PublisherTelemetryConfiguration _telemetryConfig = new PublisherTelemetryConfiguration(); //TODO: make private
        public PublisherNodeConfiguration _nodeConfig = new PublisherNodeConfiguration(); //TODO: make private
    }
}
