// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Newtonsoft.Json;

namespace OpcPublisher
{
    /// <summary>
    /// Class describing a list of nodes
    /// </summary>
    public class OpcNodeOnEndpointModel
    {
        public OpcNodeOnEndpointModel(
            string id,
            string expandedNodeId = null,
            int opcSamplingInterval = 1000,
            int opcPublishingInterval = 0,
            string displayName = null,
            int heartbeatInterval = 0,
            bool skipFirst = false)
        {
            Id = id;
            ExpandedNodeId = expandedNodeId;
            OpcSamplingInterval = opcSamplingInterval;
            OpcPublishingInterval = opcPublishingInterval;
            DisplayName = displayName;
            HeartbeatInterval = heartbeatInterval;
            SkipFirst = skipFirst;
        }

        // Id can be:
        // a NodeId ("ns=")
        // an ExpandedNodeId ("nsu=")
        public string Id { get; set; }

        // support legacy configuration file syntax
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string ExpandedNodeId { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int OpcSamplingInterval { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int OpcPublishingInterval { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string DisplayName { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int HeartbeatInterval { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public bool SkipFirst { get; set; }
    }
}
