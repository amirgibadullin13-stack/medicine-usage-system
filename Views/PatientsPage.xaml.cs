using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MedicineUsageSystem.Views;

public partial class PatientsPage : UserControl
{
    List<PatientRow> _all = new();

    public PatientsPage()
    {
        InitializeComponent();
        Reload();
    }

    void Reload()
    {
        _all = Repo.GetPatientRows();
        ApplyFilter();
    }

    void ApplyFilter()
    {
        if (CountText == null || PatientsGrid == null) return;

        var q = SearchBox.Text.Trim();
        var list = q.Length == 0
            ? _all
            : _all.Where(p => p.FullName.Contains(q, StringComparison.CurrentCultureIgnoreCase)).ToList();

        PatientsGrid.ItemsSource = list;
        CountText.Text = q.Length == 0
            ? $"Всего пациентов: {list.Count}"
            : $"Найдено: {list.Count} из {_all.Count}";
    }

    PatientRow? Selected => PatientsGrid.SelectedItem as PatientRow;

    void Back_Click(object sender, RoutedEventArgs e) => Nav.Menu();

    void Search_Changed(object sender, TextChangedEventArgs e) => ApplyFilter();

    void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        bool has = Selected != null;
        EditBtn.IsEnabled = has;
        DeleteBtn.IsEnabled = has;
    }

    void New_Click(object sender, RoutedEventArgs e) =>
        Nav.Go(new PatientEditPage(0, () => Nav.Go(new PatientsPage())));

    void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is { } row) OpenEditor(row);
    }

    void Row_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGridRow { Item: PatientRow row }) OpenEditor(row);
    }

    static void OpenEditor(PatientRow row) =>
        Nav.Go(new PatientEditPage(row.Id, () => Nav.Go(new PatientsPage())));

    void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } row) return;
        if (!Dialogs.Confirm($"Удалить пациента «{row.FullName}» вместе со всеми строками препаратов?\nЭто действие нельзя отменить."))
            return;

        Repo.DeletePatient(row.Id);
        Reload();
    }
}
