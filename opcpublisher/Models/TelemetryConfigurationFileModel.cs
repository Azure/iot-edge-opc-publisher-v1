// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System.Collections.Generic;

namespace OpcPublisher
{
    /// <summary>
    /// Class to define the telemetryconfiguration.json configuration file layout.
    /// </summary>
    public class TelemetryConfigurationFileModel
    {
        /// <summary>
        /// Default settings for all endpoints without specific configuration.
        /// </summary>
        public EndpointTelemetryConfigurationModel Defaults { get; set; }

        /// <summary>
        /// Endpoint specific configuration.
        /// </summary>
        public List<EndpointTelemetryConfigurationModel> EndpointSpecific { get; }

        /// <summary>
        /// Ctor for the telemetry configuration.
        /// </summary>
        public TelemetryConfigurationFileModel()
        {
            EndpointSpecific = new List<EndpointTelemetryConfigurationModel>();
        }
    }
}
