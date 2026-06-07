using GTA5Optimizer.Services.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Windows;

namespace GTA5Optimizer.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private IHost? _host;

        public static IServiceProvider? ServiceProvider { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            _host = Host.CreateDefaultBuilder(e.Args)
                .ConfigureServices(services =>
                {
                    services.AddGTA5OptimizerServices();

                    // ViewModels
                    services.AddSingleton<ViewModels.LogsViewModel>();
                    services.AddSingleton<ViewModels.MonitorViewModel>();
                    services.AddSingleton<ViewModels.SettingsViewModel>();
                    services.AddSingleton<ViewModels.MainWindowViewModel>();

                    // Views
                    services.AddSingleton<Views.MainWindow>();
                })
                .Build();

            _host.Start();
            ServiceProvider = _host.Services;

            var mainWindow = _host.Services.GetRequiredService<Views.MainWindow>();
            mainWindow.Show();

            base.OnStartup(e);
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            if (_host != null)
            {
                await _host.StopAsync();
                _host.Dispose();
            }
            base.OnExit(e);
        }
    }
}
