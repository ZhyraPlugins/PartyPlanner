using System;
using System.Collections.Generic;
using System.Linq;

namespace PartyPlanner.Tests;

public class EventFilterCacheTests
{
    // Minimal copy of EventFilterCache — no Dalamud dependency.
    private sealed class EventFilterCache
    {
        private readonly Dictionary<string, (List<EventType> filtered, int hash)> _cache = [];

        public List<EventType> GetFiltered(string dc, List<EventType> all, List<string> tags)
        {
            var hash = ComputeHash(tags);
            if (_cache.TryGetValue(dc, out var cached) && cached.hash == hash)
                return cached.filtered;

            var result = tags.Count == 0
                ? all.ToList()
                : all.Where(e => tags.All(t => e.TagsSet.Contains(t))).ToList();

            _cache[dc] = (result, hash);
            return result;
        }

        public void Clear() => _cache.Clear();

        private static int ComputeHash(List<string> tags)
        {
            unchecked
            {
                int h = 17;
                foreach (var t in tags.OrderBy(x => x))
                    h = h * 31 + t.GetHashCode();
                return h;
            }
        }
    }

    private static EventType MakeEvent(int id, params string[] tags) => new()
    {
        Id = id,
        Title = $"Event {id}",
        Tags = tags,
        StartsAt = DateTime.UtcNow.AddHours(1),
        EndsAt = DateTime.UtcNow.AddHours(3),
    };

    private static List<EventType> SampleEvents() =>
    [
        MakeEvent(1, "dance", "rp"),
        MakeEvent(2, "dance"),
        MakeEvent(3, "rp"),
        MakeEvent(4, "housing", "rp"),
        MakeEvent(5),
    ];

    [Fact]
    public void NoTagsReturnsAllEvents()
    {
        var cache = new EventFilterCache();
        var events = SampleEvents();
        var result = cache.GetFiltered("Chaos", events, []);
        Assert.Equal(events.Count, result.Count);
    }

    [Fact]
    public void SingleTagFiltersCorrectly()
    {
        var cache = new EventFilterCache();
        var result = cache.GetFiltered("Chaos", SampleEvents(), ["dance"]);
        Assert.Equal(2, result.Count);
        Assert.All(result, e => Assert.Contains("dance", e.Tags));
    }

    [Fact]
    public void MultipleTagsAreAndedTogether()
    {
        var cache = new EventFilterCache();
        var result = cache.GetFiltered("Chaos", SampleEvents(), ["dance", "rp"]);
        Assert.Single(result);
        Assert.Equal(1, result[0].Id);
    }

    [Fact]
    public void NoMatchingTagReturnsEmpty()
    {
        var cache = new EventFilterCache();
        var result = cache.GetFiltered("Chaos", SampleEvents(), ["unknown-tag"]);
        Assert.Empty(result);
    }

    [Fact]
    public void CacheHitReturnsSameList()
    {
        var cache = new EventFilterCache();
        var events = SampleEvents();
        var first = cache.GetFiltered("Chaos", events, ["rp"]);
        var second = cache.GetFiltered("Chaos", events, ["rp"]);
        Assert.Same(first, second);
    }

    [Fact]
    public void DifferentDcHasIsolatedCache()
    {
        var cache = new EventFilterCache();
        var events = SampleEvents();
        var chaos = cache.GetFiltered("Chaos", events, ["dance"]);
        var light = cache.GetFiltered("Light", events, ["rp"]);
        Assert.Equal(2, chaos.Count);
        Assert.Equal(3, light.Count);
    }

    [Fact]
    public void ClearInvalidatesCache()
    {
        var cache = new EventFilterCache();
        var events = SampleEvents();
        var before = cache.GetFiltered("Chaos", events, ["dance"]);
        cache.Clear();
        var after = cache.GetFiltered("Chaos", events, ["dance"]);
        Assert.NotSame(before, after);
        Assert.Equal(before.Count, after.Count);
    }

    [Fact]
    public void TagOrderDoesNotAffectCacheKey()
    {
        var cache = new EventFilterCache();
        var events = SampleEvents();
        var first = cache.GetFiltered("Chaos", events, ["rp", "dance"]);
        var second = cache.GetFiltered("Chaos", events, ["dance", "rp"]);
        Assert.Same(first, second);
    }
}
