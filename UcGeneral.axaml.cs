using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using ModBusHelper;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using static ModBusHelper.ModBusExporterLinker;

namespace UETM2;

public partial class UcGeneral : UserControl
{
    private ConfiguratorWindow mainForm;
    private DataTable delayTable;
    private bool _updating;

    public ObservableCollection<DelayRecord> DelayRecords { get; } = new();

    private class BreakerPreset
    {
        public string Name { get; set; } = "";
        public string Inn { get; set; } = "";
        public string Iotc { get; set; } = "";
        public string Nn { get; set; } = "";
        public string C1 { get; set; } = "";
        public string C2 { get; set; } = "";
        public string C3 { get; set; } = "";
        public string C4 { get; set; } = "";
    }

    private Dictionary<string, BreakerPreset> breakerPresets;

    private static bool TryParseFloat(string s, out float result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.Replace(',', '.');
        return float.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
    }

    private static bool TryParseInt(string s, out int result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
    }

    public UcGeneral() { }

    public UcGeneral(ConfiguratorWindow mainForm)
    {
        DataContext = this;
        InitializeComponent();

        switchTypeComboBox.ItemsSource = new[]
        {
            "ВГТ-35", "ВГТ-110", "ВЭБ-110", "ВГТ-220", "ВГТ-330", "ВГТ-750",
            "ВГК-500", "ВГТ-1А1-220-40-3150", "ВЭБ-220-50", "ВГБ-35-12,5-С1",
            "ВГБ-35-12,5-С2", "ВГТ-500-40-3150", "ВГТ-500-50-3150", "Пользовательский"
        };

        this.mainForm = mainForm;
        _updating = false;

        InitializeDelayTable();
        ApplyRoleRestrictions();
        LoadBreakerPresets();
        SetDefaultThresholds();
        UpdateFromDatabase();

        if (switchTypeComboBox != null)
            switchTypeComboBox.SelectionChanged += SwitchTypeComboBox_SelectionChanged;

        Validation.NumericOnly(debounceOffTextBox);
        Validation.NumericOnly(debounceOnTextBox);
        Validation.NumericOnly(secondaryCurrentTextBox);
        Validation.NumericOnly(nominalCurrentTextBox);
        Validation.NumericOnly(thresholdCurrentTextBox);
        Validation.NumericOnly(c1TextBox);
        Validation.NumericOnly(c2TextBox);
        Validation.NumericOnly(c3TextBox);
        Validation.NumericOnly(c4TextBox);
        Validation.NumericOnly(warningThresholdTextBox);
        Validation.NumericOnly(alarmThresholdTextBox);
    }

    private void SetDefaultThresholds()
    {
        warningThresholdTextBox.Text = "80";
        alarmThresholdTextBox.Text = "100";
    }

    private void InitializeDelayTable()
    {
        delayTable = new DataTable();
        delayTable.Columns.Add("Канал", typeof(string));
        delayTable.Columns.Add("Задержка отключения (мс)", typeof(string));
        delayTable.Columns.Add("Задержка включения (мс)", typeof(string));
    }

