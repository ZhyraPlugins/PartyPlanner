﻿using Dalamud.Logging;
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
        private string filterTag;
        private string windowTitle;
        private readonly Dictionary<int, List<Models.EventType>> eventsByDc = new();
        private readonly Dictionary<int, SortedDictionary<string, bool>> tagsByDc = new();

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
        private Models.EventType? eventDetails = null;

        public PluginUI(Configuration configuration)
        {
            this.configuration = configuration;
            this.partyVerseApi = new PartyVerseApi();
            windowTitle = "PartyPlanner";
            filterTag = "";

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
            DrawEventWindow();
        }

        public async void UpdateEvents()
        {
            partyVerseEvents.Clear();
            eventsByDc.Clear();
            tagsByDc.Clear();
            partyVerseEvents.AddRange(await this.partyVerseApi.GetActiveEvents());
            partyVerseEvents.AddRange(await this.partyVerseApi.GetEvents());
            foreach (var ev in partyVerseEvents)
            {
                var key = ev.LocationData.DataCenter.Id;

                if (!eventsByDc.ContainsKey(key))
                    eventsByDc.Add(key, new());
                eventsByDc[key].Add(ev);
                if (!tagsByDc.ContainsKey(key))
                    tagsByDc.Add(key, new());

                foreach(var tag in ev.Tags)
                {
                    if (!tagsByDc[key].ContainsKey(tag))
                        tagsByDc[key].Add(tag, false);
                }
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

                ImGui.Spacing();

                if (partyVerseEvents == null || partyVerseEvents.Count == 0)
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

                ImGui.Spacing();

                if (ImGui.BeginTable(string.Format("partyverse_events_{0}", dataCenter.Name), 5,
                    ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.BordersInner))
                {
                    ImGui.TableHeader("Events");
                    ImGui.TableSetupColumn("Title (click for more info)", ImGuiTableColumnFlags.WidthFixed);
                    ImGui.TableSetupColumn("Location", ImGuiTableColumnFlags.WidthFixed);
                    ImGui.TableSetupColumn("Description", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("Starts", ImGuiTableColumnFlags.WidthFixed);
                    ImGui.TableSetupColumn("Ends", ImGuiTableColumnFlags.WidthFixed);
                    ImGui.TableHeadersRow();
                    ImGui.TableNextRow();

                    foreach (var ev in events)
                    {
                        bool filtered = false;
                        foreach(var (tag, selected) in tags)
                        {
                            if(selected && !ev.Tags.Contains(tag))
                            {
                                filtered = true;
                                break;
                            }
                        }

                        if(!filtered)
                        {
                            DrawEventRow(ev);
                        }
                      
                    }

                    ImGui.EndTable();
                }
                ImGui.EndTabItem();
            }
        }

        public void DrawEventRow(Models.EventType ev)
        {
            ImGui.TableNextColumn();

            ImGui.Spacing();

            var greenColor = new Vector4(0.0742f, 0.530f, 0.150f, 1.0f);

            var title = ev.Title;
            if (title.Length > 30)
                title = title[..30] + "...";

            if (ImGui.Button(title))
            {
                this.eventDetails = ev;
                EventDetailsOpen = true;
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.TextColored(greenColor, ev.Title);
                ImGui.Text("Click to open a detailed view.");
                ImGui.EndTooltip();
            }

            string description = ev.Description;

            if (description.Length > 200)
                description = description[..200] + "...";

            ImGui.TableNextColumn();


            var originalLocation = string.Format("[{0}] {1}", ev.LocationData.Server.Name, ev.Location);
            var location = originalLocation;
            if (location.Length > 100)
                location = location[..100] + "...";

            if (ImGui.Selectable(location))
            {
                ImGui.SetClipboardText(originalLocation);
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.TextColored(greenColor, originalLocation);
                ImGui.Text("Click to copy");
                ImGui.EndTooltip();
            }

            var startsAt = ev.StartsAt.ToLocalTime();
            var endsAt = ev.EndsAt.ToLocalTime();

            ImGui.TableNextColumn();
            ImGui.TextWrapped(description);
            ImGui.TableNextColumn();
            ImGui.Text(ev.StartsAt.Humanize());
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.SetTooltip(startsAt.ToString());
                ImGui.EndTooltip();
            }
            ImGui.TableNextColumn();
            ImGui.Text(endsAt.Humanize());
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.SetTooltip(endsAt.ToString());
                ImGui.EndTooltip();
            }
            ImGui.TableNextRow();
        }

        public void DrawEventWindow()
        {
            if (!EventDetailsOpen || this.eventDetails == null)
            {
                return;
            }

            ImGui.SetNextWindowSizeConstraints(new Vector2(300, 100), new Vector2(800, 800));
            if (ImGui.Begin(string.Format("{0}", eventDetails.Title), ref this.eventDetailsOpen, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.TextWrapped(eventDetails.Description);
                ImGui.Spacing();
                ImGui.TextColored(new Vector4(0.668f, 0.146f, 0.910f, 1.0f), eventDetails.Location);
                ImGui.Spacing();
                ImGui.TextColored(new Vector4(0.156f, 0.665f, 0.920f, 1.0f),
                    string.Format("From {0} to {1}", eventDetails.StartsAt.ToLocalTime().ToString(), eventDetails.EndsAt.ToLocalTime().ToString()));
                ImGui.Spacing();
                ImGui.TextColored(new Vector4(0.0888f, 0.740f, 0.176f, 1.0f),
                    string.Format("Tags: {0}", string.Join(", ", eventDetails.Tags)));
            }
        }
    }
}
