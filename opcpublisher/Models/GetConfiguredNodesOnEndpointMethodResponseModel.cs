using Newtonsoft.Json;
using System.Collections.Generic;

namespace OpcPublisher
{
    /// <summary>
    /// Model class for a get configured nodes on endpoint response.
    /// </summary>
    public class GetConfiguredNodesOnEndpointMethodResponseModel
    {
        public GetConfiguredNodesOnEndpointMethodResponseModel()
        {
            OpcNodes = new List<OpcNodeOnEndpointModel>();
        }

        /// <param name="nodes"></param>
        public GetConfiguredNodesOnEndpointMethodResponseModel(List<OpcNodeOnEndpointModel> nodes)
        {
            OpcNodes = nodes;
        }

        [JsonProperty(NullValueHandling = NullValueHandling.Include)]
        public string EndpointUrl { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Include)]
        public List<OpcNodeOnEndpointModel> OpcNodes { get; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public ulong? ContinuationToken { get; set; }
    }
}
