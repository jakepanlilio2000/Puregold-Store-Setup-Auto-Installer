using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Net.NetworkInformation;

namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {

        [ObservableProperty] private string _targetIp = "192.168.1.101";
        [ObservableProperty] private string _targetGateway = "192.168.1.1";
        [ObservableProperty] private string _targetDns = "192.168.1.100";
        [ObservableProperty] private string _targetAltDns = "192.168.200.171";
        [ObservableProperty] private string _targetSubnet = "255.255.255.0";

        [RelayCommand]
        private async Task ChangeIp()
        {
            if (IsBusy) return;
            IsBusy = true;
            Log("------------------------------------------------");
            Log("Starting Network Configuration...");

            try
            {
                string interfaceName = DetectInterface();
                Log($"   [INFO] Detected Interface: {interfaceName}");
                string ipArgs = $"interface ip set address \"{interfaceName}\" static {TargetIp} {TargetSubnet} {TargetGateway}";
                await RunProcessAsync("netsh", ipArgs, "Setting IP Address");
                string dns1Args = $"interface ip set dns \"{interfaceName}\" static {TargetDns}";
                await RunProcessAsync("netsh", dns1Args, "Setting Primary DNS");
                string dns2Args = $"interface ip add dns \"{interfaceName}\" {TargetAltDns} index=2";
                await RunProcessAsync("netsh", dns2Args, "Setting Alternate DNS");

                Log("   [SUCCESS] Network settings applied.");
            }
            catch (Exception ex)
            {
                Log($"   [ERROR] Network Config Failed: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
                Log("------------------------------------------------");
            }
        }

        private string DetectInterface()
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();

            var ethernet = interfaces.FirstOrDefault(n => n.Name.StartsWith("Ethernet") && n.OperationalStatus == OperationalStatus.Up);
            if (ethernet != null) return ethernet.Name;

            var wifi = interfaces.FirstOrDefault(n => n.Name.StartsWith("Wi-Fi") && n.OperationalStatus == OperationalStatus.Up);
            if (wifi != null) return wifi.Name;
            return "Ethernet";
        }
    }
}
