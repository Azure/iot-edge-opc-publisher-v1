﻿
using Opc.Ua.Client;
using System;
using System.Linq;

namespace OpcPublisher
{
    using Opc.Ua;
    using static HubCommunication;
    using static OpcApplicationConfiguration;
    using static OpcPublisher.PublisherTelemetryConfiguration;
    using static Program;

    /// <summary>
    /// Class to manage the OPC monitored items, which are the nodes we need to publish.
    /// </summary>
    public class OpcMonitoredItem
    {
        public enum OpcMonitoredItemState
        {
            Unmonitored = 0,
            UnmonitoredNamespaceUpdateRequested,
            Monitored,
            RemovalRequested,
        }

        public enum OpcMonitoredItemConfigurationType
        {
            NodeId = 0,
            ExpandedNodeId
        }

        public string DisplayName { get; set; }
        public OpcMonitoredItemState State { get; set; }
        public uint AttributeId { get; set; }
        public MonitoringMode MonitoringMode { get; set; }
        public int RequestedSamplingInterval { get; set; }
        public int SamplingInterval { get; set; }
        public uint QueueSize { get; set; }
        public bool DiscardOldest { get; set; }
        public MonitoredItemNotificationEventHandler Notification { get; set; }
        public Uri EndpointUrl { get; set; }
        public MonitoredItem OpcUaClientMonitoredItem { get; set; }
        public NodeId ConfigNodeId { get; set; }
        public ExpandedNodeId ConfigExpandedNodeId { get; set; }
        public string OriginalId { get; set; }
        public OpcMonitoredItemConfigurationType ConfigType { get; set; }

        /// <summary>
        /// Ctor using NodeId (ns syntax for namespace).
        /// </summary>
        public OpcMonitoredItem(NodeId nodeId, Uri sessionEndpointUrl, string displayName)
        {
            ConfigNodeId = nodeId;
            ConfigExpandedNodeId = null;
            OriginalId = nodeId.ToString();
            ConfigType = OpcMonitoredItemConfigurationType.NodeId;
            Init(sessionEndpointUrl);
            State = OpcMonitoredItemState.Unmonitored;
            DisplayName = displayName;
        }

