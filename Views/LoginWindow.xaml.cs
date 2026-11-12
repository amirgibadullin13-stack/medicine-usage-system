using System.Windows;

namespace DrugUsageSimple.Views;

public partial class LoginWindow : Window
{
    const int MaxAttempts = 3;
    int _attempts;

    public LoginWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => LoginBox.Focus();
    }

    void Login_Click(object sender, RoutedEventArgs e)
    {
        var login = LoginBox.Text.Trim();
        var password = PassBox.Password;

        if (login.Length == 0 || password.Length == 0)
        {
            MessageText.Text = "Введите логин и пароль.";
            return;
        }

        AppUser? user;
        try
        {
            user = Repo.FindUser(login, password);
        }
        catch (Exception ex)
        {
            MessageText.Text = "Ошибка базы данных: " + ex.Message;
            return;
        }

        if (user != null)
        {
            Auth.Current = user;
            DialogResult = true;
            return;
        }

        _attempts++;
        if (_attempts >= MaxAttempts)
        {
            MessageBox.Show(
                $"Неверный логин или пароль введён {MaxAttempts} раза. Программа будет закрыта.",
                "Доступ запрещён", MessageBoxButton.OK, MessageBoxImage.Stop);
            DialogResult = false;
            return;
        }

        PassBox.Clear();
        PassBox.Focus();
        MessageText.Text = $"Неверный логин или пароль. Осталось попыток: {MaxAttempts - _attempts}.";
    }
}
