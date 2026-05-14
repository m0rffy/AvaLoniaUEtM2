using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;

namespace UETM2;

public partial class SetTimeDialog : Window
{
    public DateTime SelectedDateTime { get; private set; }

    public SetTimeDialog()
    {
        InitializeComponent();
        // Устанавливаем текущую дату и время по умолчанию
        datePicker.SelectedDate = DateTime.Now.Date;
        timePicker.SelectedTime = DateTime.Now.TimeOfDay;
    }

    private void btnOk_Click(object sender, RoutedEventArgs e)
    {
        if (datePicker.SelectedDate.HasValue && timePicker.SelectedTime.HasValue)
        {
            SelectedDateTime = datePicker.SelectedDate.Value.Date + timePicker.SelectedTime.Value;
            Close(true);
        }
        else
        {
            // На случай, если что-то не выбрано – берём текущее время
            SelectedDateTime = DateTime.Now;
            Close(true);
        }
    }

    private void btnCancel_Click(object sender, RoutedEventArgs e)
    {
        Close(false);
    }
}