using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PartyPlanner.Windows
{
    public sealed class ConfigWindow : Window, IDisposable
    {
        public ConfigWindow(Configuration configuration) : base("PartyPlanner Config", ImGuiWindowFlags.NoCollapse)
        {
        }

        public void Dispose()
        {
        }

        public override void Draw()
        {
            ImGui.Text("Source code link");
            if(ImGui.SmallButton("https://github.com/ZhyraPlugins/PartyPlanner"))
            {
                Util.OpenLink("https://github.com/ZhyraPlugins/PartyPlanner");
            }
        }
    }
}
