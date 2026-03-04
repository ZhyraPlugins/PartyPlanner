using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace PartyPlanner.Tests;

// Minimal copies of the production models, no Dalamud dependency.

public class EventType
{
    [JsonProperty("id")]
    public int Id { get; set; }
    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;
    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;
    [JsonProperty("location")]
    public string Location { get; set; } = string.Empty;
    [JsonProperty("attendeeCount")]
    public int AttendeeCount { get; set; }
    [JsonProperty("startsAt")]
    public DateTime StartsAt { get; set; }
    [JsonProperty("endsAt")]
    public DateTime EndsAt { get; set; }
    [JsonProperty("tags")]
    public string[] Tags { get; set; } = Array.Empty<string>();
    [JsonProperty("attachments")]
    public string[] Attachments { get; set; } = Array.Empty<string>();
    [JsonProperty("locationData")]
    public EventLocationData? LocationData { get; set; }

    private HashSet<string>? _tagsSet;
    [JsonIgnore]
    public HashSet<string> TagsSet => _tagsSet ??= new HashSet<string>(Tags);
}

public class EventLocationData
{
    [JsonProperty("server")]
    public EventServerData? Server { get; set; }
    [JsonProperty("dataCenter")]
    public EventServerData? DataCenter { get; set; }
}

public class EventServerData
{
    [JsonProperty("id")]
    public int Id { get; set; }
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;
    [JsonProperty("dataCenterId")]
    public int DataCenterId { get; set; }
}

public class EventsResponseType
{
    [JsonProperty("events")]
    public List<EventType> Events { get; set; } = [];
}