        /// <summary>
        /// Ctor using ExpandedNodeId (nsu syntax for namespace).
        /// </summary>
        public OpcMonitoredItem(ExpandedNodeId expandedNodeId, Uri sessionEndpointUrl, string displayName)
        {
            ConfigNodeId = null;
            ConfigExpandedNodeId = expandedNodeId;
            OriginalId = expandedNodeId.ToString();
            ConfigType = OpcMonitoredItemConfigurationType.ExpandedNodeId;
            Init(sessionEndpointUrl);
            State = OpcMonitoredItemState.UnmonitoredNamespaceUpdateRequested;
            DisplayName = displayName;
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
        /// Class used to pass data from the MonitoredItem notification to the hub message processing.
        /// </summary>
        public class MessageData
        {
            public string EndpointUrl;
            public string NodeId;
            public string ApplicationUri;
            public string DisplayName;
            public string Value;
            public string SourceTimestamp;
            public uint? StatusCode;
            public string Status;
            public bool PreserveValueQuotes;

            public MessageData()
            {
                EndpointUrl = null;
                NodeId = null;
                ApplicationUri = null;
                DisplayName = null;
                Value = null;
                StatusCode = null;
                SourceTimestamp = null;
                Status = null;
                PreserveValueQuotes = false;
            }

            public void ApplyPatterns(EndpointTelemetryConfiguration telemetryConfiguration)
            {
                if (telemetryConfiguration.EndpointUrl.Publish == true)
                {
                    EndpointUrl = telemetryConfiguration.EndpointUrl.PatternMatch(EndpointUrl);
                }
                if (telemetryConfiguration.NodeId.Publish == true)
                {
                    NodeId = telemetryConfiguration.NodeId.PatternMatch(NodeId);
                }
                if (telemetryConfiguration.MonitoredItem.ApplicationUri.Publish == true)
                {
                    ApplicationUri = telemetryConfiguration.MonitoredItem.ApplicationUri.PatternMatch(ApplicationUri);
                }
                if (telemetryConfiguration.MonitoredItem.DisplayName.Publish == true)
                {
                    DisplayName = telemetryConfiguration.MonitoredItem.DisplayName.PatternMatch(DisplayName);
                }
                if (telemetryConfiguration.Value.Value.Publish == true)
                {
                    Value = telemetryConfiguration.Value.Value.PatternMatch(Value);
                }
                if (telemetryConfiguration.Value.SourceTimestamp.Publish == true)
                {
                    SourceTimestamp = telemetryConfiguration.Value.SourceTimestamp.PatternMatch(SourceTimestamp);
                }
                if (telemetryConfiguration.Value.StatusCode.Publish == true && StatusCode != null)
                {
                    if (!string.IsNullOrEmpty(telemetryConfiguration.Value.StatusCode.Pattern))
                    {
                        Logger.Information($"'Pattern' settngs for StatusCode are ignored.");
                    }
                }
                if (telemetryConfiguration.Value.Status.Publish == true)
                {
                    Status = telemetryConfiguration.Value.Status.PatternMatch(Status);
                }
            }
        }


        /// <summary>
        /// The notification that the data for a monitored item has changed on an OPC UA server.
        /// </summary>
        public void MonitoredItem_Notification(MonitoredItem monitoredItem, MonitoredItemNotificationEventArgs args)
        {
            try
            {
                if (args == null || args.NotificationValue == null || monitoredItem == null || monitoredItem.Subscription == null || monitoredItem.Subscription.Session == null)
                {
                    return;
                }

                MonitoredItemNotification notification = args.NotificationValue as MonitoredItemNotification;
                if (notification == null)
                {
                    return;
                }

                DataValue value = notification.Value as DataValue;
                if (value == null)
                {
                    return;
                }

                MessageData messageData = new MessageData();
                if (IotCentralMode)
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
                        int markerStart = valueString.IndexOf(marker);
                        messageData.PreserveValueQuotes = true;
                        if (markerStart >= 0)
                        {
                            // we either have a value in quotes or just a value
                            int valueLength;
                            int valueStart = marker.Length;
                            if (valueString.IndexOf("\"", valueStart) >= 0)
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
                        Logger.Debug($"   IoTCentral key: {messageData.DisplayName}");
                        Logger.Debug($"   IoTCentral values: {messageData.Value}");
                    }
                }
                else
                {
                    // update the required message data to pass only the required data to HubCommunication
                    EndpointTelemetryConfiguration telemetryConfiguration = GetEndpointTelemetryConfiguration(EndpointUrl.AbsoluteUri);

                    // the endpoint URL is required to allow HubCommunication lookup the telemetry configuration
                    messageData.EndpointUrl = EndpointUrl.AbsoluteUri;
                    if (telemetryConfiguration.NodeId.Publish == true)
                    {
                        messageData.NodeId = OriginalId;
                    }
                    if (telemetryConfiguration.MonitoredItem.ApplicationUri.Publish == true)
                    {
                        messageData.ApplicationUri = (monitoredItem.Subscription.Session.Endpoint.Server.ApplicationUri + (string.IsNullOrEmpty(OpcSession.PublisherSite) ? "" : $":{OpcSession.PublisherSite}"));
                    }
                    if (telemetryConfiguration.MonitoredItem.DisplayName.Publish == true && monitoredItem.DisplayName != null)
                    {
                        // use the DisplayName as reported in the MonitoredItem
                        messageData.DisplayName = monitoredItem.DisplayName;
                    }
                    if (telemetryConfiguration.Value.SourceTimestamp.Publish == true && value.SourceTimestamp != null)
                    {
                        // use the SourceTimestamp as reported in the notification event argument in ISO8601 format
                        messageData.SourceTimestamp = value.SourceTimestamp.ToString("o");
                    }
                    if (telemetryConfiguration.Value.StatusCode.Publish == true && value.StatusCode != null)
                    {
                        // use the StatusCode as reported in the notification event argument
                        messageData.StatusCode = value.StatusCode.Code;
                    }
                    if (telemetryConfiguration.Value.Status.Publish == true && value.StatusCode != null)
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
                        int markerStart = valueString.IndexOf(marker);
                        messageData.PreserveValueQuotes = true;
                        if (markerStart >= 0)
                        {
                            // we either have a value in quotes or just a value
                            int valueLength;
                            int valueStart = marker.Length;
                            if (valueString.IndexOf("\"", valueStart) >= 0)
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

                    Logger.Debug($"   ApplicationUri: {messageData.ApplicationUri}");
                    Logger.Debug($"   EndpointUrl: {messageData.EndpointUrl}");
                    Logger.Debug($"   DisplayName: {messageData.DisplayName}");
                    Logger.Debug($"   Value: {messageData.Value}");
                }

                // add message to fifo send queue
                if (monitoredItem.Subscription == null)
                {
                    Logger.Debug($"Subscription already removed. No more details available.");
                }
                else
                {
                    Logger.Debug($"Enqueue a new message from subscription {(monitoredItem.Subscription == null ? "removed" : monitoredItem.Subscription.Id.ToString())}");
                    Logger.Debug($" with publishing interval: {monitoredItem.Subscription.PublishingInterval} and sampling interval: {monitoredItem.SamplingInterval}):");
                }
                HubCommunication.Enqueue(messageData);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Error processing monitored item notification");
            }
        }

        /// <summary>
        /// Init instance variables.
        /// </summary>
        private void Init(Uri sessionEndpointUrl)
        {
            State = OpcMonitoredItemState.Unmonitored;
            DisplayName = string.Empty;
            AttributeId = Attributes.Value;
            MonitoringMode = MonitoringMode.Reporting;
            RequestedSamplingInterval = OpcSamplingInterval;
            QueueSize = 0;
            DiscardOldest = true;
            Notification = new MonitoredItemNotificationEventHandler(MonitoredItem_Notification);
            EndpointUrl = sessionEndpointUrl;
        }
    }
}