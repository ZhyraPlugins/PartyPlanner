using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace PartyPlanner
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 1;

        public string SelectedRegion { get; set; } = string.Empty;
        public string SelectedDataCenter { get; set; } = string.Empty;

        [NonSerialized]
        public bool SelectedRegionSet = false;
        [NonSerialized]
        public bool SelectedDataCenterSet = false;

        [NonSerialized]
        private IDalamudPluginInterface? pluginInterface;

        public void Initialize(IDalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
        }

        public void Save()
        {
            this.pluginInterface!.SavePluginConfig(this);
        }
    }
}
