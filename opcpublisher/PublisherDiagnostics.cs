// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

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
    public class PublisherDiagnostics : IDisposable
    {
        /// <summary>
        /// Command line argument in which interval in seconds to show the diagnostic info.
        /// </summary>
        public static int DiagnosticsInterval { get; set; } = 0;

        /// <summary>
        /// Get the singleton.
        /// </summary>
        public static PublisherDiagnostics Instance
        {
            get
            {
                if (_instance != null)
                {
                    return _instance;
                }
                else
                {
                    lock (_singletonLock)
                    {
                        if (_instance == null)
                        {
                            _instance = new PublisherDiagnostics();
                        }
                        return _instance;
                    }
                }
            }
        }

        /// <summary>
        /// Initialize the diagnostic object.
        /// </summary>
        private PublisherDiagnostics()
        {
            // init data
            _showDiagnosticsInfoTask = null;
            _shutdownTokenSource = new CancellationTokenSource();

            // kick off the task to show diagnostic info
            if (DiagnosticsInterval > 0)
            {
                _showDiagnosticsInfoTask = Task.Run(() => ShowDiagnosticsInfoAsync(Program.ShutdownTokenSource.Token).ConfigureAwait(false));
            }
        }

        /// <summary>
        /// Implement IDisposable.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // wait for diagnostic task completion if it is enabled
                _shutdownTokenSource?.Cancel();
                _showDiagnosticsInfoTask?.Wait();
                _showDiagnosticsInfoTask = null;
                _shutdownTokenSource?.Dispose();
                _shutdownTokenSource = null;
            }
        }

        /// <summary>
        /// Implement IDisposable.
        /// </summary>
        public void Dispose()
        {
            // do cleanup
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Fetch diagnostic data.
        /// </summary>
        public DiagnosticInfoMethodResponseModel GetDiagnosticInfo()
        {
            DiagnosticInfoMethodResponseModel diagnosticInfo = new DiagnosticInfoMethodResponseModel();

            try
            {
                diagnosticInfo.PublisherStartTime = Program.PublisherStartTime;
                diagnosticInfo.NumberOfOpcSessionsConfigured = Program.NodeConfiguration.NumberOfOpcSessionsConfigured;
                diagnosticInfo.NumberOfOpcSessionsConnected = Program.NodeConfiguration.NumberOfOpcSessionsConnected;
                diagnosticInfo.NumberOfOpcSubscriptionsConfigured = Program.NodeConfiguration.NumberOfOpcSubscriptionsConfigured;
                diagnosticInfo.NumberOfOpcSubscriptionsConnected = Program.NodeConfiguration.NumberOfOpcSubscriptionsConnected;
                diagnosticInfo.NumberOfOpcMonitoredItemsConfigured = Program.NodeConfiguration.NumberOfOpcMonitoredItemsConfigured;
                diagnosticInfo.NumberOfOpcMonitoredItemsMonitored = Program.NodeConfiguration.NumberOfOpcMonitoredItemsMonitored;
                diagnosticInfo.NumberOfOpcMonitoredItemsToRemove = Program.NodeConfiguration.NumberOfOpcMonitoredItemsToRemove;
                diagnosticInfo.MonitoredItemsQueueCapacity = HubClientWrapper.Instance.MonitoredItemsQueueCapacity;
                diagnosticInfo.MonitoredItemsQueueCount = HubClientWrapper.Instance.MonitoredItemsQueueCount;
                diagnosticInfo.EnqueueCount = HubClientWrapper.Instance.EnqueueCount;
                diagnosticInfo.EnqueueFailureCount = HubClientWrapper.Instance.EnqueueFailureCount;
                diagnosticInfo.NumberOfEvents = HubClientWrapper.Instance.NumberOfEvents;
                diagnosticInfo.SentMessages = HubClientWrapper.Instance.SentMessages;
                diagnosticInfo.SentLastTime = HubClientWrapper.Instance.SentLastTime;
                diagnosticInfo.SentBytes = HubClientWrapper.Instance.SentBytes;
                diagnosticInfo.FailedMessages = HubClientWrapper.Instance.FailedMessages;
                diagnosticInfo.TooLargeCount = HubClientWrapper.Instance.TooLargeCount;
                diagnosticInfo.MissedSendIntervalCount = HubClientWrapper.Instance.MissedSendIntervalCount;
                diagnosticInfo.WorkingSetMB = Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024);
                diagnosticInfo.DefaultSendIntervalSeconds = HubClientWrapper.Instance.DefaultSendIntervalSeconds;
                diagnosticInfo.HubMessageSize = HubClientWrapper.Instance.HubMessageSize;
            }
            catch (Exception ex)
            {
                // startup might be not completed yet
                Program.Logger.Error(ex, "Collecting diagnostics information causing error {diagnosticInfo}", diagnosticInfo);
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

            if (DiagnosticsInterval >= 0)
            {
                if (Program.StartupCompleted)
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

            if (DiagnosticsInterval >= 0)
            {
                if (Program.StartupCompleted)
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
                    await Task.Delay(DiagnosticsInterval * 1000, ct).ConfigureAwait(false);

                    // only show diag after startup is completed
                    if (!Program.StartupCompleted)
                    {
                        continue;
                    }

                    DiagnosticInfoMethodResponseModel diagnosticInfo = GetDiagnosticInfo();
                    Program.Logger.Information("==========================================================================");
                    Program.Logger.Information($"OpcPublisher status @ {System.DateTime.UtcNow} (started @ {diagnosticInfo.PublisherStartTime})");
                    Program.Logger.Information("---------------------------------");
                    Program.Logger.Information($"OPC sessions (configured/connected): {diagnosticInfo.NumberOfOpcSessionsConfigured}/{diagnosticInfo.NumberOfOpcSessionsConnected}");
                    Program.Logger.Information($"OPC subscriptions (configured/connected): {diagnosticInfo.NumberOfOpcSubscriptionsConfigured}/{diagnosticInfo.NumberOfOpcSubscriptionsConnected}");
                    Program.Logger.Information($"OPC monitored items (configured/monitored/to remove): {diagnosticInfo.NumberOfOpcMonitoredItemsConfigured}/{diagnosticInfo.NumberOfOpcMonitoredItemsMonitored}/{diagnosticInfo.NumberOfOpcMonitoredItemsToRemove}");
                    Program.Logger.Information("---------------------------------");
                    Program.Logger.Information($"monitored items queue bounded capacity: {diagnosticInfo.MonitoredItemsQueueCapacity}");
                    Program.Logger.Information($"monitored items queue current items: {diagnosticInfo.MonitoredItemsQueueCount}");
                    Program.Logger.Information($"monitored item notifications enqueued: {diagnosticInfo.EnqueueCount}");
                    Program.Logger.Information($"monitored item notifications enqueue failure: {diagnosticInfo.EnqueueFailureCount}");
                    Program.Logger.Information("---------------------------------");
                    Program.Logger.Information($"messages sent to IoTHub: {diagnosticInfo.SentMessages}");
                    Program.Logger.Information($"last successful msg sent @: {diagnosticInfo.SentLastTime}");
                    Program.Logger.Information($"bytes sent to IoTHub: {diagnosticInfo.SentBytes}");
                    Program.Logger.Information($"avg msg size: {diagnosticInfo.SentBytes / (diagnosticInfo.SentMessages == 0 ? 1 : diagnosticInfo.SentMessages)}");
                    Program.Logger.Information($"msg send failures: {diagnosticInfo.FailedMessages}");
                    Program.Logger.Information($"messages too large to sent to IoTHub: {diagnosticInfo.TooLargeCount}");
                    Program.Logger.Information($"times we missed send interval: {diagnosticInfo.MissedSendIntervalCount}");
                    Program.Logger.Information($"number of events: {diagnosticInfo.NumberOfEvents}");
                    Program.Logger.Information("---------------------------------");
                    Program.Logger.Information($"current working set in MB: {diagnosticInfo.WorkingSetMB}");
                    Program.Logger.Information($"--si setting: {diagnosticInfo.DefaultSendIntervalSeconds}");
                    Program.Logger.Information($"--ms setting: {diagnosticInfo.HubMessageSize}");
                    Program.Logger.Information($"--ih setting: {diagnosticInfo.HubProtocol}");
                    Program.Logger.Information("==========================================================================");
                }
                catch (Exception ex)
                {
                    Program.Logger.Error(ex, "writing diagnostics output causing error");
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
                Program.Logger.Error(ex, "Dequeue log message causing error");
            }
            return message;
        }

        /// <summary>
        /// Writes a line to the diagnostic log.
        /// </summary>
        public void WriteLog(string message)
        {
            if (Program.StartupCompleted == false)
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

        private static readonly SemaphoreSlim _logQueueSemaphore = new SemaphoreSlim(1);
        private static readonly int _logMessageCount = 100;
        private static int _missedMessageCount;
        private static readonly Queue<string> _logQueue = new Queue<string>();
        private static CancellationTokenSource _shutdownTokenSource;
        private static Task _showDiagnosticsInfoTask;
        private static readonly List<string> _startupLog = new List<string>();

        private static readonly object _singletonLock = new object();
        private static PublisherDiagnostics _instance = null;
    }
}
