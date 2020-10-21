// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using OpcPublisher.Configurations;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace OpcPublisher
{
    /// <summary>
    /// Class to enable output to the console.
    /// </summary>
    public class PublisherDiagnostics
    {
        /// <summary>
        /// Stores startup time.
        /// </summary>
        public static DateTime PublisherStartTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Signal for completed startup.
        /// </summary>
        public static bool StartupCompleted { get; set; } = false;

        /// <summary>
        /// Number of events in the monitored items queue.
        /// </summary>
        public static long MonitoredItemsQueueCount = 0;

        /// <summary>
        /// Number of events we enqueued.
        /// </summary>
        public static long EnqueueCount = 0;

        /// <summary>
        /// Number of times enqueueing of events failed.
        /// </summary>
        public static long EnqueueFailureCount = 0;

        /// <summary>
        /// Number of events sent to the cloud.
        /// </summary>
        public static long NumberOfEvents { get; set; }

        /// <summary>
        /// Number of times we were not able to make the send interval, because too high load.
        /// </summary>
        public static long MissedSendIntervalCount { get; set; }

        /// <summary>
        /// Number of times the isze fo the event payload was too large for a telemetry message.
        /// </summary>
        public static long TooLargeCount { get; set; }

        /// <summary>
        /// Number of payload bytes we sent to the cloud.
        /// </summary>
        public static long SentBytes { get; set; }

        /// <summary>
        /// Number of messages we sent to the cloud.
        /// </summary>
        public static long SentMessages { get; set; }

        /// <summary>
        /// Time when we sent the last telemetry message.
        /// </summary>
        public static DateTime SentLastTime { get; set; }

        /// <summary>
        /// Number of times we were not able to sent the telemetry message to the cloud.
        /// </summary>
        public static long FailedMessages { get; set; }

        /// <summary>
        /// Initialize the diagnostic object.
        /// </summary>
        public PublisherDiagnostics()
        {
            // init data
            _showDiagnosticsInfoTask = null;
            _shutdownTokenSource = new CancellationTokenSource();
        }

        public void Init()
        {
            // kick off the task to show diagnostic info
            if (SettingsConfiguration.DiagnosticsInterval > 0)
            {
                _showDiagnosticsInfoTask = Task.Run(() => ShowDiagnosticsInfoAsync(Program.Instance.ShutdownTokenSource.Token).ConfigureAwait(false));
            }
        }

        /// <summary>
        /// Close
        /// </summary>
        public void Close()
        {
            // wait for diagnostic task completion if it is enabled
            _shutdownTokenSource?.Cancel();
            _showDiagnosticsInfoTask?.Wait();
            _showDiagnosticsInfoTask = null;
            _shutdownTokenSource?.Dispose();
            _shutdownTokenSource = null;
        }

        /// <summary>
        /// Fetch diagnostic data.
        /// </summary>
        public DiagnosticInfoMethodResponseModel GetDiagnosticInfo()
        {
            DiagnosticInfoMethodResponseModel diagnosticInfo = new DiagnosticInfoMethodResponseModel();

            try
            {
                diagnosticInfo.PublisherStartTime = PublisherStartTime;
                diagnosticInfo.NumberOfOpcSessionsConfigured = Program.Instance._nodeConfig.NumberOfOpcSessionsConfigured;
                diagnosticInfo.NumberOfOpcSessionsConnected = Program.Instance._nodeConfig.NumberOfOpcSessionsConnected;
                diagnosticInfo.NumberOfOpcSubscriptionsConfigured = Program.Instance._nodeConfig.NumberOfOpcSubscriptionsConfigured;
                diagnosticInfo.NumberOfOpcSubscriptionsConnected = Program.Instance._nodeConfig.NumberOfOpcSubscriptionsConnected;
                diagnosticInfo.NumberOfOpcMonitoredItemsConfigured = Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsConfigured;
                diagnosticInfo.NumberOfOpcMonitoredItemsMonitored = Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsMonitored;
                diagnosticInfo.NumberOfOpcMonitoredItemsToRemove = Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsToRemove;
                diagnosticInfo.MonitoredItemsQueueCapacity = SettingsConfiguration.MonitoredItemsQueueCapacity;
                diagnosticInfo.MonitoredItemsQueueCount = MonitoredItemsQueueCount;
                diagnosticInfo.EnqueueCount = EnqueueCount;
                diagnosticInfo.EnqueueFailureCount = EnqueueFailureCount;
                diagnosticInfo.NumberOfEvents = NumberOfEvents;
                diagnosticInfo.SentMessages = SentMessages;
                diagnosticInfo.SentLastTime = SentLastTime;
                diagnosticInfo.SentBytes = SentBytes;
                diagnosticInfo.FailedMessages = FailedMessages;
                diagnosticInfo.TooLargeCount = TooLargeCount;
                diagnosticInfo.MissedSendIntervalCount = MissedSendIntervalCount;
                diagnosticInfo.WorkingSetMB = Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024);
                diagnosticInfo.DefaultSendIntervalSeconds = SettingsConfiguration.DefaultSendIntervalSeconds;
                diagnosticInfo.HubMessageSize = SettingsConfiguration.HubMessageSize;
            }
            catch (Exception ex)
            {
                // startup might be not completed yet
                Program.Instance.Logger.Error(ex, "Collecting diagnostics information causing error {diagnosticInfo}", diagnosticInfo);
            }
            return diagnosticInfo;
        }

        /// <summary>
        /// Fetch diagnostic log data.
        /// </summary>
        public async Task<DiagnosticLogMethodResponseModel> GetDiagnosticLogAsync()
        {
            DiagnosticLogMethodResponseModel diagnosticLogMethodResponseModel = new DiagnosticLogMethodResponseModel();
            diagnosticLogMethodResponseModel.MissedMessageCount = _missedMessageCount;
            diagnosticLogMethodResponseModel.LogMessageCount = _logMessageCount;

            if (SettingsConfiguration.DiagnosticsInterval >= 0)
            {
                if (StartupCompleted)
                {
                    List<string> log = new List<string>();
                    await _logQueueSemaphore.WaitAsync().ConfigureAwait(false);
                    try
                    {
                        string message;
                        while ((message = ReadLog()) != null)
                        {
                            log.Add(message);
                        }
                    }
                    finally
                    {
                        diagnosticLogMethodResponseModel.MissedMessageCount = _missedMessageCount;
                        _missedMessageCount = 0;
                        _logQueueSemaphore.Release();
                    }
                    diagnosticLogMethodResponseModel.Log.AddRange(log);
                }
                else
                {
                    diagnosticLogMethodResponseModel.Log.Add("Startup is not yet completed. Please try later.");
                }
            }
            else
            {
                diagnosticLogMethodResponseModel.Log.Add("Diagnostic log is disabled. Please use --di to enable it.");
            }

            return diagnosticLogMethodResponseModel;
        }

        /// <summary>
        /// Fetch diagnostic startup log data.
        /// </summary>
        public Task<DiagnosticLogMethodResponseModel> GetDiagnosticStartupLogAsync()
        {
            DiagnosticLogMethodResponseModel diagnosticLogMethodResponseModel = new DiagnosticLogMethodResponseModel();
            diagnosticLogMethodResponseModel.MissedMessageCount = 0;
            diagnosticLogMethodResponseModel.LogMessageCount = _startupLog.Count;

            if (SettingsConfiguration.DiagnosticsInterval >= 0)
            {
                if (StartupCompleted)
                {
                    diagnosticLogMethodResponseModel.Log.AddRange(_startupLog);
                }
                else
                {
                    diagnosticLogMethodResponseModel.Log.Add("Startup is not yet completed. Please try later.");
                }
            }
            else
            {
                diagnosticLogMethodResponseModel.Log.Add("Diagnostic log is disabled. Please use --di to enable it.");
            }

            return Task.FromResult(diagnosticLogMethodResponseModel);
        }

        /// <summary>
        /// Kicks of the task to show diagnostic information each 30 seconds.
        /// </summary>
        public async Task ShowDiagnosticsInfoAsync(CancellationToken ct)
        {
            while (true)
            {
                if (ct.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    await Task.Delay(SettingsConfiguration.DiagnosticsInterval * 1000, ct).ConfigureAwait(false);

                    // only show diag after startup is completed
                    if (!StartupCompleted)
                    {
                        continue;
                    }

                    DiagnosticInfoMethodResponseModel diagnosticInfo = GetDiagnosticInfo();
                    Program.Instance.Logger.Information("==========================================================================");
                    Program.Instance.Logger.Information($"OpcPublisher status @ {System.DateTime.UtcNow} (started @ {diagnosticInfo.PublisherStartTime})");
                    Program.Instance.Logger.Information("---------------------------------");
                    Program.Instance.Logger.Information($"OPC sessions (configured/connected): {diagnosticInfo.NumberOfOpcSessionsConfigured}/{diagnosticInfo.NumberOfOpcSessionsConnected}");
                    Program.Instance.Logger.Information($"OPC subscriptions (configured/connected): {diagnosticInfo.NumberOfOpcSubscriptionsConfigured}/{diagnosticInfo.NumberOfOpcSubscriptionsConnected}");
                    Program.Instance.Logger.Information($"OPC monitored items (configured/monitored/to remove): {diagnosticInfo.NumberOfOpcMonitoredItemsConfigured}/{diagnosticInfo.NumberOfOpcMonitoredItemsMonitored}/{diagnosticInfo.NumberOfOpcMonitoredItemsToRemove}");
                    Program.Instance.Logger.Information("---------------------------------");
                    Program.Instance.Logger.Information($"monitored items queue bounded capacity: {diagnosticInfo.MonitoredItemsQueueCapacity}");
                    Program.Instance.Logger.Information($"monitored items queue current items: {diagnosticInfo.MonitoredItemsQueueCount}");
                    Program.Instance.Logger.Information($"monitored item notifications enqueued: {diagnosticInfo.EnqueueCount}");
                    Program.Instance.Logger.Information($"monitored item notifications enqueue failure: {diagnosticInfo.EnqueueFailureCount}");
                    Program.Instance.Logger.Information("---------------------------------");
                    Program.Instance.Logger.Information($"messages sent to IoTHub: {diagnosticInfo.SentMessages}");
                    Program.Instance.Logger.Information($"last successful msg sent @: {diagnosticInfo.SentLastTime}");
                    Program.Instance.Logger.Information($"bytes sent to IoTHub: {diagnosticInfo.SentBytes}");
                    Program.Instance.Logger.Information($"avg msg size: {diagnosticInfo.SentBytes / (diagnosticInfo.SentMessages == 0 ? 1 : diagnosticInfo.SentMessages)}");
                    Program.Instance.Logger.Information($"msg send failures: {diagnosticInfo.FailedMessages}");
                    Program.Instance.Logger.Information($"messages too large to sent to IoTHub: {diagnosticInfo.TooLargeCount}");
                    Program.Instance.Logger.Information($"times we missed send interval: {diagnosticInfo.MissedSendIntervalCount}");
                    Program.Instance.Logger.Information($"number of events: {diagnosticInfo.NumberOfEvents}");
                    Program.Instance.Logger.Information("---------------------------------");
                    Program.Instance.Logger.Information($"current working set in MB: {diagnosticInfo.WorkingSetMB}");
                    Program.Instance.Logger.Information($"--si setting: {diagnosticInfo.DefaultSendIntervalSeconds}");
                    Program.Instance.Logger.Information($"--ms setting: {diagnosticInfo.HubMessageSize}");
                    Program.Instance.Logger.Information($"--ih setting: {diagnosticInfo.HubProtocol}");
                    Program.Instance.Logger.Information("==========================================================================");
                }
                catch (Exception ex)
                {
                    Program.Instance.Logger.Error(ex, "writing diagnostics output causing error");
                }
            }
        }

        /// <summary>
        /// Reads a line from the diagnostic log.
        /// Note: caller must take semaphore
        /// </summary>
        private string ReadLog()
        {
            string message = null;
            try
            {
                message = _logQueue.Dequeue();
            }
            catch (Exception ex)
            {
                Program.Instance.Logger.Error(ex, "Dequeue log message causing error");
            }
            return message;
        }

        /// <summary>
        /// Writes a line to the diagnostic log.
        /// </summary>
        public void WriteLog(string message)
        {
            if (StartupCompleted == false)
            {
                _startupLog.Add(message);
                return;
            }

            _logQueueSemaphore.Wait();
            try
            {
                while (_logQueue.Count > _logMessageCount)
                {
                    _logQueue.Dequeue();
                    _missedMessageCount++;
                }
                _logQueue.Enqueue(message);
            }
            finally
            {
                _logQueueSemaphore.Release();
            }
        }

        private readonly SemaphoreSlim _logQueueSemaphore = new SemaphoreSlim(1);
        private readonly int _logMessageCount = 100;
        private int _missedMessageCount;
        private readonly Queue<string> _logQueue = new Queue<string>();
        private CancellationTokenSource _shutdownTokenSource;
        private Task _showDiagnosticsInfoTask;
        private readonly List<string> _startupLog = new List<string>();
    }
}
