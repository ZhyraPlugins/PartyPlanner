using Dalamud.Data;
using Dalamud.Game.Command;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using PartyPlanner.Windows;
using System;

namespace PartyPlanner
{
    public sealed class Plugin : IDalamudPlugin
    {
        public static string Name => "PartyPlanner";

        private const string commandName = "/partyplanner";

        [PluginService]
        internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService]
        internal static ICommandManager CommandManager { get; private set; } = null!;
        [PluginService]
        public static IDataManager DataManager { get; private set; } = null!;
        [PluginService]
        public static IPluginLog Logger { get; private set; } = null!;
        public Configuration Configuration { get; init; }
        public WindowSystem WindowSystem = new("PartyPlanner");
        private readonly MainWindow mainWindow;
        private readonly ConfigWindow configWindow;

        public Plugin()
        {
            this.Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            this.Configuration.Initialize(PluginInterface);

            PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
            PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
            mainWindow = new MainWindow(this.Configuration);
            configWindow = new ConfigWindow(this.Configuration);


            WindowSystem.AddWindow(mainWindow);
            WindowSystem.AddWindow(configWindow);

            CommandManager.AddHandler(commandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Display a list of community events sourced from partyverse.app"
            });

            PluginInterface.UiBuilder.Draw += DrawUI;
        }

        private void ToggleConfigUi()
        {
            configWindow.Toggle();
        }

        private void ToggleMainUi()
        {
            mainWindow.Toggle();
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
