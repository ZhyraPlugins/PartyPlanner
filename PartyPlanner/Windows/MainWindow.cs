using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using PartyPlanner.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;


namespace PartyPlanner.Windows;

public sealed class MainWindow : Window, IDisposable
{
    private readonly PartyVerseApi partyVerseApi;
    // All the events
    private readonly List<Models.EventType> partyVerseEvents = new(50);
    private readonly Dictionary<string, List<Models.EventType>> eventsByDc = [];
    private readonly Dictionary<string, SortedDictionary<string, bool>> tagsByDc = [];
    private DateTime lastUpdate = DateTime.Now;
    private string? displayError = null;
    private Configuration Configuration { get; init; }

    // Caches for string formatting and event filtering
    private readonly EventStringCache eventStringCache = new();
    private readonly EventFilterCache eventFilterCache = new();

    private CancellationTokenSource _cts = new();
    private readonly object _dataLock = new();

    public MainWindow(Configuration configuration) : base("PartyPlanner", ImGuiWindowFlags.None)
    {
        partyVerseApi = new PartyVerseApi();
        this.Configuration = configuration;

        SizeCondition = ImGuiCond.FirstUseEver;
        Size = new Vector2(1000, 500);

        try
        {
            System.Diagnostics.FileVersionInfo fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(Plugin.PluginInterface.AssemblyLocation.FullName);
            string version = fvi.FileVersion!;
            WindowName = string.Format("PartyPlanner v{0}", version);
        }
        catch (Exception e)
        {
            Plugin.Logger.Error(e, "error loading assembly");
        }

        Task.Run(() => UpdateEvents(_cts.Token));
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        partyVerseApi.Dispose();
    }

