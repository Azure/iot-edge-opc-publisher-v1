// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using OpcPublisher.Configurations;

namespace OpcPublisher
{
    /// <summary>
    /// Class to define the model for the telemetry configuration of an endpoint in the configuration file.
    /// </summary>
    public class EndpointTelemetryConfigurationModel
    {
        /// <summary>
        /// Specifies the endpoint URL the telemetry should be configured for.
        /// </summary>
        public string ForEndpointUrl { get; set; }

        /// <summary>
        /// Specifies the configuration for the value EndpointUrl.
        /// </summary>
        public TelemetrySettingsConfiguration EndpointUrl
        {
            get => _endpointUrl;
            set
            {
                _endpointUrl.Publish = value.Publish;
                _endpointUrl.Name = value.Name;
                _endpointUrl.Pattern = value.Pattern;
            }
        }

        /// <summary>
        /// Specifies the configuration for the value NodeId.
        /// </summary>
        public TelemetrySettingsConfiguration NodeId
        {
            get => _nodeId;
            set
            {
                _nodeId.Publish = value.Publish;
                _nodeId.Name = value.Name;
                _nodeId.Pattern = value.Pattern;
            }
        }

        public TelemetrySettingsConfiguration ExpandedNodeId
        {
            get => _expandedNodeId;
            set
            {
                _expandedNodeId.Publish = value.Publish;
                _expandedNodeId.Name = value.Name;
                _expandedNodeId.Pattern = value.Pattern;
            }
        }

        /// <summary>
        /// Specifies the configuration for the value MonitoredItem.
        /// </summary>
        public MonitoredItemTelemetryConfiguration MonitoredItem
        {
            get => _monitoredItem;
            set
            {
                _monitoredItem.Flat = value.Flat;
                _monitoredItem.ApplicationUri = value.ApplicationUri;
                _monitoredItem.DisplayName = value.DisplayName;
            }
        }

        /// <summary>
        /// Specifies the configuration for the value Value.
        /// </summary>
        public ValueTelemetryConfiguration Value
        {
            get => _value;
            set
            {
                _value.Flat = value.Flat;
                _value.Value = value.Value;
                _value.SourceTimestamp = value.SourceTimestamp;
                _value.StatusCode = value.StatusCode;
                _value.Status = value.Status;
            }
        }

        /// <summary>
        /// Ctor for an object.
        /// </summary>
        public EndpointTelemetryConfigurationModel()
        {
            ForEndpointUrl = null;
            _endpointUrl = new TelemetrySettingsConfiguration();
            _nodeId = new TelemetrySettingsConfiguration();
            _expandedNodeId = new TelemetrySettingsConfiguration();
            _monitoredItem = new MonitoredItemTelemetryConfiguration();
            _value = new ValueTelemetryConfiguration();
        }

        private readonly TelemetrySettingsConfiguration _endpointUrl;
        private readonly MonitoredItemTelemetryConfiguration _monitoredItem;
        private readonly ValueTelemetryConfiguration _value;
        private readonly TelemetrySettingsConfiguration _nodeId;
        private readonly TelemetrySettingsConfiguration _expandedNodeId;
    }
}
