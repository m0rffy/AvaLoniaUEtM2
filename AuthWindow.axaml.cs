using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;

namespace UETM2;

public partial class AuthWindow : Window
{
    public event Action<string>? LoginSucceeded;

    public AuthWindow()
    {
        InitializeComponent();
    }

    private async void button1_Click(object sender, RoutedEventArgs e)
    {
        var roleItem = (ComboBoxItem?)LoginComboBox.SelectedItem;
        string role = roleItem?.Content?.ToString() ?? "";
        string password = PasswordTextBox.Text ?? "";

        if (string.IsNullOrWhiteSpace(role))
        {
            await DialogHelper.ShowMessageBox("Ошибка", "Выберите уровень доступа.");
            return;
        }
        if (string.IsNullOrWhiteSpace(password))
        {
            await DialogHelper.ShowMessageBox("Ошибка", "Введите пароль.");
            return;
        }

        if (!Database.AppData.Passwords.TryGetValue(role, out string? storedPassword) ||
            storedPassword != password)
        {
            await DialogHelper.ShowMessageBox("Ошибка", "Неверный пароль.");
            return;
        }

        Database.CurrentRole = role;
        LoginSucceeded?.Invoke(role);
    }
}