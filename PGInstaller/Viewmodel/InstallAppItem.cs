using CommunityToolkit.Mvvm.ComponentModel;

namespace PGInstaller.Viewmodel
{
    public partial class InstallAppItem : ObservableObject
    {
        [ObservableProperty]
        private string _name;

        [ObservableProperty]
        private bool _isChecked;

        public InstallAppItem(string name, bool isChecked)
        {
            _name = name;
            _isChecked = isChecked;
        }
    }
}