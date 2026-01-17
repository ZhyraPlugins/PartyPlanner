using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dalamud.Logging;
using Dalamud.Utility;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using Lumina.Excel.Sheets;
using PartyPlanner.Models;

namespace PartyPlanner
{
    public partial class PartyVerseApi
    {
        private readonly GraphQLHttpClient graphQL;

        private readonly Dictionary<int, Models.ServerType> servers;
        private Dictionary<int, Models.DataCenterType> dataCenters;

        public static readonly List<string> RegionList = ["Unknown", "Japan", "North America", "Europe", "Oceania"];

        public Dictionary<int, DataCenterType> DataCenters { get => dataCenters; set => dataCenters = value; }

        public PartyVerseApi()
        {
            string version = "unknown";

            try
            {
                System.Diagnostics.FileVersionInfo fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(Plugin.PluginInterface.AssemblyLocation.FullName);
                version = fvi.FileVersion!;
            }
            catch (Exception e)
            {
                Plugin.Logger.Error(e, "error loading assembly");
            }

            graphQL = new GraphQLHttpClient("https://api.partake.gg/", new NewtonsoftJsonSerializer());
            graphQL.HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Dalamud-PartyPlanner/" + version);

            servers = [];
            dataCenters = [];

            // LocalPlayer.HomeWorld.GameData.Datacenter.Row

            var worlds = Plugin.DataManager.GetExcelSheet<World>();
            var worldGroups = Plugin.DataManager.GetExcelSheet<WorldDCGroupType>();

            if (worldGroups != null)
            {
                foreach(var worldGroup in worldGroups) {
                    var name = worldGroup.Name.ExtractText();

                        // region 7 = cloud beta dc
                        if (name != "Dev" && worldGroup.Region != 7)
                        {
                            dataCenters.Add((int)worldGroup.RowId, new Models.DataCenterType((int)worldGroup.RowId, worldGroup.Name.ExtractText(), worldGroup.Region));
                        }
                }
            }

            if (worlds != null)
            {
                foreach (var server in worlds) {
                     if (server.IsPublic && dataCenters.ContainsKey((int)server.DataCenter.RowId))
                        {
                            servers.Add((int)server.RowId, new Models.ServerType((int)server.RowId, server.Name.ExtractText(), (int)server.DataCenter.RowId));
                        }
                }
            }
        }

        public async Task<List<Models.EventType>> GetEvents(int page)
        {
            var heroRequest = new GraphQLRequest
            {
                Query = @"
                {
                      events(game: ""final-fantasy-xiv"", sortBy: STARTS_AT, limit: 100, offset: " + page * 100 + @") {
                        id,
                        title,
                        locationId,
                        ageRating,
                        attendeeCount,
                        startsAt,
                        endsAt,
                        location,
                        tags,
                        description(type: PLAIN_TEXT)
                        attendeeCount
                        attachments
                        locationData {
                          server {
                            id
                            name
                            dataCenterId
                          }
                          dataCenter {
                            id
                            name
                          }
                        }
                     }
                }"
            };

            var res = await graphQL.SendQueryAsync<Models.EventsResponseType>(heroRequest);
            var data = res.Data;

            foreach (var ev in data.Events)
            {
                // Remove emojis.
                string description = CleanUnicodeSymbolsRegex().Replace(ev.Description, string.Empty);
                ev.Description = description.Trim();

                string title = CleanUnicodeSymbolsRegex().Replace(ev.Title, string.Empty);
                ev.Title = title.Trim();
            }

            return res.Data.Events;
        }

        public async Task<List<Models.EventType>> GetActiveEvents(int page)
        {
            var nowDate = DateTime.UtcNow;
            var heroRequest = new GraphQLRequest
            {
                Query = @"
                {
                      events(game: ""final-fantasy-xiv"", sortBy: STARTS_AT, limit: 100, offset: " + page * 100 + @",
                            startsBetween: { end: """ + nowDate.ToString("o") + @"""},
                            endsBetween: { start: """ + nowDate.ToString("o") + @""" }
                        ) {
                        id,
                        title,
                        locationId,
                        ageRating,
                        attendeeCount,
                        startsAt,
                        endsAt,
                        location,
                        tags,
                        description(type: PLAIN_TEXT)
                        attendeeCount
                        attachments
                        locationData {
                          server {
                            id
                            name
                            dataCenterId
                          }
                          dataCenter {
                            id
                            name
                          }
                        }
                     }
                }"
            };

            var res = await graphQL.SendQueryAsync<Models.EventsResponseType>(heroRequest);
            var data = res.Data;

            foreach (var ev in data.Events)
            {
                // Remove emojis.
                string description = CleanUnicodeSymbolsRegex().Replace(ev.Description, string.Empty);
                ev.Description = description.Trim();

                string title = CleanUnicodeSymbolsRegex().Replace(ev.Title, string.Empty);
                ev.Title = title.Trim();
            }

            return res.Data.Events;
        }

        public Models.ServerType GetServerType(int id)
        {
            return servers[id];
        }

        public Models.DataCenterType GetDataCenterType(int id)
        {
            return dataCenters[id];
        }

        [GeneratedRegex(@"[^\u0000-\u007F]+")]
        private static partial Regex CleanUnicodeSymbolsRegex();
    }
}
