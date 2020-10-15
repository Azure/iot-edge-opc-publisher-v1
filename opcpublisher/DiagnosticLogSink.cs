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
            string message = FormatMessage(logEvent);
            Program.Diag.WriteLog(message);
            // enable below for testing
            //Console.ForegroundColor = ConsoleColor.Red;
            //Console.WriteLine(message);
            //Console.ResetColor();

            // also dump exception message and stack
            if (logEvent.Exception != null)
            {
                List<string> exceptionLog = FormatException(logEvent);
                foreach (var log in exceptionLog)
                {
                    Program.Diag.WriteLog(log);
                }
            }
        }

        /// <summary>
        /// Format the event message.
        /// </summary>
        private static string FormatMessage(LogEvent logEvent)
        {
            return $"[{logEvent.Timestamp:T} {logEvent.Level.ToString().Substring(0, 3).ToUpper(CultureInfo.InvariantCulture)}] {logEvent.RenderMessage()}";
        }

        /// <summary>
        /// Format an exception event.
        /// </summary>
        private static List<string> FormatException(LogEvent logEvent)
        {
            List<string> exceptionLog = null;
            if (logEvent.Exception != null)
            {
                exceptionLog = new List<string>();
                exceptionLog.Add(logEvent.Exception.Message);
                exceptionLog.Add(logEvent.Exception.StackTrace.ToString(CultureInfo.InvariantCulture));
            }
            return exceptionLog;
        }
    }

    /// <summary>
    /// Class for own Serilog log extension.
    /// </summary>
    public static class DiagnosticLogSinkExtensions
    {
        public static LoggerConfiguration DiagnosticLogSink(
                  this LoggerSinkConfiguration loggerConfiguration)
        {
            return loggerConfiguration.Sink(new DiagnosticLogSink());
        }
    }
}
