using GTA5Optimizer.UI.Services;
using GTA5Optimizer.UI.ViewModels;

namespace GTA5Optimizer.UI.Views;

public partial class MainWindow : Window
{
    private readonly HotkeyService _hotkeyService;

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        _hotkeyService = new HotkeyService();
        _hotkeyService.OptimizeRequested += async () =>
        {
            if (viewModel.OptimizeCommand.CanExecute(null))
                await viewModel.OptimizeCommand.ExecuteAsync(null);
        };

        Loaded += (_, _) => _hotkeyService.Initialize(this);
        Closed += (_, _) =>
        {
            _hotkeyService.Dispose();
            viewModel.Dispose();
        };
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        // Minimize to tray instead of closing
        Hide();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void NavButton_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.RadioButton rb) return;

        // Hide all pages
        PageOptimization.Visibility = Visibility.Collapsed;
        PageMonitoring.Visibility = Visibility.Collapsed;
        PageDiagnostics.Visibility = Visibility.Collapsed;
        PageBenchmark.Visibility = Visibility.Collapsed;
        PageLogs.Visibility = Visibility.Collapsed;
        PageSettings.Visibility = Visibility.Collapsed;

        // Show the selected page
        switch (rb.Name)
        {
            case "NavOptimization":
                PageOptimization.Visibility = Visibility.Visible;
                break;
            case "NavMonitoring":
                PageMonitoring.Visibility = Visibility.Visible;
                break;
            case "NavDiagnostics":
                PageDiagnostics.Visibility = Visibility.Visible;
                break;
            case "NavBenchmark":
                PageBenchmark.Visibility = Visibility.Visible;
                break;
            case "NavLogs":
                PageLogs.Visibility = Visibility.Visible;
                break;
            case "NavSettings":
                PageSettings.Visibility = Visibility.Visible;
                break;
        }
    }
}
