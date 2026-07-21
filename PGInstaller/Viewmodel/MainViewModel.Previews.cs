namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        partial void OnSelectedDepartmentChanged(string? value)
        {
            PreviewList.Clear();
            if (string.IsNullOrEmpty(value)) return;

            var allApps = new List<string>
            {
                "Google Chrome", "Mozilla Firefox", "Microsoft Edge", "WinRAR", "Notepad++",
                "Mozilla Thunderbird", "Oracle Java Runtime", "All VC++ Redistributables",
                "WPS Office 2020", "Revo Uninstaller Pro", "Adobe Acrobat PRO DC", "Sticky Notes",
                "IObit Driver Booster", "Radmin Server (+ Config)", "Zoom", "Advanced IP Scanner",
                "PITK", "A&VGW", "PuTTY (+ Registry Settings)", "WinSCP (+ Config)", "Radmin Viewer",
                "PIMS", "MMS (PCOMM)", "Chrome Bookmarks (CBM)", ".NET Framework 3.5", "FSDM",
                "Wamp 1.7.2", "Wampserver 3.4.0", "Inventory Tools", "Variance", "Coreldraw Graphics X5",
                "Photoshop CS6", "Illustrator CS6", "Bartender", "Bartender Drivers", "Java Oracle",
                "VLC Media Player"
            };

            var defaultAppsForDept = new List<string>
            {
                "Google Chrome", "Mozilla Firefox", "WinRAR", "Notepad++", "Mozilla Thunderbird",
                "Oracle Java Runtime", "All VC++ Redistributables", "WPS Office 2020", "Revo Uninstaller Pro",
                "Adobe Acrobat PRO DC", "Sticky Notes"
            };

            switch (value)
            {
                case "IT":
                    defaultAppsForDept.AddRange(new[] { "Zoom", "Advanced IP Scanner", "PITK", "PuTTY (+ Registry Settings)", "Radmin Server (+ Config)", "WinSCP (+ Config)", "Radmin Viewer", "PIMS", "MMS (PCOMM)", "Chrome Bookmarks (CBM)", "A&VGW" });
                    break;
                case "HRD":
                    defaultAppsForDept.AddRange(new[] { ".NET Framework 3.5", "FSDM", "Wamp 1.7.2" });
                    break;
                case "ICD":
                    defaultAppsForDept.AddRange(new[] { "PIMS", "MMS (PCOMM)", "Wampserver 3.4.0", "Inventory Tools", "Variance" });
                    break;
                case "Payables":
                case "Admin":
                case "Audit":
                    defaultAppsForDept.AddRange(new[] { "MMS (PCOMM)", "PIMS" });
                    break;
                case "Creative":
                    defaultAppsForDept.AddRange(new[] { "Coreldraw Graphics X5", "Photoshop CS6", "Illustrator CS6" });
                    break;
                case "Receiving":
                    defaultAppsForDept.AddRange(new[] { "MMS (PCOMM)", "Bartender", "Bartender Drivers" });
                    break;
                case "Treasury":
                    defaultAppsForDept.AddRange(new[] { "Java Oracle" });
                    break;
                case "Store Operations (Manager)":
                    defaultAppsForDept.AddRange(new[] { "VLC Media Player", "PIMS", "MMS (PCOMM)", "Zoom" });
                    break;
                case "Store Operations (Customer Service)":
                    defaultAppsForDept.AddRange(new[] { "Zoom", "Bartender" });
                    break;
                case "Store Operations (Selling)":
                    defaultAppsForDept.AddRange(new[] { "PIMS", "MMS (PCOMM)", "Bartender" });
                    break;
                case "Store Operations (HBC)":
                    defaultAppsForDept.AddRange(new[] { "PIMS", "MMS (PCOMM)" });
                    break;
            }

            foreach (var app in allApps)
            {
                PreviewList.Add(new InstallAppItem(app, defaultAppsForDept.Contains(app)));
            }
        }
    }
}