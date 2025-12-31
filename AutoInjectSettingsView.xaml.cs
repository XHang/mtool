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
            var dialog = new OpenFileDialog
            {
                Filter = "DLL 文件 (*.dll)|*.dll|所有文件 (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                var vm = DataContext as AutoInjectSettingsViewModel;
                string field = (sender as Button).Tag as string;

                if (field == "InjectorPath")
                    vm.Settings.InjectorPath = dialog.FileName;

                if (field == "DllPath")
                    vm.Settings.DllPath = dialog.FileName;

                if (field == "DllPath32")
                    vm.Settings.DllPath32 = dialog.FileName;

                if (field == "WolfDDL")
                    vm.Settings.WolfDDL = dialog.FileName;

                if (field == "WolfDDL3")
                    vm.Settings.WolfDDL3 = dialog.FileName;

                if (field == "RGSSDDL")
                    vm.Settings.RGSSDDL = dialog.FileName;

                if (field == "RGSSDDL64")
                    vm.Settings.RGSSDDL64 = dialog.FileName;

                if (field == "NwPath")
                    vm.Settings.NwPath = dialog.FileName;

                if (field == "NwDir")
                    vm.Settings.NwDir = dialog.FileName;
            }
        }
    }

}
