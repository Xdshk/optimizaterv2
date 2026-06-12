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

        Loaded += (_, _) =>
        {
            _hotkeyService.Initialize(this);
            // Show initial page after all elements are loaded
            var area = FindVisualChild<Grid>(this, "ContentArea");
            if (area != null)
            {
                var firstPage = area.Children.OfType<FrameworkElement>().FirstOrDefault(c => c.Name == "PageOptimization");
                if (firstPage != null)
                    firstPage.Visibility = Visibility.Visible;
            }
        };
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

        // Find the ContentArea grid by walking the visual tree
        var contentArea = FindVisualChild<Grid>(this, "ContentArea");
        if (contentArea == null) return;

        // Map radio button names to page names
        var pageMap = new Dictionary<string, string>
        {
            { "NavOptimization", "PageOptimization" },
            { "NavMonitoring", "PageMonitoring" },
            { "NavDiagnostics", "PageDiagnostics" },
            { "NavBenchmark", "PageBenchmark" },
            { "NavLogs", "PageLogs" },
            { "NavSettings", "PageSettings" },
        };

        // Hide all pages
        foreach (var child in contentArea.Children.OfType<FrameworkElement>())
        {
            if (pageMap.Values.Contains(child.Name))
                child.Visibility = Visibility.Collapsed;
        }

        // Show selected page
        if (pageMap.TryGetValue(rb.Name, out var targetName))
        {
            var target = contentArea.Children.OfType<FrameworkElement>().FirstOrDefault(c => c.Name == targetName);
            if (target != null)
                target.Visibility = Visibility.Visible;
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent, string name) where T : FrameworkElement
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T fe && fe.Name == name)
                return fe;
            var result = FindVisualChild<T>(child, name);
            if (result != null)
                return result;
        }
        return null;
    }
}
