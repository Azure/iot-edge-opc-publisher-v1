// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Opc.Ua;
using Opc.Ua.Client;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace OpcPublisher
{
    /// <summary>
    /// Class to manage the OPC monitored items, which are the nodes we need to publish.
    /// </summary>
    public class OpcUaMonitoredItemManager
    {
        /// <summary>
        /// The state of the monitored item.
        /// </summary>
        public enum OpcMonitoredItemState
        {
            Unmonitored = 0,
            UnmonitoredNamespaceUpdateRequested,
            Monitored,
            RemovalRequested,
        }

        /// <summary>
        /// The configuration type of the monitored item.
        /// </summary>
        public enum OpcMonitoredItemConfigurationType
        {
            NodeId = 0,
            ExpandedNodeId
        }

        /// <summary>
        /// The display name to use in the telemetry event for the monitored item.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Flag to signal that the display name was requested by the node configuration.
        /// </summary>
        public bool DisplayNameFromConfiguration { get; set; }

        /// <summary>
        /// The state of the monitored item.
        /// </summary>
        public OpcMonitoredItemState State { get; set; }

        /// <summary>
        /// The OPC UA attributes to use when monitoring the node.
        /// </summary>
        public uint AttributeId { get; set; }

        /// <summary>
        /// The OPC UA monitoring mode to use when monitoring the node.
        /// </summary>
        public MonitoringMode MonitoringMode { get; set; }

        /// <summary>
        /// The requested sampling interval to be used for the node.
        /// </summary>
        public int RequestedSamplingInterval { get; set; }

        /// <summary>
        /// The actual sampling interval used for the node.
        /// </summary>
        public double SamplingInterval { get; set; }

        /// <summary>
        /// Flag to signal that the sampling interval was requested by the node configuration.
        /// </summary>
        public bool RequestedSamplingIntervalFromConfiguration { get; set; }

        /// <summary>
        /// The OPC UA queue size to use for the node monitoring.
        /// </summary>
        public uint QueueSize { get; set; }

        /// <summary>
        /// A flag to control the queue behaviour of the OPC UA stack for the node.
        /// </summary>
        public bool DiscardOldest { get; set; }

        /// <summary>
        /// The event handler of the node in case the OPC UA stack detected a change.
        /// </summary>
        public MonitoredItemNotificationEventHandler Notification { get; set; }

        /// <summary>
        /// The endpoint URL of the OPC UA server this nodes is residing on.
        /// </summary>
        public string EndpointUrl { get; set; }

        /// <summary>
        /// The OPC UA stacks monitored item object.
        /// </summary>
        public MonitoredItem OpcUaClientMonitoredItem { get; set; }

        /// <summary>
        /// The OPC UA identifier of the node in NodeId ("ns=") syntax.
        /// </summary>
        public NodeId ConfigNodeId { get; set; }

        /// <summary>
        /// The OPC UA identifier of the node in ExpandedNodeId ("nsu=") syntax.
        /// </summary>
        public ExpandedNodeId ConfigExpandedNodeId { get; set; }

        /// <summary>
        /// The OPC UA identifier of the node as it was configured.
        /// </summary>
        public string OriginalId { get; set; }

        /// <summary>
        /// Identifies the configuration type of the node.
        /// </summary>
        public OpcMonitoredItemConfigurationType ConfigType { get; set; }

        public const int HeartbeatIntvervalMax = 24 * 60 * 60;

        public static int? HeartbeatIntervalDefault { get; set; } = 0;

        public int HeartbeatInterval
        {
            get => _heartbeatInterval;
            set => _heartbeatInterval = value <= 0 ? 0 : value > HeartbeatIntvervalMax ? HeartbeatIntvervalMax : value;
        }

        public bool HeartbeatIntervalFromConfiguration { get; set; } = false;

        public MessageDataModel HeartbeatMessage { get; set; } = null;

        public Timer HeartbeatSendTimer { get; set; } = null;

        public bool SkipNextEvent { get; set; } = false;

        public static bool SkipFirstDefault { get; set; } = false;

        public bool SkipFirst { get; set; }

        public bool SkipFirstFromConfiguration { get; set; } = false;

        public const string SuppressedOpcStatusCodesDefault = "BadNoCommunication, BadWaitingForInitialData";

        public static List<uint> SuppressedOpcStatusCodes { get; } = new List<uint>();

        /// <summary>
        /// Ctor using NodeId (ns syntax for namespace).
        /// </summary>
        public OpcUaMonitoredItemManager(NodeId nodeId, string sessionEndpointUrl, int? samplingInterval,
            string displayName, int? heartbeatInterval, bool? skipFirst)
        {
            ConfigNodeId = nodeId;
            ConfigExpandedNodeId = null;
            OriginalId = nodeId.ToString();
            ConfigType = OpcMonitoredItemConfigurationType.NodeId;
            Init(sessionEndpointUrl, samplingInterval, displayName, heartbeatInterval, skipFirst);
            State = OpcMonitoredItemState.Unmonitored;
        }

        /// <summary>
        /// Ctor using ExpandedNodeId ("nsu=") syntax.
        /// </summary>
        public OpcUaMonitoredItemManager(ExpandedNodeId expandedNodeId, string sessionEndpointUrl, int? samplingInterval,
            string displayName, int? heartbeatInterval, bool? skipFirst)
        {
            ConfigNodeId = null;
            ConfigExpandedNodeId = expandedNodeId;
            OriginalId = expandedNodeId.ToString();
            ConfigType = OpcMonitoredItemConfigurationType.ExpandedNodeId;
            Init(sessionEndpointUrl, samplingInterval, displayName, heartbeatInterval, skipFirst);
            State = OpcMonitoredItemState.UnmonitoredNamespaceUpdateRequested;
        }

        /// <summary>
        /// Checks if the monitored item does monitor the node described by the given objects.
        /// </summary>
        public bool IsMonitoringThisNode(NodeId nodeId, ExpandedNodeId expandedNodeId, NamespaceTable namespaceTable)
        {
            if (State == OpcMonitoredItemState.RemovalRequested)
            {
                return false;
            }
            if (ConfigType == OpcMonitoredItemConfigurationType.NodeId)
            {
                if (nodeId != null)
                {
                    if (ConfigNodeId == nodeId)
                    {
                        return true;
                    }
                }
                if (expandedNodeId != null)
                {
                    string namespaceUri = namespaceTable.ToArray().ElementAtOrDefault(ConfigNodeId.NamespaceIndex);
                    if (expandedNodeId.NamespaceUri != null && expandedNodeId.NamespaceUri.Equals(namespaceUri, StringComparison.OrdinalIgnoreCase))
                    {
                        if (expandedNodeId.Identifier.ToString().Equals(ConfigNodeId.Identifier.ToString(), StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }
            if (ConfigType == OpcMonitoredItemConfigurationType.ExpandedNodeId)
            {
                if (nodeId != null)
                {
                    int namespaceIndex = namespaceTable.GetIndex(ConfigExpandedNodeId?.NamespaceUri);
                    if (nodeId.NamespaceIndex == namespaceIndex)
                    {
                        if (nodeId.Identifier.ToString().Equals(ConfigExpandedNodeId.Identifier.ToString(), StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
                if (expandedNodeId != null)
                {
                    if (ConfigExpandedNodeId.NamespaceUri != null &&
                        ConfigExpandedNodeId.NamespaceUri.Equals(expandedNodeId.NamespaceUri, StringComparison.OrdinalIgnoreCase) &&
                        ConfigExpandedNodeId.Identifier.ToString().Equals(expandedNodeId.Identifier.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// The notification that the data for a monitored item has changed on an OPC UA server.
        /// </summary>
        public void MonitoredItemNotificationEventHandler(MonitoredItem monitoredItem, MonitoredItemNotificationEventArgs e)
        {
            try
            {
                if (e == null || e.NotificationValue == null || monitoredItem == null || monitoredItem.Subscription == null || monitoredItem.Subscription.Session == null)
                {
                    return;
                }

                if (!(e.NotificationValue is MonitoredItemNotification notification))
                {
                    return;
                }

                if (!(notification.Value is DataValue value))
                {
                    return;
                }

                // filter out configured suppression status codes
                if (SuppressedOpcStatusCodes != null && SuppressedOpcStatusCodes.Contains(notification.Value.StatusCode.Code))
                {
                    Program.Logger.Debug($"Filtered notification with status code '{notification.Value.StatusCode.Code}'");
                    return;
                }

                // stop the heartbeat timer
                HeartbeatSendTimer?.Change(Timeout.Infinite, Timeout.Infinite);

                MessageDataModel messageData = new MessageDataModel();
                if (Program._clientWrapper.IotCentralMode)
                {
                    // for IoTCentral we use the DisplayName as the key in the telemetry and the Value as the value.
                    if (monitoredItem.DisplayName != null)
                    {
                        // use the DisplayName as reported in the MonitoredItem
                        messageData.DisplayName = monitoredItem.DisplayName;
                    }
                    if (value.Value != null)
                    {
                        // use the Value as reported in the notification event argument encoded with the OPC UA JSON endcoder
                        JsonEncoder encoder = new JsonEncoder(monitoredItem.Subscription.Session.MessageContext, false);
                        value.ServerTimestamp = DateTime.MinValue;
                        value.SourceTimestamp = DateTime.MinValue;
                        value.StatusCode = StatusCodes.Good;
                        encoder.WriteDataValue("Value", value);
                        string valueString = encoder.CloseAndReturnText();
                        // we only want the value string, search for everything till the real value starts
                        // and get it
                        string marker = "{\"Value\":{\"Value\":";
                        int markerStart = valueString.IndexOf(marker, StringComparison.InvariantCulture);
                        messageData.PreserveValueQuotes = true;
                        if (markerStart >= 0)
                        {
                            // we either have a value in quotes or just a value
                            int valueLength;
                            int valueStart = marker.Length;
                            if (valueString.IndexOf("\"", valueStart, StringComparison.InvariantCulture) >= 0)
                            {
                                // value is in quotes and two closing curly brackets at the end
                                valueStart++;
                                valueLength = valueString.Length - valueStart - 3;
                            }
                            else
                            {
                                // value is without quotes with two curly brackets at the end
                                valueLength = valueString.Length - marker.Length - 2;
                                messageData.PreserveValueQuotes = false;
                            }
                            messageData.Value = valueString.Substring(valueStart, valueLength);
                        }
                        Program.Logger.Debug($"   IoTCentral key: {messageData.DisplayName}");
                        Program.Logger.Debug($"   IoTCentral values: {messageData.Value}");
                    }
                }
                else
                {
                    // update the required message data to pass only the required data to the hub communication
                    EndpointTelemetryConfigurationModel telemetryConfiguration = Program.TelemetryConfiguration.GetEndpointTelemetryConfiguration(EndpointUrl);

                    // the endpoint URL is required to allow HubCommunication lookup the telemetry configuration
                    messageData.EndpointUrl = EndpointUrl;

                    if (telemetryConfiguration.ExpandedNodeId.Publish == true)
                    {
                        messageData.ExpandedNodeId = ConfigExpandedNodeId?.ToString();
                    }
                    if (telemetryConfiguration.NodeId.Publish == true)
                    {
                        messageData.NodeId = OriginalId;
                    }
                    if (telemetryConfiguration.MonitoredItem.ApplicationUri.Publish == true)
                    {
                        messageData.ApplicationUri = monitoredItem.Subscription.Session.Endpoint.Server.ApplicationUri + (string.IsNullOrEmpty(OpcUaSessionManager.PublisherSite) ? "" : $":{OpcUaSessionManager.PublisherSite}");
                    }
                    if (telemetryConfiguration.MonitoredItem.DisplayName.Publish == true && monitoredItem.DisplayName != null)
                    {
                        // use the DisplayName as reported in the MonitoredItem
                        messageData.DisplayName = monitoredItem.DisplayName;
                    }
                    if (telemetryConfiguration.Value.SourceTimestamp.Publish == true)
                    {
                        // use the SourceTimestamp as reported in the notification event argument in ISO8601 format
                        messageData.SourceTimestamp = value.SourceTimestamp.ToString("o", CultureInfo.InvariantCulture);
                    }
                    if (telemetryConfiguration.Value.StatusCode.Publish == true)
                    {
                        // use the StatusCode as reported in the notification event argument
                        messageData.StatusCode = value.StatusCode.Code;
                    }
                    if (telemetryConfiguration.Value.Status.Publish == true)
                    {
                        // use the StatusCode as reported in the notification event argument to lookup the symbolic name
                        messageData.Status = StatusCode.LookupSymbolicId(value.StatusCode.Code);
                    }
                    if (telemetryConfiguration.Value.Value.Publish == true && value.Value != null)
                    {
                        // use the Value as reported in the notification event argument encoded with the OPC UA JSON endcoder
                        JsonEncoder encoder = new JsonEncoder(monitoredItem.Subscription.Session.MessageContext, false);
                        value.ServerTimestamp = DateTime.MinValue;
                        value.SourceTimestamp = DateTime.MinValue;
                        value.StatusCode = StatusCodes.Good;
                        encoder.WriteDataValue("Value", value);
                        string valueString = encoder.CloseAndReturnText();
                        // we only want the value string, search for everything till the real value starts
                        // and get it
                        string marker = "{\"Value\":{\"Value\":";
                        int markerStart = valueString.IndexOf(marker, StringComparison.InvariantCulture);
                        messageData.PreserveValueQuotes = true;
                        if (markerStart >= 0)
                        {
                            // we either have a value in quotes or just a value
                            int valueLength;
                            int valueStart = marker.Length;
                            if (valueString.IndexOf("\"", valueStart, StringComparison.InvariantCulture) >= 0)
                            {
                                // value is in quotes and two closing curly brackets at the end
                                valueStart++;
                                valueLength = valueString.Length - valueStart - 3;
                            }
                            else
                            {
                                // value is without quotes with two curly brackets at the end
                                valueLength = valueString.Length - marker.Length - 2;
                                messageData.PreserveValueQuotes = false;
                            }
                            messageData.Value = valueString.Substring(valueStart, valueLength);
                        }
                    }

                    // currently the pattern processing is done here, which adds runtime to the notification processing.
                    // In case of perf issues it can be also done in CreateJsonMessageAsync of IoTHubMessaging.cs.

                    // apply patterns
                    messageData.ApplyPatterns(telemetryConfiguration);

                    Program.Logger.Debug($"   ApplicationUri: {messageData.ApplicationUri}");
                    Program.Logger.Debug($"   EndpointUrl: {messageData.EndpointUrl}");
                    Program.Logger.Debug($"   DisplayName: {messageData.DisplayName}");
                    Program.Logger.Debug($"   Value: {messageData.Value}");
                }

                // add message to fifo send queue
                if (monitoredItem.Subscription == null)
                {
                    Program.Logger.Debug($"Subscription already removed. No more details available.");
                }
                else
                {
                    Program.Logger.Debug($"Enqueue a new message from subscription {(monitoredItem.Subscription == null ? "removed" : monitoredItem.Subscription.Id.ToString(CultureInfo.InvariantCulture))}");
                    Program.Logger.Debug($" with publishing interval: {monitoredItem?.Subscription?.PublishingInterval} and sampling interval: {monitoredItem?.SamplingInterval}):");
                }

                // setupo heartbeat processing
                if (HeartbeatInterval > 0)
                {
                    if (HeartbeatMessage != null)
                    {
                        // ensure that the timestamp of the message is larger than the current heartbeat message
                        lock (HeartbeatMessage)
                        {
                            DateTime sourceTimestamp;
                            DateTime heartbeatSourceTimestamp;
                            if (DateTime.TryParse(messageData.SourceTimestamp, out sourceTimestamp) && DateTime.TryParse(HeartbeatMessage.SourceTimestamp, out heartbeatSourceTimestamp))
                            {
                                if (heartbeatSourceTimestamp >= sourceTimestamp)
                                {
                                    Program.Logger.Warning($"HeartbeatMessage has larger or equal timestamp than message. Adjusting...");
                                    sourceTimestamp.AddMilliseconds(1);
                                }
                                messageData.SourceTimestamp = sourceTimestamp.ToString("o", CultureInfo.InvariantCulture);
                            }

                            // store the message for the heartbeat
                            HeartbeatMessage = messageData;
                        }
                    }
                    else
                    {
                        HeartbeatMessage = messageData;
                    }

                    // recharge the heartbeat timer
                    HeartbeatSendTimer.Change(HeartbeatInterval * 1000, HeartbeatInterval * 1000);
                    Program.Logger.Debug($"Setting up {HeartbeatInterval} sec heartbeat for node '{DisplayName}'.");
                }

                // skip event if needed
                if (SkipNextEvent)
                {
                    Program.Logger.Debug($"Skipping first telemetry event for node '{DisplayName}'.");
                    SkipNextEvent = false;
                }
                else
                {
                    // enqueue the telemetry event
                    Program._clientWrapper.Enqueue(messageData);
                }
            }
            catch (Exception ex)
            {
                Program.Logger.Error(ex, "Error processing monitored item notification");
            }
        }

        /// <summary>
        /// Init instance variables.
        /// </summary>
        private void Init(string sessionEndpointUrl, int? samplingInterval, string displayName, int? heartbeatInterval, bool? skipFirst)
        {
            State = OpcMonitoredItemState.Unmonitored;
            AttributeId = Attributes.Value;
            MonitoringMode = MonitoringMode.Reporting;
            QueueSize = 0;
            DiscardOldest = true;
            Notification = new MonitoredItemNotificationEventHandler(MonitoredItemNotificationEventHandler);
            EndpointUrl = sessionEndpointUrl;
            DisplayName = displayName;
            DisplayNameFromConfiguration = string.IsNullOrEmpty(displayName) ? false : true;
            RequestedSamplingInterval = samplingInterval ?? Configurations.OpcApplicationConfiguration.OpcSamplingInterval;
            RequestedSamplingIntervalFromConfiguration = samplingInterval != null ? true : false;
            SamplingInterval = RequestedSamplingInterval;
            HeartbeatInterval = (int)(heartbeatInterval == null ? HeartbeatIntervalDefault : heartbeatInterval);
            HeartbeatIntervalFromConfiguration = heartbeatInterval != null ? true : false;
            SkipFirst = skipFirst == null ? SkipFirstDefault : (bool)skipFirst;
            SkipFirstFromConfiguration = skipFirst != null ? true : false;
        }

        /// <summary>
        /// Timer callback for heartbeat telemetry send.
        /// </summary>
        internal void HeartbeatSend(object state)
        {
            System.Diagnostics.Debug.Assert(state == null);
            // send the last known message
            lock (HeartbeatMessage)
            {
                if (HeartbeatMessage != null)
                {
                    // advance the SourceTimestamp
                    DateTime sourceTimestamp;
                    if (DateTime.TryParse(HeartbeatMessage.SourceTimestamp, out sourceTimestamp))
                    {
                        sourceTimestamp = sourceTimestamp.AddSeconds(HeartbeatInterval);
                        HeartbeatMessage.SourceTimestamp = sourceTimestamp.ToString("o", CultureInfo.InvariantCulture);
                    }

                    // enqueue the message
                    Program._clientWrapper.Enqueue(HeartbeatMessage);
                    Program.Logger.Debug($"Message enqueued for heartbeat with sourceTimestamp '{HeartbeatMessage.SourceTimestamp}'.");
                }
                else
                {
                    Program.Logger.Warning($"No message is available for heartbeat.");
                }
            }
        }

        private int _heartbeatInterval;
    }
}
