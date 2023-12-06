using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dalamud.Logging;
using Dalamud.Utility;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;

namespace PartyPlanner
{
    public partial class PartyVerseApi
    {
        private GraphQLHttpClient graphQL;

        public Dictionary<int, Models.ServerType> Servers { get; private init; }
        public Dictionary<int, Models.DataCenterType> DataCenters { get; private init; }

        public static readonly List<string> RegionList = new() { "Unknown", "Japan", "North America", "Europe", "Oceania" };

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

            Servers = new();
            DataCenters = new();

            // LocalPlayer.HomeWorld.GameData.Datacenter.Row

            var servers = Plugin.DataManager.GetExcelSheet<Lumina.Excel.GeneratedSheets.World>(Dalamud.ClientLanguage.English);
            var datacenters = Plugin.DataManager.GetExcelSheet<Lumina.Excel.GeneratedSheets.WorldDCGroupType>(Dalamud.ClientLanguage.English);

            if (datacenters != null)
            {
                // 0 is unknown
                for (uint i = 1; i < datacenters.RowCount; i++)
                {
                    var dc = datacenters.GetRow(i);

                    if (dc != null)
                    {
                        var name = dc.Name.RawString;

                        // region 7 = cloud beta dc
                        if (name != "Dev" && dc.Region != 7)
                        {
                            DataCenters.Add((int)dc.RowId, new Models.DataCenterType((int)dc.RowId, dc.Name, dc.Region));
                            Plugin.Logger.Info("id: {0}", dc.RowId);
                            Plugin.Logger.Info("name: {0}", dc.Name);
                            Plugin.Logger.Info("region: {0}", dc.Region);
                            Plugin.Logger.Info("---");
                        }

                    }

                }
            }

            if (servers != null)
            {
                // 0 is unknown
                for (uint i = 1; i < servers.RowCount; i++)
                {
                    var server = servers.GetRow(i);

                    if (server != null)
                    {
                        if (server.IsPublic && DataCenters.ContainsKey((int)server.DataCenter.Row))
                        {
                            Servers.Add((int)server.RowId, new Models.ServerType((int)server.RowId, server.Name.RawString, (int)server.DataCenter.Row));
                            Plugin.Logger.Info("id: {0}", server.RowId);
                            Plugin.Logger.Info("region: {0}", server.Region);
                            Plugin.Logger.Info("name: {0}", server.Name);
                            Plugin.Logger.Info("DataCenter: {0}", server.DataCenter.Row);
                            Plugin.Logger.Info("is public: {0}", server.IsPublic);
                            Plugin.Logger.Info("DataCenter: {0}", DataCenters[(int)server.DataCenter.Row].Name);
                            Plugin.Logger.Info("----");
                        }
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
            return Servers[id];
        }

        public Models.DataCenterType GetDataCenterType(int id)
        {
            return DataCenters[id];
        }

        [GeneratedRegex(@"[^\u0000-\u007F]+")]
        private static partial Regex CleanUnicodeSymbolsRegex();
    }
}
