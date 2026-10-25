namespace PGInstaller.Services;
internal static class PackageAssets
{
    public static IReadOnlyDictionary<string, string[][]> Required { get; } = new Dictionary<string, string[][]>(StringComparer.OrdinalIgnoreCase)
    {
        ["Google Chrome"] = [["chrome.exe"]],
        ["Mozilla Firefox"] = [["Firefox.exe"]],
        ["Microsoft Edge"] = [["edge.msi"]],
        ["WinRAR"] = [["winrar.exe"]],
        ["Notepad++"] = [["npp.exe"]],
        ["Mozilla Thunderbird"] = [["Thunderbird.exe"]],
        ["Oracle Java Runtime"] = [["jre.exe", "java.exe"]],
        ["All VC++ Redistributables"] = [["vcredistAIO.zip"]],
        ["WPS Office 2020"] = [["WPS.zip"]],
        ["Revo Uninstaller Pro"] = [["revo.exe"]],
        ["Adobe Acrobat PRO DC"] = [["acrobat.zip"]],
        ["Sticky Notes"] = [["sticky.exe"]],
        ["IObit Driver Booster"] = [["drv.exe"]],
        ["Radmin Server"] = [["radmins.msi"], ["install.bat"], ["newtstop.dll"], ["nts64helper.dll"]],
        ["Zoom"] = [["ZoomInstaller.exe"]],
        ["Advanced IP Scanner"] = [["ipscanner.exe"]],
        ["PITK"] = [["PITK Setup.exe", "pitk.exe"]],
        ["A&VGW"] = [["A&VGWSetup.exe"]],
        ["PuTTY"] = [["putty.msi"]],
        ["WinSCP"] = [["WinSCP.exe"]],
        ["Radmin Viewer"] = [["radminv.msi"]],
        ["PIMS"] = [["pims.zip"], ["netfx.zip"]],
        ["MMS (PCOMM)"] = [["MMS.zip"], ["MMS.ws"], ["AS400.KMP"]],
        ["Chrome Bookmarks (CBM)"] = [],
        [".NET Framework 3.5"] = [["netfx.zip"]],
        ["FSDM"] = [["FSDM.zip"]],
        ["Wamp 1.7.2"] = [["wamp5.exe"]],
        ["Wamp 2"] = [["wamp2.exe"]],
        ["Wamp 2.5"] = [["wamp2.5.exe"]],
        ["Wampserver 3.4.0"] = [["wampserver.exe"], ["php_sqlsrv_83_ts_x64.dll"], ["php_pdo_sqlsrv_83_ts_x64.dll"]],
        ["Bartender 10.1"] = [["bt10.1.exe"], ["bt.zip"], ["bartend.exe"]],
        ["Bartender 2016"] = [["bt2016.exe"], ["bt.zip"]],
        ["Bartender 2022"] = [["bt2022.exe"], ["bt.zip"]],
        ["Argox Driver"] = [["argox_drvr.exe"]],
        ["Zebra Driver"] = [["zebra_drvr.exe"]],
        ["Inventory Tools"] = [["InventoryTools.zip"]],
        ["Variance"] = [["Variance.zip"]],
        ["Coreldraw Graphics X5"] = [["crdx5.exe"]],
        ["Coreldraw Graphics X7"] = [["crdx7.exe"]],
        ["Photoshop CS6"] = [["pscs6.zip"]],
        ["Illustrator CS6"] = [["illucs6.zip"]],
        ["VLC Media Player"] = [["vlc.exe"]]
    };

    public static IEnumerable<string[]> For(string app, string? department)
    {
        foreach (var group in Required[app])
            yield return group;
        if (app == "Wamp 2" && department == "HRD")
            yield return["wampHR.zip"];
    }
}
