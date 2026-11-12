using System.Windows;

namespace MedicineUsageSystem;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Nav.Host = Host;
        Nav.Menu();
    }
}
