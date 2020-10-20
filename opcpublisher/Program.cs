// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Opc.Ua;
using Opc.Ua.Server;
using OpcPublisher.Configurations;
using OpcPublisher.Interfaces;
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
        /// run foroever flag
        /// </summary>
        public static bool NoShutdown = false;

        /// <summary>
        /// interval for flushing log file
        /// </summary>
        public static TimeSpan LogFileFlushTimeSpanSec = TimeSpan.FromSeconds(30);

        public static string DeviceConnectionString { get; set; } = null;

        /// <summary>
        /// Telemetry configuration object.
        /// </summary>
        public static PublisherTelemetryConfiguration TelemetryConfiguration { get; set; }

        /// <summary>
        /// Node configuration object.
        /// </summary>
        public static IPublisherNodeConfiguration NodeConfiguration { get; set; }

        /// <summary>
        /// Diagnostics object.
        /// </summary>
        public static PublisherDiagnostics Diag { get; set; }

        /// <summary>
        /// Shutdown token source.
        /// </summary>
        public static CancellationTokenSource ShutdownTokenSource { get; set; } = new CancellationTokenSource();

        /// <summary>
        /// Used as delay in sec when shutting down the application.
        /// </summary>
        public static uint PublisherShutdownWaitPeriod { get; } = 10;

        /// <summary>
        /// Stores startup time.
        /// </summary>
        public static DateTime PublisherStartTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Logging object.
        /// </summary>
        public static Serilog.Core.Logger Logger { get; set; } = null;

        /// <summary>
        /// Signal for completed startup.
        /// </summary>
        public static bool StartupCompleted { get; set; } = false;

        /// <summary>
        /// Name of the log file.
        /// </summary>
        public static string LogFileName { get; set; } = $"{Utils.GetHostName()}-publisher.log";

        /// <summary>
        /// Log level.
        /// </summary>
        public static string LogLevel { get; set; } = "info";

        /// <summary>
        /// Flag indicating if we are running in an IoT Edge context
        /// </summary>
        public static bool RunningInIoTEdgeContext = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IOTEDGE_IOTHUBHOSTNAME")) &&
                                                     !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IOTEDGE_MODULEGENERATIONID")) &&
                                                     !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IOTEDGE_WORKLOADURI")) &&
                                                     !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IOTEDGE_DEVICEID")) &&
                                                     !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IOTEDGE_MODULEID"));

        /// <summary>
        /// Synchronous main method of the app.
        /// </summary>
        public static void Main(string[] args)
        {
            if (RunningInIoTEdgeContext)
            {
                var waitForDebugger = args.Any(a => a.ToLower().Contains("wfd") || a.ToLower().Contains("waitfordebugger"));

                if (waitForDebugger)
                {
                    Console.WriteLine("Waiting for debugger to attach...");

                    while (!Debugger.IsAttached)
                    {
                        Thread.Sleep(1000);
                    }

                    Console.WriteLine("Debugger attached.");
                }
            }

            MainAsync(args).Wait();
        }

        /// <summary>
        /// Asynchronous part of the main method of the app.
        /// </summary>
        public static async Task MainAsync(string[] args)
        {
            try
            {
                // initialize logging
                InitLogging();

                // show version
                Program.Logger.Information($"OPC Publisher V{FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion} starting up...");
                
                // detect the runtime environment. either we run standalone (native or containerized) or as IoT Edge module (containerized)
                // check if we have an environment variable containing an IoT Edge connectionstring, we run as IoT Edge module
                if (RunningInIoTEdgeContext)
                {
                    Console.WriteLine("IoTEdge detected.");
                }

                CommandLineArgumentsConfiguration.Parse(args);
                                
                // allow canceling the application
                var quitEvent = new ManualResetEvent(false);
                try
                {
                    Console.CancelKeyPress += (sender, eArgs) =>
                    {
                        quitEvent.Set();
                        eArgs.Cancel = true;
                        Program.ShutdownTokenSource.Cancel();
                    };
                }
                catch (Exception ex)
                {
                    Program.Logger.Error(ex, "waiting for cancel key pressed causing error");
                }

                // init OPC configuration and tracing
                OpcApplicationConfiguration opcApplicationConfiguration = new OpcApplicationConfiguration();
                await opcApplicationConfiguration.ConfigureAsync().ConfigureAwait(false);

                // log shopfloor site setting
                if (string.IsNullOrEmpty(OpcUaSessionManager.PublisherSite))
                {
                    Program.Logger.Information("There is no site configured.");
                }
                else
                {
                    Program.Logger.Information($"Publisher is in site '{OpcUaSessionManager.PublisherSite}'.");
                }

                // start our server interface
                try
                {
                    Program.Logger.Information($"Starting server on endpoint {OpcApplicationConfiguration.ApplicationConfiguration.ServerConfiguration.BaseAddresses[0].ToString(CultureInfo.InvariantCulture)} ...");
                    _publisherServer.Start(OpcApplicationConfiguration.ApplicationConfiguration);
                    Program.Logger.Information("Server started.");
                }
                catch (Exception e)
                {
                    Program.Logger.Fatal(e, "Failed to start Publisher OPC UA server.");
                    Program.Logger.Fatal("exiting...");
                    return;
                }

                // initialize the telemetry configuration
                TelemetryConfiguration = PublisherTelemetryConfiguration.Instance;

                // initialize the node configuration
                NodeConfiguration = PublisherNodeConfiguration.Instance;


                // initialize and start EdgeHub communication
                _clientWrapper.InitHubCommunication(RunningInIoTEdgeContext, DeviceConnectionString);
                
                // initialize message processing
                _clientWrapper.InitMessageProcessing();

                // kick off OPC session creation and node monitoring
                await SessionStartAsync().ConfigureAwait(false);

                // Show notification on session events
                _publisherServer.CurrentInstance.SessionManager.SessionActivated += ServerEventStatus;
                _publisherServer.CurrentInstance.SessionManager.SessionClosing += ServerEventStatus;
                _publisherServer.CurrentInstance.SessionManager.SessionCreated += ServerEventStatus;

                // startup completed
                StartupCompleted = true;

                // stop on user request
                Program.Logger.Information("");
                Program.Logger.Information("");
                if (NoShutdown)
                {
                    // wait forever if asked to do so
                    Program.Logger.Information("Publisher is running infinite...");
                    await Task.Delay(Timeout.Infinite).ConfigureAwait(false);
                }
                else
                {
                    Program.Logger.Information("Publisher is running. Press CTRL-C to quit.");

                    // wait for Ctrl-C
                    await Task.Delay(Timeout.Infinite, Program.ShutdownTokenSource.Token).ConfigureAwait(false);
                }

                Program.Logger.Information("");
                Program.Logger.Information("");
                Program.ShutdownTokenSource.Cancel();
                Program.Logger.Information("Publisher is shutting down...");

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
                Program.Logger.Fatal(e, e.StackTrace);
                e = e.InnerException ?? null;
                while (e != null)
                {
                    Program.Logger.Fatal(e, e.StackTrace);
                    e = e.InnerException ?? null;
                }
                Program.Logger.Fatal("Publisher exiting... ");
            }

            // shutdown diagnostics
            Program.Diag.Dispose();
            Diag = null;
        }

        /// <summary>
        /// Start all sessions.
        /// </summary>
        public static async Task SessionStartAsync()
        {
            try
            {
                await Program.NodeConfiguration.OpcSessionsListSemaphore.WaitAsync().ConfigureAwait(false);
                Program.NodeConfiguration.OpcSessions.ForEach(s => s.ConnectAndMonitorSession.Set());
            }
            catch (Exception e)
            {
                Program.Logger.Fatal(e, "Failed to start all sessions.");
            }
            finally
            {
                Program.NodeConfiguration.OpcSessionsListSemaphore.Release();
            }
        }

        /// <summary>
        /// Shutdown all sessions.
        /// </summary>
        public static async Task SessionShutdownAsync()
        {
            try
            {
                while (Program.NodeConfiguration.OpcSessions.Count > 0)
                {
                    OpcUaSessionManager opcSession = null;
                    try
                    {
                        await Program.NodeConfiguration.OpcSessionsListSemaphore.WaitAsync().ConfigureAwait(false);
                        opcSession = Program.NodeConfiguration.OpcSessions.ElementAt(0);
                        Program.NodeConfiguration.OpcSessions.RemoveAt(0);
                    }
                    finally
                    {
                        Program.NodeConfiguration.OpcSessionsListSemaphore.Release();
                    }
                    await (opcSession?.ShutdownAsync()).ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                Program.Logger.Fatal(e, "Failed to shutdown all sessions.");
            }

            // Wait and continue after a while.
            uint maxTries = PublisherShutdownWaitPeriod;
            while (true)
            {
                int sessionCount = Program.NodeConfiguration.OpcSessions.Count;
                if (sessionCount == 0)
                {
                    return;
                }
                if (maxTries-- == 0)
                {
                    Program.Logger.Information($"There are still {sessionCount} sessions alive. Ignore and continue shutdown.");
                    return;
                }
                Program.Logger.Information($"Publisher is shutting down. Wait {OpcUaSessionManager.SessionConnectWaitSec} seconds, since there are stil {sessionCount} sessions alive...");
                await Task.Delay(OpcUaSessionManager.SessionConnectWaitSec * 1000).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Handler for server status changes.
        /// </summary>
        private static void ServerEventStatus(Session session, SessionEventReason reason)
        {
            PrintSessionStatus(session, reason.ToString());
        }

        /// <summary>
        /// Shows the session status.
        /// </summary>
        private static void PrintSessionStatus(Session session, string reason)
        {
            lock (session.DiagnosticsLock)
            {
                string item = string.Format(CultureInfo.InvariantCulture, "{0,9}:{1,20}:", reason, session.SessionDiagnostics.SessionName);
                if (session.Identity != null)
                {
                    item += string.Format(CultureInfo.InvariantCulture, ":{0,20}", session.Identity.DisplayName);
                }
                item += string.Format(CultureInfo.InvariantCulture, ":{0}", session.Id);
                Program.Logger.Information(item);
            }
        }

        /// <summary>
        /// Initialize logging.
        /// </summary>
        public static void InitLogging()
        {
            LoggerConfiguration loggerConfiguration = new LoggerConfiguration();

            // set the log level
            switch (LogLevel)
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

            // enable remote logging not in any case for perf reasons
            if (PublisherDiagnostics.DiagnosticsInterval >= 0)
            {
                loggerConfiguration.WriteTo.DiagnosticLogSink();
            }

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("_GW_LOGP")))
            {
                LogFileName = Environment.GetEnvironmentVariable("_GW_LOGP");
            }

            if (!string.IsNullOrEmpty(LogFileName))
            {
                // configure rolling file sink
                const int MAX_LOGFILE_SIZE = 1024 * 1024;
                const int MAX_RETAINED_LOGFILES = 2;
                loggerConfiguration.WriteTo.File(LogFileName, fileSizeLimitBytes: MAX_LOGFILE_SIZE, flushToDiskInterval: LogFileFlushTimeSpanSec, rollOnFileSizeLimit: true, retainedFileCountLimit: MAX_RETAINED_LOGFILES);
            }

            // initialize publisher diagnostics
            Diag = PublisherDiagnostics.Instance;

            Logger = loggerConfiguration.CreateLogger();
            Program.Logger.Information($"Current directory is: {System.IO.Directory.GetCurrentDirectory()}");
            Program.Logger.Information($"Log file is: {LogFileName}");
            Program.Logger.Information($"Log level is: {LogLevel}");
            return;
        }
               
        private static PublisherServer _publisherServer = new PublisherServer();
        public static HubClientWrapper _clientWrapper = new HubClientWrapper(); //TODO: make private
    }
}
