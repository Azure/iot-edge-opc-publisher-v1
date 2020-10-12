// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace OpcPublisher.Configurations
{
    /// <summary>
    /// Class to define the Value related telemetry configuration.
    /// </summary>
    public class ValueTelemetryConfiguration
    {
        /// <summary>
        /// Controls if the Value object should be flattened.
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
        /// The Value value telemetry configuration.
        /// </summary>
        public TelemetrySettingsConfiguration Value
        {
            get => _value;
            set
            {
                _value.Publish = value.Publish;
                _value.Name = value.Name;
                _value.Pattern = value.Pattern;
            }
        }

        /// <summary>
        /// The SourceTimestamp value telemetry configuration.
        /// </summary>
        public TelemetrySettingsConfiguration SourceTimestamp
        {
            get => _sourceTimestamp;
            set
            {
                _sourceTimestamp.Publish = value.Publish;
                _sourceTimestamp.Name = value.Name;
                _sourceTimestamp.Pattern = value.Pattern;
            }
        }

        /// <summary>
        /// The StatusCode value telemetry configuration.
        /// </summary>
        public TelemetrySettingsConfiguration StatusCode
        {
            get => _statusCode;
            set
            {
                _statusCode.Publish = value.Publish;
                _statusCode.Name = value.Name;
                _statusCode.Pattern = value.Pattern;
            }
        }

        /// <summary>
        /// The Status value telemetry configuration.
        /// </summary>
        public TelemetrySettingsConfiguration Status
        {
            get => _status;
            set
            {
                _status.Publish = value.Publish;
                _status.Name = value.Name;
                _status.Pattern = value.Pattern;
            }
        }

        /// <summary>
        /// Ctor of the object.
        /// </summary>
        public ValueTelemetryConfiguration()
        {
            _flat = null;
            _value = new TelemetrySettingsConfiguration();
            _sourceTimestamp = new TelemetrySettingsConfiguration();
            _statusCode = new TelemetrySettingsConfiguration();
            _status = new TelemetrySettingsConfiguration();
        }

        private bool? _flat;
        private readonly TelemetrySettingsConfiguration _value;
        private readonly TelemetrySettingsConfiguration _sourceTimestamp;
        private readonly TelemetrySettingsConfiguration _statusCode;
        private readonly TelemetrySettingsConfiguration _status;
    }
}
