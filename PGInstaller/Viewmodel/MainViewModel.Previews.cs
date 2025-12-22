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
                "7-Zip",
                "Notepad++",
                "Mozilla Thunderbird",
                "Oracle Java Runtime",
                "All VC++ Redistributables",
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
                    break;
                case "HRD":
                    PreviewList.Insert(0, ".NET Framework 3.5");
                    break;
                case "Store Operations (Manager)":
                    PreviewList.Add("Microsoft Teams");
                    break;
                case "Store Operations (Customer Service)":
                    PreviewList.Add("Zoom");
                    break;
                case "Store Operations (Gcash)":
                    PreviewList.Add("GitHub CLI");
                    break;
                case "Store Operations (HBC)":
                    PreviewList.Add("Slack");
                    break;
                default:

                    break;
            }
        }
    }
}
