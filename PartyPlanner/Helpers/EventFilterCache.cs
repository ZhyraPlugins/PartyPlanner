using PartyPlanner.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PartyPlanner.Helpers;

public class EventFilterCache
{
    private readonly Dictionary<string, (List<EventType> filteredEvents, int stateHash)> cache = [];

    public List<EventType> GetFiltered(
        string dataCenterName,
        List<EventType> allEvents,
        List<string> selectedTags,
        string searchText,
        SortMode sortMode)
    {
        var stateHash = ComputeStateHash(selectedTags, searchText, sortMode);

        if (cache.TryGetValue(dataCenterName, out var cached) && cached.stateHash == stateHash)
            return cached.filteredEvents;

        IEnumerable<EventType> result = selectedTags.Count == 0
            ? allEvents
            : allEvents.Where(ev => selectedTags.All(t => ev.TagsSet.Contains(t)));

        if (!string.IsNullOrEmpty(searchText))
            result = result.Where(ev =>
                ev.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                ev.Description.Contains(searchText, StringComparison.OrdinalIgnoreCase));

        result = sortMode switch
        {
            SortMode.StartsAtDesc      => result.OrderByDescending(e => e.StartsAt),
            SortMode.EndsAtAsc         => result.OrderBy(e => e.EndsAt),
            SortMode.EndsAtDesc        => result.OrderByDescending(e => e.EndsAt),
            SortMode.AttendeeCountDesc => result.OrderByDescending(e => e.AttendeeCount),
            _                          => result.OrderBy(e => e.StartsAt),
        };

        var list = result.ToList();
        cache[dataCenterName] = (list, stateHash);
        return list;
    }

    public void Clear() => cache.Clear();

    private static int ComputeStateHash(List<string> selectedTags, string searchText, SortMode sortMode)
    {
        unchecked
        {
            int hash = 17;
            foreach (var tag in selectedTags.OrderBy(t => t))
                hash = hash * 31 + tag.GetHashCode();
            hash = hash * 31 + searchText.GetHashCode();
            hash = hash * 31 + (int)sortMode;
            return hash;
        }
    }
}
