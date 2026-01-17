namespace PartyPlanner.Helpers;

/// <summary>
/// Stores pre-formatted display strings for an event.
/// </summary>
public class CachedEventStrings
{
    public string StartsAtHumanized { get; set; } = string.Empty;
    public string EndsAtHumanized { get; set; } = string.Empty;
    public string StartsAtLocal { get; set; } = string.Empty;
    public string EndsAtLocal { get; set; } = string.Empty;
    public string FormattedTags { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
}
