using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
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

        private void UninstallGame(Game game)
        {
            if (game == null)
                return;

            string installDir = game.InstallDirectory;
            if (string.IsNullOrWhiteSpace(installDir) || !Directory.Exists(installDir))
            {
                PlayniteApi.Dialogs.ShowErrorMessage("无法找到该游戏的安装目录，无法卸载。", "卸载失败");
                return;
            }

            // 防止误删根目录
            var root = Path.GetPathRoot(installDir)?.TrimEnd('\\', '/');
            var trimmed = installDir.TrimEnd('\\', '/');
            if (!string.IsNullOrEmpty(root) && string.Equals(root, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                PlayniteApi.Dialogs.ShowErrorMessage("检测到安装目录为根目录，出于安全考虑已取消卸载。", "卸载被取消");
                return;
            }

            var result = PlayniteApi.Dialogs.ShowMessage(
                $"确定要删除以下文件夹及其所有内容吗？\n{installDir}",
                "卸载游戏",
                MessageBoxButton.YesNo
            );

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                Directory.Delete(installDir, true);

                // 从 Playnite 库中删除该游戏
                try
                {
                    PlayniteApi.Database.Games.Remove(game);
                }
                catch
                {
                    // 如果删除数据库项失败，仍向用户报告文件已删除
                }

                PlayniteApi.Dialogs.ShowMessage("安装目录已删除，游戏已从库中移除。", "卸载完成");
            }
            catch (Exception ex)
            {
                PlayniteApi.Dialogs.ShowErrorMessage($"删除安装目录时发生错误：{ex.Message}", "卸载失败");
            }
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
                },
                new GameMenuItem
                {
                    Description = "转区（Locale Emulator）",
                    Action = a => ConfigureGameForLocaleEmulator(args.Games.First())
                },
                new GameMenuItem
                {
                    Description = "卸载游戏（删除安装目录）",
                    Action = a => UninstallGame(args.Games.First())
                }
            };
        }

        private void ConfigureGameForLocaleEmulator(Game game)
        {
            if (game == null)
                return;

            var s = settingss.Settings;
            if (string.IsNullOrWhiteSpace(s.LocaleEmulatorPath))
            {
                PlayniteApi.Dialogs.ShowErrorMessage("请先配置 Locale Emulator 路径。", "转区失败");
                return;
            }

            if (game.GameActions == null || !game.GameActions.Any())
            {
                PlayniteApi.Dialogs.ShowErrorMessage("该游戏没有启动指令，无法执行转区。", "转区失败");
                return;
            }

            string installDir = game.InstallDirectory;
            if (string.IsNullOrWhiteSpace(installDir))
            {
                PlayniteApi.Dialogs.ShowErrorMessage("无法找到该游戏的安装目录，无法执行转区。", "转区失败");
                return;
            }

            var action = game.GameActions.First();
            if (action == null || string.IsNullOrWhiteSpace(action.Path))
            {
                PlayniteApi.Dialogs.ShowErrorMessage("该游戏启动指令为空，无法执行转区。", "转区失败");
                return;
            }

            string originalValue = action.Path.Replace("{InstallDir}", string.Empty);
            string relativeValue = originalValue.TrimStart('\\', '/');
            string combined = installDir.TrimEnd('\\', '/') +
                (string.IsNullOrEmpty(relativeValue) ? string.Empty : "\\") +
                relativeValue;
            string arguments = "\"" + combined + "\"";

            action.Path = s.LocaleEmulatorPath;
            action.Arguments = arguments;
            action.WorkingDir = Path.GetDirectoryName(s.LocaleEmulatorPath);
            action.IsPlayAction = true;

            PlayniteApi.Database.Games.Update(game);
            PlayniteApi.Dialogs.ShowMessage("转区配置完成。", "转区成功");
        }

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

            // -------------------------------
            // 新逻辑：中文路径 + Wolf/RGSS → 创建软链接
            // -------------------------------
            gameExe = HandleChinesePathIfNeeded(gameExe);

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
                $"Inject ? note:The exe file must be MTool_Game.exe if made from Wolf Engine!",
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
            var action = game.GameActions?.FirstOrDefault();
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
                !exeName.Equals("nw.exe", StringComparison.OrdinalIgnoreCase) &&
                !exeName.Equals("MTool_Game.exe", StringComparison.OrdinalIgnoreCase))
                return;

            string installDir = game.InstallDirectory;
            if (string.IsNullOrWhiteSpace(installDir))
                return;

            string[] parts = installDir
                .TrimEnd('\\', '/')
                .Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2)
                return;

            string newName = parts.Last();
            game.Name = newName;
        }

        // ============================================================
        // 新增：中文路径处理 + 创建软链接
        // ============================================================
        private string HandleChinesePathIfNeeded(string gameExe)
        {
            string dir = Path.GetDirectoryName(gameExe);
            bool hasChinese = dir.Any(c => c > 127);

            bool isWolf = IsWolfGame(gameExe);
            bool isWolf3 = IsWolf3Game(gameExe);
            bool isRgss = IsRgssGame(gameExe);

            if (!hasChinese || !(isWolf || isRgss || isWolf3))
                return gameExe;

            // 创建 reference 根目录
            Directory.CreateDirectory(settingss.Settings.TempFolder);

            // 生成 UUID（仅字母）
            string uuid = new string(Guid.NewGuid()
                .ToString("N")
                .Where(char.IsLetter)
                .ToArray());

            string linkPath = Path.Combine(settingss.Settings.TempFolder, uuid);

            // 创建软链接（junction）
            CreateJunction(linkPath, dir);

            // 返回软链接中的 exe 路径
            return Path.Combine(linkPath, Path.GetFileName(gameExe));
        }

        private void CreateJunction(string link, string target)
        {
            if (Directory.Exists(link))
                Directory.Delete(link);

            Directory.CreateDirectory(Path.GetDirectoryName(link));

            var psi = new ProcessStartInfo("cmd.exe",
                $"/c mklink /J \"{link}\" \"{target}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };

            Process.Start(psi)?.WaitForExit();
        }

        // ============================================================

        private string ResolveDllForGame(string gameExe)
        {
            var s = settingss.Settings;
            bool is32 = IsGame32Bit(gameExe);
            bool isWolf = IsWolfGame(gameExe);
            bool isWolf3 = IsWolf3Game(gameExe);
            bool isRgss = IsRgssGame(gameExe);

            if (isWolf)
                return RequireDll(s.WolfDDL, "Wolf 引擎", "Wolf DLL 路径");

            if (isWolf3)
                return RequireDll(s.WolfDDL3, "Wolf3 引擎", "Wolf3 DLL 路径");

            if (isRgss)
                return is32
                    ? RequireDll(s.RGSSDDL, "RGSS 32 位", "RGSS 32 位 DLL 路径")
                    : RequireDll(s.RGSSDDL64, "RGSS 64 位", "RGSS 64 位 DLL 路径");

            return is32
                ? RequireDll(s.DllPath32, "32 位游戏", "32 位 DLL 路径")
                : RequireDll(s.DllPath, "64 位游戏", "64 位 DLL 路径");
        }

        private string RequireDll(string dllPath, string engineName, string configName)
        {
            if (string.IsNullOrWhiteSpace(dllPath))
            {
                PlayniteApi.Dialogs.ShowErrorMessage(
                    $"检测到该游戏为 {engineName}，但你没有配置 {configName}。",
                    "配置不完整"
                );
                return null;
            }
            return dllPath;
        }

        private bool IsWolfGame(string gameExe)
        {
            var dir = Path.GetDirectoryName(gameExe);
            if (dir == null)
                return false;

            bool hasGameIni = File.Exists(Path.Combine(dir, "Game.ini"));
            bool hasScriptVdf = File.Exists(Path.Combine(dir, "Script.vdf"));
            bool hasAppendDb = File.Exists(Path.Combine(dir, "Append.db"));
            bool wolfDataLock = File.Exists(Path.Combine(dir, "wolfDataLock.json"));
         

            return hasGameIni  && (hasScriptVdf || hasAppendDb || wolfDataLock);
        }

        private bool IsWolf3Game(string gameExe)
        {
            // 1. 必须先是 Wolf 引擎，否则不可能是 Wolf3
            if (!IsWolfGame(gameExe))
                return false;

            try
            {
                var info = FileVersionInfo.GetVersionInfo(gameExe);

                // 可能是 "3.11.123.456" 或 "3.21" 或 "3.0"
                string version = info.FileVersion;

                if (string.IsNullOrWhiteSpace(version))
                    return false;

                // 主版本号
                string major = version.Split('.')[0];

                return major == "3";
            }
            catch
            {
                return false;
            }
        }


        private bool IsRgssGame(string gameExe)
        {
            var dir = Path.GetDirectoryName(gameExe);
            if (dir == null)
                return false;

            // ① 判断是否存在 RGSS 加密包
            string[] archives = { "Game.rgssad", "Game.rgss2a", "Game.rgss3a" };
            if (archives.Any(a => File.Exists(Path.Combine(dir, a))))
                return true;

            // ② 判断是否存在包含 RGSS 的 DLL
            if (Directory.EnumerateFiles(dir, "*.dll")
                         .Any(f => Path.GetFileName(f)
                         .Contains("RGSS")))
                return true;

            // ③ 判断 EXE 的 File Description 是否包含 RGSS
            try
            {
                var info = FileVersionInfo.GetVersionInfo(gameExe);
                if (!string.IsNullOrEmpty(info.FileDescription) &&
                    info.FileDescription.Contains("RGSS"))
                    return true;
            }
            catch
            {
                // 忽略异常，继续返回 false
            }

            return false;
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
                    Arguments = $"\"{gameExe}\" \"{dllPath}\"",
                    WorkingDir = Path.GetDirectoryName(injectExe),
                    IsPlayAction = true
                }
            };

            game.GameStartedScript =
                $"Start-Process \"{nwExe}\" `\n-WorkingDirectory \"{nwDir}\"";

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

                    return machine == 0x014C;
                }
            }
            catch
            {
                return false;
            }
        }

        public override ISettings GetSettings(bool firstRunSettings) => settingss;
        public override System.Windows.Controls.UserControl GetSettingsView(bool firstRunSettings)
            => new AutoInjectSettingsView();
    }
}
