using ModBusHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ModBusHelper.ModBusExporterLinker;

namespace UETM2;

public static class Database
{
    public static GeneralSettings_TextFormat GeneralSettings_TextFormat = new();
    public static List<ModBusProfile.journal_record> Filtered_Journal_Records = new();
    public static string CurrentRole = "";

    private static AppData _appData = new AppData();
    public static AppData AppData
    {
        get => _appData;
        private set => _appData = value;
    }

    public static List<DeviceInfo> Devices => AppData.Devices;

    static Database()
    {
        LoadAppData();
    }

    public static void LoadAppData()
    {
        AppData.Passwords = LocalDatabase.GetAllPasswords();
        AppData.Devices = LocalDatabase.GetAllDevices();
    }

    public static void SaveAppData()
    {
        LocalDatabase.SaveAllDevices(AppData.Devices);
    }
}