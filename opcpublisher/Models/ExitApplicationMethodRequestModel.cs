using Newtonsoft.Json;

namespace OpcPublisher
{
    /// <summary>
    /// Model for an exit application request.
    /// </summary>
    public class ExitApplicationMethodRequestModel
    {
        public ExitApplicationMethodRequestModel()
        {
        }

        [JsonProperty(NullValueHandling = NullValueHandling.Include)]
        public int SecondsTillExit { get; set; }
    }
}
