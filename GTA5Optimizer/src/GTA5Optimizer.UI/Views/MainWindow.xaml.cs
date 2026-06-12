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

        // Find pages by name — they may not be direct fields if nested in templates
        var pages = new Dictionary<string, string>
        {
            { "NavOptimization", "PageOptimization" },
            { "NavMonitoring", "PageMonitoring" },
            { "NavDiagnostics", "PageDiagnostics" },
            { "NavBenchmark", "PageBenchmark" },
            { "NavLogs", "PageLogs" },
            { "NavSettings", "PageSettings" },
        };

        // Hide all pages
        foreach (var pageName in pages.Values)
        {
            if (FindName(pageName) is System.Windows.FrameworkElement el)
                el.Visibility = Visibility.Collapsed;
        }

        // Show selected page
        if (pages.TryGetValue(rb.Name, out var target) && FindName(target) is System.Windows.FrameworkElement targetEl)
            targetEl.Visibility = Visibility.Visible;
    }
}
