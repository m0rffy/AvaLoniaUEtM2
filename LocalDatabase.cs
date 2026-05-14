using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UETM2;

public static class LocalDatabase
{
    internal static readonly string DbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Uetm_2_0", "config.db");
    internal static readonly string ConnectionString = $"Data Source={DbPath};Version=3;";

    static LocalDatabase()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DbPath) ?? "");

        using var conn = new SQLiteConnection(ConnectionString);
        conn.Open();
        var cmd = conn.CreateCommand();

        cmd.CommandText = @"
        CREATE TABLE IF NOT EXISTS Passwords (
            Role TEXT PRIMARY KEY,
            Password TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS Devices (
            IP TEXT PRIMARY KEY,
            Port INTEGER NOT NULL,
            InstallationPlace TEXT,
            SwitchLabel TEXT
        );
        CREATE TABLE IF NOT EXISTS ChangeLog (
            Id INTEGER PRIMARY KEY,
            Timestamp TEXT NOT NULL,
            UserRole TEXT NOT NULL,
            Description TEXT NOT NULL,
            DeviceIP TEXT,
            CurrentA REAL,
            ResourcePercent REAL,
            Channel TEXT
        );";
        cmd.ExecuteNonQuery();

        AddColumnIfMissing(conn, "ChangeLog", "DeviceIP", "TEXT");
        AddColumnIfMissing(conn, "ChangeLog", "CurrentA", "REAL");
        AddColumnIfMissing(conn, "ChangeLog", "ResourcePercent", "REAL");
        AddColumnIfMissing(conn, "ChangeLog", "Channel", "TEXT");

        cmd.CommandText = "SELECT COUNT(*) FROM Passwords";
        long cnt = (long)cmd.ExecuteScalar();
        if (cnt == 0)
        {
            cmd.CommandText = "INSERT INTO Passwords (Role, Password) VALUES ('Администратор', 'admin'), ('Пользователь', 'user')";
            cmd.ExecuteNonQuery();
        }

        if (File.Exists(DbPath))
        {
            File.SetAttributes(DbPath, File.GetAttributes(DbPath) | FileAttributes.Hidden);
        }
    }

    private static void AddColumnIfMissing(SQLiteConnection conn, string table, string column, string type)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name='{column}'";
        long count = (long)cmd.ExecuteScalar();
        if (count == 0)
        {
            cmd.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {type}";
            cmd.ExecuteNonQuery();
        }
    }

    public static List<DeviceInfo> GetAllDevices()
    {
        var list = new List<DeviceInfo>();
        using var conn = new SQLiteConnection(ConnectionString);
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT IP, Port, InstallationPlace, SwitchLabel FROM Devices";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new DeviceInfo
            {
                IP = reader.GetString(0),
                Port = reader.GetInt32(1),
                InstallationPlace = reader.IsDBNull(2) ? "" : reader.GetString(2),
                SwitchLabel = reader.IsDBNull(3) ? "" : reader.GetString(3)
            });
        }
        return list;
    }

    public static void SaveAllDevices(List<DeviceInfo> devices)
    {
        using var conn = new SQLiteConnection(ConnectionString);
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Devices";
        cmd.ExecuteNonQuery();

        if (devices.Count == 0) return;

        cmd.CommandText = "INSERT INTO Devices (IP, Port, InstallationPlace, SwitchLabel) VALUES (@ip, @p, @pl, @sl)";
        foreach (var d in devices)
        {
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@ip", d.IP);
            cmd.Parameters.AddWithValue("@p", d.Port);
            cmd.Parameters.AddWithValue("@pl", (object?)d.InstallationPlace ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@sl", (object?)d.SwitchLabel ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }
    }

    public static Dictionary<string, string> GetAllPasswords()
    {
        var dict = new Dictionary<string, string>();
        using var conn = new SQLiteConnection(ConnectionString);
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Role, Password FROM Passwords";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            dict[reader.GetString(0)] = reader.GetString(1);
        }
        return dict;
    }

    public static void SavePassword(string role, string password)
    {
        using var conn = new SQLiteConnection(ConnectionString);
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR REPLACE INTO Passwords (Role, Password) VALUES (@r, @p)";
        cmd.Parameters.AddWithValue("@r", role);
        cmd.Parameters.AddWithValue("@p", password);
        cmd.ExecuteNonQuery();
    }

    public static void AddLogEntry(string userRole, string description,
        string? deviceIP = null, float? currentA = null,
        float? resourcePercent = null, string? channel = null)
    {
        using var conn = new SQLiteConnection(ConnectionString);
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO ChangeLog 
            (Timestamp, UserRole, Description, DeviceIP, CurrentA, ResourcePercent, Channel)
            VALUES (@t, @r, @d, @ip, @ca, @rp, @ch)";
        cmd.Parameters.AddWithValue("@t", DateTime.Now.ToString("o"));
        cmd.Parameters.AddWithValue("@r", userRole);
        cmd.Parameters.AddWithValue("@d", description);
        cmd.Parameters.AddWithValue("@ip", (object?)deviceIP ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ca", (object?)currentA ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@rp", (object?)resourcePercent ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ch", (object?)channel ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public static List<ChangeLogEntry> GetLogEntries(int limit = 500)
    {
        var list = new List<ChangeLogEntry>();
        using var conn = new SQLiteConnection(ConnectionString);
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT Timestamp, UserRole, Description, DeviceIP, CurrentA, ResourcePercent, Channel
                           FROM ChangeLog ORDER BY Id DESC LIMIT @lim";
        cmd.Parameters.AddWithValue("@lim", limit);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new ChangeLogEntry
            {
                Timestamp = DateTime.Parse(reader.GetString(0)),
                UserRole = reader.GetString(1),
                Description = reader.GetString(2),
                DeviceIP = reader.IsDBNull(3) ? null : reader.GetString(3),
                CurrentA = reader.IsDBNull(4) ? null : (float?)reader.GetFloat(4),
                ResourcePercent = reader.IsDBNull(5) ? null : (float?)reader.GetFloat(5),
                Channel = reader.IsDBNull(6) ? null : reader.GetString(6)
            });
        }
        return list;
    }
}

public class ChangeLogEntry
{
    public DateTime Timestamp { get; set; }
    public string UserRole { get; set; } = "";
    public string Description { get; set; } = "";
    public string? DeviceIP { get; set; }
    public float? CurrentA { get; set; }
    public float? ResourcePercent { get; set; }
    public string? Channel { get; set; }
}
