using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PartyPlanner.Models
{
    public class EventType
    {
        [JsonProperty("locationId")]
        public int LocationId { get; set; }
        [JsonProperty("description")]
        public string Description { get; set; } = string.Empty;
        [JsonProperty("title")]
        public string Title { get; set; } = string.Empty;
        [JsonProperty("tags")]
        public string[] Tags { get; set; } = Array.Empty<string>();
        [JsonProperty("startsAt")]
        public DateTime StartsAt { get; set; }
        [JsonProperty("endsAt")]
        public DateTime EndsAt { get; set; }
        [JsonProperty("location")]
        public string Location { get; set; } = string.Empty;
        [JsonProperty("attendeeCount")]
        public int AttendeeCount { get; set; }
        [JsonProperty("attachments")]
        public string[] Attachments { get; set; } = Array.Empty<string>();
    }

    public class EventsResponseType
    {
        [JsonProperty("events")]
        public List<EventType> Events { get; set; } = new List<EventType>();
    }

    public class ActiveEventsResponseType
    {
        [JsonProperty("activeEvents")]
        public List<EventType> ActiveEvents { get; set; } = new List<EventType>();
    }
}
