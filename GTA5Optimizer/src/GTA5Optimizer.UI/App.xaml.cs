using GTA5Optimizer.Services.Extensions;
using GTA5Optimizer.UI.Services;
using GTA5Optimizer.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GTA5Optimizer.UI
{
    public partial class App : System.Windows.Application
    {
        private IHost? _host;
        private TrayService? _trayService;

        public static IServiceProvider? ServiceProvider { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            _host = Host.CreateDefaultBuilder(e.Args)
                .ConfigureServices(services =>
                {
                    services.AddGTA5OptimizerServices();

                    // UI Services
                    services.AddSingleton<TrayService>();
                    services.AddSingleton<OverlayService>();

                    // ViewModels
                    services.AddSingleton<LogsViewModel>();
                    services.AddSingleton<MonitorViewModel>();
                    services.AddSingleton<SettingsViewModel>();
                    services.AddSingleton<DiagnosticsViewModel>();
                    services.AddSingleton<BenchmarkViewModel>();
                    services.AddSingleton<ProfileViewModel>();
                    services.AddSingleton<MainWindowViewModel>();

                    // Views
                    services.AddSingleton<Views.MainWindow>();
                })
                .Build();

            _host.Start();
            ServiceProvider = _host.Services;

            // Initialize tray
            _trayService = _host.Services.GetRequiredService<TrayService>();
            _trayService.Initialize();
            _trayService.ShowRequested += () =>
            {
                var app = System.Windows.Application.Current;
                app?.Dispatcher.Invoke(() =>
                {
                    var mw = _host.Services.GetRequiredService<Views.MainWindow>();
                    mw.Show();
                    mw.WindowState = WindowState.Normal;
                    mw.Activate();
                });
            };
            _trayService.ExitRequested += () =>
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    Shutdown();
                });
            };

            var mainWindow = _host.Services.GetRequiredService<Views.MainWindow>();
            mainWindow.Show();

            base.OnStartup(e);
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            _trayService?.Dispose();
            if (_host != null)
            {
                await _host.StopAsync();
                _host.Dispose();
            }
            base.OnExit(e);
        }
    }
}
