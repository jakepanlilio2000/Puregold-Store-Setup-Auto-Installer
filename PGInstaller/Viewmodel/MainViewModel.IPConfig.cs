using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        [ObservableProperty]
        private string _targetIp = "192.168.1.101";
        [ObservableProperty]
        private string _targetGateway = "192.168.1.1";
        [ObservableProperty]
        private string _targetDns = "192.168.1.100";
        [ObservableProperty]
        private string _targetAltDns = "192.168.200.171";
        [ObservableProperty]
        private string _targetSubnet = "255.255.255.0";
        [RelayCommand]
        private async Task ChangeIp()
        {
            if (IsBusy)
                return;
            IsBusy = true;
            Log("------------------------------------------------");
            Log("Starting Network Configuration...");
            try
            {
                foreach (var value in new[]
                {
                    TargetIp,
                    TargetGateway,
                    TargetDns,
                    TargetAltDns,
                    TargetSubnet
                }

                )
                    if (!IPAddress.TryParse(value, out var address) || address.AddressFamily != AddressFamily.InterNetwork || value.Split('.').Length != 4)
                        throw new ArgumentException($"Invalid IPv4 address: {value}");
                var mask = IPAddress.Parse(TargetSubnet).GetAddressBytes();
                uint bits = ((uint)mask[0] << 24) | ((uint)mask[1] << 16) | ((uint)mask[2] << 8) | mask[3];
                uint inverse = ~bits;
                if (bits == 0 || (inverse & (inverse + 1)) != 0)
                    throw new ArgumentException("Subnet mask must contain contiguous network bits.");
                string interfaceName = DetectInterface();
                Log($"   [INFO] Detected Interface: {interfaceName}");
                string ipArgs = $"interface ip set address \"{interfaceName}\" static {TargetIp} {TargetSubnet} {TargetGateway}";
                if (!await RunProcessAsync("netsh", ipArgs, "Setting IP Address"))
                    return;
                string dns1Args = $"interface ip set dns \"{interfaceName}\" static {TargetDns}";
                if (!await RunProcessAsync("netsh", dns1Args, "Setting Primary DNS"))
                    return;
                string dns2Args = $"interface ip add dns \"{interfaceName}\" {TargetAltDns} index=2";
                if (!await RunProcessAsync("netsh", dns2Args, "Setting Alternate DNS"))
                    return;
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
            var ethernet = interfaces.FirstOrDefault(n => n.Name.StartsWith("Ethernet", StringComparison.OrdinalIgnoreCase) && n.OperationalStatus == OperationalStatus.Up);
            if (ethernet != null)
                return ethernet.Name;
            var wifi = interfaces.FirstOrDefault(n => n.Name.StartsWith("Wi-Fi", StringComparison.OrdinalIgnoreCase) && n.OperationalStatus == OperationalStatus.Up);
            if (wifi != null)
                return wifi.Name;
            var active = interfaces.FirstOrDefault(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211);
            return active?.Name ?? throw new InvalidOperationException("No active Ethernet or Wi-Fi adapter found.");
        }
    }
}