    private void LoadBreakerPresets()
    {
        breakerPresets = new Dictionary<string, BreakerPreset>(StringComparer.OrdinalIgnoreCase)
        {
            ["ВГТ-35"] = new BreakerPreset { Name = "ВГТ-35", Inn = "3000", Iotc = "10", Nn = "3000", C1 = "1.8818", C2 = "0.9204", C3 = "-0.0005", C4 = "0.0004" },
            ["ВГТ-110"] = new BreakerPreset { Name = "ВГТ-110", Inn = "3000", Iotc = "10", Nn = "5000", C1 = "2.697", C2 = "2.1231", C3 = "-0.0355", C4 = "-0.0009" },
            ["ВЭБ-110"] = new BreakerPreset { Name = "ВЭБ-110", Inn = "3000", Iotc = "10", Nn = "5000", C1 = "2.697", C2 = "2.1231", C3 = "-0.0355", C4 = "-0.0009" },
            ["ВГТ-220"] = new BreakerPreset { Name = "ВГТ-220", Inn = "3000", Iotc = "10", Nn = "5000", C1 = "2.697", C2 = "2.1231", C3 = "-0.0355", C4 = "-0.0009" },
            ["ВГТ-330"] = new BreakerPreset { Name = "ВГТ-330", Inn = "3000", Iotc = "10", Nn = "3000", C1 = "2.777", C2 = "3.1702", C3 = "-0.2999", C4 = "0.0087" },
            ["ВГТ-750"] = new BreakerPreset { Name = "ВГТ-750", Inn = "3000", Iotc = "10", Nn = "3000", C1 = "2.777", C2 = "3.1702", C3 = "-0.2999", C4 = "0.0087" },
            ["ВГК-500"] = new BreakerPreset { Name = "ВГК-500", Inn = "3000", Iotc = "10", Nn = "3000", C1 = "2.777", C2 = "3.1702", C3 = "-0.2999", C4 = "0.0087" },
            ["ВГТ-1А1-220-40-3150"] = new BreakerPreset { Name = "ВГТ-1А1-220-40-3150", Inn = "3000", Iotc = "10", Nn = "5000", C1 = "2.8987", C2 = "2.2302", C3 = "-0.0165", C4 = "-0.0011" },
            ["ВЭБ-220-50"] = new BreakerPreset { Name = "ВЭБ-220-50", Inn = "3000", Iotc = "10", Nn = "3000", C1 = "2.3296", C2 = "2.9799", C3 = "-0.1924", C4 = "0.0038" },
            ["ВГБ-35-12,5-С1"] = new BreakerPreset { Name = "ВГБ-35-12,5-С1", Inn = "3000", Iotc = "10", Nn = "10000", C1 = "0.99241", C2 = "0.2519", C3 = "-0.001", C4 = "-0.00004" },
            ["ВГБ-35-12,5-С2"] = new BreakerPreset { Name = "ВГБ-35-12,5-С2", Inn = "3000", Iotc = "10", Nn = "3960", C1 = "0.63", C2 = "2", C3 = "10.1", C4 = "0" },
            ["ВГТ-500-40-3150"] = new BreakerPreset { Name = "ВГТ-500-40-3150", Inn = "3000", Iotc = "10", Nn = "5000", C1 = "3.7437", C2 = "7.9298", C3 = "-0.7593", C4 = "0.0224" },
            ["ВГТ-500-50-3150"] = new BreakerPreset { Name = "ВГТ-500-50-3150", Inn = "3000", Iotc = "10", Nn = "5000", C1 = "2.8403", C2 = "5.0202", C3 = "-0.3635", C4 = "0.0081" }
        };
    }

    private void ApplyRoleRestrictions()
    {
        bool isAdmin = Database.CurrentRole == "Администратор";
        nominalCurrentTextBox.IsReadOnly = !isAdmin;
        maxCurrentTextBox.IsReadOnly = !isAdmin;
        switchTypeComboBox.IsEnabled = isAdmin;
        switchLabelTextBox.IsReadOnly = !isAdmin;
        switchModelTextBox.IsReadOnly = !isAdmin;
        thresholdCurrentTextBox.IsReadOnly = !isAdmin;
        nominalOperationsTextBox.IsReadOnly = !isAdmin;
        installationPlaceTextBox.IsReadOnly = !isAdmin;
        c1TextBox.IsReadOnly = !isAdmin;
        c2TextBox.IsReadOnly = !isAdmin;
        c3TextBox.IsReadOnly = !isAdmin;
        c4TextBox.IsReadOnly = !isAdmin;
        primaryCurrentTextBox.IsReadOnly = !isAdmin;
        secondaryCurrentTextBox.IsReadOnly = !isAdmin;
        debounceOffTextBox.IsReadOnly = !isAdmin;
        debounceOnTextBox.IsReadOnly = !isAdmin;
        delayDataGrid.IsReadOnly = !isAdmin;
        warningThresholdTextBox.IsReadOnly = !isAdmin;
        alarmThresholdTextBox.IsReadOnly = !isAdmin;
    }

