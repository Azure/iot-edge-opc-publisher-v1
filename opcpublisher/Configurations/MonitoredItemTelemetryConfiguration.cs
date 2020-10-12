// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace OpcPublisher.Configurations
{
    /// <summary>
    /// Class to define the MonitoredItem related telemetry configuration.
    /// </summary>
    public class MonitoredItemTelemetryConfiguration
    {
        /// <summary>
        /// Controls if the MonitoredItem object should be flattened.
        /// </summary>
        public bool? Flat
        {
            get => _flat;
            set
            {
                if (value != null)
                {
                    _flat = value;
                }
            }
        }

        /// <summary>
        /// The ApplicationUri value telemetry configuration.
        /// </summary>
        public TelemetrySettingsConfiguration ApplicationUri
        {
            get => _applicationUri;
            set
            {
                _applicationUri.Publish = value.Publish;
                _applicationUri.Name = value.Name;
                _applicationUri.Pattern = value.Pattern;
            }
        }

        /// <summary>
        /// The DisplayName value telemetry configuration.
        /// </summary>
        public TelemetrySettingsConfiguration DisplayName
        {
            get => _displayName;
            set
            {
                _displayName.Publish = value.Publish;
                _displayName.Name = value.Name;
                _displayName.Pattern = value.Pattern;
            }
        }

        /// <summary>
        /// Ctor of the object.
        /// </summary>
        public MonitoredItemTelemetryConfiguration()
        {
            _flat = null;
            _applicationUri = new TelemetrySettingsConfiguration();
            _displayName = new TelemetrySettingsConfiguration();
        }

        private bool? _flat;
        private readonly TelemetrySettingsConfiguration _applicationUri;
        private readonly TelemetrySettingsConfiguration _displayName;
    }
}
