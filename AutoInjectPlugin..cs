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

            // 先检查注入器和 nw.exe 是否配置
            if (string.IsNullOrWhiteSpace(settings.InjectorPath) ||
                string.IsNullOrWhiteSpace(settings.NwPath))
            {
                PlayniteApi.Dialogs.ShowErrorMessage(
                    "请先配置注入路径（inject.exe、nw.exe）。",
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

            // 原始 Path（可能带 {InstallDir}）
            string gameExe = action.Path;

            // 如果包含 {InstallDir}，替换为实际 Installation Folder
            if (!string.IsNullOrWhiteSpace(game.InstallDirectory) &&
                gameExe.Contains("{InstallDir}"))
            {
                var installDirTrimmed = game.InstallDirectory
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                gameExe = gameExe.Replace("{InstallDir}", installDirTrimmed);
            }

            string exeName = Path.GetFileName(gameExe);

            // ⭐ 自动改名逻辑（保持你原来的）
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

                    string newName = $"{secondLast}\\{last}";

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

            // ⭐ 用解析后的真正 EXE 路径判断位数
            bool is32 = IsGame32Bit(gameExe);

            string dllPath;

            if (is32)
            {
                if (string.IsNullOrWhiteSpace(settings.DllPath32))
                {
                    PlayniteApi.Dialogs.ShowErrorMessage(
                        "检测到该游戏为 32 位，但你没有配置 32 位 DLL 路径。",
                        "配置不完整"
                    );
                    return;
                }

                dllPath = settings.DllPath32;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(settings.DllPath))
                {
                    PlayniteApi.Dialogs.ShowErrorMessage(
                        "检测到该游戏为 64 位，但你没有配置 64 位 DLL 路径。",
                        "配置不完整"
                    );
                    return;
                }

                dllPath = settings.DllPath;
            }

            string injectExe = settings.InjectorPath;
            string nwExe = settings.NwPath;
            string nwDir = string.IsNullOrWhiteSpace(settings.NwDir)
                ? Path.GetDirectoryName(settings.NwPath)
                : settings.NwDir;

            // ⭐ 设置新的启动动作
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

            // ⭐ 设置 GameStartedScript
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

        private bool IsGame32Bit(string exePath)
        {
            try
            {
                using (var stream = new FileStream(exePath, FileMode.Open, FileAccess.Read))
                using (var reader = new BinaryReader(stream))
                {
                    stream.Seek(0x3C, SeekOrigin.Begin);
                    int peOffset = reader.ReadInt32();

                    stream.Seek(peOffset + 4, SeekOrigin.Begin);
                    ushort machine = reader.ReadUInt16();

                    // 0x014C = 32-bit
                    // 0x8664 = 64-bit
                    return machine == 0x014C;
                }
            }
            catch
            {
                // 读取失败默认按 64 位处理
                return false;
            }
        }

    }
}