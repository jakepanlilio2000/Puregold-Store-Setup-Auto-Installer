using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PGInstaller.Viewmodel
{
    partial class MainViewModel
    {
        partial void OnSelectedDepartmentChanged(string? value)
        {
            PreviewList.Clear();
            if (string.IsNullOrEmpty(value))
                return;
            var commonApps = new List<string>
            {
                "Google Chrome",
                "Mozilla Firefox",
                "WinRAR",
                "Notepad++",
                "Mozilla Thunderbird",
                "Oracle Java Runtime",
                "All VC++ Redistributables",
                "WPS Office 2020",
                "Revo Uninstaller Pro",
                "MMS (IBM PCOMM)",
                "Adobe Acrobat PRO DC"
            };

            foreach (var app in commonApps)
                PreviewList.Add(app);

            switch (value)
            {
                case "IT":
                    PreviewList.Add("Zoom");
                    PreviewList.Add("Advanced IP Scanner");
                    PreviewList.Add("PuTTY (+ Registry Settings)");
                    PreviewList.Add("Radmin Server (+ Config)");
                    PreviewList.Add("WinSCP (+ Config)");
                    PreviewList.Add("PIMS");
                    break;
                case "HRD":
                    PreviewList.Insert(0, ".NET Framework 3.5");
                    break;
                case "ICD":
                    PreviewList.Add("PIMS");
                    break;
                case "Payables":
                    PreviewList.Add("MORE SOON");
                    break;
                case "Admin":
                    PreviewList.Add("MORE SOON");
                    break;
                case "Audit":
                    PreviewList.Add("MORE SOON");
                    break;
                case "Creative":
                    PreviewList.Add("Coreldraw Graphics");
                    PreviewList.Add("Coreldraw Technical Suite");
                    PreviewList.Add("Photoshop");
                    break;
                case "Receiving":
                    PreviewList.Add("MORE SOON");
                    break;
                case "Treasury":
                    PreviewList.Add("Java Oracle");
                    break;
                case "Store Operations (Manager)":
                    PreviewList.Add("Microsoft Teams");
                    PreviewList.Add("VLC Media Player");
                    PreviewList.Add("PIMS");
                    break;
                case "Store Operations (Customer Service)":
                    PreviewList.Add("Zoom");
                    break;
                case "Store Operations (Gcash)":
                    PreviewList.Add("GitHub CLI");
                    break;
                case "Store Operations (HBC)":
                    PreviewList.Add("PIMS");
                    break;
                default:

                    break;
            }
        }
    }
}