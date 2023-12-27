using Newtonsoft.Json;

namespace PartyPlanner.Models
{
    public class ServerType
    {
        public ServerType(int id, string name, int dataCenter)
        {
            Id = id;
            Name = name;
            DataCenter = dataCenter;
        }

        [JsonProperty("id")]
        public int Id { get; set; }
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;
        [JsonProperty("datacenter")]
        public int DataCenter { get; set; }


    }

    public class DataCenterType
    {
        [JsonProperty("id")]
        public int Id { get; set; }
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;
        [JsonProperty("location")]
        public int Region { get; set; } = 0;

        public DataCenterType(int Id, string Name, int Region)
        {
            this.Id = Id;
            this.Name = Name;
            this.Region = Region;
        }
    }
}
