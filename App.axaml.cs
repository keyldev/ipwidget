using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using IpWidget.Services;

namespace IpWidget;

public partial class App : Application
{
    private TrayIcon? _tray;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // window can hide to tray without the app quitting
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var icon = IconFactory.Create();
            var window = new MainWindow { Icon = icon };
            window.RequestQuit = () => desktop.Shutdown();
            desktop.MainWindow = window;

            _tray = new TrayIcon
            {
                Icon = icon,
                ToolTipText = "IP Widget",
                IsVisible = true,
            };

            var menu = new NativeMenu();
            var show = new NativeMenuItem("Показать");
            show.Click += (_, _) => window.ShowFromTray();
            var refresh = new NativeMenuItem("Обновить");
            refresh.Click += (_, _) => { window.ShowFromTray(); window.TriggerCheck(); };
            var quit = new NativeMenuItem("Выход");
            quit.Click += (_, _) => desktop.Shutdown();

            menu.Items.Add(show);
            menu.Items.Add(refresh);
            menu.Items.Add(new NativeMenuItemSeparator());
            menu.Items.Add(quit);
            _tray.Menu = menu;
            _tray.Clicked += (_, _) => window.ShowFromTray();

            TrayIcon.SetIcons(this, new TrayIcons { _tray });
        }

        base.OnFrameworkInitializationCompleted();
    }
}
