using Dalamud.Logging;
using Humanizer;
using ImGuiNET;
using System;
using System.Collections.Generic;
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
        private readonly List<Models.EventType> partyVerseEvents = new(50);
        private string filterTag;
        private string windowTitle;

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

            Task.Run(async () =>
            {
                partyVerseEvents.Clear();
                partyVerseEvents.AddRange(await this.partyVerseApi.GetActiveEvents());
                partyVerseEvents.AddRange(await this.partyVerseApi.GetEvents());
            });
        }

        public void Dispose()
        {
        }

        public void Draw()
        {
            DrawMainWindow();
            DrawEventWindow();
        }

        public void DrawMainWindow()
        {
            if (!Visible)
            {
                return;
            }

            ImGui.SetNextWindowSize(new Vector2(1000, 500), ImGuiCond.FirstUseEver);
            if (ImGui.Begin(windowTitle, ref this.visible, ImGuiWindowFlags.None))
            {
                if (ImGui.Button("Reload Events"))
                {
                    Task.Run(async () =>
                    {
                        partyVerseEvents.Clear();
                        partyVerseEvents.AddRange(await this.partyVerseApi.GetActiveEvents());
                        partyVerseEvents.AddRange(await this.partyVerseApi.GetEvents());
                    });
                }

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
                            foreach (var datacenter in this.partyVerseApi.DataCenters)
                            {
                                if (datacenter.Value.Location == location)
                                    DrawDataCenter(datacenter.Value);
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
                var events = partyVerseEvents
                        .FindAll(ev => ev.LocationId >= 0 &&  ev.LocationId < partyVerseApi.Servers.Count
                        && partyVerseApi.GetServerType(ev.LocationId).DataCenter == dataCenter.Id);

                var tags = events.SelectMany(ev => ev.Tags).Distinct().OrderBy(x => x);

                if (filterTag != "" && !tags.Contains(filterTag))
                    filterTag = "";

                if (ImGui.RadioButton("None", filterTag == ""))
                {
                    filterTag = "";
                }

                foreach (var tag in tags)
                {
                    ImGui.SameLine();
                    if (ImGui.RadioButton(tag, filterTag == tag))
                    {
                        filterTag = tag;
                    }
                }

                ImGui.Spacing();

                if (ImGui.BeginTable(string.Format("partyverse_events_{0}", dataCenter.Name), 5,
                    ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.BordersInner))
                {
                    ImGui.TableHeader("Events");
                    ImGui.TableSetupColumn("Title", ImGuiTableColumnFlags.WidthFixed);
                    ImGui.TableSetupColumn("Location", ImGuiTableColumnFlags.WidthFixed);
                    ImGui.TableSetupColumn("Description", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("Starts", ImGuiTableColumnFlags.WidthFixed);
                    ImGui.TableSetupColumn("Ends", ImGuiTableColumnFlags.WidthFixed);
                    ImGui.TableHeadersRow();
                    ImGui.TableNextRow();

                    foreach (var ev in events.Where(x => filterTag == "" || x.Tags.Contains(filterTag)))
                    {
                        var serverType = partyVerseApi.GetServerType(ev.LocationId);

                        DrawEventRow(ev, serverType);
                    }

                    ImGui.EndTable();
                }
                ImGui.EndTabItem();
            }
        }

        public void DrawEventRow(Models.EventType ev, Models.ServerType serverType)
        {
            ImGui.TableNextColumn();

            ImGui.Spacing();

            var greenColor = new Vector4(0.0742f, 0.530f, 0.150f, 1.0f);
            ImGui.PushStyleColor(ImGuiCol.Button, greenColor);

            var title = ev.Title;
            if (title.Length > 30)
                title = title[..30] + "...";

            if (ImGui.Button(title))
            {
                this.eventDetails = ev;
                EventDetailsOpen = true;
            }
            ImGui.PopStyleColor();

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


            var originalLocation = string.Format("[{0}] {1}", serverType.Name, ev.Location);
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
