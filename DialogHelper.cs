using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using System;
using System.Threading.Tasks;

namespace UETM2;

public static class DialogHelper
{
    public static async Task ShowMessageBox(string title, string message)
    {
        var msgBox = new Window
        {
            Title = title,
            Width = 450,
            MinWidth = 350,
            MaxWidth = 800,
            SizeToContent = SizeToContent.Height, // Высота подстраивается под текст
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true,
            Content = new StackPanel
            {
                Margin = new Thickness(15),
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 15) },
                    new Button { Content = "OK", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, Width = 80 }
                }
            }
        };
        if (msgBox.Content is StackPanel panel && panel.Children[1] is Button okBtn)
        {
            okBtn.Click += (s, e) => msgBox.Close();
        }
        var mainWindow = GetMainWindow();
        if (mainWindow != null)
            await msgBox.ShowDialog(mainWindow);
        else
            msgBox.Show();
        await Task.CompletedTask;
    }

    public static async Task<bool> ShowMessageBox(string title, string message, MessageBoxButtons buttons)
    {
        if (buttons == MessageBoxButtons.OK)
        {
            await ShowMessageBox(title, message);
            return true;
        }

        var msgBox = new Window
        {
            Title = title,
            Width = 450,
            MinWidth = 350,
            MaxWidth = 800,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true,
            Content = new StackPanel
            {
                Margin = new Thickness(15),
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 15) },
                    new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        Spacing = 20,
                        Children =
                        {
                            new Button { Content = "Да", Width = 70 },
                            new Button { Content = "Нет", Width = 70 }
                        }
                    }
                }
            }
        };

        var tcs = new TaskCompletionSource<bool>();
        if (msgBox.Content is StackPanel outer && outer.Children[1] is StackPanel btns)
        {
            var yesBtn = (Button)btns.Children[0];
            var noBtn = (Button)btns.Children[1];
            yesBtn.Click += (s, e) => { msgBox.Close(); tcs.TrySetResult(true); };
            noBtn.Click += (s, e) => { msgBox.Close(); tcs.TrySetResult(false); };
        }

        var mainWindow = GetMainWindow();
        if (mainWindow != null)
            await msgBox.ShowDialog(mainWindow);
        else
            msgBox.Show();
        return await tcs.Task;
    }

    public static async Task<string?> ShowSaveFileDialog(string title, string defaultExt, string filter)
    {
        var topLevel = TopLevel.GetTopLevel(GetMainWindow());
        if (topLevel == null) return null;
        var options = new FilePickerSaveOptions
        {
            Title = title,
            DefaultExtension = defaultExt,
            FileTypeChoices = new[] { new FilePickerFileType(filter) { Patterns = new[] { $"*.{defaultExt}" } } }
        };
        var file = await topLevel.StorageProvider.SaveFilePickerAsync(options);
        return file?.Path.LocalPath;
    }

    private static Window? GetMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime lifetime)
            return lifetime.MainWindow;
        return null;
    }
}

public enum MessageBoxButtons
{
    OK,
    YesNo
}