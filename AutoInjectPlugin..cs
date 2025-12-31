using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;

namespace AutoInjectPlugin
{
    public class AutoInjectPlugin : GenericPlugin
    {
        public override Guid Id { get; } = Guid.Parse("7E9E6F34-2B1A-4C9F-9C2C-1C7E8D3A1234");
        private readonly AutoInjectSettingsViewModel settingss;

        public AutoInjectPlugin(IPlayniteAPI api) : base(api)
        {
            settingss = new AutoInjectSettingsViewModel(this);

            Properties = new GenericPluginProperties
            {
                HasSettings = true
            };
        }

        // ---------------------------
        // 右键菜单入口
        // ---------------------------
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

        // ---------------------------
        // 主流程（已极简）
        // ---------------------------
        private void ConfigureGameForInject(Game game)
        {
            if (!ConfirmUserIntent(game))
                return;

            if (!ValidateGlobalSettings())
                return;

            string gameExe = ResolveGameExecutable(game);
            if (string.IsNullOrEmpty(gameExe))
                return;

            AutoRenameGameIfNeeded(game, gameExe);

            string dllPath = ResolveDllForGame(gameExe);
            if (dllPath == null)
                return;

            ApplyInjectConfiguration(game, gameExe, dllPath);

            PlayniteApi.Dialogs.ShowMessage("配置完成！");
        }

        // ============================================================
        // 抽取的函数区域
        // ============================================================

        private bool ConfirmUserIntent(Game game)
        {
            var result = PlayniteApi.Dialogs.ShowMessage(
                string.Format("你是否要把《{0}》改为注入器启动？", game.Name),
                "注入器配置",
                MessageBoxButton.YesNo
            );

            return result == MessageBoxResult.Yes;
        }

        private bool ValidateGlobalSettings()
        {
            var s = settingss.Settings;

            if (string.IsNullOrWhiteSpace(s.InjectorPath) ||
                string.IsNullOrWhiteSpace(s.NwPath))
            {
                PlayniteApi.Dialogs.ShowErrorMessage(
                    "请先配置注入路径（inject.exe、nw.exe）。",
                    "配置不完整"
                );
                return false;
            }

            return true;
        }

        private string ResolveGameExecutable(Game game)
        {
            var action = game.GameActions != null ? game.GameActions.FirstOrDefault() : null;
            if (action == null)
            {
                PlayniteApi.Dialogs.ShowErrorMessage(
                    "该游戏没有启动动作，无法配置。",
                    "注入器配置失败"
                );
                return null;
            }

            string exe = action.Path;

            if (!string.IsNullOrWhiteSpace(game.InstallDirectory) &&
                exe.Contains("{InstallDir}"))
            {
                string realDir = game.InstallDirectory.TrimEnd('\\', '/');
                exe = exe.Replace("{InstallDir}", realDir);
            }

            return exe;
        }

        private void AutoRenameGameIfNeeded(Game game, string gameExe)
        {
            string exeName = Path.GetFileName(gameExe);
            if (!exeName.Equals("Game.exe", StringComparison.OrdinalIgnoreCase) &&
                !exeName.Equals("nw.exe", StringComparison.OrdinalIgnoreCase))
                return;

            string installDir = game.InstallDirectory;
            if (string.IsNullOrWhiteSpace(installDir))
                return;

            string[] parts = installDir
                .TrimEnd('\\', '/')
                .Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2)
                return;

            string secondLast = parts[parts.Length - 2];
            string last = parts[parts.Length - 1];

            string newName = secondLast + "\\" + last;

            string[] cleanParts = newName.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (cleanParts.Length > 0)
                newName = cleanParts[cleanParts.Length - 1];

            game.Name = newName;
        }

        private string ResolveDllForGame(string gameExe)
        {
            bool is32 = IsGame32Bit(gameExe);
            var s = settingss.Settings;

            if (is32)
            {
                if (string.IsNullOrWhiteSpace(s.DllPath32))
                {
                    PlayniteApi.Dialogs.ShowErrorMessage(
                        "检测到该游戏为 32 位，但你没有配置 32 位 DLL 路径。",
                        "配置不完整"
                    );
                    return null;
                }
                return s.DllPath32;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(s.DllPath))
                {
                    PlayniteApi.Dialogs.ShowErrorMessage(
                        "检测到该游戏为 64 位，但你没有配置 64 位 DLL 路径。",
                        "配置不完整"
                    );
                    return null;
                }
                return s.DllPath;
            }
        }

        private void ApplyInjectConfiguration(Game game, string gameExe, string dllPath)
        {
            var s = settingss.Settings;

            string injectExe = s.InjectorPath;
            string nwExe = s.NwPath;
            string nwDir = string.IsNullOrWhiteSpace(s.NwDir)
                ? Path.GetDirectoryName(s.NwPath)
                : s.NwDir;

            game.GameActions = new ObservableCollection<GameAction>
            {
                new GameAction
                {
                    Name = "Play (MTool Inject)",
                    Type = GameActionType.File,
                    Path = injectExe,
                    Arguments = "\"" + gameExe + "\" \"" + dllPath + "\"",
                    WorkingDir = Path.GetDirectoryName(injectExe),
                    IsPlayAction = true
                }
            };

            game.GameStartedScript =
                "Start-Process \"" + nwExe + "\" `" + Environment.NewLine +
                "-WorkingDirectory \"" + nwDir + "\"";

            game.UseGlobalPostScript = false;
            game.UseGlobalGameStartedScript = false;
            PlayniteApi.Database.Games.Update(game);
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

                    return machine == 0x014C; // 32-bit
                }
            }
            catch
            {
                return false; // 默认按 64 位
            }
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
