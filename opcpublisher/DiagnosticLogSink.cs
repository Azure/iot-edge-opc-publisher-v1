// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using System.Collections.Generic;
using System.Globalization;

namespace OpcPublisher
{
    /// <summary>
    /// Diagnostic sink for Serilog.
    /// </summary>
    public class DiagnosticLogSink : ILogEventSink
    {
        /// <summary>
        /// Put a log event to our sink.
        /// </summary>
        public void Emit(LogEvent logEvent)
        {
            string message = $"[{logEvent.Timestamp:T} {logEvent.Level.ToString().Substring(0, 3).ToUpper(CultureInfo.InvariantCulture)}] {logEvent.RenderMessage()}";
            Program.Instance._diag.WriteLog(message);
            
            // also dump exception message and stack
            List<string> exceptionLog = new List<string>();
            if (logEvent.Exception != null)
            {
                exceptionLog.Add(logEvent.Exception.Message);
                exceptionLog.Add(logEvent.Exception.StackTrace.ToString(CultureInfo.InvariantCulture));
            }
            foreach (var log in exceptionLog)
            {
                Program.Instance._diag.WriteLog(log);
            }
        }
    }

    /// <summary>
    /// Class for own Serilog log extension.
    /// </summary>
    public static class DiagnosticLogSinkExtensions
    {
        public static LoggerConfiguration DiagnosticLogSink(this LoggerSinkConfiguration loggerConfiguration)
        {
            return loggerConfiguration.Sink(new DiagnosticLogSink());
        }
    }
}
