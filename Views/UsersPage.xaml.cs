using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DrugUsageSimple.Views;

/// <summary>Управление пользователями — только для администратора.</summary>
public partial class UsersPage : UserControl
{
    public UsersPage()
    {
        InitializeComponent();
        Reload();
    }

    AppUser? Selected => UsersGrid.SelectedItem as AppUser;

    void Reload() => UsersGrid.ItemsSource = Repo.GetUsers();

    void Say(string text, bool error)
    {
        StatusText.Text = text;
        StatusText.Foreground = (Brush)FindResource(error ? "DangerBrush" : "SuccessBrush");
    }

    void Back_Click(object sender, RoutedEventArgs e) => Nav.Menu();

    void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Selected is { } u) LoginBox.Text = u.Login;
    }

    void Add_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var login = LoginBox.Text.Trim();
            Repo.AddUser(LoginBox.Text, PassBox.Text, AdminCheck.IsChecked == true);
            Reload();
            LoginBox.Clear();
            PassBox.Clear();
            AdminCheck.IsChecked = false;
            Say($"Пользователь «{login}» добавлен.", false);
        }
        catch (UserException ex)
        {
            Say(ex.Message, true);
        }
    }

    void ChangePassword_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } u)
        {
            Say("Выберите пользователя в таблице, чтобы изменить его пароль.", true);
            return;
        }
        try
        {
            var newPassword = PassBox.Text;
            Repo.ChangePassword(u.Id, newPassword);
            if (Auth.Current != null && Auth.Current.Id == u.Id)
                Auth.Current.Password = newPassword;
            Reload();
            PassBox.Clear();
            Say($"Пароль пользователя «{u.Login}» изменён.", false);
        }
        catch (UserException ex)
        {
            Say(ex.Message, true);
        }
    }

    void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } u)
        {
            Say("Выберите пользователя в таблице, чтобы удалить его.", true);
            return;
        }
        if (!Dialogs.Confirm($"Удалить пользователя «{u.Login}»?")) return;

        try
        {
            Repo.DeleteUser(u.Id, Auth.Current?.Id ?? 0);
            Reload();
            LoginBox.Clear();
            Say($"Пользователь «{u.Login}» удалён.", false);
        }
        catch (UserException ex)
        {
            Say(ex.Message, true);
        }
    }
}
