using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ModBusHelper;
using NModbus;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace UETM2;

public partial class UcManagement : UserControl
{
    private ConfiguratorWindow mainForm;
    private BackgroundWorker backgroundWorker;
    private DataTable rmsTable;
    private DataTable cntvTable;
    private ModBusProfile profileHelper = new ModBusProfile();
    private ModBusCommands commandsHelper = new ModBusCommands();
    private Tuple<TcpClient, IModbusMaster>? _connection;
    private string _lastIp = "";
    private bool _manualTimeSet = false;
    private float _inom1 = 1.0f;

    public ObservableCollection<RmsRecord> RmsRecords { get; } = new();
    public ObservableCollection<CntvRecord> CntvRecords { get; } = new();

    public UcManagement() { }

    public UcManagement(ConfiguratorWindow mainForm)
    {
        DataContext = this;
        InitializeComponent();
        this.mainForm = mainForm;
        InitializeDataTables();
        InitializeBackgroundWorker();

        this.mainForm.ConnectionStarted += OnConnectionStarted;
        this.mainForm.ConnectionStopped += OnConnectionStopped;

        ipTextBox.Text = _lastIp;

        var existingConnection = this.mainForm.GetCurrentConnection();
        if (existingConnection != null && existingConnection.Item1.Connected)
        {
            OnConnectionStarted(existingConnection);
        }
    }

    private void InitializeDataTables()
    {
        rmsTable = new DataTable();
        rmsTable.Columns.Add("Канал", typeof(string));
        rmsTable.Columns.Add("Значение (А)", typeof(float));

        cntvTable = new DataTable();
        cntvTable.Columns.Add("Канал", typeof(string));
        cntvTable.Columns.Add("Выработанный ресурс (%)", typeof(float));
        cntvTable.Columns.Add("Количество отключений", typeof(int));
        cntvTable.Columns.Add("Количество включений", typeof(int));
    }

    private void InitializeBackgroundWorker()
    {
        backgroundWorker = new BackgroundWorker();
        backgroundWorker.WorkerSupportsCancellation = true;
        backgroundWorker.DoWork += BackgroundWorker_DoWork;
    }

    private void OnConnectionStarted(Tuple<TcpClient, IModbusMaster> connection)
    {
        _connection = connection;
        WaitForBackgroundWorkerStop();
        if (!backgroundWorker.IsBusy)
            backgroundWorker.RunWorkerAsync(_connection);
    }

