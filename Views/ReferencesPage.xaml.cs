using System.Windows;
using System.Windows.Controls;

namespace MedicineUsageSystem.Views;

public partial class ReferencesPage : UserControl
{
    public ReferencesPage()
    {
        InitializeComponent();
        DepartmentsEditor.Init(RefKind.Departments, "Справочник отделений", "отделение");
        DrugsEditor.Init(RefKind.Drugs, "Справочник препаратов", "препарат");
    }

    void Back_Click(object sender, RoutedEventArgs e) => Nav.Menu();
}
