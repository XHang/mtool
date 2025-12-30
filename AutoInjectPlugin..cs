using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;

namespace AutoInjectPlugin
{
    public class AutoInjectPlugin : GenericPlugin
    {


        public override Guid Id { get; } = Guid.Parse("7E9E6F34-2B1A-4C9F-9C2C-1C7E8D3A1234");

        private readonly AutoInjectSettingsViewModel settingss;

        // 记录已经处理过的游戏
        private readonly HashSet<Guid> processedGames = new HashSet<Guid>();

        

        public AutoInjectPlugin(IPlayniteAPI api) : base(api)
        {
            settingss = new AutoInjectSettingsViewModel(this);

            Properties = new GenericPluginProperties
            {
                HasSettings = true
            };
        }
        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            if (!args.Games.Any())
                return null;

            return new List<GameMenuItem>
            {
                new GameMenuItem
                {
                    Description = "使用 MTool 注入器启动（配置此游戏）",
                    Action = a => ConfigureGameForInject(args.Games.First())
                }
            };
        }
        private void ConfigureGameForInject(Game game)
        {
            var settings = settingss.Settings;

            var result = PlayniteApi.Dialogs.ShowMessage(
                $"你是否要把《{game.Name}》改为注入器启动？",
                "注入器配置",
                MessageBoxButton.YesNo
            );

            if (result != MessageBoxResult.Yes)
                return;

            if (string.IsNullOrWhiteSpace(settings.InjectorPath) ||
                string.IsNullOrWhiteSpace(settings.DllPath) ||
                string.IsNullOrWhiteSpace(settings.NwPath))
            {
                PlayniteApi.Dialogs.ShowErrorMessage(
                    "请先配置注入路径（inject.exe、mzHook.dll、nw.exe）。",
                    "配置不完整"
                );
                return;
            }

            var action = game.GameActions?.FirstOrDefault();
            if (action == null)
            {
                PlayniteApi.Dialogs.ShowErrorMessage(
                    "该游戏没有启动动作，无法配置。",
                    "注入器配置失败"
                );
                return;
            }

            string gameExe = action.Path;
            string exeName = Path.GetFileName(gameExe);

            // ⭐ 自动改名：使用 InstallationDirectory 的最后两级文件夹名
            if (exeName.Equals("Game.exe", StringComparison.OrdinalIgnoreCase) ||
                exeName.Equals("nw.exe", StringComparison.OrdinalIgnoreCase))
            {
                string installDir = game.InstallDirectory;

                string[] parts = installDir
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                if (parts.Length >= 2)
                {
                    string last = parts[parts.Length - 1];
                    string secondLast = parts[parts.Length - 2];

                    // 先按你的规则拼一遍
                    string newName = $"{secondLast}\\{last}";

                    // 然后如果还包含 / 或 \，取最后一段作为真正的显示名
                    if (newName.Contains('\\') || newName.Contains('/'))
                    {
                        var nameParts = newName
                            .Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
                        if (nameParts.Length > 0)
                        {
                            newName = nameParts[nameParts.Length - 1];
                        }
                    }

                    game.Name = newName;
                }
            }

            string injectExe = settings.InjectorPath;
            string dllPath = settings.DllPath;
            string nwExe = settings.NwPath;
            string nwDir = string.IsNullOrWhiteSpace(settings.NwDir)
                ? Path.GetDirectoryName(settings.NwPath)
                : settings.NwDir;

            game.GameActions = new ObservableCollection<GameAction>
    {
        new GameAction
        {
            Name = "Play (MTool Inject)",
            Type = GameActionType.File,
            Path = injectExe,
            Arguments = $"\"{gameExe}\" \"{dllPath}\"",
            WorkingDir = Path.GetDirectoryName(injectExe),
            IsPlayAction = true,
        }
    };

            game.GameStartedScript =
        $@"Start-Process ""{nwExe}"" `
-WorkingDirectory ""{nwDir}""";

            game.UseGlobalPostScript = false;

            PlayniteApi.Database.Games.Update(game);

            PlayniteApi.Dialogs.ShowMessage("配置完成！");
        }




        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settingss;
        }

        public override System.Windows.Controls.UserControl GetSettingsView(bool firstRunSettings)
        {
            return new AutoInjectSettingsView();
        }

    }
}