using Dalamud.Data;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using PartyPlanner.Windows;

namespace PartyPlanner
{
    public sealed class Plugin : IDalamudPlugin
    {
        public static string Name => "PartyPlanner";

        private const string commandName = "/partyplanner";

        [PluginService]
        internal static DalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService]
        internal static ICommandManager CommandManager { get; private set; } = null!;
        [PluginService]
        public static IDataManager DataManager { get; private set; } = null!;
        [PluginService]
        public static IPluginLog Logger { get; private set; } = null!;
        private Configuration Configuration { get; init; }
        public WindowSystem WindowSystem = new("PartyPlanner");
        private readonly MainWindow mainWindow;

        public Plugin()
        {
            this.Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            this.Configuration.Initialize(PluginInterface);

            mainWindow = new MainWindow(this);


            WindowSystem.AddWindow(mainWindow);

            CommandManager.AddHandler(commandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Display a list of community events sourced from partyverse.app"
            });

            PluginInterface.UiBuilder.Draw += DrawUI;
            //PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
        }

        public void Dispose()
        {
            this.WindowSystem.RemoveAllWindows();

            mainWindow.Dispose();

            CommandManager.RemoveHandler(commandName);
        }

        private void OnCommand(string command, string args)
        {
            mainWindow.IsOpen = true;

        }

        private void DrawUI()
        {
            this.WindowSystem.Draw();
        }
    }
}
