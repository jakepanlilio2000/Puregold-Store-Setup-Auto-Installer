using System;
using System.Diagnostics;
using System.DirectoryServices.AccountManagement;
using System.Management;
using System.Runtime.InteropServices;
using System.Security;
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

        private async Task<bool> HandleDomainJoinAsync()
        {
            if (IsDomainJoined())
            {
                Log("   [INFO] Machine is already joined to the domain.");
                return true;
            }

            Log("   [WARN] Machine is NOT joined to the domain.");
            var result = MessageBox.Show(
                "This computer is not currently joined to the company domain.\n\n" +
                "Department package installation is recommended after joining the domain.\n\n" +
                "Would you like to join the domain now?",
                "Domain Membership Check",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.No)
            {
                Log("   [WARN] Skipping domain join. Some configurations may not apply.");
                return true;
            }

            if (result == MessageBoxResult.Cancel)
            {
                Log("   [INFO] Installation cancelled by user.");
                return false;
            }

            var joinWindow = new DomainJoinWindow { Owner = Application.Current.MainWindow, JoinAction = JoinDomainAsync };
            bool? dialogResult = joinWindow.ShowDialog();

            if (dialogResult == true)
            {
                Log("   [SUCCESS] Domain join process completed.");

                var rebootResult = MessageBox.Show(
                    "Domain join was successful, but a system restart is required to apply changes.\n\n" +
                    "Would you like to restart now? (Installation will resume after reboot)",
                    "Restart Required",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (rebootResult == MessageBoxResult.Yes)
                {
                    Log("   [REBOOT] Restarting system to apply domain changes...");
                    Process.Start(new ProcessStartInfo { FileName = "shutdown.exe", Arguments = "/r /t 0 /c \"Restarting to apply domain changes\"", UseShellExecute = false });
                    Application.Current.Shutdown();
                    return false;
                }
                else
                {
                    Log("   [WARN] Restart postponed. Please restart manually before proceeding with installation.");
                    return false; 
                }
            }

            Log("   [WARN] Domain join window closed without completing.");
            return true;
        }
    }
}