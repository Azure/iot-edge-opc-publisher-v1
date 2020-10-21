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

            { "dc|deviceconnectionstring=", $"{(SettingsConfiguration.RunningInIoTEdgeContext ? "not supported when running as IoTEdge module\n" : $"You must create a device with name <applicationname> manually and pass in the connectionstring of this device.\nDefault: none")}",
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


            // opc configuration options
            { "pn|portnum=", $"the server port of the publisher OPC server endpoint.\nDefault: {OpcApplicationConfiguration.ServerPort}", (ushort p) => OpcApplicationConfiguration.ServerPort = p },
            { "pa|path=", $"the enpoint URL path part of the publisher OPC server endpoint.\nDefault: '{OpcApplicationConfiguration.ServerPath}'", (string a) => OpcApplicationConfiguration.ServerPath = a },
            { "lr|ldsreginterval=", $"the LDS(-ME) registration interval in ms. If 0, then the registration is disabled.\nDefault: {OpcApplicationConfiguration.LdsRegistrationInterval}", (int i) => {
                    if (i >= 0)
                    {
                        OpcApplicationConfiguration.LdsRegistrationInterval = i;
                    }
                    else
                    {
                        throw new OptionException("The ldsreginterval must be larger or equal 0.", "ldsreginterval");
                    }
                }
            },
            { "ol|opcmaxstringlen=", $"the max length of a string opc can transmit/receive.\nDefault: {OpcApplicationConfiguration.OpcMaxStringLength}", (int i) => {
                    if (i > 0)
                    {
                        OpcApplicationConfiguration.OpcMaxStringLength = i;
                    }
                    else
                    {
                        throw new OptionException("The max opc string length must be larger than 0.", "opcmaxstringlen");
                    }
                }
            },
            { "ot|operationtimeout=", $"the operation timeout of the publisher OPC UA client in ms.\nDefault: {OpcApplicationConfiguration.OpcOperationTimeout}", (int i) => {
                    if (i >= 0)
                    {
                        OpcApplicationConfiguration.OpcOperationTimeout = i;
                    }
                    else
                    {
                        throw new OptionException("The operation timeout must be larger or equal 0.", "operationtimeout");
                    }
                }
            },
            { "oi|opcsamplinginterval=", "the publisher is using this as default value in milliseconds to request the servers to sample the nodes with this interval\n" +
                "this value might be revised by the OPC UA servers to a supported sampling interval.\n" +
                "please check the OPC UA specification for details how this is handled by the OPC UA stack.\n" +
                "a negative value will set the sampling interval to the publishing interval of the subscription this node is on.\n" +
                $"0 will configure the OPC UA server to sample in the highest possible resolution and should be taken with care.\nDefault: {OpcApplicationConfiguration.OpcSamplingInterval}", (int i) => OpcApplicationConfiguration.OpcSamplingInterval = i
            },
            { "op|opcpublishinginterval=", "the publisher is using this as default value in milliseconds for the publishing interval setting of the subscriptions established to the OPC UA servers.\n" +
                "please check the OPC UA specification for details how this is handled by the OPC UA stack.\n" +
                $"a value less than or equal zero will let the server revise the publishing interval.\nDefault: {OpcApplicationConfiguration.OpcPublishingInterval}", (int i) => {
                    if (i > 0 && i >= OpcApplicationConfiguration.OpcSamplingInterval)
                    {
                        OpcApplicationConfiguration.OpcPublishingInterval = i;
                    }
                    else
                    {
                        if (i <= 0)
                        {
                            OpcApplicationConfiguration.OpcPublishingInterval = 0;
                        }
                        else
                        {
                            throw new OptionException($"The opcpublishinterval ({i}) must be larger than the opcsamplinginterval ({OpcApplicationConfiguration.OpcSamplingInterval}).", "opcpublishinterval");
                        }
                    }
                }
            },
            { "ct|createsessiontimeout=", $"specify the timeout in seconds used when creating a session to an endpoint. On unsuccessful connection attemps a backoff up to {OpcApplicationConfiguration.OpcSessionCreationBackoffMax} times the specified timeout value is used.\nMin: 1\nDefault: {OpcApplicationConfiguration.OpcSessionCreationTimeout}", (uint u) => {
                    if (u > 1)
                    {
                        OpcApplicationConfiguration.OpcSessionCreationTimeout = u;
                    }
                    else
                    {
                        throw new OptionException("The createsessiontimeout must be greater than 1 sec", "createsessiontimeout");
                    }
                }
            },
            { "ki|keepaliveinterval=", $"specify the interval in seconds the publisher is sending keep alive messages to the OPC servers on the endpoints it is connected to.\nMin: 2\nDefault: {OpcApplicationConfiguration.OpcKeepAliveIntervalInSec}", (int i) => {
                    if (i >= 2)
                    {
                        OpcApplicationConfiguration.OpcKeepAliveIntervalInSec = i;
                    }
                    else
                    {
                        throw new OptionException("The keepaliveinterval must be greater or equal 2", "keepalivethreshold");
                    }
                }
            },
            { "kt|keepalivethreshold=", $"specify the number of keep alive packets a server can miss, before the session is disconneced\nMin: 1\nDefault: {OpcApplicationConfiguration.OpcKeepAliveDisconnectThreshold}", (uint u) => {
                    if (u > 1)
                    {
                        OpcApplicationConfiguration.OpcKeepAliveDisconnectThreshold = u;
                    }
                    else
                    {
                        throw new OptionException("The keepalivethreshold must be greater than 1", "keepalivethreshold");
                    }
                }
            },

            { "aa|autoaccept", $"the publisher trusts all servers it is establishing a connection to.\nDefault: {OpcSecurityConfiguration.AutoAcceptCerts}", b => OpcSecurityConfiguration.AutoAcceptCerts = b != null },

            { "fd|fetchdisplayname=", $"same as fetchname.\nDefault: {SettingsConfiguration.FetchOpcNodeDisplayName}", (bool b) => SettingsConfiguration.FetchOpcNodeDisplayName = b },
            { "fn|fetchname", $"enable to read the display name of a published node from the server. this will increase the runtime.\nDefault: {SettingsConfiguration.FetchOpcNodeDisplayName}", b => SettingsConfiguration.FetchOpcNodeDisplayName = b != null },

            { "ss|suppressedopcstatuscodes=", $"specifies the OPC UA status codes for which no events should be generated.\n" +
                $"Default: {SettingsConfiguration.SuppressedOpcStatusCodesDefault}", (string s) => opcStatusCodesToSuppress = s },


            // cert store options
            { "at|appcertstoretype=", $"the own application cert store type. \n(allowed values: Directory, X509Store)\nDefault: '{OpcSecurityConfiguration.OpcOwnCertStoreType}'", (string s) => {
                    if (s.Equals(CertificateStoreType.X509Store, StringComparison.OrdinalIgnoreCase) || s.Equals(CertificateStoreType.Directory, StringComparison.OrdinalIgnoreCase))
                    {
                        OpcSecurityConfiguration.OpcOwnCertStoreType = s.Equals(CertificateStoreType.X509Store, StringComparison.OrdinalIgnoreCase) ? CertificateStoreType.X509Store : CertificateStoreType.Directory;
                        OpcSecurityConfiguration.OpcOwnCertStorePath = s.Equals(CertificateStoreType.X509Store, StringComparison.OrdinalIgnoreCase) ? OpcSecurityConfiguration.OpcOwnCertX509StorePathDefault : OpcSecurityConfiguration.OpcOwnCertDirectoryStorePathDefault;
                    }
                    else
                    {
                        throw new OptionException();
                    }
                }
            },
            { "ap|appcertstorepath=", $"the path where the own application cert should be stored\nDefault (depends on store type):\n" +
                    $"X509Store: '{OpcSecurityConfiguration.OpcOwnCertX509StorePathDefault}'\n" +
                    $"Directory: '{OpcSecurityConfiguration.OpcOwnCertDirectoryStorePathDefault}'", (string s) => OpcSecurityConfiguration.OpcOwnCertStorePath = s
            },

            { "tp|trustedcertstorepath=", $"the path of the trusted cert store\nDefault: '{OpcSecurityConfiguration.OpcTrustedCertDirectoryStorePathDefault}'", (string s) => OpcSecurityConfiguration.OpcTrustedCertStorePath = s },

            { "rp|rejectedcertstorepath=", $"the path of the rejected cert store\nDefault '{OpcSecurityConfiguration.OpcRejectedCertDirectoryStorePathDefault}'", (string s) => OpcSecurityConfiguration.OpcRejectedCertStorePath = s },

            { "ip|issuercertstorepath=", $"the path of the trusted issuer cert store\nDefault '{OpcSecurityConfiguration.OpcIssuerCertDirectoryStorePathDefault}'", (string s) => OpcSecurityConfiguration.OpcIssuerCertStorePath = s },

            { "csr", $"show data to create a certificate signing request\nDefault '{OpcSecurityConfiguration.ShowCreateSigningRequestInfo}'", c => OpcSecurityConfiguration.ShowCreateSigningRequestInfo = c != null },

            { "ab|applicationcertbase64=", $"update/set this applications certificate with the certificate passed in as bas64 string", (string s) =>
                {
                    OpcSecurityConfiguration.NewCertificateBase64String = s;
                }
            },
            { "af|applicationcertfile=", $"update/set this applications certificate with the certificate file specified", (string s) =>
                {
                    if (File.Exists(s))
                    {
                        OpcSecurityConfiguration.NewCertificateFileName = s;
                    }
                    else
                    {
                        throw new OptionException("The file '{s}' does not exist.", "applicationcertfile");
                    }
                }
            },

            { "pb|privatekeybase64=", $"initial provisioning of the application certificate (with a PEM or PFX fomat) requires a private key passed in as base64 string", (string s) =>
                {
                    OpcSecurityConfiguration.PrivateKeyBase64String = s;
                }
            },
            { "pk|privatekeyfile=", $"initial provisioning of the application certificate (with a PEM or PFX fomat) requires a private key passed in as file", (string s) =>
                {
                    if (File.Exists(s))
                    {
                        OpcSecurityConfiguration.PrivateKeyFileName = s;
                    }
                    else
                    {
                        throw new OptionException("The file '{s}' does not exist.", "privatekeyfile");
                    }
                }
            },

            { "cp|certpassword=", $"the optional password for the PEM or PFX or the installed application certificate", (string s) =>
                {
                    OpcSecurityConfiguration.CertificatePassword = s;
                }
            },

            { "tb|addtrustedcertbase64=", $"adds the certificate to the applications trusted cert store passed in as base64 string (multiple strings supported)", (string s) =>
                {
                    OpcSecurityConfiguration.TrustedCertificateBase64Strings.AddRange(ParseListOfStrings(s));
                }
            },
            { "tf|addtrustedcertfile=", $"adds the certificate file(s) to the applications trusted cert store passed in as base64 string (multiple filenames supported)", (string s) =>
                {
                    OpcSecurityConfiguration.TrustedCertificateFileNames.AddRange(ParseListOfFileNames(s, "addtrustedcertfile"));
                }
            },

            { "ib|addissuercertbase64=", $"adds the specified issuer certificate to the applications trusted issuer cert store passed in as base64 string (multiple strings supported)", (string s) =>
                {
                    OpcSecurityConfiguration.IssuerCertificateBase64Strings.AddRange(ParseListOfStrings(s));
                }
            },
            { "if|addissuercertfile=", $"adds the specified issuer certificate file(s) to the applications trusted issuer cert store (multiple filenames supported)", (string s) =>
                {
                    OpcSecurityConfiguration.IssuerCertificateFileNames.AddRange(ParseListOfFileNames(s, "addissuercertfile"));
                }
            },

            { "rb|updatecrlbase64=", $"update the CRL passed in as base64 string to the corresponding cert store (trusted or trusted issuer)", (string s) =>
                {
                    OpcSecurityConfiguration.CrlBase64String = s;
                }
            },
            { "uc|updatecrlfile=", $"update the CRL passed in as file to the corresponding cert store (trusted or trusted issuer)", (string s) =>
                {
                    if (File.Exists(s))
                    {
                        OpcSecurityConfiguration.CrlFileName = s;
                    }
                    else
                    {
                        throw new OptionException("The file '{s}' does not exist.", "updatecrlfile");
                    }
                }
            },

            { "rc|removecert=", $"remove cert(s) with the given thumbprint(s) (multiple thumbprints supported)", (string s) =>
                {
                    OpcSecurityConfiguration.ThumbprintsToRemove.AddRange(ParseListOfStrings(s));
                }
            },

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
                },
            { "vc|verboseconsole=", $"ignored, only supported for backward comaptibility.", b => {}},
            { "as|autotrustservercerts=", $"same as autoaccept, only supported for backward cmpatibility.\nDefault: {OpcSecurityConfiguration.AutoAcceptCerts}", (bool b) => OpcSecurityConfiguration.AutoAcceptCerts = b },
            { "tt|trustedcertstoretype=", $"ignored, only supported for backward compatibility. the trusted cert store will always reside in a directory.", s => { }},
            { "rt|rejectedcertstoretype=", $"ignored, only supported for backward compatibility. the rejected cert store will always reside in a directory.", s => { }},
            { "it|issuercertstoretype=", $"ignored, only supported for backward compatibility. the trusted issuer cert store will always reside in a directory.", s => { }},

        };

        public void Parse(string[] args)
        {
            // parse the command line
            try
            {
                List<string> extraArgs = options.Parse(args);

                // validate and parse extra arguments
                const int APP_NAME_INDEX = 0;
                switch (extraArgs.Count)
                {
                    case 0:
                        OpcApplicationConfiguration.ApplicationName = Utils.GetHostName();
                        break;
                    case 1:
                        OpcApplicationConfiguration.ApplicationName = extraArgs[APP_NAME_INDEX];
                        break;
                    case 2:
                        OpcApplicationConfiguration.ApplicationName = extraArgs[APP_NAME_INDEX];
                        if (SettingsConfiguration.RunningInIoTEdgeContext)
                        {
                            Console.WriteLine($"Warning: connection string parameter is not supported in IoTEdge context, given parameter is ignored");
                        }
                        break;
                    default:
                        Program.Instance.Logger.Error("Error in command line options");
                        Program.Instance.Logger.Error($"Command line arguments: {String.Join(" ", args)}");
                        Usage(options);
                        break;
                }

                // verify opc status codes to suppress
                List<string> statusCodesToSuppress = ParseListOfStrings(opcStatusCodesToSuppress);
                foreach (var statusCodeValueOrName in statusCodesToSuppress)
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
                foreach (var suppressedOpcStatusCode in SettingsConfiguration.SuppressedOpcStatusCodes)
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
        /// Helper to build a list of filenames out of a comma separated list of filenames (optional in double quotes).
        /// </summary>
        private static List<string> ParseListOfFileNames(string s, string option)
        {
            List<string> fileNames = new List<string>();
            if (s[0] == '"' && (s.Count(c => c.Equals('"')) % 2 == 0))
            {
                while (s.Contains('"', StringComparison.InvariantCulture))
                {
                    int first = 0;
                    int next = 0;
                    first = s.IndexOf('"', next);
                    next = s.IndexOf('"', ++first);
                    var fileName = s.Substring(first, next - first);
                    if (File.Exists(fileName))
                    {
                        fileNames.Add(fileName);
                    }
                    else
                    {
                        throw new OptionException($"The file '{fileName}' does not exist.", option);
                    }
                    s = s.Substring(++next);
                }
            }
            else if (s.Contains(',', StringComparison.InvariantCulture))
            {
                List<string> parsedFileNames = s.Split(',').ToList();
                parsedFileNames = parsedFileNames.Select(st => st.Trim()).ToList();
                foreach (var fileName in parsedFileNames)
                {
                    if (File.Exists(fileName))
                    {
                        fileNames.Add(fileName);
                    }
                    else
                    {
                        throw new OptionException($"The file '{fileName}' does not exist.", option);
                    }

                }
            }
            else
            {
                if (File.Exists(s))
                {
                    fileNames.Add(s);
                }
                else
                {
                    throw new OptionException($"The file '{s}' does not exist.", option);
                }
            }
            return fileNames;
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
            Program.Instance.Logger.Information("Usage: {0}.exe <applicationname> [<iothubconnectionstring>] [<options>]", Assembly.GetEntryAssembly().GetName().Name);
            Program.Instance.Logger.Information("");
            Program.Instance.Logger.Information("OPC Edge Publisher to subscribe to configured OPC UA servers and send telemetry to Azure IoTHub.");
            Program.Instance.Logger.Information("To exit the application, just press CTRL-C while it is running.");
            Program.Instance.Logger.Information("");
            Program.Instance.Logger.Information("applicationname: the OPC UA application name to use, required");
            Program.Instance.Logger.Information("                 The application name is also used to register the publisher under this name in the");
            Program.Instance.Logger.Information("                 IoTHub device registry.");
            Program.Instance.Logger.Information("");
            Program.Instance.Logger.Information("iothubconnectionstring: the IoTHub owner connectionstring, optional");
            Program.Instance.Logger.Information("");
            Program.Instance.Logger.Information("There are a couple of environment variables which can be used to control the application:");
            Program.Instance.Logger.Information("_HUB_CS: sets the IoTHub owner connectionstring");
            Program.Instance.Logger.Information("_GW_LOGP: sets the filename of the log file to use");
            Program.Instance.Logger.Information("_TPC_SP: sets the path to store certificates of trusted stations");
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
