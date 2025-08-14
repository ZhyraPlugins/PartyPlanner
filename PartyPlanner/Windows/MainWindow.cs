using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Humanizer;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
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

        Task.Run(UpdateEvents);
    }

    public void Dispose()
    {
    }

    public async void UpdateEvents()
    {
        displayError = null;
        partyVerseEvents.Clear();
        eventsByDc.Clear();
        tagsByDc.Clear();

        int page = 0;
        bool queryMore = true;

        try
        {
            while (queryMore)
            {
                var newEvents = await partyVerseApi.GetActiveEvents(page);
                queryMore = newEvents.Count >= 100;
                partyVerseEvents.AddRange(newEvents);
                page += 1;
            }

            page = 0;
            queryMore = true;
            while (queryMore)
            {
                var newEvents = await partyVerseApi.GetEvents(page);
                queryMore = newEvents.Count >= 100;
                partyVerseEvents.AddRange(newEvents);
                page += 1;
            }

            lastUpdate = DateTime.Now;

            foreach (var ev in partyVerseEvents)
            {
                if (ev.LocationData == null || ev.LocationData.DataCenter == null) continue;
                var key = ev.LocationData.DataCenter.Name;

                if (!eventsByDc.ContainsKey(key))
                    eventsByDc.Add(key, []);
                eventsByDc[key].Add(ev);
                if (!tagsByDc.ContainsKey(key))
                    tagsByDc.Add(key, []);

                foreach (var tag in ev.Tags)
                {
                    if (!tagsByDc[key].ContainsKey(tag))
                        tagsByDc[key].Add(tag, false);
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.Error(ex, "error getting events");
            displayError = string.Format("Error getting events: {0}, {1}", ex.Message, ex.InnerException);
        }
    }

    public override void OnOpen()
    {
        base.OnOpen();
        if (lastUpdate.AddMinutes(5).CompareTo(DateTime.Now) <= 0)
        {
            Task.Run(UpdateEvents);
        }
    }

    public override void Draw()
    {
        if (ImGui.Button("Reload Events"))
            Task.Run(UpdateEvents);
        ImGui.SameLine();
        ImGui.Text(string.Format("Updated {0}", lastUpdate.Humanize()));

        ImGui.Spacing();

        if (displayError != null)
        {
            ImGui.Text(displayError);
        }
        else if (partyVerseEvents == null || partyVerseEvents.Count == 0)
        {
            ImGui.Text("Loading events...");
        }
        else
        {
            ImGui.BeginTabBar("region_tab_bar");

            for (var location = 1; location < PartyVerseApi.RegionList.Count; location++)
            {
                var regionName = PartyVerseApi.RegionList[location];

                if (this.Configuration.SelectedRegion.IsNullOrEmpty())
                {
                    this.Configuration.SelectedRegion = regionName;
                    this.Configuration.Save();
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
                    this.Configuration.SelectedRegion = regionName;
                    this.Configuration.Save();
                    ImGui.BeginTabBar("datacenters_tab_bar");
                    foreach (var dataCenter in this.partyVerseApi.DataCenters)
                    {
                        if (dataCenter.Value.Region == location)
                        {
                            DrawDataCenter(dataCenter.Value);
                        }
                    }
                    ImGui.EndTabBar();
                    ImGui.EndTabItem();
                }
            }
        }
    }

    public void DrawDataCenter(Models.DataCenterType dataCenter)
    {
        if (this.Configuration.SelectedDataCenter.IsNullOrEmpty())
        {
            this.Configuration.SelectedDataCenter = dataCenter.Name;
            this.Configuration.Save();
        }

        var open = this.Configuration.SelectedDataCenter == dataCenter.Name;
        var true_val = true;

        var flags = ImGuiTabItemFlags.None;

        if (!this.Configuration.SelectedRegionSet && open)
        {
            flags |= ImGuiTabItemFlags.SetSelected;
            this.Configuration.SelectedRegionSet = true;
        }

        if (ImGui.BeginTabItem(dataCenter.Name, ref true_val, flags))
        {
            this.Configuration.SelectedDataCenter = dataCenter.Name;
            this.Configuration.Save();
            var events = eventsByDc.GetValueOrDefault(dataCenter.Name);
            events ??= [];

            var tags = tagsByDc.GetValueOrDefault(dataCenter.Name);
            tags ??= [];

            var i = 0;
            foreach (var (tag, selected) in tags)
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

            foreach (var ev in events)
            {
                bool filtered = false;
                foreach (var (tag, selected) in tags)
                {
                    if (selected && !ev.Tags.Contains(tag))
                    {
                        filtered = true;
                        break;
                    }
                }

                if (!filtered)
                {
                    ImGui.Separator();
                    ImGui.PushID(ev.Id);
                    DrawEventRow(ev);
                    ImGui.PopID();
                }

            }
            ImGui.EndTabItem();
        }
    }

    public static void DrawEventRow(Models.EventType ev)
    {
        ImGui.Spacing();

        var greenColor = new Vector4(0.0742f, 0.530f, 0.150f, 1.0f);

        ImGui.TextColored(new Vector4(0.668f, 0.146f, 0.910f, 1.0f), ev.Title);
        if (ImGui.IsItemClicked())
        {
            Util.OpenLink("https://www.partake.gg/events/{0}".Format(ev.Id));
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.Text("Click to open the partake.gg website.");
            ImGui.EndTooltip();
        }

        ImGui.Text("Location:");
        ImGui.SameLine();
        var location = string.Format("[{0}] {1}", ev.LocationData.Server.Name, ev.Location);
        if (ImGui.Selectable(location))
        {
            ImGui.SetClipboardText(location);
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.TextColored(greenColor, location);
            ImGui.Text("Click to copy");
            ImGui.EndTooltip();
        }

        var startsAt = ev.StartsAt.ToLocalTime();
        var endsAt = ev.EndsAt.ToLocalTime();
        ImGui.Text(string.Format("Starts {0}", ev.StartsAt.Humanize()));
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.SetTooltip(startsAt.ToString());
            ImGui.EndTooltip();
        }
        ImGui.SameLine();
        ImGui.Text(string.Format("|  Ends {0}", ev.EndsAt.Humanize()));
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.SetTooltip(endsAt.ToString());
            ImGui.EndTooltip();
        }
        ImGui.TextColored(new Vector4(0.156f, 0.665f, 0.920f, 1.0f),
           string.Format("From {0} to {1}", ev.StartsAt.ToLocalTime().ToString(), ev.EndsAt.ToLocalTime().ToString()));

        ImGui.TextColored(new Vector4(0.0888f, 0.740f, 0.176f, 1.0f),
            string.Format("Tags: {0}", string.Join(", ", ev.Tags)));

        if (ImGui.CollapsingHeader("More details"))
        {
            ImGui.TextWrapped(ev.Description);
        }
    }
}
