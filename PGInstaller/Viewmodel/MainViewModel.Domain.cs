using CommunityToolkit.Mvvm.Input;
using System;
using System.Diagnostics;
using System.DirectoryServices.AccountManagement;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        private bool IsDomainJoined()
        {
            try
            {
                var properties = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties();
                return !string.IsNullOrEmpty(properties.DomainName) && properties.DomainName != properties.HostName;
            }
            catch
            {
                return false;
            }
        }

        private async Task<(bool success, bool rebootRequired, string message)> JoinDomainAsync(string domain, string username, SecureString securePassword)
        {
            return await Task.Run(() =>
            {
                IntPtr passwordPtr = IntPtr.Zero;
                try
                {
                    passwordPtr = Marshal.SecureStringToBSTR(securePassword);
                    string plainPassword = Marshal.PtrToStringBSTR(passwordPtr);
                    using var context = new PrincipalContext(ContextType.Domain, domain);
                    if (!context.ValidateCredentials(username, plainPassword))
                    {
                        return (false, false, "Invalid credentials. Please check your username and password.");
                    }
                    using var cs = new ManagementClass("Win32_ComputerSystem");
                    foreach (var obj in cs.GetInstances())
                    {
                        using var computer = (ManagementObject)obj;
                        var args = new object[] { domain, plainPassword, username, null!, 3 };
                        var result = computer.InvokeMethod("JoinDomainOrWorkgroup", args);

                        int returnCode = Convert.ToInt32(result);

                        Array.Clear(args, 0, args.Length);
                        plainPassword = "";

                        if (returnCode == 0)
                        {
                            return (true, false, "Successfully joined the domain.");
                        }
                        else if (returnCode == 2691)
                        {
                            return (true, true, "Successfully joined the domain. A reboot is required to complete the process.");
                        }
                        else
                        {
                            string errorMsg = GetDomainJoinErrorMessage(returnCode);
                            return (false, false, $"Failed to join domain. Error code: {returnCode} - {errorMsg}");
                        }
                    }
                    return (false, false, "Could not find computer system object.");
                }
                catch (PrincipalServerDownException)
                {
                    return (false, false, "Domain controller unreachable. Please check network connectivity and DNS.");
                }
                catch (PrincipalOperationException ex)
                {
                    return (false, false, $"Domain operation failed: {ex.Message}");
                }
                catch (Exception ex)
                {
                    return (false, false, $"Exception during domain join: {ex.Message}");
                }
                finally
                {
                    if (passwordPtr != IntPtr.Zero)
                    {
                        Marshal.ZeroFreeBSTR(passwordPtr);
                    }
                }
            });
        }

        private string GetDomainJoinErrorMessage(int code)
        {
            return code switch
            {
                5 => "Access denied. Insufficient permissions.",
                87 => "Invalid parameter.",
                1326 => "Logon failure: unknown user name or bad password.",
                1355 => "The specified domain either does not exist or could not be contacted.",
                1909 => "The referenced account is currently locked out.",
                2087 => "The computer could not be added to the domain. The account already exists.",
                2224 => "The account already exists.",
                _ => "Unknown error."
            };
        }

        [RelayCommand]
        private async Task RenameComputer()
        {
            string currentName = Environment.MachineName;
            string newName = await Application.Current.Dispatcher.InvokeAsync(() =>
                ShowInputDialog("Enter new computer name:", currentName));

            if (!string.IsNullOrWhiteSpace(newName) && newName != currentName)
            {
                Log($"   [INIT] Renaming computer to '{newName}'...");
                try
                {
                    // WMIC is universally supported for renaming in Windows
                    await RunProcessAsync("wmic", $"computersystem where name=\"{currentName}\" call rename name=\"{newName}\"", "Renaming Computer", true);

                    var restart = MessageBox.Show(
                        "A restart is required to apply the new computer name.\n\nRestart now?",
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
                catch (Exception ex)
                {
                    Log($"   [ERROR] Failed to rename computer: {ex.Message}");
                }
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
            catch
            {

            }

            if (JoinDomainAfterInstall)
            {
                string domainName = await Application.Current.Dispatcher.InvokeAsync(() =>
                    ShowInputDialog("Enter Domain Name (e.g., corp.local):", ""));

                string domainUser = await Application.Current.Dispatcher.InvokeAsync(() =>
                    ShowInputDialog("Enter Domain Admin Username:", "Administrator"));

                string domainPassword = await Application.Current.Dispatcher.InvokeAsync(() =>
                    ShowInputDialog("Enter Domain Admin Password:", ""));

                if (string.IsNullOrWhiteSpace(domainName) || string.IsNullOrWhiteSpace(domainUser))
                {
                    Log("   [WARN] Domain join cancelled or incomplete.");
                    return true; 
                }

                Log($"   [INIT] Joining domain: {domainName}...");

                string script = $@"
$domain = '{domainName}'
$username = '{domainName}\{domainUser}'
$password = ConvertTo-SecureString '{domainPassword}' -AsPlainText -Force
$credential = New-Object System.Management.Automation.PSCredential($username, $password)

try {{
    Add-Computer -DomainName $domain -Credential $credential -Force -ErrorAction Stop
    Write-Host 'Successfully joined the domain.'
}} catch {{
    Write-Error $_.Exception.Message
    exit 1
}}
";
                byte[] scriptBytes = Encoding.Unicode.GetBytes(script);
                string encodedScript = Convert.ToBase64String(scriptBytes);

                bool success = await RunProcessAsync(
                    "powershell",
                    $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {encodedScript}",
                    "Joining Domain",
                    true);

                if (success)
                {
                    Log("   [SUCCESS] Successfully joined the domain.");
                    var restart = MessageBox.Show(
                        "A restart is required to complete the domain join and apply Group Policies.\n\nRestart now?",
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
                        return false; 
                    }
                }
                else
                {
                    Log("   [ERROR] Failed to join the domain. Check credentials and network connectivity.");
                }
            }
            else
            {
                Log("   [INFO] Domain join skipped (checkbox unchecked or already joined).");
            }

            return true;
        }
        [RelayCommand]
        private async Task JoinDomain()
        {
            string domainName = await Application.Current.Dispatcher.InvokeAsync(() =>
                ShowInputDialog("Enter Domain Name (e.g., corp.local):", ""));

            string domainUser = await Application.Current.Dispatcher.InvokeAsync(() =>
                ShowInputDialog("Enter Domain Admin Username:", "Administrator"));

            string domainPassword = await Application.Current.Dispatcher.InvokeAsync(() =>
                ShowInputDialog("Enter Domain Admin Password:", ""));

            if (string.IsNullOrWhiteSpace(domainName) || string.IsNullOrWhiteSpace(domainUser))
            {
                Log("   [WARN] Domain join cancelled or incomplete.");
                return;
            }

            Log($"   [INIT] Joining domain: {domainName}...");

            string script = $@"
$domain = '{domainName}'
$username = '{domainName}\{domainUser}'
$password = ConvertTo-SecureString '{domainPassword}' -AsPlainText -Force
$credential = New-Object System.Management.Automation.PSCredential($username, $password)

try {{
    Add-Computer -DomainName $domain -Credential $credential -Force -ErrorAction Stop
    Write-Host 'Successfully joined the domain.'
}} catch {{
    Write-Error $_.Exception.Message
    exit 1
}}
";
            byte[] scriptBytes = Encoding.Unicode.GetBytes(script);
            string encodedScript = Convert.ToBase64String(scriptBytes);

            bool success = await RunProcessAsync(
                "powershell",
                $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {encodedScript}",
                "Joining Domain",
                true);

            if (success)
            {
                Log("   [SUCCESS] Successfully joined the domain.");
                var restart = MessageBox.Show(
                    "A restart is required to complete the domain join and apply Group Policies.\n\nRestart now?",
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
            else
            {
                Log("   [ERROR] Failed to join the domain. Check credentials and network connectivity.");
            }
        }
    }

}