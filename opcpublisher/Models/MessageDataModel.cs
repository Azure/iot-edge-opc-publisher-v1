// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------


namespace OpcPublisher
{
    /// <summary>
    /// Class used to pass data from the MonitoredItem notification to the hub message processing.
    /// </summary>
    public class MessageDataModel
    {
        /// <summary>
        /// The endpoint URL the monitored item belongs to.
        /// </summary>
        public string EndpointUrl { get; set; }

        /// <summary>
        /// The OPC UA NodeId of the monitored item.
        /// </summary>
        public string NodeId { get; set; }

        /// <summary>
        /// The OPC UA Node Id with the namespace expanded.
        /// </summary>
        public string ExpandedNodeId { get; set; }

        /// <summary>
        /// The Application URI of the OPC UA server the node belongs to.
        /// </summary>
        public string ApplicationUri { get; set; }

        /// <summary>
        /// The display name of the node.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// The value of the node.
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// The OPC UA source timestamp the value was seen.
        /// </summary>
        public string SourceTimestamp { get; set; }

        /// <summary>
        /// The OPC UA status code of the value.
        /// </summary>
        public uint? StatusCode { get; set; }

        /// <summary>
        /// The OPC UA status of the value.
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Flag if the encoding of the value should preserve quotes.
        /// </summary>
        public bool PreserveValueQuotes { get; set; }

        /// <summary>
        /// Ctor of the object.
        /// </summary>
        public MessageDataModel()
        {
            EndpointUrl = null;
            NodeId = null;
            ExpandedNodeId = null;
            ApplicationUri = null;
            DisplayName = null;
            Value = null;
            StatusCode = null;
            SourceTimestamp = null;
            Status = null;
            PreserveValueQuotes = false;
        }

        /// <summary>
        /// Apply the patterns specified in the telemetry configuration on the message data fields.
        /// </summary>
        public void ApplyPatterns(EndpointTelemetryConfigurationModel telemetryConfiguration)
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
                    Program.Logger.Information($"'Pattern' settngs for StatusCode are ignored.");
                }
            }
            if (telemetryConfiguration.Value.Status.Publish == true)
            {
                Status = telemetryConfiguration.Value.Status.PatternMatch(Status);
            }
        }
    }
}
