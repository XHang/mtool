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

            // 弹出确认框
            var result = PlayniteApi.Dialogs.ShowMessage(
                $"你是否要把《{game.Name}》改为注入器启动？",
                "注入器配置",
                MessageBoxButton.YesNo
            );

            if (result != MessageBoxResult.Yes)
                return;

            // ⭐ 用户点 YES 后才检查配置是否完整
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

            // 获取原始 Play Action
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

            // 使用配置化路径
            string injectExe = settings.InjectorPath;
            string dllPath = settings.DllPath;
            string nwExe = settings.NwPath;
            string nwDir = string.IsNullOrWhiteSpace(settings.NwDir)
                ? Path.GetDirectoryName(settings.NwPath)
                : settings.NwDir;

            // 修改 Play Action（必须用 ObservableCollection）
            game.GameActions = new ObservableCollection<GameAction>
    {
        new GameAction
        {
            Name = "Play (MTool Inject)",
            Type = GameActionType.File,
            Path = injectExe,
            Arguments = $"\"{gameExe}\" \"{dllPath}\"",
            WorkingDir = Path.GetDirectoryName(injectExe)
        }
    };

            // 修改启动后脚本（使用配置）
            game.GameStartedScript =
        $@"Start-Process ""{nwExe}"" `
    -WorkingDirectory ""{nwDir}""";

            // 禁用全局脚本
            game.UseGlobalPostScript = false;

            // 保存修改
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