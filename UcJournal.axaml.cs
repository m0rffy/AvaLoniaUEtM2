using Avalonia.Controls;
using Avalonia.Interactivity;
using ModBusHelper;
using OfficeOpenXml;
using System;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace UETM2;

public partial class UcJournal : UserControl
{
    private ConfiguratorWindow mainForm;
    private ModBusProfile profileHelper = new ModBusProfile();
    private DataTable journalTable;

    public ObservableCollection<JournalEntry> JournalEntries { get; } = new();

    public UcJournal() { }

    public UcJournal(ConfiguratorWindow mainForm)
    {
        DataContext = this;
        InitializeComponent();
        this.mainForm = mainForm;

        journalTable = new DataTable();
        journalTable.Columns.Add("№", typeof(int));
        journalTable.Columns.Add("Тип события", typeof(string));
        journalTable.Columns.Add("Канал / IP", typeof(string));
        journalTable.Columns.Add("Дата и время", typeof(string));
        journalTable.Columns.Add("Ток (А)", typeof(float));
        journalTable.Columns.Add("Ресурс (%)", typeof(float));
    }

    private string ChannelNumberToLetter(int number) => number switch
    {
        0 => "A",
        1 => "B",
        2 => "C",
        3 => "N",
        _ => "-"
    };

    private async void UpdateJournalButton_Click(object? sender, RoutedEventArgs e)
    {
        JournalEntries.Clear();
        journalTable.Rows.Clear();
        int idx = 0;

        try
        {
            var localEntries = LocalDatabase.GetLogEntries(1000);
            foreach (var entry in localEntries)
            {
                JournalEntries.Add(new JournalEntry
                {
                    Index = idx,
                    EventType = entry.Description,
                    Source = entry.DeviceIP ?? "",
                    DateTime = entry.Timestamp.ToString("dd.MM.yyyy HH:mm:ss"),
                    CurrentA = entry.CurrentA,
                    ResourcePercent = entry.ResourcePercent
                });
                journalTable.Rows.Add(idx, entry.Description, entry.DeviceIP ?? "",
                    entry.Timestamp.ToString("dd.MM.yyyy HH:mm:ss"), entry.CurrentA ?? (object)DBNull.Value,
                    entry.ResourcePercent ?? (object)DBNull.Value);
                idx++;
            }
        }
        catch (Exception ex)
        {
            await DialogHelper.ShowMessageBox("Ошибка", $"Ошибка чтения локального журнала: {ex.Message}");
        }

        var conn = mainForm.GetCurrentConnection();
        if (conn?.Item1?.Connected == true)
        {
            try
            {
                var records = profileHelper.journal_record_Read(conn.Item2);
                foreach (var rec in records)
                {
                    if (rec.hdr.rtype == 1 || rec.hdr.rtype == 2)
                    {
                        if (rec.hdr.rtype == 1 && rec.hdr.udt == 3) continue;
                        string eventType = rec.hdr.rtype == 1
                            ? (rec.hdr.subtype == 0 ? "Отключение" : "Включение")
                            : "Обнуление";
                        DateTime dt = PtpTimeHelper.PtpToDateTime(rec.hdr.stamp.ns, rec.hdr.stamp.slo);
                        string channel = (rec.hdr.rtype == 1) ? ChannelNumberToLetter(rec.hdr.udt) : "";

                        JournalEntries.Add(new JournalEntry
                        {
                            Index = idx,
                            EventType = eventType,
                            Source = channel,
                            DateTime = dt.ToString("dd.MM.yyyy HH:mm:ss"),
                            CurrentA = rec.Ii,
                            ResourcePercent = rec.Ri
                        });
                        journalTable.Rows.Add(idx, eventType, channel,
                            dt.ToString("dd.MM.yyyy HH:mm:ss"), rec.Ii, rec.Ri);
                        idx++;
                    }
                }
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowMessageBox("Ошибка", $"Ошибка чтения журнала устройства: {ex.Message}");
            }
        }

        await DialogHelper.ShowMessageBox("Информация", $"Загружено {journalTable.Rows.Count} записей");
    }

    private async void ExportButton_Click(object? sender, RoutedEventArgs e)
    {
        var filePath = await DialogHelper.ShowSaveFileDialog("Экспорт в Excel", "xlsx", "Excel файлы (*.xlsx)|*.xlsx");
        if (string.IsNullOrEmpty(filePath)) return;

        try
        {
            ExcelPackage.License.SetNonCommercialPersonal("Пользователь UETM");

            using (var package = new ExcelPackage())
            {
                var ws = package.Workbook.Worksheets.Add("Журнал");
                ws.Cells[1, 1].Value = "№";
                ws.Cells[1, 2].Value = "Тип события";
                ws.Cells[1, 3].Value = "Канал / IP";
                ws.Cells[1, 4].Value = "Дата и время";
                ws.Cells[1, 5].Value = "Ток (А)";
                ws.Cells[1, 6].Value = "Ресурс (%)";

                for (int i = 0; i < journalTable.Rows.Count; i++)
                {
                    var row = journalTable.Rows[i];
                    ws.Cells[i + 2, 1].Value = row["№"].ToString();
                    ws.Cells[i + 2, 2].Value = row["Тип события"].ToString();
                    ws.Cells[i + 2, 3].Value = row["Канал / IP"].ToString();
                    ws.Cells[i + 2, 4].Value = row["Дата и время"].ToString();
                    ws.Cells[i + 2, 5].Value = row["Ток (А)"] == DBNull.Value ? "" : Convert.ToSingle(row["Ток (А)"]).ToString("F2");
                    ws.Cells[i + 2, 6].Value = row["Ресурс (%)"] == DBNull.Value ? "" : Convert.ToSingle(row["Ресурс (%)"]).ToString("F2");
                }
                ws.Cells.AutoFitColumns();

                FileInfo fi = new FileInfo(filePath);
                package.SaveAs(fi);
            }
            await DialogHelper.ShowMessageBox("Успешно", "Экспорт завершён");
        }
        catch (Exception ex)
        {
            await DialogHelper.ShowMessageBox("Ошибка", $"Ошибка экспорта: {ex.Message}");
        }
    }
}

public class JournalEntry
{
    public int Index { get; set; }
    public string EventType { get; set; } = "";
    public string Source { get; set; } = "";
    public string DateTime { get; set; } = "";
    public float? CurrentA { get; set; }
    public float? ResourcePercent { get; set; }
}