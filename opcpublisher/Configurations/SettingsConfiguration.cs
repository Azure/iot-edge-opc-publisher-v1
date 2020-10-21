using Microsoft.Azure.Devices.Client;
using Opc.Ua;
using System;
using System.Collections.Generic;
using System.IO;

namespace OpcPublisher.Configurations
{
    public class SettingsConfiguration
    {
        /// <summary>
        /// run foroever flag
        /// </summary>
        public static bool NoShutdown = false;

        /// <summary>
        /// interval for flushing log file
        /// </summary>
        public static TimeSpan LogFileFlushTimeSpanSec = TimeSpan.FromSeconds(30);

        /// <summary>
        /// DeviceConnectionString
        /// </summary>
        public static string DeviceConnectionString { get; set; } = null;

        /// <summary>
        /// Used as delay in sec when shutting down the application.
        /// </summary>
        public static uint PublisherShutdownWaitPeriod { get; } = 10;

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
        /// Name of the node configuration file.
        /// </summary>
        public static string PublisherNodeConfigurationFilename { get; set; } = $"{Directory.GetCurrentDirectory()}{Path.DirectorySeparatorChar}publishednodes.json";

        /// <summary>
        /// Specifies the queue capacity for monitored item events.
        /// </summary>
        public static int MonitoredItemsQueueCapacity { get; set; } = 8192;

        /// <summary>
        /// Specifies max message size in byte for hub communication allowed.
        /// </summary>
        public const uint HubMessageSizeMax = 256 * 1024;

        /// <summary>
        /// Specifies the message size in bytes used for hub communication.
        /// </summary>
        public static uint HubMessageSize { get; set; } = HubMessageSizeMax;

        /// <summary>
        /// Specifies the send interval in seconds after which a message is sent to the hub.
        /// </summary>
        public static int DefaultSendIntervalSeconds { get; set; } = 10;

        /// <summary>
        /// The protocol to use for hub communication.
        /// </summary>
        public const TransportType IotHubProtocol = TransportType.Mqtt;
        public const TransportType EdgeHubProtocol = TransportType.Amqp;

        /// <summary>
        /// Command line argument in which interval in seconds to show the diagnostic info.
        /// </summary>
        public static int DiagnosticsInterval { get; set; } = 0;

        /// <summary>
        /// Max allowed payload of an IoTHub direct method call response.
        /// </summary>
        public const int MaxResponsePayloadLength = (128 * 1024) - 256;

        /// <summary>
        /// Command line option to flag to read the node display names from the server and use it in telemetry events.
        /// </summary>
        public static bool FetchOpcNodeDisplayName { get; set; } = false;

        /// <summary>
        /// Command line argument to set the site to be added to telemetry events, identifying the source of the event,
        /// by prepending it to the ApplicationUri value of the event.
        /// </summary>
        public static string PublisherSite { get; set; }

        /// <summary>
        /// Command line argument to control the time to wait till a new attempt is made
        /// to establish a connection which is not yet connected again.
        /// </summary>
        public static int SessionConnectWaitSec { get; set; } = 10;

        /// <summary>
        /// HeartbeatIntvervalMax
        /// </summary>
        public const int HeartbeatIntvervalMax = 24 * 60 * 60;

        /// <summary>
        /// HeartbeatIntervalDefault
        /// </summary>
        public static int? HeartbeatIntervalDefault { get; set; } = 0;

        /// <summary>
        /// SkipFirstDefault
        /// </summary>
        public static bool SkipFirstDefault { get; set; } = false;

        /// <summary>
        /// SuppressedOpcStatusCodesDefault
        /// </summary>
        public const string SuppressedOpcStatusCodesDefault = "BadNoCommunication, BadWaitingForInitialData";

        /// <summary>
        /// SuppressedOpcStatusCodes
        /// </summary>
        public static List<uint> SuppressedOpcStatusCodes { get; } = new List<uint>();
    }
}
