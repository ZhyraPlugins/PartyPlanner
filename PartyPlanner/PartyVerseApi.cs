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
    public class PartyVerseApi
    {
        private GraphQLHttpClient graphQL { get; init; }

        public Dictionary<int, Models.ServerType> Servers { get; private init; }
        public Dictionary<int, Models.DataCenterType> DataCenters { get; private init; }

        public static readonly List<string> RegionList = new() { "eu", "us", "jp", "oce" };

        public PartyVerseApi()
        {
            string version = "unknown";
            
            try
            {
                System.Diagnostics.FileVersionInfo fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(Plugin.PluginInterface.AssemblyLocation.FullName);
                version = fvi.FileVersion!;
            } catch(Exception e)
            {
                PluginLog.Error(e, "error loading assembly");
            }

            graphQL = new GraphQLHttpClient("https://api.partake.gg/", new NewtonsoftJsonSerializer());
            graphQL.HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Dalamud-PartyPlanner/" + version);

            var serverIdsStr = System.Text.Encoding.Default.GetString(Properties.Resources.servers_ids);
            serverIdsStr = serverIdsStr.Trim(new char[] { '\uFEFF', '\u200B' });
            var serverList = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Models.ServerType>>(serverIdsStr);

            if (serverList == null)
            {
                throw new Exception("Error deserializing server list.");
            }

            Servers = serverList.ToDictionary((x) => x.Id, (x) => x);

            var datacentersStr = System.Text.Encoding.Default.GetString(Properties.Resources.datacenters);
            datacentersStr = datacentersStr.Trim(new char[] { '\uFEFF', '\u200B' });
            var dataCentersList = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Models.DataCenterType>>(datacentersStr);

            if (dataCentersList == null)
            {
                throw new Exception("Error deserializing data center list.");
            }

            DataCenters = dataCentersList.ToDictionary((x) => x.Id, (x) => x);

            try
            {
            }
            catch (Exception e)
            {
                PluginLog.Error("{0}", e);
            }

            
        }

        public async Task<List<Models.EventType>> GetEvents(int page)
        {
            var heroRequest = new GraphQLRequest
            {
                Query = @"
                {
                      events(game: ""final-fantasy-xiv"", sortBy: STARTS_AT, limit: 100, offset: " + (page * 100) + @") {
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

            foreach(var ev in data.Events)
            {
                // Remove emojis.
                string description = Regex.Replace(ev.Description, @"[^\u0000-\u007F]+", string.Empty);
                ev.Description = description.Trim();

                string title = Regex.Replace(ev.Title, @"[^\u0000-\u007F]+", string.Empty);
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
                      events(game: ""final-fantasy-xiv"", sortBy: STARTS_AT, limit: 100, offset: " + (page * 100) + @",
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
                string description = Regex.Replace(ev.Description, @"[^\u0000-\u007F]+", string.Empty);
                ev.Description = description.Trim();

                string title = Regex.Replace(ev.Title, @"[^\u0000-\u007F]+", string.Empty);
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
    }
}
