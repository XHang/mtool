using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;

namespace AutoInjectPlugin
{
    public class AutoInjectSettings : ObservableObject
    {
        private string injectorPath = "";
        public string InjectorPath { get => injectorPath; set => SetValue(ref injectorPath, value); }

        private string dllPath = "";
        public string DllPath { get => dllPath; set => SetValue(ref dllPath, value); }

        private string dllPath32 = "";
        public string DllPath32 { get => dllPath32; set => SetValue(ref dllPath32, value); }

        private string wolfDDL = "";
        public string WolfDDL { get => wolfDDL; set => SetValue(ref wolfDDL, value); }

        private string wolfDDL3 = "";
        public string WolfDDL3 { get => wolfDDL3; set => SetValue(ref wolfDDL3, value); }


        private string rgssDDL = "";
        public string RGSSDDL { get => rgssDDL; set => SetValue(ref rgssDDL, value); }

        private string rgssDDL64 = "";
        public string RGSSDDL64 { get => rgssDDL64; set => SetValue(ref rgssDDL64, value); }

        private string nwPath = "";
        public string NwPath { get => nwPath; set => SetValue(ref nwPath, value); }

        private string nwDir = "";
        public string NwDir { get => nwDir; set => SetValue(ref nwDir, value); }
    }

    public class AutoInjectSettingsViewModel : ObservableObject, ISettings
    {
        private readonly AutoInjectPlugin plugin;
        private AutoInjectSettings editingClone { get; set; }

        public AutoInjectSettings Settings { get; set; }

        public AutoInjectSettingsViewModel(AutoInjectPlugin plugin)
        {
            this.plugin = plugin;

            var saved = plugin.LoadPluginSettings<AutoInjectSettings>();
            Settings = saved ?? new AutoInjectSettings();
        }

        public void BeginEdit()
        {
            editingClone = Serialization.GetClone(Settings);
        }

        public void CancelEdit()
        {
            Settings = editingClone;
        }

        public void EndEdit()
        {
            plugin.SavePluginSettings(Settings);
        }

        // ★★★ 必须实现的接口方法（你的 SDK 要求）
        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();

            // 如果你想验证路径是否为空，可以写：
            // if (string.IsNullOrEmpty(Settings.InjectorPath))
            //     errors.Add("InjectorPath 不能为空");

            // 目前不做验证，直接返回 true
            return true;
        }
    }
}