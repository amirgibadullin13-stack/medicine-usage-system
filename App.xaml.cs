using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using MedicineUsageSystem.Views;

namespace MedicineUsageSystem;

public partial class App : Application
{
    bool _switching;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Окно входа показывается до главного окна, поэтому закрываем программу явно
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Русская локаль: даты ДД.ММ.ГГГГ, русский календарь в DatePicker
        var ru = new CultureInfo("ru-RU");
        CultureInfo.DefaultThreadCurrentCulture = ru;
        CultureInfo.DefaultThreadCurrentUICulture = ru;
        Thread.CurrentThread.CurrentCulture = ru;
        Thread.CurrentThread.CurrentUICulture = ru;
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(ru.IetfLanguageTag)));

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show("Произошла ошибка:\n" + args.Exception.Message, "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        try
        {
            Db.Init();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Не удалось открыть базу данных:\n" + ex.Message, "Ошибка запуска",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        StartSession();
    }

    /// <summary>Окно входа, затем главное окно. Закрытие окна входа или 3 ошибки — выход из программы.</summary>
    public void StartSession()
    {
        Auth.Current = null;

        var login = new LoginWindow();
        if (login.ShowDialog() != true)
        {
            Shutdown();
            return;
        }

        var main = new MedicineUsageSystem.MainWindow();
        MainWindow = main;
        main.Closed += (_, _) =>
        {
            if (!_switching) Shutdown();
        };
        main.Show();
    }

    /// <summary>Выход из учётной записи с возвратом к окну входа.</summary>
    public void Logout()
    {
        _switching = true;
        MainWindow?.Close();
        _switching = false;
        StartSession();
    }
}
