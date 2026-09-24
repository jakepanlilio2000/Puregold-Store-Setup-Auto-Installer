using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        private string _lastDomainName = "";

        private bool IsDomainJoined()
        {
            try
            {
                var properties = IPGlobalProperties.GetIPGlobalProperties();
                return !string.IsNullOrEmpty(properties.DomainName) &&
                       !properties.DomainName.Equals(properties.HostName, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        [RelayCommand]
        private async Task RenameComputer()
        {
            string currentName = Environment.MachineName;
            string newName = await Application.Current.Dispatcher.InvokeAsync(() =>
                ShowInputDialog("Enter new computer name:", currentName));

            if (!string.IsNullOrWhiteSpace(newName) && !newName.Equals(currentName, StringComparison.OrdinalIgnoreCase))
            {
                bool renamed = await ExecuteRenameComputerInternal(newName);
                if (renamed)
                {
                    var restart = MessageBox.Show(
                        $"Computer has been renamed to '{newName}'.\n\nA restart is required to apply the new computer name.\n\nRestart now?",
                        "Restart Required",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (restart == MessageBoxResult.Yes)
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "shutdown.exe",
                            Arguments = "/r /t 0",
                            CreateNoWindow = true,
                            UseShellExecute = false
                        });
                    }
                }
            }
        }

        public async Task<(bool valid, string message)> ValidateDomainPrerequisitesAsync(string domainName)
        {
            return await Task.Run(async () =>
            {
                if (string.IsNullOrWhiteSpace(domainName))
                {
                    return (false, "Domain name cannot be empty.");
                }

                string domain = domainName.Trim();

                // 1. Check network availability
                if (!NetworkInterface.GetIsNetworkAvailable())
                {
                    return (false, "No active network connection detected. Please connect an Ethernet cable or connect to Wi-Fi.");
                }

                // 2. Gather active DNS servers from UP interfaces
                var dnsServers = new List<string>();
                try
                {
                    var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                        .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                                    n.NetworkInterfaceType != NetworkInterfaceType.Loopback);

                    foreach (var iface in interfaces)
                    {
                        var ipProps = iface.GetIPProperties();
                        foreach (var dns in ipProps.DnsAddresses)
                        {
                            if (dns.AddressFamily == AddressFamily.InterNetwork)
                            {
                                string ipStr = dns.ToString();
                                if (!dnsServers.Contains(ipStr))
                                {
                                    dnsServers.Add(ipStr);
                                }
                            }
                        }
                    }
                }
                catch { }

                string dnsSummary = dnsServers.Count > 0 ? string.Join(", ", dnsServers) : "None detected";

                // 3. DNS Resolution Check
                IPAddress[] hostAddresses;
                try
                {
                    hostAddresses = await Dns.GetHostAddressesAsync(domain);
                }
                catch (SocketException)
                {
                    return (false,
                        $"Could not resolve domain '{domain}' via DNS.\n\n" +
                        $"Current DNS Server(s) on this PC: {dnsSummary}\n\n" +
                        $"Active Directory requires this computer's Primary DNS to point directly to the Domain Controller IP (e.g., 192.168.1.100 or store server).\n\n" +
                        $"If your DNS is set to a router (like 192.168.100.1 or 192.168.1.1) or public DNS (8.8.8.8), the domain controller cannot be contacted.");
                }
                catch (Exception ex)
                {
                    return (false, $"DNS resolution error for '{domain}': {ex.Message}");
                }

                if (hostAddresses == null || hostAddresses.Length == 0)
                {
                    return (false, $"No IP addresses found for domain '{domain}'. Please verify your DNS settings.");
                }

                // 4. Test connectivity to DC (port 389 LDAP, 445 SMB, 53 DNS, 88 Kerberos)
                var ipv4Addresses = hostAddresses.Where(a => a.AddressFamily == AddressFamily.InterNetwork).ToArray();
                if (ipv4Addresses.Length > 0)
                {
                    var targetIp = ipv4Addresses[0];
                    bool reachable = false;
                    int[] portsToTest = { 389, 445, 53, 88 };

                    foreach (int port in portsToTest)
                    {
                        try
                        {
                            using var tcpClient = new TcpClient();
                            var connectTask = tcpClient.ConnectAsync(targetIp, port);
                            if (await Task.WhenAny(connectTask, Task.Delay(2000)) == connectTask && tcpClient.Connected)
                            {
                                reachable = true;
                                break;
                            }
                        }
                        catch { }
                    }

                    if (!reachable)
                    {
                        return (false,
                            $"Domain '{domain}' resolved to {targetIp}, but the Domain Controller did not respond on AD ports (389 LDAP / 445 SMB / 53 DNS).\n\n" +
                            $"Please verify that the Domain Controller is online and that network firewall rules allow Active Directory communication.");
                    }
                }

                return (true, $"Domain '{domain}' is reachable.");
            });
        }

        public async Task<(bool success, bool rebootRequired, string message)> ExecuteDomainJoinAsync(
            string domainName,
            string domainUser,
            string domainPassword)
        {
            return await Task.Run(async () =>
            {
                string trimmedDomain = domainName.Trim();
                string rawUser = domainUser.Trim();

                // Format username properly (do not prepend domain if already qualified)
                string fullUsername;
                if (rawUser.Contains('\\') || rawUser.Contains('@'))
                {
                    fullUsername = rawUser;
                }
                else
                {
                    fullUsername = $"{trimmedDomain}\\{rawUser}";
                }

                Log($"   [INIT] Joining domain: {trimmedDomain} with user account '{fullUsername}'...");

                string escapedDomain = trimmedDomain.Replace("'", "''");
                string escapedUser = fullUsername.Replace("'", "''");
                string escapedPass = domainPassword.Replace("'", "''");

                string script = $@"
$domain = '{escapedDomain}'
$username = '{escapedUser}'
$password = ConvertTo-SecureString '{escapedPass}' -AsPlainText -Force
$credential = New-Object System.Management.Automation.PSCredential($username, $password)

try {{
    Add-Computer -DomainName $domain -Credential $credential -Force -ErrorAction Stop
    Write-Output 'Successfully joined the domain.'
    exit 0
}} catch {{
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 1
}}
";
                byte[] scriptBytes = Encoding.Unicode.GetBytes(script);
                string encodedScript = Convert.ToBase64String(scriptBytes);

                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {encodedScript}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

                process.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        outputBuilder.AppendLine(e.Data);
                        string? clean = CleanLogLine(e.Data);
                        if (clean != null) Log($"    > {clean}");
                    }
                };

                process.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        errorBuilder.AppendLine(e.Data);
                        string? clean = CleanLogLine(e.Data);
                        if (clean != null) Log($"    > {clean}");
                    }
                };

                CurrentTaskDescription = "Joining Domain";
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync();

                bool success = process.ExitCode == 0;

                if (success)
                {
                    Log("   [SUCCESS] Successfully joined the domain.");
                    _lastDomainName = trimmedDomain;
                    return (true, true, $"Successfully joined domain '{trimmedDomain}'. A computer restart is required to apply changes.");
                }
                else
                {
                    string rawError = errorBuilder.ToString().Trim();
                    string translated = TranslateDomainError(rawError, trimmedDomain, fullUsername);
                    Log($"   [ERROR] Failed to join domain: {translated}");
                    return (false, false, translated);
                }
            });
        }

        private string TranslateDomainError(string rawError, string domain, string user)
        {
            if (string.IsNullOrWhiteSpace(rawError))
            {
                return "Failed to join domain. Check credentials, DNS configuration, and network connectivity.";
            }

            if (rawError.Contains("The specified domain either does not exist or could not be contacted", StringComparison.OrdinalIgnoreCase) ||
                rawError.Contains("1355"))
            {
                return $"The domain '{domain}' either does not exist or could not be contacted.\n\n" +
                       "Please ensure the computer is connected to the store network and that Primary DNS points directly to the Domain Controller IP.";
            }

            if (rawError.Contains("Logon failure", StringComparison.OrdinalIgnoreCase) ||
                rawError.Contains("unknown user name or bad password", StringComparison.OrdinalIgnoreCase) ||
                rawError.Contains("1326"))
            {
                return $"Logon failure: Invalid username or password for account '{user}'. Please verify the credentials.";
            }

            if (rawError.Contains("Access is denied", StringComparison.OrdinalIgnoreCase) ||
                rawError.Contains("Access denied", StringComparison.OrdinalIgnoreCase) ||
                rawError.Contains(" 5 "))
            {
                return $"Access denied for account '{user}'. This account does not have permission to join computers to domain '{domain}'.";
            }

            if (rawError.Contains("The account already exists", StringComparison.OrdinalIgnoreCase) ||
                rawError.Contains("2224") || rawError.Contains("2087"))
            {
                return $"The computer account '{Environment.MachineName}' already exists in domain '{domain}'.";
            }

            if (rawError.Contains("locked out", StringComparison.OrdinalIgnoreCase) ||
                rawError.Contains("1909"))
            {
                return $"The user account '{user}' is currently locked out in Active Directory.";
            }

            // Strip CLIXML fragments if any exist
            string cleaned = Regex.Replace(rawError, @"<[^>]+>", " ").Trim();
            cleaned = Regex.Replace(cleaned, @"_x000D__x000A_", " ").Trim();
            cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();

            return !string.IsNullOrWhiteSpace(cleaned) ? cleaned : rawError;
        }

        private async Task<bool> PromptAndExecuteDomainJoinAsync(bool isPartOfInstaller)
        {
            var dialogResult = await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                string initialDomain = !string.IsNullOrWhiteSpace(_lastDomainName) ? _lastDomainName : "";
                var joinWindow = new DomainJoinWindow(initialDomain)
                {
                    Owner = Application.Current.MainWindow,
                    ValidateAction = ValidateDomainPrerequisitesAsync,
                    JoinAction = ExecuteDomainJoinAsync
                };

                return joinWindow.ShowDialog();
            });

            if (dialogResult == true)
            {
                await CheckDomainStatusAsync();

                var restart = MessageBox.Show(
                    "Domain join completed successfully!\n\nA system restart is required to complete the domain join and apply Group Policies.\n\nRestart now?",
                    "Restart Required",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (restart == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "shutdown.exe",
                        Arguments = "/r /t 0",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                    return false; // reboot initiated, abort installer flow
                }

                return true; // user postponed restart
            }
            else
            {
                Log("   [INFO] Domain join cancelled by user.");
                return true; // continue setup without domain join
            }
        }

        private async Task<bool> HandleDomainJoinAsync()
        {
            try
            {
                var properties = IPGlobalProperties.GetIPGlobalProperties();

                if (!string.IsNullOrEmpty(properties.DomainName) &&
                    !properties.DomainName.Equals(properties.HostName, StringComparison.OrdinalIgnoreCase))
                {
                    Log($"   [INFO] Computer is already joined to domain: '{properties.DomainName}'. Skipping domain join prompt.");
                    return true;
                }
            }
            catch { }

            if (JoinDomainAfterInstall)
            {
                return await PromptAndExecuteDomainJoinAsync(isPartOfInstaller: true);
            }
            else
            {
                Log("   [INFO] Domain join skipped (checkbox unchecked or already joined).");
                return true;
            }
        }

        [RelayCommand]
        private async Task JoinDomain()
        {
            try
            {
                var properties = IPGlobalProperties.GetIPGlobalProperties();
                if (!string.IsNullOrEmpty(properties.DomainName) &&
                    !properties.DomainName.Equals(properties.HostName, StringComparison.OrdinalIgnoreCase))
                {
                    var already = MessageBox.Show(
                        $"This computer is already joined to domain: '{properties.DomainName}'.\n\nDo you want to join a different domain?",
                        "Already Joined Domain",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (already != MessageBoxResult.Yes) return;
                }
            }
            catch { }

            await PromptAndExecuteDomainJoinAsync(isPartOfInstaller: false);
        }
    }
}