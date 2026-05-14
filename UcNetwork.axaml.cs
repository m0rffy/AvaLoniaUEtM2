using Avalonia.Controls;
using Avalonia.Input;
using ModBusHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using static ModBusHelper.ModBusExporterLinker;

namespace UETM2;

public partial class UcNetwork : UserControl
{
    private ConfiguratorWindow mainForm;
    private GeneralSettings_TextFormat settings;
    private bool _updating;

    public UcNetwork() { }

    public UcNetwork(ConfiguratorWindow mainForm)
    {
        InitializeComponent();
        this.mainForm = mainForm;
        _updating = false;
        settings = Database.GeneralSettings_TextFormat;

        SetupInputValidation();
        ApplyRoleRestrictions();
        UpdateFromDatabase();
    }

    private void SetupInputValidation()
    {
        macTextBox.AddHandler(TextInputEvent, MacAddress_TextInput, handledEventsToo: true);
        macTextBox.TextChanged += MacAddress_TextChanged;
        macTextBox.MaxLength = 17;

        ipTextBox.AddHandler(TextInputEvent, IpAddress_TextInput, handledEventsToo: true);
        ipTextBox.TextChanged += IpAddress_TextChanged;
        ipTextBox.MaxLength = 15;

        maskTextBox.AddHandler(TextInputEvent, IpAddress_TextInput, handledEventsToo: true);
        maskTextBox.TextChanged += IpAddress_TextChanged;
        maskTextBox.MaxLength = 15;

        ptpMasterMacTextBox.AddHandler(TextInputEvent, MacAddress_TextInput, handledEventsToo: true);
        ptpMasterMacTextBox.TextChanged += MacAddress_TextChanged;
        ptpMasterMacTextBox.MaxLength = 17;

        ptpPortTextBox.AddHandler(TextInputEvent, Port_TextInput, handledEventsToo: true);
        ptpPortTextBox.TextChanged += Port_TextChanged;
        ptpPortTextBox.MaxLength = 5;

        ptpIdTextBox.AddHandler(TextInputEvent, ClkId_TextInput, handledEventsToo: true);
        ptpIdTextBox.TextChanged += ClkId_TextChanged;
        ptpIdTextBox.MaxLength = 8;
    }

    private void ApplyRoleRestrictions()
    {
        bool isAdmin = Database.CurrentRole == "Администратор";
        macTextBox.IsReadOnly = !isAdmin;
        ipTextBox.IsReadOnly = !isAdmin;
        maskTextBox.IsReadOnly = !isAdmin;
        ptpMasterMacTextBox.IsReadOnly = !isAdmin;
        ptpPortTextBox.IsReadOnly = !isAdmin;
        ptpIdTextBox.IsReadOnly = !isAdmin;

        if (!isAdmin)
        {
            macTextBox.Background = Avalonia.Media.Brushes.LightGray;
            ipTextBox.Background = Avalonia.Media.Brushes.LightGray;
            maskTextBox.Background = Avalonia.Media.Brushes.LightGray;
            ptpMasterMacTextBox.Background = Avalonia.Media.Brushes.LightGray;
            ptpPortTextBox.Background = Avalonia.Media.Brushes.LightGray;
            ptpIdTextBox.Background = Avalonia.Media.Brushes.LightGray;
        }
    }

    private void MacAddress_TextInput(object? sender, TextInputEventArgs e)
    {
        if (Database.CurrentRole != "Администратор") { e.Handled = true; return; }
        string text = e.Text ?? "";
        if (!string.IsNullOrEmpty(text))
        {
            char c = text[0];
            if (!char.IsDigit(c) && !":".Contains(c) && !(c >= 'a' && c <= 'f') && !(c >= 'A' && c <= 'F'))
                e.Handled = true;
        }
    }

