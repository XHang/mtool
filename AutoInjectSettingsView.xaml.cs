using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;

namespace AutoInjectPlugin
{
    public partial class AutoInjectSettingsView : UserControl
    {
        public AutoInjectSettingsView()
        {
            InitializeComponent();
        }
        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as AutoInjectSettingsViewModel;
            string field = (sender as Button).Tag as string;

            // -------------------------------
            // 1. 判断是否是文件夹字段
            // -------------------------------
            bool isFolderField = field == "NwDir" || field == "TempFolder";

            if (isFolderField)
            {
                // 选择文件夹
                var dialog = new System.Windows.Forms.FolderBrowserDialog();

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string path = dialog.SelectedPath;

                    if (field == "NwDir")
                        vm.Settings.NwDir = path;

                    if (field == "TempFolder")
                        vm.Settings.TempFolder = path;
                }

                return;
            }

            // -------------------------------
            // 2. 选择文件字段（exe 或 dll）
            // -------------------------------
            var fileDialog = new OpenFileDialog();

            // exe 字段
            if (field == "InjectorPath" || field == "NwPath")
            {
                fileDialog.Filter = "可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*";
            }
            else
            {
                // 其他字段默认是 DLL
                fileDialog.Filter = "DLL 文件 (*.dll)|*.dll|所有文件 (*.*)|*.*";
            }

            if (fileDialog.ShowDialog() == true)
            {
                string file = fileDialog.FileName;

                switch (field)
                {
                    case "InjectorPath": vm.Settings.InjectorPath = file; break;
                    case "DllPath": vm.Settings.DllPath = file; break;
                    case "DllPath32": vm.Settings.DllPath32 = file; break;
                    case "WolfDDL": vm.Settings.WolfDDL = file; break;
                    case "WolfDDL3": vm.Settings.WolfDDL3 = file; break;
                    case "RGSSDDL": vm.Settings.RGSSDDL = file; break;
                    case "RGSSDDL64": vm.Settings.RGSSDDL64 = file; break;
                    case "NwPath": vm.Settings.NwPath = file; break;
                }
            }
        }

    }

}
