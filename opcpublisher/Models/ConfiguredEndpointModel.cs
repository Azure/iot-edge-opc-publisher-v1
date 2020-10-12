namespace OpcPublisher
{
    /// <summary>
    /// Model for configured endpoint response element.
    /// </summary>
    public class ConfiguredEndpointModel
    {
        public ConfiguredEndpointModel(string endpointUrl)
        {
            EndpointUrl = endpointUrl;
        }

        public string EndpointUrl { get; set; }
    }
}
