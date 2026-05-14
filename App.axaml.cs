using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace UETM2;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var authWindow = new AuthWindow();
            authWindow.LoginSucceeded += (role) =>
            {
                var configuratorWindow = new ConfiguratorWindow(role);
                desktop.MainWindow = configuratorWindow;
                configuratorWindow.Show();
                authWindow.Close();
            };
            desktop.MainWindow = authWindow;
        }
        base.OnFrameworkInitializationCompleted();
    }
}