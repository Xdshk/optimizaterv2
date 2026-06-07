using GTA5Optimizer.UI.ViewModels;

namespace GTA5Optimizer.UI.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = App.ServiceProvider?.GetService(typeof(MainWindowViewModel)) as MainWindowViewModel;
    }
}
