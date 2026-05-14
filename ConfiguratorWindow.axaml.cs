// ConfiguratorWindow.axaml.cs
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ModBusHelper;
using NModbus;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using static ModBusHelper.ModBusExporterLinker;

namespace UETM2;

public partial class ConfiguratorWindow : Window
{
    private ModBusExporterLinker ExporterLinkerHelper = new ModBusExporterLinker();
    public static ConfiguratorWindow? CurrentInstance;
    private string userRole;
    private bool settingsExpanded = false;

    private Tuple<TcpClient, IModbusMaster>? _connection;
    private string _ip = "";
    private int _port;

    private DispatcherTimer connectionTimeoutTimer;

    public event Action<Tuple<TcpClient, IModbusMaster>>? ConnectionStarted;
    public event Action? ConnectionStopped;
    public event Action? SettingsRead;

    public UcManagement ucManagement;
    public UcGeneral ucGeneral;
    public UcNetwork ucNetwork;
    public UcJournal ucJournal;

    public ConfiguratorWindow() { }

    public ConfiguratorWindow(string role)
    {
        userRole = role;
        InitializeComponent();
        Database.GeneralSettings_TextFormat = ExporterLinkerHelper.GeneralSettings_Text_Default;
        Database.Filtered_Journal_Records = ExporterLinkerHelper.journal_record_Default;
        CurrentInstance = this;

        RefreshDevicesList();

        ConnectionStarted += OnConnectionStarted;
        ConnectionStopped += OnConnectionStopped;
        SettingsRead += OnSettingsRead;
        this.Closing += ConfiguratorWindow_Closing;

        ucManagement = new UcManagement(this);
        ucGeneral = new UcGeneral(this);
        ucNetwork = new UcNetwork(this);
        ucJournal = new UcJournal(this);

        ApplyRoleRestrictions();
        ShowControl(ucManagement);

        connectionTimeoutTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(3000) };
        connectionTimeoutTimer.Tick += ConnectionTimeoutTimer_Tick;
    }

    private void ApplyRoleRestrictions()
    {
        bool isAdmin = Database.CurrentRole == "Администратор";
        btnWrite.IsEnabled = isAdmin;
        btnChangePassword.IsEnabled = isAdmin;
    }

    public void SetConnectionParams(string ip, int port)
    {
        _ip = ip;
        _port = port;
    }

    public Tuple<TcpClient, IModbusMaster>? GetCurrentConnection() => _connection;

    public async void Connect()
    {
        if (string.IsNullOrEmpty(_ip) || !IPAddress.TryParse(_ip, out _))
        {
            await DialogHelper.ShowMessageBox("Ошибка", "IP-адрес не задан или некорректен.");
            return;
        }
        if (_port < 1 || _port > 65535)
        {
            await DialogHelper.ShowMessageBox("Ошибка", "Некорректный порт.");
            return;
        }

        connectionTimeoutTimer.Start();
        try
        {
            _connection = await Task.Run(() => ConnectionManager.OpenConnection(_ip, _port));
            connectionTimeoutTimer.Stop();
            ReadAllSettings();
            ConnectionStarted?.Invoke(_connection);
        }
        catch (TimeoutException ex)
        {
            connectionTimeoutTimer.Stop();
            await DialogHelper.ShowMessageBox("Ошибка подключения", ex.Message);
            Disconnect();
        }
        catch (Exception ex)
        {
            connectionTimeoutTimer.Stop();
            await DialogHelper.ShowMessageBox("Ошибка", $"Ошибка подключения: {ex.Message}");
            Disconnect();
        }
    }

    public void Disconnect()
    {
        try
        {
            if (_connection != null)
            {
                ConnectionManager.CloseConnection();
                _connection = null;
            }
        }
        catch { }
        finally
        {
            ConnectionStopped?.Invoke();
        }
    }

    private void ConnectionTimeoutTimer_Tick(object? sender, EventArgs e)
    {
        connectionTimeoutTimer.Stop();
        if (_connection == null || !_connection.Item1.Connected)
        {
            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await DialogHelper.ShowMessageBox("Ошибка подключения", "Не удалось подключиться к устройству.");
                Disconnect();
            });
        }
    }

    private void ReadAllSettings()
    {
        try
        {
            Database.GeneralSettings_TextFormat = ExporterLinkerHelper.get_GeneralSettings_Text(_connection!);
            SettingsRead?.Invoke();
            ucGeneral.UpdateFromDatabase();
            ucNetwork.UpdateFromDatabase();
            if (ChildFormPanel.Content is UcGeneral general)
                general.UpdateFromDatabase();
            else if (ChildFormPanel.Content is UcNetwork network)
                network.UpdateFromDatabase();
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.InvokeAsync(async () =>
                await DialogHelper.ShowMessageBox("Ошибка", $"Ошибка чтения настроек: {ex.Message}"));
        }
    }

    private void OnConnectionStarted(Tuple<TcpClient, IModbusMaster> connection)
    {
        Dispatcher.UIThread.InvokeAsync(() => UpdateAllCards());
    }

    private void OnConnectionStopped()
    {
        Dispatcher.UIThread.InvokeAsync(() => UpdateAllCards());
    }

    private void OnSettingsRead()
    {
        if (_connection?.Item1?.Connected == true)
        {
            var remoteEndPoint = _connection.Item1.Client.RemoteEndPoint as IPEndPoint;
            if (remoteEndPoint != null)
            {
                string activeIP = remoteEndPoint.Address.ToString();
                if (activeIP.StartsWith("::ffff:")) activeIP = activeIP.Substring(7);
                DeviceInfo? activeDev = Database.Devices.Find(d => d.IP == activeIP);
                if (activeDev != null)
                {
                    activeDev.InstallationPlace = Database.GeneralSettings_TextFormat.cmns.MntPlce ?? "";
                    activeDev.SwitchLabel = Database.GeneralSettings_TextFormat.swrcs.swnf.label ?? "";
                    Database.SaveAppData();
                    Dispatcher.UIThread.InvokeAsync(() => UpdateDeviceCard(activeDev));
                }
            }
        }
    }

    public void RefreshDevicesList()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.InvokeAsync(RefreshDevicesList);
            return;
        }
        devicesPanel.Children.Clear();
        foreach (var dev in Database.Devices)
        {
            var card = CreateDeviceCard(dev);
            devicesPanel.Children.Add(card);
        }
    }

    private DeviceCard CreateDeviceCard(DeviceInfo dev)
    {
        var card = new DeviceCard();
        bool isActive = IsDeviceActive(dev);
        card.SetData(dev.IP, dev.InstallationPlace, dev.SwitchLabel, isActive);
        card.Tag = dev;
        card.DeleteClicked += (s, e) => BtnDelete_Click(dev);
        card.ConnectClicked += (s, e) => BtnConnect_Click(dev);
        return card;
    }

    private bool IsDeviceActive(DeviceInfo dev)
    {
        if (_connection?.Item1?.Connected == true)
        {
            var remoteEndPoint = _connection.Item1.Client.RemoteEndPoint as IPEndPoint;
            if (remoteEndPoint != null)
            {
                string remoteIP = remoteEndPoint.Address.ToString();
                if (remoteIP.StartsWith("::ffff:")) remoteIP = remoteIP.Substring(7);
                return remoteIP == dev.IP;
            }
        }
        return false;
    }

    private void UpdateAllCards()
    {
        string? activeIP = null;
        if (_connection?.Item1?.Connected == true)
        {
            var remoteEndPoint = _connection.Item1.Client.RemoteEndPoint as IPEndPoint;
            if (remoteEndPoint != null)
            {
                activeIP = remoteEndPoint.Address.ToString();
                if (activeIP.StartsWith("::ffff:")) activeIP = activeIP.Substring(7);
            }
        }
        foreach (var child in devicesPanel.Children)
        {
            if (child is DeviceCard card && card.Tag is DeviceInfo dev)
            {
                bool isActive = (dev.IP == activeIP);
                card.SetData(dev.IP, dev.InstallationPlace, dev.SwitchLabel, isActive);
            }
        }
    }

    private void UpdateDeviceCard(DeviceInfo dev)
    {
        foreach (var child in devicesPanel.Children)
        {
            if (child is DeviceCard card && card.Tag is DeviceInfo d && d.IP == dev.IP)
            {
                bool isActive = IsDeviceActive(dev);
                card.SetData(dev.IP, dev.InstallationPlace, dev.SwitchLabel, isActive);
                break;
            }
        }
    }

    private async void BtnDelete_Click(DeviceInfo dev)
    {
        if (dev == null) return;
        if (await DialogHelper.ShowMessageBox("Подтверждение", $"Удалить устройство {dev.IP}?", MessageBoxButtons.YesNo))
        {
            if (IsDeviceActive(dev)) Disconnect();
            Database.Devices.Remove(dev);
            Database.SaveAppData();
            RefreshDevicesList();
        }
    }

    private async void BtnConnect_Click(DeviceInfo dev)
    {
        if (IsDeviceActive(dev)) { Disconnect(); return; }
        if (_connection?.Item1?.Connected == true) Disconnect();
        SetConnectionParams(dev.IP, dev.Port);
        Connect();
    }

    private void ShowControl(UserControl control)
    {
        ChildFormPanel.Content = control;
    }

    private void BtnManagement_Click(object? sender, RoutedEventArgs e) => ShowControl(ucManagement);
    private void BtnSettings_Click(object? sender, RoutedEventArgs e)
    {
        settingsExpanded = !settingsExpanded;
        btnGeneral.IsVisible = settingsExpanded;
        btnNetwork.IsVisible = settingsExpanded;
        btnSettings.Content = settingsExpanded ? "Настройки -" : "Настройки +";
    }
    private void BtnGeneral_Click(object? sender, RoutedEventArgs e) => ShowControl(ucGeneral);
    private void BtnNetwork_Click(object? sender, RoutedEventArgs e) => ShowControl(ucNetwork);
    private void BtnJournal_Click(object? sender, RoutedEventArgs e) => ShowControl(ucJournal);

    private async void helpMenu_Click(object? sender, RoutedEventArgs e)
    {
        string info = "АО «Уралэлектротяжмаш»\nАдрес: ул. Фронтовых бригад, 22, Екатеринбург\nВерсия: 2.0";
        await DialogHelper.ShowMessageBox("О предприятии", info);
    }

    private async void UserHelp_Click(object? sender, RoutedEventArgs e)
    {
        string info =
            "Порядок работы:\n" +
            "1. Управление - ввод IP и порта (502) - Добавить.\n" +
            "2. Подключиться – загрузка настроек с устройства.\n" +
            "3. Записать – отправка настроек.\n" +
            "\nОграничения:\n" +
            "- Токи: целые числа в пределах, указанных в ошибках.\n" +
            "- Коэффициенты C1-C4: ввод с точкой (дробная часть).\n" +
            "- Установка времени недоступна при PTP-синхронизации.\n" +
            "- При изменении данных устройство перезагружается, соединение рвётся.\n" +
            "\nЖурнал:\n" +
            "- Обновить – чтение с устройства, Экспорт в EXCEL.\n" +
            "\nКоманды для администратора:\n" +
            "- Очистить ресурс, Установить время, Перезагрузить, Изменить пароль\n" +
            "\nПрочее:\n" +
            "- Таймаут подключения, перезагрузки несколько секунд сек.\n" +
            "- Список устройств и настройки хранятся в файле config.db.";
        await DialogHelper.ShowMessageBox("Руководство", info);
    }

    private async void BtnWrite_Click(object sender, RoutedEventArgs e)
    {
        if (_connection?.Item1?.Connected != true)
        {
            await DialogHelper.ShowMessageBox("Ошибка", "Нет подключения к устройству.");
            return;
        }

        if (!await DialogHelper.ShowMessageBox("Подтверждение",
            "Вы уверены, что хотите записать настройки в устройство? После записи устройство перезагрузится, соединение будет разорвано.",
            MessageBoxButtons.YesNo))
            return;

        DeviceInfo? activeDev = null;
        string oldIp = _ip;
        if (_connection?.Item1?.Connected == true)
        {
            var remoteEndPoint = _connection.Item1.Client.RemoteEndPoint as IPEndPoint;
            if (remoteEndPoint != null)
            {
                string remoteIP = remoteEndPoint.Address.ToString();
                if (remoteIP.StartsWith("::ffff:")) remoteIP = remoteIP.Substring(7);
                activeDev = Database.Devices.Find(d => d.IP == remoteIP);
            }
        }

        if (ChildFormPanel.Content is UcGeneral general)
        {
            if (!general.SaveToDatabase()) return;
        }
        else if (ChildFormPanel.Content is UcNetwork network)
        {
            if (!network.SaveToDatabase()) return;
        }

        btnWrite.IsEnabled = false;
        var connection = _connection;

        float? capturedCurrentA = null;
        float? capturedResource = null;
        string? capturedPhase = null;
        string capturedDeviceIP = _ip;

        try
        {
            var rmsTable = ucManagement.GetRmsDataTable();
            var cntvTable = ucManagement.GetCntvDataTable();
            if (rmsTable.Rows.Count > 0)
            {
                capturedCurrentA = Convert.ToSingle(rmsTable.Rows[0][1]);
                capturedPhase = rmsTable.Rows[0][0].ToString();
            }
            if (cntvTable.Rows.Count > 0)
                capturedResource = Convert.ToSingle(cntvTable.Rows[0][1]);
        }
        catch { }

        try
        {
            await Task.Run(() =>
                ExporterLinkerHelper.WriteSettings(Database.GeneralSettings_TextFormat, true, connection));
        }
        catch (Exception ex)
        {
            Disconnect();
            btnWrite.IsEnabled = true;
            if (!IsConnectionError(ex))
                await DialogHelper.ShowMessageBox("Ошибка", $"Ошибка записи: {ex.Message}");
            RefreshDevicesList();
            return;
        }
        finally
        {
            Disconnect();
            await Task.Delay(300);
            btnWrite.IsEnabled = true;
        }

        string? newIp = null;
        if (Database.GeneralSettings_TextFormat.nets.ips.ipAddr != null &&
            Database.GeneralSettings_TextFormat.nets.ips.ipAddr.Length >= 4)
        {
            newIp = $"{Database.GeneralSettings_TextFormat.nets.ips.ipAddr[0]}." +
                    $"{Database.GeneralSettings_TextFormat.nets.ips.ipAddr[1]}." +
                    $"{Database.GeneralSettings_TextFormat.nets.ips.ipAddr[2]}." +
                    $"{Database.GeneralSettings_TextFormat.nets.ips.ipAddr[3]}";
        }

        if (activeDev != null && !string.IsNullOrEmpty(newIp) && oldIp != newIp)
        {
            DeviceInfo? existing = Database.Devices.Find(d => d.IP == newIp && d != activeDev);
            if (existing != null) Database.Devices.Remove(existing);
            activeDev.IP = newIp;
            activeDev.InstallationPlace = Database.GeneralSettings_TextFormat.cmns.MntPlce ?? "";
            activeDev.SwitchLabel = Database.GeneralSettings_TextFormat.swrcs.swnf.label ?? "";
            Database.SaveAppData();
        }

        LocalDatabase.AddLogEntry(
            Database.CurrentRole,
            "Настройки записаны в устройство",
            deviceIP: capturedDeviceIP,
            currentA: capturedCurrentA,
            resourcePercent: capturedResource,
            channel: capturedPhase
        );

        if (ExporterLinkerHelper.WasRebootCommandSent)
        {
            await DialogHelper.ShowMessageBox("Успешно",
                "Настройки успешно записаны. Устройство перезагружается. Подождите несколько секунд и подключитесь заново.");
        }
        else
        {
            await DialogHelper.ShowMessageBox("Предупреждение",
                "Настройки записаны в буфер, но команда перезагрузки не была подтверждена. Возможно, потребуется перезагрузить устройство вручную.");
        }

        RefreshDevicesList();
    }

    private async void btnChangePassword_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new ChangePasswordDialog();
        if (await dlg.ShowDialog<bool>(this))
        {
            if (!Database.AppData.Passwords.TryGetValue("Администратор", out string? adminPass) ||
                adminPass != dlg.CurrentPassword)
            {
                await DialogHelper.ShowMessageBox("Ошибка", "Неверный текущий пароль администратора.");
                return;
            }
            string role = dlg.SelectedRole;
            LocalDatabase.SavePassword(role, dlg.NewPassword);
            Database.AppData.Passwords[role] = dlg.NewPassword;
            await DialogHelper.ShowMessageBox("Успех", $"Пароль для роли «{role}» изменён.");
        }
    }

    private bool IsConnectionError(Exception ex)
    {
        string msg = ex.Message.ToLower();
        return msg.Contains("socket") || msg.Contains("connection") || msg.Contains("disconnected") ||
               msg.Contains("transport") || (ex.InnerException != null && IsConnectionError(ex.InnerException));
    }

    private void ConfiguratorWindow_Closing(object? sender, CancelEventArgs e)
    {
        Disconnect();
        Database.SaveAppData();
    }
}