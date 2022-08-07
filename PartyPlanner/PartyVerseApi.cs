﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dalamud.Logging;
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
            Assembly assembly = Assembly.GetExecutingAssembly();
            System.Diagnostics.FileVersionInfo fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
            string version = fvi.FileVersion!;

            graphQL = new GraphQLHttpClient("https://partyverse.app/api/", new NewtonsoftJsonSerializer());
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

        public async Task<Models.EventsResponseType> GetEvents()
        {
            var heroRequest = new GraphQLRequest
            {
                Query = @"
                {
                      events(game: ""final-fantasy-xiv"", sortBy: STARTS_AT) {
                        title,
                        locationId,
                        ageRating,
                        attendeeCount,
                        startsAt,
                        endsAt,
                        launchUrl,
                        location,
                        tags,
                        description(type: PLAIN_TEXT)
                     }
                }"
            };

            var res = await graphQL.SendQueryAsync<Models.EventsResponseType>(heroRequest);
            var data = res.Data;

            foreach(var ev in data.Events)
            {
                // Remove emojis.
                string result = Regex.Replace(ev.Description, @"\p{Cs}", "");
                ev.Description = result;
            }

            return res.Data;
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