    private void OnConnectionStopped()
    {
        _manualTimeSet = false;
        if (backgroundWorker.IsBusy)
            backgroundWorker.CancelAsync();
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            RmsRecords.Clear();
            CntvRecords.Clear();
            deviceStatusLabel.Text = "";
            syncStatusLabel.Text = "";
            rtcStatusLabel.Text = "";
            deviceTimeLabel.Text = "";
            serialNumberLabel.Text = "";
            firmwareVersionLabel.Text = "";
        });
    }

    private bool IsConnectionError(Exception ex)
    {
        if (ex == null) return false;
        if (ex is AggregateException agg)
            return agg.InnerExceptions.Any(IsConnectionError);
        if (ex is SocketException || ex is IOException)
            return true;
        string msg = ex.Message.ToLower();
        return msg.Contains("socket") || msg.Contains("connection") || msg.Contains("disconnected") ||
               msg.Contains("transport") || msg.Contains("disposed") || msg.Contains("timeout") ||
               msg.Contains("broken pipe") || (ex.InnerException != null && IsConnectionError(ex.InnerException));
    }

    private void BackgroundWorker_DoWork(object? sender, DoWorkEventArgs e)
    {
        var connection = e.Argument as Tuple<TcpClient, IModbusMaster>;
        if (connection == null) return;

        TcpClient tcpClient = connection.Item1;
        IModbusMaster modbusMaster = connection.Item2;

        while (!backgroundWorker.CancellationPending)
        {
            if (tcpClient == null || !tcpClient.Connected)
            {
                Dispatcher.UIThread.InvokeAsync(() => mainForm.Disconnect());
                break;
            }

            try
            {
                var esp = profileHelper.ect_state_page_Read(modbusMaster);
                var ssp = profileHelper.swrct_state_page_Read(modbusMaster);
                var timeData = profileHelper.time_Read(modbusMaster);
                DateTime dt = PtpTimeHelper.PtpToDateTime(timeData.ptpval.ns, timeData.ptpval.slo, timeData.ptsecHi);
                var cmns = profileHelper.cmns_Read(modbusMaster);

                float inom1 = 1.0f;
                string inom1Str = Database.GeneralSettings_TextFormat.meas.primct.Inom1;
                if (!string.IsNullOrEmpty(inom1Str))
                    float.TryParse(inom1Str, NumberStyles.Any, CultureInfo.InvariantCulture, out inom1);
                _inom1 = inom1;

                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    UpdateRmsTable(esp.rms);
                    UpdateCntvTable(ssp.cntv);
                    UpdateStatus(esp.ste);
                    UpdateRtcStatus(ssp.ste.Length > 2 ? ssp.ste[2] : (byte)0);
                    if (!_manualTimeSet)
                        deviceTimeLabel.Text = dt.ToString("dd.MM.yyyy HH:mm:ss");
                    serialNumberLabel.Text = cmns.SerialNo.ToString();
                    firmwareVersionLabel.Text = cmns.FmwVer.ToString();
                });
            }
            catch (Exception ex)
            {
                if (IsConnectionError(ex))
                {
                    Dispatcher.UIThread.InvokeAsync(() => mainForm.Disconnect());
                    break;
                }
                else
                {
                    Dispatcher.UIThread.InvokeAsync(async () =>
                        await DialogHelper.ShowMessageBox("Ошибка", $"Ошибка чтения: {ex.Message}"));
                    break;
                }
            }

            Thread.Sleep(2000);
        }
    }

    private void UpdateRmsTable(float[] rms)
    {
        rmsTable.Rows.Clear();
        RmsRecords.Clear();
        string[] labels = { "A", "B", "C" };
        for (int i = 0; i < labels.Length; i++)
        {
            float value = i < rms.Length ? (float)Math.Sqrt(Math.Max(0, rms[i])) * _inom1 : 0.0f;
            rmsTable.Rows.Add(labels[i], value);
            RmsRecords.Add(new RmsRecord { Channel = labels[i], Value = value });
        }
    }

    private void UpdateCntvTable(ModBusProfile.SWCNT[] cntv)
    {
        cntvTable.Rows.Clear();
        CntvRecords.Clear();
        string[] labels = { "A", "B", "C" };
        for (int i = 0; i < labels.Length; i++)
        {
            if (i < cntv.Length)
            {
                cntvTable.Rows.Add(labels[i], cntv[i].Racc, cntv[i].ofcnt, cntv[i].oNacnt);
                CntvRecords.Add(new CntvRecord
                {
                    Channel = labels[i],
                    Resource = cntv[i].Racc,
                    OffCount = cntv[i].ofcnt,
                    OnCount = cntv[i].oNacnt
                });
            }
        }
    }

    private void UpdateStatus(byte[] ste)
    {
        deviceStatusLabel.Text = (ste.Length > 0 && ste[0] == 0) ? "В работе" : "Ошибка";
        if (ste.Length > 1)
            syncStatusLabel.Text = ste[1] switch
            {
                0 => "Не синхронизированы",
                1 => "Грубая",
                2 => "Точная",
                _ => "Неизвестно"
            };
    }

    private void UpdateRtcStatus(byte rtcSte) => rtcStatusLabel.Text = rtcSte == 0 ? "Работают" : "Ошибка";

    public void WaitForBackgroundWorkerStop()
    {
        if (backgroundWorker != null && backgroundWorker.IsBusy)
        {
            backgroundWorker.CancelAsync();
            while (backgroundWorker.IsBusy)
            {
                Thread.Sleep(20);
            }
        }
    }

    private async void AddDeviceButton_Click(object? sender, RoutedEventArgs e)
    {
        string ip = ipTextBox.Text.Trim();
        if (!IPAddress.TryParse(ip, out _))
        {
            await DialogHelper.ShowMessageBox("Ошибка", "Некорректный IP адрес.");
            return;
        }
        if (!int.TryParse(portTextBox.Text, out int port) || port < 1 || port > 65535)
        {
            await DialogHelper.ShowMessageBox("Ошибка", "Некорректный порт (1–65535).");
            return;
        }
        if (Database.Devices.Exists(d => d.IP == ip))
        {
            await DialogHelper.ShowMessageBox("Информация", "Устройство с таким IP уже есть в списке.");
            return;
        }

        _lastIp = ip;

        var newDev = new DeviceInfo
        {
            IP = ip,
            Port = port,
            InstallationPlace = "",
            SwitchLabel = ""
        };
        Database.Devices.Add(newDev);
        Database.SaveAppData();
        mainForm.RefreshDevicesList();
        portTextBox.Text = "";
    }

    private async void ClearResourceButton_Click(object? sender, RoutedEventArgs e)
    {
        if (Database.CurrentRole != "Администратор")
        {
            await DialogHelper.ShowMessageBox("Ошибка", "Требуются права администратора.");
            return;
        }
        if (!await DialogHelper.ShowMessageBox("Подтверждение", "Обнулить счётчик ресурса выключателя?", MessageBoxButtons.YesNo))
            return;

        var conn = mainForm.GetCurrentConnection();
        if (conn?.Item1?.Connected == true)
        {
            try
            {
                var response = commandsHelper.nulify_swrc(conn.Item2);
                if (response.Data[0] == 0xFF)
                    await DialogHelper.ShowMessageBox("Успешно", "Счётчик ресурса обнулён.");
                else
                    await DialogHelper.ShowMessageBox("Ошибка", "Ошибка обнуления счётчика.");
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowMessageBox("Ошибка", $"Ошибка: {ex.Message}");
            }
        }
        else
        {
            await DialogHelper.ShowMessageBox("Ошибка", "Нет подключения к устройству.");
        }
    }

    private async void SetTimeButton_Click(object? sender, RoutedEventArgs e)
    {
        if (Database.CurrentRole != "Администратор")
        {
            await DialogHelper.ShowMessageBox("Ошибка", "Требуются права администратора.");
            return;
        }

        var conn = mainForm.GetCurrentConnection();
        if (conn?.Item1?.Connected != true)
        {
            await DialogHelper.ShowMessageBox("Ошибка", "Нет подключения к устройству.");
            return;
        }

        try
        {
            var esp = profileHelper.ect_state_page_Read(conn.Item2);
            if (esp.ste.Length > 1 && esp.ste[1] != 0)
            {
                await DialogHelper.ShowMessageBox("Операция запрещена",
                    "Невозможно изменить время – устройство синхронизировано с PTP мастером.");
                return;
            }
        }
        catch (Exception ex)
        {
            await DialogHelper.ShowMessageBox("Ошибка", $"Ошибка проверки синхронизации: {ex.Message}");
            return;
        }

        var dlg = new SetTimeDialog();
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner != null && await dlg.ShowDialog<bool>(owner))
        {
            try
            {
                (uint ns, uint slo, ushort ptsecHi) = PtpTimeHelper.DateTimeToPtp(dlg.SelectedDateTime);
                ushort[] registers = new ushort[6];
                registers[0] = (ushort)((ns >> 16) & 0xFFFF);
                registers[1] = (ushort)(ns & 0xFFFF);
                registers[2] = (ushort)((slo >> 16) & 0xFFFF);
                registers[3] = (ushort)(slo & 0xFFFF);
                registers[4] = ptsecHi;
                registers[5] = 0;

                conn.Item2.WriteMultipleRegisters(0, 2816, registers);
                Thread.Sleep(200);

                var commands = new ModBusCommands();
                commands.upload_settings(conn.Item2, 0x0100);

                deviceTimeLabel.Text = dlg.SelectedDateTime.ToString("dd.MM.yyyy HH:mm:ss");
                mainForm.Disconnect();
                mainForm.RefreshDevicesList();

                await DialogHelper.ShowMessageBox("Успешно",
                    "Время установлено. Устройство перезагружается. Подождите несколько секунд и подключитесь заново.");
            }
            catch (Exception ex)
            {
                if (IsConnectionError(ex))
                {
                    deviceTimeLabel.Text = dlg.SelectedDateTime.ToString("dd.MM.yyyy HH:mm:ss");
                    mainForm.Disconnect();
                    mainForm.RefreshDevicesList();
                    await DialogHelper.ShowMessageBox("Информация", "Время установлено. Устройство перезагружается.");
                }
                else
                {
                    await DialogHelper.ShowMessageBox("Ошибка", $"Ошибка установки времени: {ex.Message}");
                }
            }
        }
    }

    private async void RebootButton_Click(object? sender, RoutedEventArgs e)
    {
        if (Database.CurrentRole != "Администратор")
        {
            await DialogHelper.ShowMessageBox("Ошибка", "Требуются права администратора.");
            return;
        }

        if (!await DialogHelper.ShowMessageBox("Подтверждение", "Перезагрузить устройство? Соединение будет разорвано.", MessageBoxButtons.YesNo))
            return;

        var conn = mainForm.GetCurrentConnection();
        if (conn?.Item1?.Connected == true)
        {
            try
            {
                var response = commandsHelper.reset_ect(conn.Item2);
                if (response.Data[0] == 0xFF)
                {
                    await DialogHelper.ShowMessageBox("Успешно", "Устройство перезагружается.");
                    mainForm.Disconnect();
                    mainForm.RefreshDevicesList();
                }
                else
                    await DialogHelper.ShowMessageBox("Ошибка", "Ошибка перезагрузки.");
            }
            catch (Exception ex)
            {
                if (IsConnectionError(ex))
                {
                    await DialogHelper.ShowMessageBox("Информация", "Устройство перезагружается.");
                    mainForm.Disconnect();
                    mainForm.RefreshDevicesList();
                }
                else
                {
                    await DialogHelper.ShowMessageBox("Ошибка", $"Ошибка: {ex.Message}");
                }
            }
        }
        else
        {
            await DialogHelper.ShowMessageBox("Ошибка", "Нет подключения к устройству.");
        }
    }

    public DataTable GetRmsDataTable() => rmsTable;
    public DataTable GetCntvDataTable() => cntvTable;
    public string GetDeviceStatusText() => deviceStatusLabel.Text;
    public string GetSyncStatusText() => syncStatusLabel.Text;
    public string GetRtcStatusText() => rtcStatusLabel.Text;
    public string GetDeviceTimeText() => deviceTimeLabel.Text;
    public string GetSerialNumberText() => serialNumberLabel.Text;
    public string GetFirmwareVersionText() => firmwareVersionLabel.Text;
}

public class RmsRecord
{
    public string Channel { get; set; } = "";
    public float Value { get; set; }
}

public class CntvRecord
{
    public string Channel { get; set; } = "";
    public float Resource { get; set; }
    public int OffCount { get; set; }
    public int OnCount { get; set; }
}