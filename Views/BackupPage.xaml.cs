using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace MedicineUsageSystem.Views;

public partial class BackupPage : UserControl
{
    const string FileFilter = "Резервная копия (*.json)|*.json";

    public BackupPage()
    {
        InitializeComponent();
        PathText.Text = "Файл базы данных: " + Db.FilePath;
        RefreshStats();
    }

    void Back_Click(object sender, RoutedEventArgs e) => Nav.Menu();

    void RefreshStats()
    {
        var s = Repo.GetStats();
        StatsText.Text = $"Пациентов: {s.Patients}   ·   Строк препаратов: {s.Rows}   ·   Отделений: {s.Departments}   ·   Препаратов: {s.Drugs}";
    }

    void Result(string text, bool error)
    {
        ResultText.Text = text;
        ResultText.Foreground = (Brush)FindResource(error ? "DangerBrush" : "SuccessBrush");
    }

    void Create_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Filter = FileFilter,
            FileName = $"drugusage_backup_{DateTime.Now:yyyyMMdd_HHmm}.json"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            BackupService.Create(dlg.FileName);
            Result($"Резервная копия сохранена:\n{dlg.FileName}", false);
        }
        catch (Exception ex)
        {
            Result("Не удалось создать копию: " + ex.Message, true);
        }
    }

    void Restore_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = FileFilter };
        if (dlg.ShowDialog() != true) return;

        BackupFile file;
        try
        {
            file = BackupService.Read(dlg.FileName);
        }
        catch (UserException ex)
        {
            Result(ex.Message, true);
            Dialogs.Warn(ex.Message, "Файл не подходит");
            return;
        }
        catch (Exception ex)
        {
            Result("Не удалось прочитать файл: " + ex.Message, true);
            return;
        }

        var question =
            $"В файле: пациентов — {file.Patients.Count}, отделений — {file.Departments.Count}, препаратов — {file.Drugs.Count}.\n" +
            $"Создан: {file.CreatedAt:dd.MM.yyyy HH:mm}\n\n" +
            "Да — ЗАМЕНИТЬ все текущие данные данными из копии.\n" +
            "Нет — ОБЪЕДИНИТЬ: добавить данные из копии к текущим (дубликаты пропускаются).\n" +
            "Отмена — ничего не менять.";

        var choice = MessageBox.Show(question, "Восстановление данных",
            MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Cancel);
        if (choice == MessageBoxResult.Cancel) return;

        bool replace = choice == MessageBoxResult.Yes;
        if (replace && !Dialogs.Confirm("Все текущие данные будут удалены и заменены данными из копии. Продолжить?"))
            return;

        try
        {
            var (added, skipped) = BackupService.Restore(file, replace);
            RefreshStats();
            Result(
                (replace ? "Данные заменены данными из копии." : "Данные из копии объединены с текущими.") +
                $"\nДобавлено пациентов: {added}" + (skipped > 0 ? $", пропущено дубликатов: {skipped}" : "") + "." +
                "\nПроверьте списки в разделах «Пациенты» и «Справочники».", false);
        }
        catch (Exception ex)
        {
            Result("Не удалось восстановить данные, текущая база не изменена: " + ex.Message, true);
        }
    }
}
