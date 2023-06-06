using Dalamud.Logging;
using Dalamud.Utility;
using Humanizer;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;


namespace PartyPlanner
{
    // It is good to have this be disposable in general, in case you ever need it
    // to do any cleanup
    class PluginUI : IDisposable
    {
        private Configuration configuration;
        private PartyVerseApi partyVerseApi { get; init; }
        // All the events
        private readonly List<Models.EventType> partyVerseEvents = new(50);
        private string windowTitle;
        private readonly Dictionary<int, List<Models.EventType>> eventsByDc = new();
        private readonly Dictionary<int, SortedDictionary<string, bool>> tagsByDc = new();
        private DateTime lastUpdate = DateTime.Now;
        private string? error = null;

        // this extra bool exists for ImGui, since you can't ref a property
        private bool visible = false;
        public bool Visible
        {
            get { return this.visible; }
            set { this.visible = value; }
        }

        private bool eventDetailsOpen = false;
        public bool EventDetailsOpen
        {
            get { return this.eventDetailsOpen; }
            set { this.eventDetailsOpen = value; }
        }

        public PluginUI(Configuration configuration)
        {
            this.configuration = configuration;
            this.partyVerseApi = new PartyVerseApi();
            windowTitle = "PartyPlanner";

            try
            {
                System.Diagnostics.FileVersionInfo fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(Plugin.PluginInterface.AssemblyLocation.FullName);
                string version = fvi.FileVersion!;
                windowTitle = string.Format("PartyPlanner v{0}", version);
            }
            catch (Exception e)
            {
                PluginLog.Error(e, "error loading assembly");
            }

            Task.Run(UpdateEvents);
        }

        public void Dispose()
        {
        }

        public void Draw()
        {
            DrawMainWindow();
        }

        public async void UpdateEvents()
        {
            error = null;
            partyVerseEvents.Clear();
            eventsByDc.Clear();
            tagsByDc.Clear();

            int page = 0;
            bool queryMore = true;

            try
            {
                while (queryMore)
                {
                    var newEvents = await this.partyVerseApi.GetActiveEvents(page);
                    queryMore = newEvents.Count >= 100;
                    partyVerseEvents.AddRange(newEvents);
                    page += 1;
                }

                page = 0;
                queryMore = true;
                while (queryMore)
                {
                    var newEvents = await this.partyVerseApi.GetEvents(page);
                    queryMore = newEvents.Count >= 100;
                    partyVerseEvents.AddRange(newEvents);
                    page += 1;
                }

                lastUpdate = DateTime.Now;

                foreach (var ev in partyVerseEvents)
                {
                    if (ev.LocationData == null || ev.LocationData.DataCenter == null) continue;
                    var key = ev.LocationData.DataCenter.Id;

                    if (!eventsByDc.ContainsKey(key))
                        eventsByDc.Add(key, new());
                    eventsByDc[key].Add(ev);
                    if (!tagsByDc.ContainsKey(key))
                        tagsByDc.Add(key, new());

                    foreach (var tag in ev.Tags)
                    {
                        if (!tagsByDc[key].ContainsKey(tag))
                            tagsByDc[key].Add(tag, false);
                    }
                }
            } catch (Exception ex)
            {
                error = string.Format("Error getting events: {0}", ex.Message);
            }
        }

        public void DrawMainWindow()
        {
            if (!Visible)
                return;

            ImGui.SetNextWindowSize(new Vector2(1000, 500), ImGuiCond.FirstUseEver);
            if (ImGui.Begin(windowTitle, ref this.visible, ImGuiWindowFlags.None))
            {
                if (ImGui.Button("Reload Events"))
                    Task.Run(UpdateEvents);
                ImGui.SameLine();
                ImGui.Text(string.Format("Updated {0}", lastUpdate.Humanize()));

                ImGui.Spacing();

                if (error != null)
                {
                    ImGui.Text(error);
                }
                else if (partyVerseEvents == null || partyVerseEvents.Count == 0)
                {
                    ImGui.Text("Loading events...");
                }
                else
                {
                    ImGui.BeginTabBar("region_tab_bar");

                    foreach (var location in PartyVerseApi.RegionList)
                    {
                        if (ImGui.BeginTabItem(location.ToUpper()))
                        {
                            ImGui.BeginTabBar("datacenters_tab_bar");
                            foreach (var dataCenter in this.partyVerseApi.DataCenters)
                            {
                                if (dataCenter.Value.Location == location)
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
            ImGui.End();
        }

        public void DrawDataCenter(Models.DataCenterType dataCenter)
        {
            if (ImGui.BeginTabItem(dataCenter.Name))
            {
                var events = eventsByDc.GetValueOrDefault(dataCenter.Id);
                events ??= new();

                var tags = tagsByDc.GetValueOrDefault(dataCenter.Id);
                tags ??= new();

                ImGui.Spacing();

                foreach (var (tag, selected) in tags)
                {
                    ImGui.SameLine();
                    var selectedLocal = selected;
                    if (ImGui.Checkbox(tag, ref selectedLocal))
                    {
                        tags[tag] = selectedLocal;
                    }
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

        public void DrawEventRow(Models.EventType ev)
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
}
