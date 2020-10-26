// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Opc.Ua;
using System.Net;

namespace OpcPublisher
{
    /// <summary>
    /// Describes the publishing information of a node.
    /// </summary>
    public class NodePublishingConfigurationModel
    {
        /// <summary>
        /// The endpoint URL of the OPC UA server.
        /// </summary>
        public string EndpointUrl { get; set; }

        /// <summary>
        /// Flag if a secure transport should be used to connect to the endpoint.
        /// </summary>
        public bool UseSecurity { get; set; }

        /// <summary>
        /// The node to monitor in "ns=" syntax.
        /// </summary>
        public NodeId NodeId { get; set; }

        /// <summary>
        /// The node to monitor in "nsu=" syntax.
        /// </summary>
        public ExpandedNodeId ExpandedNodeId { get; set; }

        /// <summary>
        /// The node id as it was configured.
        /// </summary>
        public string OriginalId { get; set; }

        /// <summary>
        /// The display name to use for the node in telemetry events.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// The OPC UA sampling interval for the node.
        /// </summary>
        public int? OpcSamplingInterval { get; set; }

        /// <summary>
        /// The OPC UA publishing interval for the node.
        /// </summary>
        public int? OpcPublishingInterval { get; set; }

        /// <summary>
        /// Flag to enable a hardbeat telemetry event publish for the node.
        /// </summary>
        public int? HeartbeatInterval { get; set; }

        /// <summary>
        /// Flag to skip the first telemetry event for the node after connect.
        /// </summary>
        public bool? SkipFirst { get; set; }

        /// <summary>
        /// Gets or sets the authentication mode to authenticate against the OPC UA Server.
        /// </summary>
        public OpcUserSessionAuthenticationMode OpcAuthenticationMode { get; set; }

        /// <summary>
        /// Gets or sets the auth credential when OpcAuthenticationMode is set to UsernamePassword.
        /// </summary>
        public NetworkCredential AuthCredential { get; set; }
    }
}