    private void MacAddress_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_updating) return;
        if (sender is TextBox tb)
        {
            string text = tb.Text?.ToUpper() ?? "";
            string formatted = FormatMacAddress(text);
            if (formatted != tb.Text)
            {
                _updating = true;
                int cursor = tb.CaretIndex;
                tb.Text = formatted;
                tb.CaretIndex = Math.Min(cursor + (formatted.Length - text.Length), formatted.Length);
                _updating = false;
            }
        }
    }

    private string FormatMacAddress(string input)
    {
        string digits = new string(input.Where(c => char.IsDigit(c) || (c >= 'A' && c <= 'F')).ToArray());
        if (digits.Length > 12) digits = digits[..12];
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < digits.Length; i++)
        {
            if (i > 0 && i % 2 == 0) sb.Append(':');
            sb.Append(digits[i]);
        }
        return sb.ToString();
    }

    private void IpAddress_TextInput(object? sender, TextInputEventArgs e)
    {
        if (Database.CurrentRole != "Администратор") { e.Handled = true; return; }
        string text = e.Text ?? "";
        if (!string.IsNullOrEmpty(text))
        {
            char c = text[0];
            if (!char.IsDigit(c) && c != '.')
                e.Handled = true;
        }
    }

    private void IpAddress_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_updating) return;
        if (sender is TextBox tb)
        {
            string text = tb.Text ?? "";
            string filtered = new string(text.Where(c => char.IsDigit(c) || c == '.').ToArray());
            if (filtered.Count(c => c == '.') > 3)
            {
                int dotCount = 0;
                StringBuilder sb = new StringBuilder();
                foreach (char c in filtered)
                {
                    if (c == '.')
                    {
                        dotCount++;
                        if (dotCount <= 3) sb.Append(c);
                    }
                    else sb.Append(c);
                }
                filtered = sb.ToString();
            }

            if (filtered != text)
            {
                _updating = true;
                tb.Text = filtered;
                tb.CaretIndex = filtered.Length;
                _updating = false;
            }
        }
    }

    private void Port_TextInput(object? sender, TextInputEventArgs e)
    {
        if (Database.CurrentRole != "Администратор") { e.Handled = true; return; }
        string text = e.Text ?? "";
        if (!string.IsNullOrEmpty(text))
        {
            if (!char.IsDigit(text[0]))
                e.Handled = true;
        }
    }

    private void Port_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_updating) return;
        if (sender is TextBox tb)
        {
            string text = tb.Text ?? "";
            if (string.IsNullOrEmpty(text)) return;
            if (int.TryParse(text, out int port))
            {
                if (port > 65535)
                {
                    _updating = true;
                    tb.Text = "65535";
                    tb.CaretIndex = tb.Text.Length;
                    _updating = false;
                }
            }
            else
            {
                string digits = new string(text.Where(char.IsDigit).ToArray());
                if (digits != text)
                {
                    _updating = true;
                    tb.Text = digits;
                    tb.CaretIndex = digits.Length;
                    _updating = false;
                }
            }
        }
    }

    private void ClkId_TextInput(object? sender, TextInputEventArgs e)
    {
        if (Database.CurrentRole != "Администратор") { e.Handled = true; return; }
        string text = e.Text ?? "";
        if (!string.IsNullOrEmpty(text))
        {
            char c = text[0];
            if (!char.IsLetterOrDigit(c) && c != '_' && c != '-')
                e.Handled = true;
        }
    }

    private void ClkId_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_updating) return;
        if (sender is TextBox tb)
        {
            string text = tb.Text ?? "";
            string filtered = new string(text.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-').ToArray());
            if (filtered.Length > 8) filtered = filtered[..8];
            if (filtered != text)
            {
                _updating = true;
                tb.Text = filtered;
                tb.CaretIndex = filtered.Length;
                _updating = false;
            }
        }
    }

    public void UpdateFromDatabase()
    {
        if (_updating) return;
        _updating = true;
        settings = Database.GeneralSettings_TextFormat;

        if (settings.nets.ownAddr != null && settings.nets.ownAddr.Length >= 6)
            macTextBox.Text = string.Format("{0:X2}:{1:X2}:{2:X2}:{3:X2}:{4:X2}:{5:X2}",
                settings.nets.ownAddr[0], settings.nets.ownAddr[1], settings.nets.ownAddr[2],
                settings.nets.ownAddr[3], settings.nets.ownAddr[4], settings.nets.ownAddr[5]);
        else macTextBox.Text = "00:00:00:00:00:00";

        if (settings.nets.ips.ipAddr != null && settings.nets.ips.ipAddr.Length >= 4)
            ipTextBox.Text = string.Format("{0}.{1}.{2}.{3}",
                settings.nets.ips.ipAddr[0], settings.nets.ips.ipAddr[1],
                settings.nets.ips.ipAddr[2], settings.nets.ips.ipAddr[3]);
        else ipTextBox.Text = "0.0.0.0";

        if (settings.nets.ips.ipMask != null && settings.nets.ips.ipMask.Length >= 4)
            maskTextBox.Text = string.Format("{0}.{1}.{2}.{3}",
                settings.nets.ips.ipMask[0], settings.nets.ips.ipMask[1],
                settings.nets.ips.ipMask[2], settings.nets.ips.ipMask[3]);
        else maskTextBox.Text = "0.0.0.0";

        if (settings.syns.ptps.mmadr != null && settings.syns.ptps.mmadr.Length >= 6)
            ptpMasterMacTextBox.Text = string.Format("{0:X2}:{1:X2}:{2:X2}:{3:X2}:{4:X2}:{5:X2}",
                settings.syns.ptps.mmadr[0], settings.syns.ptps.mmadr[1], settings.syns.ptps.mmadr[2],
                settings.syns.ptps.mmadr[3], settings.syns.ptps.mmadr[4], settings.syns.ptps.mmadr[5]);
        else ptpMasterMacTextBox.Text = "00:00:00:00:00:00";

        ptpPortTextBox.Text = settings.syns.ptps.id.portNum ?? "";
        ptpIdTextBox.Text = settings.syns.ptps.id.clkId ?? "";

        _updating = false;
    }

    public Dictionary<string, string> GetNetworkSettingsDictionary()
    {
        return new Dictionary<string, string>
        {
            ["MAC-адрес"] = macTextBox.Text ?? "",
            ["IP-адрес"] = ipTextBox.Text ?? "",
            ["Маска подсети"] = maskTextBox.Text ?? "",
            ["PTP MAC-адрес мастера"] = ptpMasterMacTextBox.Text ?? "",
            ["PTP порт"] = ptpPortTextBox.Text ?? "",
            ["PTP идентификатор"] = ptpIdTextBox.Text ?? ""
        };
    }

    public bool SaveToDatabase()
    {
        if (Database.CurrentRole != "Администратор") return false;

        if (!IsValidMacAddress(macTextBox.Text))
        {
            DialogHelper.ShowMessageBox("Ошибка", "Некорректный MAC-адрес.");
            UpdateFromDatabase();
            return false;
        }
        if (!IsValidIpAddress(ipTextBox.Text))
        {
            DialogHelper.ShowMessageBox("Ошибка", "Некорректный IP-адрес.");
            UpdateFromDatabase();
            return false;
        }
        if (!IsValidIpAddress(maskTextBox.Text))
        {
            DialogHelper.ShowMessageBox("Ошибка", "Некорректная маска подсети.");
            UpdateFromDatabase();
            return false;
        }
        if (!IsValidMacAddress(ptpMasterMacTextBox.Text))
        {
            DialogHelper.ShowMessageBox("Ошибка", "Некорректный PTP MAC-адрес мастера.");
            UpdateFromDatabase();
            return false;
        }
        if (!int.TryParse(ptpPortTextBox.Text, out int port) || port < 0 || port > 65535)
        {
            DialogHelper.ShowMessageBox("Ошибка", "Порт должен быть целым числом от 0 до 65535.");
            UpdateFromDatabase();
            return false;
        }
        if (ptpIdTextBox.Text?.Length > 8)
        {
            DialogHelper.ShowMessageBox("Ошибка", "Идентификатор PTP не может быть длиннее 8 символов.");
            UpdateFromDatabase();
            return false;
        }
        if (!ModBusFunctions.ValidateStringLength(ptpIdTextBox.Text ?? "", 8, out _))
        {
            DialogHelper.ShowMessageBox("Ошибка", "Идентификатор PTP слишком длинный. Максимум 7 байт в UTF-8.");
            UpdateFromDatabase();
            return false;
        }

        try
        {
            string[] macParts = macTextBox.Text?.Split(':') ?? Array.Empty<string>();
            for (int i = 0; i < 6; i++)
                settings.nets.ownAddr[i] = Convert.ToByte(macParts[i], 16);

            string[] ipParts = ipTextBox.Text?.Split('.') ?? Array.Empty<string>();
            for (int i = 0; i < 4; i++)
                settings.nets.ips.ipAddr[i] = byte.Parse(ipParts[i]);

            string[] maskParts = maskTextBox.Text?.Split('.') ?? Array.Empty<string>();
            for (int i = 0; i < 4; i++)
                settings.nets.ips.ipMask[i] = byte.Parse(maskParts[i]);

            string[] ptpMacParts = ptpMasterMacTextBox.Text?.Split(':') ?? Array.Empty<string>();
            for (int i = 0; i < 6; i++)
                settings.syns.ptps.mmadr[i] = Convert.ToByte(ptpMacParts[i], 16);

            settings.syns.ptps.id.portNum = ptpPortTextBox.Text ?? "";
            settings.syns.ptps.id.clkId = ptpIdTextBox.Text ?? "";

            Database.GeneralSettings_TextFormat = settings;
            return true;
        }
        catch (Exception ex)
        {
            DialogHelper.ShowMessageBox("Ошибка", $"Ошибка преобразования данных: {ex.Message}");
            UpdateFromDatabase();
            return false;
        }
    }

    private bool IsValidMacAddress(string mac)
    {
        if (string.IsNullOrEmpty(mac)) return false;
        string[] parts = mac.Split(':');
        if (parts.Length != 6) return false;
        foreach (string part in parts)
            if (part.Length != 2 || !Regex.IsMatch(part, @"^[0-9A-Fa-f]{2}$"))
                return false;
        return true;
    }

    private bool IsValidIpAddress(string ip)
    {
        if (string.IsNullOrEmpty(ip)) return false;
        string[] parts = ip.Split('.');
        if (parts.Length != 4) return false;
        foreach (string part in parts)
            if (!byte.TryParse(part, out _))
                return false;
        return true;
    }
}