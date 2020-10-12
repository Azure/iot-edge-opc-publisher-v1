using Newtonsoft.Json;

namespace OpcPublisher
{
    /// <summary>
    /// Model for a get configured nodes on endpoint request.
    /// </summary>
    public class GetConfiguredNodesOnEndpointMethodRequestModel
    {
        public GetConfiguredNodesOnEndpointMethodRequestModel(string endpointUrl, ulong? continuationToken = null)
        {
            EndpointUrl = endpointUrl;
            ContinuationToken = continuationToken;
        }
        [JsonProperty(NullValueHandling = NullValueHandling.Include)]
        public string EndpointUrl { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Include)]
        public ulong? ContinuationToken { get; set; }
    }
}
