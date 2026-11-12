using System.Windows;
using System.Windows.Controls;

namespace DrugUsageSimple.Views;

public partial class MainMenuPage : UserControl
{
    public MainMenuPage()
    {
        InitializeComponent();

        if (Auth.Current is { } u)
            UserText.Text = $"Вы вошли как: {u.Login} ({u.Role.ToLowerInvariant()})";

        if (Auth.IsAdmin)
            UsersTile.Visibility = Visibility.Visible;
    }

    void Entry_Click(object sender, RoutedEventArgs e) => Nav.Go(new PatientEditPage(0, Nav.Menu));

    void Patients_Click(object sender, RoutedEventArgs e) => Nav.Go(new PatientsPage());

    void Reports_Click(object sender, RoutedEventArgs e) => Nav.Go(new ReportsPage());

    void References_Click(object sender, RoutedEventArgs e) => Nav.Go(new ReferencesPage());

    void Backup_Click(object sender, RoutedEventArgs e) => Nav.Go(new BackupPage());

    void Users_Click(object sender, RoutedEventArgs e)
    {
        if (Auth.IsAdmin) Nav.Go(new UsersPage());
    }

    void Password_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new ChangePasswordWindow { Owner = Window.GetWindow(this) };
        dlg.ShowDialog();
    }

    void Logout_Click(object sender, RoutedEventArgs e)
    {
        if (Dialogs.Confirm("Выйти из учётной записи?"))
            ((App)Application.Current).Logout();
    }
}
