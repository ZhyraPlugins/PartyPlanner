using PartyPlanner.Models;
using System.Collections.Generic;
using System.Linq;

namespace PartyPlanner.Helpers;

/// <summary>
/// Caches filtered event lists by datacenter and tag selection state.
/// </summary>
public class EventFilterCache
{
    private readonly Dictionary<string, (List<EventType> filteredEvents, int tagStateHash)> cache = [];

    /// <summary>
    /// Gets events for a datacenter filtered by selected tags.
    /// Returns cached result if tag selection hasn't changed.
    /// </summary>
    public List<EventType> GetFiltered(string dataCenterName, List<EventType> allEvents, List<string> selectedTags)
    {
        var tagStateHash = ComputeTagStateHash(selectedTags);

        if (cache.TryGetValue(dataCenterName, out var cached) && cached.tagStateHash == tagStateHash)
        {
            return cached.filteredEvents;
        }

        List<EventType> filteredEvents;
        if (selectedTags.Count == 0)
        {
            filteredEvents = allEvents;
        }
        else
        {
            filteredEvents = allEvents.Where(ev =>
                selectedTags.All(tag => ev.TagsSet.Contains(tag))
            ).ToList();
        }

        cache[dataCenterName] = (filteredEvents.ToList(), tagStateHash);
        return filteredEvents;
    }

    /// <summary>
    /// Computes a hash from the list of selected tags.
    /// </summary>
    private static int ComputeTagStateHash(List<string> selectedTags)
    {
        unchecked
        {
            int hash = 17;
            foreach (var tag in selectedTags.OrderBy(t => t))
            {
                hash = hash * 31 + tag.GetHashCode();
            }
            return hash;
        }
    }

    /// <summary>
    /// Clears all cached filtered lists.
    /// </summary>
    public void Clear()
    {
        cache.Clear();
    }
}
