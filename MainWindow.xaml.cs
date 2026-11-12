using System.Windows;

namespace DrugUsageSimple;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Nav.Host = Host;
        Nav.Menu();
    }
}
