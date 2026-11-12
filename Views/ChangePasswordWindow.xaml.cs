using System.Windows;

namespace MedicineUsageSystem.Views;

/// <summary>Смена собственного пароля текущим пользователем.</summary>
public partial class ChangePasswordWindow : Window
{
    public ChangePasswordWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => OldBox.Focus();
    }

    void Save_Click(object sender, RoutedEventArgs e)
    {
        var me = Auth.Current;
        if (me == null)
        {
            DialogResult = false;
            return;
        }

        if (Repo.FindUser(me.Login, OldBox.Password) == null)
        {
            MessageText.Text = "Текущий пароль введён неверно.";
            return;
        }
        if (NewBox.Password != RepeatBox.Password)
        {
            MessageText.Text = "Новый пароль и повтор не совпадают.";
            return;
        }

        try
        {
            Repo.ChangePassword(me.Id, NewBox.Password);
        }
        catch (UserException ex)
        {
            MessageText.Text = ex.Message;
            return;
        }

        me.Password = NewBox.Password;
        MessageBox.Show("Пароль изменён.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
        DialogResult = true;
    }
}
