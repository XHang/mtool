using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;

namespace AutoInjectPlugin
{
    public class AutoInjectPlugin : GenericPlugin
    {
        public override Guid Id { get; } = Guid.Parse("7E9E6F34-2B1A-4C9F-9C2C-1C7E8D3A1234");

        private readonly AutoInjectSettingsViewModel settings;

        // 记录已经处理过的游戏
        private readonly HashSet<Guid> processedGames = new HashSet<Guid>();

        private bool running = true;

        public AutoInjectPlugin(IPlayniteAPI api) : base(api)
        {
            settings = new AutoInjectSettingsViewModel(this);

            Properties = new GenericPluginProperties
            {
                HasSettings = true
            };

            // 确认插件加载
            PlayniteApi.Dialogs.ShowMessage("插件已加载");

            // 初始化已存在的游戏，避免对旧库弹窗
            foreach (var game in PlayniteApi.Database.Games)
            {
                processedGames.Add(game.Id);
            }

            // 启动后台线程轮询数据库
            var thread = new Thread(DatabaseWatcher);
            thread.IsBackground = true;
            thread.Start();
        }

        private void DatabaseWatcher()
        {
            while (running)
            {
                Thread.Sleep(1000); // 每秒检查一次

                var allGames = PlayniteApi.Database.Games.ToList();

                foreach (var game in allGames)
                {
                    if (!processedGames.Contains(game.Id))
                    {
                        processedGames.Add(game.Id);
                        HandleNewGame(game);
                    }
                }
            }
        }

        private void HandleNewGame(Game game)
        {
            if (game == null)
                return;

            if (game.GameActions == null || game.GameActions.Count == 0)
                return;

            var originalExe = game.GameActions[0].Path;
            if (string.IsNullOrEmpty(originalExe) || !File.Exists(originalExe))
                return;

            var result = PlayniteApi.Dialogs.ShowMessage(
                "是否要配置 MTool 注入？",
                "MTool 注入",
                MessageBoxButton.YesNo
            );

            if (result == MessageBoxResult.No)
                return;

            var exeDir = Path.GetDirectoryName(originalExe);

            game.GameActions[0].Path = settings.Settings.InjectorPath;
            game.GameActions[0].Arguments =
                $"\"{originalExe}\" \"{settings.Settings.DllPath}\"";
            game.GameActions[0].WorkingDir = exeDir;

            game.PostScript =
$@"Start-Process ""{settings.Settings.NwPath}"" -ArgumentList """"{settings.Settings.NwDir}""""";

            PlayniteApi.Database.Games.Update(game);
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settings;
        }

        public override System.Windows.Controls.UserControl GetSettingsView(bool firstRunSettings)
        {
            return new AutoInjectSettingsView();
        }

        // 如果你以后找到了 Plugin 里 OnApplicationStopped 的准确签名，
        // 可以在这里把 running = false，优雅结束线程。
        // 目前靠进程退出自然结束也没问题。
    }
}