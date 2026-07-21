using CommunityToolkit.Mvvm.ComponentModel;

namespace PGInstaller.Viewmodel
{
    public partial class BloatwareItem : ObservableObject
    {
        [ObservableProperty]
        private string _displayName = "";

        [ObservableProperty]
        private string _uninstallString = "";

        [ObservableProperty]
        private bool _isUwp;

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private bool _isKnownBloat;
    }
}