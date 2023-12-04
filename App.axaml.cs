using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;

using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

using TestingEO.ViewModels;
using TestingEO.Views;

namespace TestingEO
{
    public class App : Application
    {
        public override void Initialize()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Async(w =>
                {
                    // write to file
                    w.File("TestingEO.log", rollingInterval: RollingInterval.Day);
                    // write to console
                    w.Console(theme: AnsiConsoleTheme.Code);
                })
                .CreateLogger();
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                BindingPlugins.DataValidators.RemoveAt(0);
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainWindowViewModel(),
                };
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
