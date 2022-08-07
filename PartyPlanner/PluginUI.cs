﻿using Dalamud.Logging;
using ImGuiNET;
using System;
using System.Numerics;
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
        private Models.EventsResponseType? partyVerseEvents;

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
        private Models.EventType? eventDetailsType = null;

        public PluginUI(Configuration configuration)
        {
            this.configuration = configuration;
            this.partyVerseApi = new PartyVerseApi();
            this.partyVerseEvents = null;

            Task.Run(async () =>
            {
                this.partyVerseEvents = await this.partyVerseApi.GetEvents();
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
            if (ImGui.Begin("PartyPlanner", ref this.visible, ImGuiWindowFlags.None))
            {
                if (ImGui.Button("Reload Events"))
                {
                    Task.Run(async () =>
                    {
                        this.partyVerseEvents = await this.partyVerseApi.GetEvents();
                    });
                }

                ImGui.Spacing();

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
            ImGui.End();
        }

        public void DrawDataCenter(Models.DataCenterType dataCenter)
        {
            if (ImGui.BeginTabItem(dataCenter.Name))
            {
                if (ImGui.BeginTable(string.Format("partyverse_events_{0}", dataCenter.Name), 5,
                    ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.BordersInner | ImGuiTableFlags.SizingFixedFit))
                {
                    ImGui.TableHeader("Events");
                    ImGui.TableSetupColumn("Title", ImGuiTableColumnFlags.WidthFixed);
                    ImGui.TableSetupColumn("Location", ImGuiTableColumnFlags.WidthFixed);
                    ImGui.TableSetupColumn("Description", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("Starts", ImGuiTableColumnFlags.WidthFixed);
                    ImGui.TableSetupColumn("Ends", ImGuiTableColumnFlags.WidthFixed);
                    ImGui.TableHeadersRow();
                    ImGui.TableNextRow();

                    if (this.partyVerseEvents != null)
                    {
                        foreach (var ev in this.partyVerseEvents.Events)
                        {
                            var serverType = partyVerseApi.GetServerType(ev.LocationId);

                            if (serverType.DataCenter != dataCenter.Id)
                                continue;

                            ImGui.TableNextColumn();
                            if(ImGui.Selectable(ev.Title))
                            {
                                this.eventDetailsType = ev;
                                EventDetailsOpen = true;
                            }

                            ImGui.TableNextColumn();
                            ImGui.Text(string.Format("[{0}] {1}", serverType.Name, ev.Location));
                            ImGui.TableNextColumn();
                            ImGui.TextWrapped(ev.Description);
                            ImGui.TableNextColumn();
                            ImGui.Text(ev.StartsAt.ToString());
                            ImGui.TableNextColumn();
                            ImGui.Text(ev.EndsAt.ToString());
                            ImGui.TableNextRow();
                        }
                    }

                    ImGui.EndTable();
                }
                ImGui.EndTabItem();
            }
        }

        public void DrawEventWindow()
        {
            if (!EventDetailsOpen || this.eventDetailsType == null)
            {
                return;
            }

            ImGui.SetNextWindowSizeConstraints(new Vector2(300, 100), new Vector2(800, 800));
            if (ImGui.Begin(string.Format("{0}", eventDetailsType.Title), ref this.eventDetailsOpen, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.TextWrapped(eventDetailsType.Description);
                ImGui.Spacing();
                ImGui.TextColored(new Vector4(0.668f, 0.146f, 0.910f, 1.0f), eventDetailsType.Location);
                ImGui.Spacing();
                ImGui.TextColored(new Vector4(0.156f, 0.665f, 0.920f, 1.0f), 
                    string.Format("From {0} to {1}", eventDetailsType.StartsAt.ToString(), eventDetailsType.EndsAt.ToString()));
                ImGui.Spacing();
                ImGui.TextColored(new Vector4(0.0888f, 0.740f, 0.176f, 1.0f), 
                    string.Format("Tags: {0}", string.Join(", ", eventDetailsType.Tags)));
            }
        }
    }
}