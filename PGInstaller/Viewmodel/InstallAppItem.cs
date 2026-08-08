using CommunityToolkit.Mvvm.ComponentModel;

namespace PGInstaller.Viewmodel
{
    public partial class InstallAppItem : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private bool _isChecked;

        public InstallAppItem(string name, bool isChecked = false)
        {
            Name = name;
            IsChecked = isChecked;
        }
    }
}