using CommunityToolkit.Mvvm.ComponentModel;

namespace PGInstaller.Viewmodel
{
    public partial class InstallAppItem : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private bool _isChecked;

        [ObservableProperty]
        private bool _isInstalled;

        [ObservableProperty]
        private bool _forceInstall;

        public InstallAppItem(string name, bool isChecked = false, bool isInstalled = false, bool forceInstall = false)
        {
            Name = name;
            IsChecked = isChecked;
            IsInstalled = isInstalled;
            ForceInstall = forceInstall;
        }
    }
}
