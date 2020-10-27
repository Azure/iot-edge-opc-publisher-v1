// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Mono.Options;
using Opc.Ua;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace OpcPublisher.Configurations
{
    public class CommandLineArgumentsParser
    {
        // command line options
        private Mono.Options.OptionSet options = new Mono.Options.OptionSet {
            // Publisher configuration options
            { "pf|publishfile=", $"the filename to configure the nodes to publish.\nDefault: '{SettingsConfiguration.PublisherNodeConfigurationFilename}'", (string p) => SettingsConfiguration.PublisherNodeConfigurationFilename = p },
            { "s|site=", $"the site OPC Publisher is working in. if specified this domain is appended (delimited by a ':' to the 'ApplicationURI' property when telemetry is sent to IoTHub.\n" +
                    "The value must follow the syntactical rules of a DNS hostname.\nDefault: not set", (string s) => {
                    Regex siteNameRegex = new Regex("^(([a-zA-Z0-9]|[a-zA-Z0-9][a-zA-Z0-9\\-]*[a-zA-Z0-9])\\.)*([A-Za-z0-9]|[A-Za-z0-9][A-Za-z0-9\\-]*[A-Za-z0-9])$");
                    if (siteNameRegex.IsMatch(s))
                    {
                        SettingsConfiguration.PublisherSite = s;
                    }
                    else
                    {
                        throw new OptionException("The shopfloor site is not a valid DNS hostname.", "site");
                    }
                }
            },
            { "sw|sessionconnectwait=", $"specify the wait time in seconds publisher is trying to connect to disconnected endpoints and starts monitoring unmonitored items\nMin: 10\nDefault: {SettingsConfiguration.SessionConnectWaitSec}", (int i) => {
                    if (i > 10)
                    {
                        SettingsConfiguration.SessionConnectWaitSec = i;
                    }
                    else
                    {
                        throw new OptionException("The sessionconnectwait must be greater than 10 sec", "sessionconnectwait");
                    }
                }
            },
            { "mq|monitoreditemqueuecapacity=", $"specify how many notifications of monitored items can be stored in the internal queue, if the data can not be sent quick enough to IoTHub\nMin: 1024\nDefault: {SettingsConfiguration.MonitoredItemsQueueCapacity}", (int i) => {
                    if (i >= 1024)
                    {
                        SettingsConfiguration.MonitoredItemsQueueCapacity = i;
                    }
                    else
                    {
                        throw new OptionException("The monitoreditemqueueitems must be greater than 1024", "monitoreditemqueueitems");
                    }
                }
            },
            { "di|diagnosticsinterval=", $"shows publisher diagnostic info at the specified interval in seconds (need log level info).\n-1 disables remote diagnostic log and diagnostic output\n0 disables diagnostic output\nDefault: {SettingsConfiguration.DiagnosticsInterval}", (int i) => SettingsConfiguration.DiagnosticsInterval = i },

            { "ns|noshutdown=", $"same as runforever.\nDefault: {SettingsConfiguration.NoShutdown}", (bool b) => SettingsConfiguration.NoShutdown = b },
            { "rf|runforever", $"publisher can not be stopped by pressing a key on the console, but will run forever.\nDefault: {SettingsConfiguration.NoShutdown}", b => SettingsConfiguration.NoShutdown = b != null },

            { "lf|logfile=", $"the filename of the logfile to use.\nDefault: './{SettingsConfiguration.LogFileName}'", (string l) => SettingsConfiguration.LogFileName = l },
            { "lt|logflushtimespan=", $"the timespan in seconds when the logfile should be flushed.\nDefault: {SettingsConfiguration.LogFileFlushTimeSpanSec} sec", (int s) => {
                    if (s > 0)
                    {
                        SettingsConfiguration.LogFileFlushTimeSpanSec = TimeSpan.FromSeconds(s);
                    }
                    else
                    {
                        throw new Mono.Options.OptionException("The logflushtimespan must be a positive number.", "logflushtimespan");
                    }
                }
            },
            { "ll|loglevel=", $"the loglevel to use (allowed: fatal, error, warn, info, debug, verbose).\nDefault: info", (string l) => {
                    List<string> logLevels = new List<string> {"fatal", "error", "warn", "info", "debug", "verbose"};
#pragma warning disable CA1308 // Normalize strings to uppercase
                    if (logLevels.Contains(l.ToLowerInvariant()))
                    {
                        SettingsConfiguration.LogLevel = l.ToLowerInvariant();
                    }
#pragma warning restore CA1308 // Normalize strings to uppercase
                    else
                    {
                        throw new Mono.Options.OptionException("The loglevel must be one of: fatal, error, warn, info, debug, verbose", "loglevel");
                    }
                }
            },


            { "ms|iothubmessagesize=", $"the max size of a message which can be send to IoTHub. when telemetry of this size is available it will be sent.\n0 will enforce immediate send when telemetry is available\nMin: 0\nMax: {SettingsConfiguration.HubMessageSizeMax}\nDefault: {SettingsConfiguration.HubMessageSize}", (uint u) => {
                    if (u >= 0 && u <= SettingsConfiguration.HubMessageSizeMax)
                    {
                        SettingsConfiguration.HubMessageSize = u;
                    }
                    else
                    {
                        throw new OptionException("The iothubmessagesize must be in the range between 1 and 256*1024.", "iothubmessagesize");
                    }
                }
            },
            { "si|iothubsendinterval=", $"the interval in seconds when telemetry should be send to IoTHub. If 0, then only the iothubmessagesize parameter controls when telemetry is sent.\nDefault: '{SettingsConfiguration.DefaultSendIntervalSeconds}'", (int i) => {
                    if (i >= 0)
                    {
                        SettingsConfiguration.DefaultSendIntervalSeconds = i;
                    }
                    else
                    {
                        throw new OptionException("The iothubsendinterval must be larger or equal 0.", "iothubsendinterval");
                    }
                }
            },

            { "dc|deviceconnectionstring=", $"{(SettingsConfiguration.RunningInIoTEdgeContext ? "not supported when running as IoTEdge module\n" : $"You must create a device manually and pass in the connectionstring of this device.\nDefault: none")}",
                (string dc) => SettingsConfiguration.DeviceConnectionString = (SettingsConfiguration.RunningInIoTEdgeContext ? null : dc)
            },
           
            { "hb|heartbeatinterval=", "the publisher is using this as default value in seconds for the heartbeat interval setting of nodes without\n" +
                "a heartbeat interval setting.\n" +
                $"Default: {SettingsConfiguration.HeartbeatIntervalDefault}", (int i) => {
                    if (i >= 0 && i <= SettingsConfiguration.HeartbeatIntvervalMax)
                    {
                        SettingsConfiguration.HeartbeatIntervalDefault = i;
                    }
                    else
                    {
                        throw new OptionException($"The heartbeatinterval setting ({i}) must be larger or equal than 0.", "opcpublishinterval");
                    }
                }
            },
            { "sf|skipfirstevent=", "the publisher is using this as default value for the skip first event setting of nodes without\n" +
                "a skip first event setting.\n" +
                $"Default: {SettingsConfiguration.SkipFirstDefault}", (bool b) => { SettingsConfiguration.SkipFirstDefault = b; }
            },

            { "oi|opcsamplinginterval=", "the publisher is using this as default value in milliseconds to request the servers to sample the nodes with this interval\n" +
                "this value might be revised by the OPC UA servers to a supported sampling interval.\n" +
                "please check the OPC UA specification for details how this is handled by the OPC UA stack.\n" +
                "a negative value will set the sampling interval to the publishing interval of the subscription this node is on.\n" +
                $"0 will configure the OPC UA server to sample in the highest possible resolution and should be taken with care.\nDefault: {SettingsConfiguration.DefaultOpcSamplingInterval}", (int i) => SettingsConfiguration.DefaultOpcSamplingInterval = i
            },
            { "op|opcpublishinginterval=", "the publisher is using this as default value in milliseconds for the publishing interval setting of the subscriptions established to the OPC UA servers.\n" +
                "please check the OPC UA specification for details how this is handled by the OPC UA stack.\n" +
                $"a value less than or equal zero will let the server revise the publishing interval.\nDefault: {SettingsConfiguration.DefaultOpcPublishingInterval}", (int i) => {
                    if (i > 0 && i >= SettingsConfiguration.DefaultOpcSamplingInterval)
                    {
                        SettingsConfiguration.DefaultOpcPublishingInterval = i;
                    }
                    else
                    {
                        if (i <= 0)
                        {
                            SettingsConfiguration.DefaultOpcPublishingInterval = 0;
                        }
                        else
                        {
                            throw new OptionException($"The opcpublishinterval ({i}) must be larger than the opcsamplinginterval ({SettingsConfiguration.DefaultOpcSamplingInterval}).", "opcpublishinterval");
                        }
                    }
                }
            },
            
            { "aa|autoaccept", $"the publisher servers interface accepts all client connections without certificate validation.\nDefault: {SettingsConfiguration.AutoAcceptCerts}", b => SettingsConfiguration.AutoAcceptCerts = b != null },

            { "fd|fetchdisplayname=", $"same as fetchname.\nDefault: {SettingsConfiguration.FetchOpcNodeDisplayName}", (bool b) => SettingsConfiguration.FetchOpcNodeDisplayName = b },
            { "fn|fetchname", $"enable to read the display name of a published node from the server. this will increase the runtime.\nDefault: {SettingsConfiguration.FetchOpcNodeDisplayName}", b => SettingsConfiguration.FetchOpcNodeDisplayName = b != null },

            { "ss|suppressedopcstatuscodes=", $"specifies the OPC UA status codes for which no events should be generated.\n" +
                $"Default: {SettingsConfiguration.SuppressedOpcStatusCodesDefault}", (string s) => opcStatusCodesToSuppress = s },

            // misc
            { "h|help", "show this message and exit", h => shouldShowHelp = h != null },

            // all the following are only supported to not break existing command lines, but some of them are just ignored
            { "st|opcstacktracemask=", $"ignored, only supported for backward comaptibility.", i => {}},
            { "sd|shopfloordomain=", $"same as site option, only there for backward compatibility\n" +
                    "The value must follow the syntactical rules of a DNS hostname.\nDefault: not set", (string s) => {
                    Regex siteNameRegex = new Regex("^(([a-zA-Z0-9]|[a-zA-Z0-9][a-zA-Z0-9\\-]*[a-zA-Z0-9])\\.)*([A-Za-z0-9]|[A-Za-z0-9][A-Za-z0-9\\-]*[A-Za-z0-9])$");
                    if (siteNameRegex.IsMatch(s))
                    {
                        SettingsConfiguration.PublisherSite = s;
                    }
                    else
                    {
                        throw new OptionException("The shopfloor domain is not a valid DNS hostname.", "shopfloordomain");
                    }
                }
            }
        };

        public void Parse(string[] args)
        {
            try
            {
                // parse the command line
                List<string> extraArgs = options.Parse(args);

                // display the invalid arguments (but continue)
                foreach (string arg in extraArgs)
                {
                    Program.Instance.Logger.Error("Unknown command line argument: " + arg);
                }

                // verify opc status codes to suppress
                List<string> statusCodesToSuppress = ParseListOfStrings(opcStatusCodesToSuppress);
                foreach (string statusCodeValueOrName in statusCodesToSuppress)
                {
                    uint statusCodeValue;
                    try
                    {
                        // convert integers and prefixed hex values
                        statusCodeValue = (uint)new UInt32Converter().ConvertFromInvariantString(statusCodeValueOrName);
                        SettingsConfiguration.SuppressedOpcStatusCodes.Add(statusCodeValue);
                    }
                    catch
                    {
                        // convert non prefixed hex values
                        if (uint.TryParse(statusCodeValueOrName, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out statusCodeValue))
                        {
                            SettingsConfiguration.SuppressedOpcStatusCodes.Add(statusCodeValue);
                        }
                        else
                        {
                            // convert constant names
                            statusCodeValue = StatusCodes.GetIdentifier(statusCodeValueOrName);
                            if (statusCodeValueOrName.Equals("Good", StringComparison.InvariantCulture) || statusCodeValue != 0)
                            {
                                SettingsConfiguration.SuppressedOpcStatusCodes.Add(statusCodeValue);
                            }
                            else
                            {
                                throw new OptionException($"The OPC UA status code '{statusCodeValueOrName}' to suppress is unknown. Please specify a valid string, int or hex value.", "suppressedopcstatuscodes");
                            }
                        }
                    }
                }

                // filter out duplicate status codes
                List<uint> distinctSuppressedOpcStatusCodes = SettingsConfiguration.SuppressedOpcStatusCodes.Distinct().ToList();
                SettingsConfiguration.SuppressedOpcStatusCodes.Clear();
                SettingsConfiguration.SuppressedOpcStatusCodes.AddRange(distinctSuppressedOpcStatusCodes);

                // show suppressed status codes
                Program.Instance.Logger.Information($"OPC UA monitored item notifications with one of the following {SettingsConfiguration.SuppressedOpcStatusCodes.Count} status codes will not generate telemetry events:");
                foreach (uint suppressedOpcStatusCode in SettingsConfiguration.SuppressedOpcStatusCodes)
                {
                    string statusName = StatusCodes.GetBrowseName(suppressedOpcStatusCode);
                    Program.Instance.Logger.Information($"StatusCode: {(string.IsNullOrEmpty(statusName) ? "Unknown" : statusName)} (dec: {suppressedOpcStatusCode}, hex: {suppressedOpcStatusCode:X})");
                }
            }
            catch (OptionException e)
            {
                // show message
                Program.Instance.Logger.Error(e, "Error in command line options");
                Program.Instance.Logger.Error($"Command line arguments: {String.Join(" ", args)}");

                // show usage
                Usage(options);
            }

            // show usage if requested
            if (shouldShowHelp)
            {
                Usage(options);
            }
        }

        /// <summary>
        /// Helper to build a list of strings out of a comma separated list of strings (optional in double quotes).
        /// </summary>
        public static List<string> ParseListOfStrings(string s)
        {
            List<string> strings = new List<string>();
            if (s[0] == '"' && (s.Count(c => c.Equals('"')) % 2 == 0))
            {
                while (s.Contains('"', StringComparison.InvariantCulture))
                {
                    int first = 0;
                    int next = 0;
                    first = s.IndexOf('"', next);
                    next = s.IndexOf('"', ++first);
                    strings.Add(s.Substring(first, next - first));
                    s = s.Substring(++next);
                }
            }
            else if (s.Contains(',', StringComparison.InvariantCulture))
            {
                strings = s.Split(',').ToList();
                strings = strings.Select(st => st.Trim()).ToList();
            }
            else
            {
                strings.Add(s);
            }
            return strings;
        }

        /// <summary>
        /// Usage message.
        /// </summary>
        public void Usage(Mono.Options.OptionSet options)
        {
            // show usage
            Program.Instance.Logger.Information("");
            Program.Instance.Logger.Information($"OPC Publisher V{FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion}");
            Program.Instance.Logger.Information($"Informational version: V{(Attribute.GetCustomAttribute(Assembly.GetEntryAssembly(), typeof(AssemblyInformationalVersionAttribute)) as AssemblyInformationalVersionAttribute).InformationalVersion}");
            Program.Instance.Logger.Information("");
            Program.Instance.Logger.Information("Usage: {0}.exe [<options>]", Assembly.GetEntryAssembly().GetName().Name);
            Program.Instance.Logger.Information("");
            Program.Instance.Logger.Information("OPC Edge Publisher to subscribe to configured OPC UA servers and send telemetry to Azure IoTHub.");
            Program.Instance.Logger.Information("To exit the application, just press CTRL-C while it is running.");
            Program.Instance.Logger.Information("");
            Program.Instance.Logger.Information("There are a couple of environment variables which can be used to control the application:");
            Program.Instance.Logger.Information("_GW_LOGP: sets the filename of the log file to use");
            Program.Instance.Logger.Information("_GW_PNFP: sets the filename of the publishing configuration file");
            Program.Instance.Logger.Information("");
            Program.Instance.Logger.Information("Command line arguments overrule environment variable settings.");
            Program.Instance.Logger.Information("");

            // output the options
            Program.Instance.Logger.Information("Options:");

            StringBuilder stringBuilder = new StringBuilder();
            StringWriter stringWriter = new StringWriter(stringBuilder);
            options.WriteOptionDescriptions(stringWriter);
            
            string[] helpLines = stringBuilder.ToString().Split("\n");
            foreach (string line in helpLines)
            {
                Program.Instance.Logger.Information(line);
            }
        }

        private static bool shouldShowHelp = false;
        private static string opcStatusCodesToSuppress = SettingsConfiguration.SuppressedOpcStatusCodesDefault;
    }
}
