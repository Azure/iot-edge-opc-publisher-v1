// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Opc.Ua;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace OpcPublisher
{
    /// <summary>
    /// Class describing the nodes which should be published. It supports two formats:
    /// - NodeId syntax using the namespace index (ns) syntax. This is only used in legacy environments and is only supported for backward compatibility.
    /// - List of ExpandedNodeId syntax, to allow putting nodes with similar publishing and/or sampling intervals in one object
    /// </summary>
    public partial class ConfigurationFileEntryLegacyModel
    {
        /// <summary>
        /// Ctor of the object.
        /// </summary>
        public ConfigurationFileEntryLegacyModel()
        {
        }

        /// <summary>
         /// Ctor of the object.
         /// </summary>
        public ConfigurationFileEntryLegacyModel(string nodeId, string endpointUrl)
        {
            NodeId = new NodeId(nodeId);
            EndpointUrl = new Uri(endpointUrl);
        }

        /// <summary>
        /// The endpoint URL of the OPC UA server.
        /// </summary>
        public Uri EndpointUrl { get; set; }

        /// <summary>
        /// Flag if a secure transport should be used to connect to the endpoint.
        /// </summary>
        [DefaultValue(true)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
        public bool? UseSecurity { get; set; }

        /// <summary>
        /// The node to monitor in "ns=" syntax. This key is only supported for backward compatibility and should not be used anymore.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public NodeId NodeId { get; set; }

        /// <summary>
        /// Gets ot sets the authentication mode to authenticate against the OPC UA Server.
        /// </summary>
        [DefaultValue(OpcAuthenticationMode.Anonymous)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(StringEnumConverter))]
        public OpcAuthenticationMode OpcAuthenticationMode { get; set; }

        /// <summary>
        /// Gets or sets the encrypted username to authenticate against the OPC UA Server (when OpcAuthenticationMode is set to UsernamePassword
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
        public string EncryptedAuthUsername
        {
            get
            {
                return EncryptedAuthCredential?.UserName;
            }
            set
            {
                if (EncryptedAuthCredential == null)
                    EncryptedAuthCredential = new EncryptedNetworkCredential();

                EncryptedAuthCredential.UserName = value;
            }
        }

        /// <summary>
        /// Gets or sets the encrypted password to authenticate against the OPC UA Server (when OpcAuthenticationMode is set to UsernamePassword
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
        public string EncryptedAuthPassword
        {
            get
            {
                return EncryptedAuthCredential?.Password;
            }
            set
            {
                if (EncryptedAuthCredential == null)
                    EncryptedAuthCredential = new EncryptedNetworkCredential();

                EncryptedAuthCredential.Password = value;
            }
        }

        /// <summary>
        /// Gets or sets the encrypted auth credential when OpcAuthenticationMode is set to UsernamePassword.
        /// </summary>
        [JsonIgnore]
        public EncryptedNetworkCredential EncryptedAuthCredential { get; set; }

        /// <summary>
        /// Instead all nodes should be defined in this collection.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
#pragma warning disable CA2227 // Collection properties should be read only
        public List<OpcNodeOnEndpointModel> OpcNodes { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only
    }
}