    public async Task UpdateEvents(CancellationToken ct = default)
    {
        lock (_dataLock)
        {
            displayError = null;
        }

        var localEvents = new List<Models.EventType>(50);
        var localByDc = new Dictionary<string, List<Models.EventType>>();
        var localTagsByDc = new Dictionary<string, SortedDictionary<string, bool>>();

        int page = 0;
        bool queryMore = true;

        try
        {
            while (queryMore)
            {
                var newEvents = await partyVerseApi.GetActiveEvents(page);
                queryMore = newEvents.Count >= 100;
                localEvents.AddRange(newEvents);
                page += 1;
            }

            page = 0;
            queryMore = true;
            while (queryMore)
            {
                var newEvents = await partyVerseApi.GetEvents(page);
                queryMore = newEvents.Count >= 100;
                localEvents.AddRange(newEvents);
                page += 1;
            }

            var localLastUpdate = DateTime.Now;

            foreach (var ev in localEvents)
            {
                if (ev.LocationData == null || ev.LocationData.DataCenter == null) continue;
                var key = ev.LocationData.DataCenter.Name;

                if (!localByDc.ContainsKey(key))
                    localByDc.Add(key, []);
                localByDc[key].Add(ev);
                if (!localTagsByDc.ContainsKey(key))
                    localTagsByDc.Add(key, []);

                foreach (var tag in ev.Tags)
                {
                    if (!localTagsByDc[key].ContainsKey(tag))
                        localTagsByDc[key].Add(tag, false);
                }
            }

            eventFilterCache.Clear();
            eventStringCache.Clear();

            lock (_dataLock)
            {
                partyVerseEvents.Clear();
                partyVerseEvents.AddRange(localEvents);
                eventsByDc.Clear();
                foreach (var kvp in localByDc) eventsByDc[kvp.Key] = kvp.Value;
                tagsByDc.Clear();
                foreach (var kvp in localTagsByDc) tagsByDc[kvp.Key] = kvp.Value;
                lastUpdate = localLastUpdate;
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.Error(ex, "error getting events");
            lock (_dataLock)
            {
                displayError = string.Format("Error getting events: {0}, {1}", ex.Message, ex.InnerException);
            }
        }
    }

    public override void OnOpen()
    {
        base.OnOpen();
        if (lastUpdate.AddMinutes(5).CompareTo(DateTime.Now) <= 0)
        {
            Task.Run(() => UpdateEvents(_cts.Token));
        }
    }


    public override void Draw()
    {
        if (ImGui.Button("Reload Events"))
            Task.Run(() => UpdateEvents(_cts.Token));
        ImGui.SameLine();

        ImGui.Text(eventStringCache.GetLastUpdateString(lastUpdate));

        ImGui.Spacing();

        string? localError;
        int evCount;
        lock (_dataLock) { localError = displayError; evCount = partyVerseEvents.Count; }

        if (localError != null)
        {
            ImGui.Text(localError);
        }
        else if (evCount == 0)
        {
            ImGui.Text("Loading events...");
        }
        else
        {
            if (ImGui.BeginTabBar("region_tab_bar"))
            {
                for (var location = 1; location < PartyVerseApi.RegionList.Count; location++)
                {
                    var regionName = PartyVerseApi.RegionList[location];

                    if (this.Configuration.SelectedRegion.IsNullOrEmpty())
                    {
                        this.Configuration.SelectedRegion = regionName;
                    }

                    var open = this.Configuration.SelectedRegion.Equals(regionName);
                    var true_val = true;

                    var flags = ImGuiTabItemFlags.None;

                    if (!this.Configuration.SelectedRegionSet && open)
                    {
                        flags |= ImGuiTabItemFlags.SetSelected;
                        this.Configuration.SelectedRegionSet = true;
                    }

                    if (ImGui.BeginTabItem(regionName, ref true_val, flags))
                    {
                        if (this.Configuration.SelectedRegion != regionName)
                        {
                            this.Configuration.SelectedRegion = regionName;
                            this.Configuration.Save();
                        }
                        if (ImGui.BeginTabBar("datacenters_tab_bar"))
                        {
                            foreach (var dataCenter in this.partyVerseApi.DataCenters)
                            {
                                if (dataCenter.Value.Region == location)
                                {
                                    DrawDataCenter(dataCenter.Value);
                                }
                            }
                            ImGui.EndTabBar();
                        }
                        ImGui.EndTabItem();
                    }
                }
                ImGui.EndTabBar();
            }
        }
    }

    public void DrawDataCenter(Models.DataCenterType dataCenter)
    {
        if (this.Configuration.SelectedDataCenter.IsNullOrEmpty())
        {
            this.Configuration.SelectedDataCenter = dataCenter.Name;
        }

        var open = this.Configuration.SelectedDataCenter == dataCenter.Name;
        var true_val = true;

        var flags = ImGuiTabItemFlags.None;

        if (!this.Configuration.SelectedDataCenterSet && open)
        {
            flags |= ImGuiTabItemFlags.SetSelected;
            this.Configuration.SelectedDataCenterSet = true;
        }

        if (ImGui.BeginTabItem(dataCenter.Name, ref true_val, flags))
        {
            if (this.Configuration.SelectedDataCenter != dataCenter.Name)
            {
                this.Configuration.SelectedDataCenter = dataCenter.Name;
                this.Configuration.Save();
            }

            List<Models.EventType> events;
            SortedDictionary<string, bool> tags;
            lock (_dataLock)
            {
                var rawEvents = eventsByDc.GetValueOrDefault(dataCenter.Name);
                events = rawEvents != null ? new List<Models.EventType>(rawEvents) : [];
                var rawTags = tagsByDc.GetValueOrDefault(dataCenter.Name);
                tags = rawTags ?? [];
            }

            var i = 0;
            foreach (var (tag, selected) in tags.ToList())
            {
                ImGui.SameLine();
                if (i % 8 == 0)
                {
                    ImGui.NewLine();
                }

                var selectedLocal = selected;
                if (ImGui.Checkbox(tag, ref selectedLocal))
                {
                    tags[tag] = selectedLocal;
                }

                i += 1;
            }

            var selectedTags = tags.Where(t => t.Value).Select(t => t.Key).ToList();
            var filteredEvents = eventFilterCache.GetFiltered(dataCenter.Name, events, selectedTags);

            foreach (var ev in filteredEvents)
            {
                ImGui.Separator();
                ImGui.PushID(ev.Id);
                EventRenderer.DrawEventRow(ev, eventStringCache.GetOrCompute(ev));
                ImGui.PopID();
            }
            ImGui.EndTabItem();
        }
    }

}
