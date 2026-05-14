using Avalonia.Controls;
using Avalonia.Interactivity;
using System;

namespace UETM2;

public partial class DeviceCard : UserControl
{
    public event EventHandler? DeleteClicked;
    public event EventHandler? ConnectClicked;

    public DeviceCard()
    {
        InitializeComponent();
    }

    public void SetData(string ip, string place, string switchLabel, bool isActive)
    {
        ipLabel.Text = ip;
        placeLabel.Text = string.IsNullOrEmpty(place) ? "-" : place;
        switchLabelName.Text = string.IsNullOrEmpty(switchLabel) ? "-" : switchLabel;
        if (cardBorder != null)
            cardBorder.Background = isActive ? Avalonia.Media.Brushes.LightBlue : Avalonia.Media.Brushes.LightGray;
        if (connectButton != null)
            connectButton.Content = isActive ? "Отключиться" : "Подключиться";
    }

    private void deleteButton_Click(object sender, RoutedEventArgs e)
    {
        DeleteClicked?.Invoke(this, EventArgs.Empty);
    }

    private void connectButton_Click(object sender, RoutedEventArgs e)
    {
        ConnectClicked?.Invoke(this, EventArgs.Empty);
    }
}