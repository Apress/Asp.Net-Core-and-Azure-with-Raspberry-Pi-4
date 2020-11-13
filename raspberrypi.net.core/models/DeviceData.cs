using Newtonsoft.Json;
namespace raspberrypi.net.core.Models
{
    public class DeviceData
    {
        [JsonProperty(PropertyName="temperature")]
        public double Temperature { get; set; } = 0;
        [JsonProperty(PropertyName="messageid")]
        public int MessageId { get; set; } = 0;
        [JsonProperty(PropertyName="deviceid")]
        public string DeviceId {get;set;} = Program.DeviceId;
    }
}