    private void SwitchTypeComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_updating) return;
        string selected = switchTypeComboBox.SelectedItem?.ToString() ?? "";
        if (selected == "Пользовательский")
        {
            SetFieldsReadOnly(false);
            return;
        }
        if (breakerPresets.TryGetValue(selected, out var preset))
        {
            bool same = nominalCurrentTextBox.Text == preset.Inn &&
                        thresholdCurrentTextBox.Text == preset.Iotc &&
                        nominalOperationsTextBox.Text == preset.Nn &&
                        c1TextBox.Text == preset.C1 &&
                        c2TextBox.Text == preset.C2 &&
                        c3TextBox.Text == preset.C3 &&
                        c4TextBox.Text == preset.C4;

            if (!same)
                ApplyBreakerPreset(preset);
            SetFieldsReadOnly(true);
        }
    }

    private void SetFieldsReadOnly(bool readOnly)
    {
        nominalCurrentTextBox.IsReadOnly = readOnly;
        maxCurrentTextBox.IsReadOnly = readOnly;
        thresholdCurrentTextBox.IsReadOnly = readOnly;
        nominalOperationsTextBox.IsReadOnly = readOnly;
        c1TextBox.IsReadOnly = readOnly;
        c2TextBox.IsReadOnly = readOnly;
        c3TextBox.IsReadOnly = readOnly;
        c4TextBox.IsReadOnly = readOnly;

        var bg = readOnly ? Brushes.Gainsboro : Brushes.White;
        nominalCurrentTextBox.Background = bg;
        maxCurrentTextBox.Background = bg;
        thresholdCurrentTextBox.Background = bg;
        nominalOperationsTextBox.Background = bg;
        c1TextBox.Background = bg;
        c2TextBox.Background = bg;
        c3TextBox.Background = bg;
        c4TextBox.Background = bg;
    }

    private void ApplyBreakerPreset(BreakerPreset preset)
    {
        nominalCurrentTextBox.Text = preset.Inn;
        thresholdCurrentTextBox.Text = preset.Iotc;
        nominalOperationsTextBox.Text = preset.Nn;
        c1TextBox.Text = preset.C1;
        c2TextBox.Text = preset.C2;
        c3TextBox.Text = preset.C3;
        c4TextBox.Text = preset.C4;
    }

    public void UpdateFromDatabase()
    {
        if (_updating) return;
        _updating = true;

        var settings = Database.GeneralSettings_TextFormat;

        nominalCurrentTextBox.Text = settings.swrcs.swnf.Inn ?? "";
        maxCurrentTextBox.Text = settings.swrcs.swnf.Imax ?? "";
        switchLabelTextBox.Text = (settings.swrcs.swnf.label ?? "").Replace("\0", "");
        switchModelTextBox.Text = (settings.swrcs.swnf.model ?? "").Replace("\0", "");
        thresholdCurrentTextBox.Text = settings.swrcs.algo.Iotc ?? "";
        nominalOperationsTextBox.Text = settings.swrcs.algo.Nn ?? "";
        c1TextBox.Text = settings.swrcs.algo.C1 ?? "";
        c2TextBox.Text = settings.swrcs.algo.C2 ?? "";
        c3TextBox.Text = settings.swrcs.algo.C3 ?? "";
        c4TextBox.Text = settings.swrcs.algo.C4 ?? "";
        installationPlaceTextBox.Text = settings.cmns.MntPlce ?? "";
        primaryCurrentTextBox.Text = settings.meas.primct.Inom1 ?? "";

        if (int.TryParse(settings.meas.primct.Inom2, out int secMA))
            secondaryCurrentTextBox.Text = (secMA / 1000.0).ToString(CultureInfo.InvariantCulture);
        else secondaryCurrentTextBox.Text = settings.meas.primct.Inom2 ?? "";

        if (short.TryParse(settings.swrcs.contacts.ajtr.offd, out short offdTenths))
            debounceOffTextBox.Text = (offdTenths / 10.0).ToString(CultureInfo.InvariantCulture);
        else debounceOffTextBox.Text = settings.swrcs.contacts.ajtr.offd ?? "";

        if (short.TryParse(settings.swrcs.contacts.ajtr.ond, out short ondTenths))
            debounceOnTextBox.Text = (ondTenths / 10.0).ToString(CultureInfo.InvariantCulture);
        else debounceOnTextBox.Text = settings.swrcs.contacts.ajtr.ond ?? "";

        FillDelayRecords();

        string detected = DetectBreakerType();
        switchTypeComboBox.SelectedItem = detected;
        SetFieldsReadOnly(detected != "Пользовательский");
        _updating = false;
    }

    private void FillDelayRecords()
    {
        DelayRecords.Clear();
        var settings = Database.GeneralSettings_TextFormat;
        var cdly = settings.swrcs.contacts.cdly ?? new SCDLY_cdly_TextFormat[0];
        string[] uiChannels = { "A", "B", "C" };
        for (int i = 0; i < uiChannels.Length; i++)
        {
            string offDelay = i < cdly.Length ? (cdly[i].offd / 10.0).ToString(CultureInfo.InvariantCulture) : "0";
            string onDelay = i < cdly.Length ? (cdly[i].ond / 10.0).ToString(CultureInfo.InvariantCulture) : "0";
            DelayRecords.Add(new DelayRecord
            {
                Channel = uiChannels[i],
                OffDelay = offDelay,
                OnDelay = onDelay
            });
        }
    }

    private string DetectBreakerType()
    {
        string inn = nominalCurrentTextBox.Text;
        string iotc = thresholdCurrentTextBox.Text;
        string nn = nominalOperationsTextBox.Text;
        string c1 = c1TextBox.Text;
        string c2 = c2TextBox.Text;
        string c3 = c3TextBox.Text;
        string c4 = c4TextBox.Text;

        foreach (var kvp in breakerPresets)
        {
            var p = kvp.Value;
            if (p.Inn == inn && p.Iotc == iotc && p.Nn == nn && p.C1 == c1 && p.C2 == c2 && p.C3 == c3 && p.C4 == c4)
                return kvp.Key;
        }
        return "Пользовательский";
    }

    public bool SaveToDatabase()
    {
        if (_updating) return false;
        _updating = true;

        if (Database.CurrentRole != "Администратор")
        {
            _updating = false;
            return false;
        }

        // Пороги сигнализации
        if (!int.TryParse(warningThresholdTextBox.Text, out int warningThreshold) || warningThreshold < 0 || warningThreshold > 80)
        {
            Dispatcher.UIThread.InvokeAsync(async () =>
                await DialogHelper.ShowMessageBox("Ошибка", "Порог предупредительной сигнализации должен быть целым числом от 0 до 80."));
            UpdateFromDatabase();
            _updating = false;
            return false;
        }
        if (!int.TryParse(alarmThresholdTextBox.Text, out int alarmThreshold) || alarmThreshold < 80 || alarmThreshold > 100)
        {
            Dispatcher.UIThread.InvokeAsync(async () =>
                await DialogHelper.ShowMessageBox("Ошибка", "Порог аварийной сигнализации должен быть целым числом от 80 до 100."));
            UpdateFromDatabase();
            _updating = false;
            return false;
        }
        if (alarmThreshold <= warningThreshold)
        {
            Dispatcher.UIThread.InvokeAsync(async () =>
                await DialogHelper.ShowMessageBox("Ошибка", "Порог аварийной сигнализации должен быть больше порога предупредительной."));
            UpdateFromDatabase();
            _updating = false;
            return false;
        }

        // Проверка длины строк
        if (!ModBusFunctions.ValidateStringLength(switchLabelTextBox.Text, 10, out _))
        {
            Dispatcher.UIThread.InvokeAsync(async () =>
                await DialogHelper.ShowMessageBox("Ошибка", "Обозначение выключателя слишком длинное. Максимум 9 байт в UTF-8."));
            UpdateFromDatabase();
            _updating = false;
            return false;
        }
        if (!ModBusFunctions.ValidateStringLength(switchModelTextBox.Text, 32, out _))
        {
            Dispatcher.UIThread.InvokeAsync(async () =>
                await DialogHelper.ShowMessageBox("Ошибка", "Марка выключателя слишком длинная. Максимум 31 байт в UTF-8."));
            UpdateFromDatabase();
            _updating = false;
            return false;
        }
        if (!ModBusFunctions.ValidateStringLength(installationPlaceTextBox.Text, 32, out _))
        {
            Dispatcher.UIThread.InvokeAsync(async () =>
                await DialogHelper.ShowMessageBox("Ошибка", "Место установки слишком длинное. Максимум 31 байт в UTF-8."));
            UpdateFromDatabase();
            _updating = false;
            return false;
        }

        var newSettings = Database.GeneralSettings_TextFormat;

        if (!TryParseInt(nominalCurrentTextBox.Text, out int innA) || innA < 0 || innA > 65000)
        {
            Dispatcher.UIThread.InvokeAsync(async () =>
                await DialogHelper.ShowMessageBox("Ошибка", "Номинальный ток должен быть целым числом от 0 до 65000 А."));
            UpdateFromDatabase();
            _updating = false;
            return false;
        }
        newSettings.swrcs.swnf.Inn = innA.ToString();

        if (!int.TryParse(maxCurrentTextBox.Text, out int imaxKA) || imaxKA < 0 || imaxKA > 100)
        {
            Dispatcher.UIThread.InvokeAsync(async () =>
                await DialogHelper.ShowMessageBox("Ошибка", "Номинальный ток отключения должен быть целым числом от 0 до 100 кА."));
            UpdateFromDatabase();
            _updating = false;
            return false;
        }
        newSettings.swrcs.swnf.Imax = imaxKA.ToString();

        newSettings.swrcs.swnf.label = switchLabelTextBox.Text ?? "";
        newSettings.swrcs.swnf.model = switchModelTextBox.Text ?? "";

        if (!TryParseFloat(thresholdCurrentTextBox.Text, out float iotcF) || iotcF < 0 || iotcF > 65000)
        {
            Dispatcher.UIThread.InvokeAsync(async () =>
                await DialogHelper.ShowMessageBox("Ошибка", "Ток порога должен быть числом от 0 до 65000 А."));
            UpdateFromDatabase();
            _updating = false;
            return false;
        }
        newSettings.swrcs.algo.Iotc = iotcF.ToString(CultureInfo.InvariantCulture);

        if (!int.TryParse(nominalOperationsTextBox.Text, out int nn) || nn < 0 || nn > 65000)
        {
            Dispatcher.UIThread.InvokeAsync(async () =>
                await DialogHelper.ShowMessageBox("Ошибка", "Номинальное количество отключений должно быть целым числом от 0 до 65000."));
            UpdateFromDatabase();
            _updating = false;
            return false;
        }
        newSettings.swrcs.algo.Nn = nn.ToString();

        if (!TryParseFloat(c1TextBox.Text, out float c1Val) ||
            !TryParseFloat(c2TextBox.Text, out float c2Val) ||
            !TryParseFloat(c3TextBox.Text, out float c3Val) ||
            !TryParseFloat(c4TextBox.Text, out float c4Val))
        {
            Dispatcher.UIThread.InvokeAsync(async () =>
                await DialogHelper.ShowMessageBox("Ошибка", "Введите корректные числа для коэффициентов C1-C4."));
            UpdateFromDatabase();
            _updating = false;
            return false;
        }
        newSettings.swrcs.algo.C1 = c1Val.ToString(CultureInfo.InvariantCulture);
        newSettings.swrcs.algo.C2 = c2Val.ToString(CultureInfo.InvariantCulture);
        newSettings.swrcs.algo.C3 = c3Val.ToString(CultureInfo.InvariantCulture);
        newSettings.swrcs.algo.C4 = c4Val.ToString(CultureInfo.InvariantCulture);

        newSettings.cmns.MntPlce = installationPlaceTextBox.Text ?? "";

        if (!TryParseInt(primaryCurrentTextBox.Text, out int inom1A) || inom1A < 0 || inom1A > 65000)
        {
            Dispatcher.UIThread.InvokeAsync(async () =>
                await DialogHelper.ShowMessageBox("Ошибка", "Номинальный первичный ток должен быть целым числом от 0 до 65000 А."));
            UpdateFromDatabase();
            _updating = false;
            return false;
        }
        newSettings.meas.primct.Inom1 = inom1A.ToString();

        if (!TryParseFloat(secondaryCurrentTextBox.Text, out float secA) || secA < 0 || secA > 10)
        {
            Dispatcher.UIThread.InvokeAsync(async () =>
                await DialogHelper.ShowMessageBox("Ошибка", "Номинальный вторичный ток должен быть числом от 0 до 10 А."));
            UpdateFromDatabase();
            _updating = false;
            return false;
        }
        int secMA = (int)(secA * 1000);
        newSettings.meas.primct.Inom2 = secMA.ToString();

        if (!TryParseFloat(debounceOffTextBox.Text, out float offdMs) ||
            !TryParseFloat(debounceOnTextBox.Text, out float ondMs) ||
            offdMs * 10 > short.MaxValue || ondMs * 10 > short.MaxValue)
        {
            Dispatcher.UIThread.InvokeAsync(async () =>
                await DialogHelper.ShowMessageBox("Ошибка", "Введите корректные числа для задержек дребезга."));
            UpdateFromDatabase();
            _updating = false;
            return false;
        }
        newSettings.swrcs.contacts.ajtr.offd = ((int)(offdMs * 10)).ToString(CultureInfo.InvariantCulture);
        newSettings.swrcs.contacts.ajtr.ond = ((int)(ondMs * 10)).ToString(CultureInfo.InvariantCulture);

        // Сохраняем данные из таблицы задержек (из коллекции DelayRecords, синхронизированной с панелью)
        string[] uiChannels = { "A", "B", "C" };
        for (int i = 0; i < DelayRecords.Count && i < uiChannels.Length; i++)
        {
            var rec = DelayRecords[i];
            if (!TryParseFloat(rec.OffDelay, out float offdVal) ||
                !TryParseFloat(rec.OnDelay, out float ondVal) ||
                offdVal * 10 > short.MaxValue || ondVal * 10 > short.MaxValue)
            {
                Dispatcher.UIThread.InvokeAsync(async () =>
                    await DialogHelper.ShowMessageBox("Ошибка", $"Введите корректные числа для задержек канала {rec.Channel}."));
                UpdateFromDatabase();
                _updating = false;
                return false;
            }
            if (newSettings.swrcs.contacts.cdly.Length <= i)
                Array.Resize(ref newSettings.swrcs.contacts.cdly, i + 1);
            newSettings.swrcs.contacts.cdly[i].offd = (short)(offdVal * 10);
            newSettings.swrcs.contacts.cdly[i].ond = (short)(ondVal * 10);
        }

        Database.GeneralSettings_TextFormat = newSettings; // newSettings формируется в процессе
        FillDelayRecords();
        _updating = false;
        return true;
    }

    public Dictionary<string, string> GetGeneralSettingsDictionary()
    {
        return new Dictionary<string, string>
        {
            ["Номинальный ток (А)"] = nominalCurrentTextBox.Text ?? "",
            ["Максимальный ток отключения (кА)"] = maxCurrentTextBox.Text ?? "",
            ["Тип выключателя"] = switchTypeComboBox.Text ?? "",
            ["Обозначение выключателя"] = switchLabelTextBox.Text ?? "",
            ["Марка выключателя"] = switchModelTextBox.Text ?? "",
            ["Ток порога (А)"] = thresholdCurrentTextBox.Text ?? "",
            ["Номинальное количество отключений"] = nominalOperationsTextBox.Text ?? "",
            ["C1"] = c1TextBox.Text ?? "",
            ["C2"] = c2TextBox.Text ?? "",
            ["C3"] = c3TextBox.Text ?? "",
            ["C4"] = c4TextBox.Text ?? "",
            ["Место установки"] = installationPlaceTextBox.Text ?? "",
            ["Первичный ток (А)"] = primaryCurrentTextBox.Text ?? "",
            ["Вторичный ток (А)"] = secondaryCurrentTextBox.Text ?? "",
            ["Задержка дребезга (отключение) (мс)"] = debounceOffTextBox.Text ?? "",
            ["Задержка дребезга (включение) (мс)"] = debounceOnTextBox.Text ?? "",
            ["Порог предупредительной сигнализации (%)"] = warningThresholdTextBox.Text ?? "",
            ["Порог аварийной сигнализации (%)"] = alarmThresholdTextBox.Text ?? ""
        };
    }

    public DataTable GetDelayTable() => delayTable;
}

public class DelayRecord
{
    public string Channel { get; set; } = "";
    public string OffDelay { get; set; } = "";
    public string OnDelay { get; set; } = "";
}