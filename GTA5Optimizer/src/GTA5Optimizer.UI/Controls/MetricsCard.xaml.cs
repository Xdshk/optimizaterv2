using System.Windows.Controls;

namespace GTA5Optimizer.UI.Controls;

/// <summary>
/// Interaction logic for MetricsCard.xaml
/// </summary>
public partial class MetricsCard : UserControl
{
    public MetricsCard()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register("Title", typeof(string), typeof(MetricsCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register("Value", typeof(string), typeof(MetricsCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty PercentageProperty =
        DependencyProperty.Register("Percentage", typeof(double), typeof(MetricsCard), new PropertyMetadata(0.0));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Percentage
    {
        get => (double)GetValue(PercentageProperty);
        set => SetValue(PercentageProperty, value);
    }
}