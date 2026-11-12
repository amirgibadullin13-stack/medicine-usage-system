using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace DrugUsageSimple.Views;

public partial class ReportsPage : UserControl
{
    static readonly string[] SortOptions =
    {
        "По дате использования",
        "По препарату",
        "По ФИО пациента"
    };

    ReportFilter _applied = new();
    List<DetailRow> _detail = new();
    List<TotalRow> _totals = new();

    public ReportsPage()
    {
        InitializeComponent();

        var deps = new List<RefItem> { new() { Id = 0, Name = "Все отделения" } };
        deps.AddRange(Repo.GetRefs(RefKind.Departments));
        DepartmentBox.ItemsSource = deps;
        DepartmentBox.SelectedIndex = 0;

        SortBox.ItemsSource = SortOptions;
        SortBox.SelectedIndex = 0;
        SortBox.SelectionChanged += (_, _) => Refresh(); // обработчик подключаем после начальной установки

        Refresh();
    }

    void Back_Click(object sender, RoutedEventArgs e) => Nav.Menu();

    bool ReadFilter(out ReportFilter f)
    {
        f = new ReportFilter();

        if (FromPicker.SelectedDate == null && !string.IsNullOrWhiteSpace(FromPicker.Text))
        {
            Dialogs.Warn("Начальная дата введена неверно. Используйте формат ДД.ММ.ГГГГ.");
            return false;
        }
        if (ToPicker.SelectedDate == null && !string.IsNullOrWhiteSpace(ToPicker.Text))
        {
            Dialogs.Warn("Конечная дата введена неверно. Используйте формат ДД.ММ.ГГГГ.");
            return false;
        }

        f.From = FromPicker.SelectedDate;
        f.To = ToPicker.SelectedDate;
        if (f.From != null && f.To != null && f.From > f.To)
        {
            Dialogs.Warn("Начальная дата периода позже конечной.");
            return false;
        }

        if (DepartmentBox.SelectedItem is RefItem dep && dep.Id != 0)
        {
            f.DepartmentId = dep.Id;
            f.DepartmentName = dep.Name;
        }
        return true;
    }

    void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (!ReadFilter(out var f)) return;
        _applied = f;
        Refresh();
    }

    void Reset_Click(object sender, RoutedEventArgs e)
    {
        FromPicker.SelectedDate = null;
        ToPicker.SelectedDate = null;
        DepartmentBox.SelectedIndex = 0;
        _applied = new ReportFilter();
        Refresh();
    }

    List<DetailRow> LoadSorted()
    {
        var cmp = StringComparer.CurrentCultureIgnoreCase;
        var rows = Repo.GetDetail(_applied);

        IEnumerable<DetailRow> sorted = SortBox.SelectedIndex switch
        {
            1 => rows.OrderBy(r => r.Drug, cmp).ThenBy(r => r.Date).ThenBy(r => r.FullName, cmp),
            2 => rows.OrderBy(r => r.FullName, cmp).ThenBy(r => r.Date).ThenBy(r => r.Drug, cmp),
            _ => rows.OrderBy(r => r.Date).ThenBy(r => r.FullName, cmp).ThenBy(r => r.Drug, cmp)
        };
        return sorted.ToList();
    }

    void Refresh()
    {
        _applied.SortName = SortBox.SelectedItem as string ?? SortOptions[0];

        _detail = LoadSorted();
        _totals = Repo.GetTotals(_applied);

        DetailGrid.ItemsSource = _detail;
        TotalsGrid.ItemsSource = _totals;

        FilterSummary.Text =
            $"Применённые фильтры: период — {_applied.PeriodText}; отделение — {_applied.DepartmentName}; " +
            $"сортировка — {_applied.SortName.ToLowerInvariant()}";
        DetailCount.Text = $"Строк в отчёте: {_detail.Count}";
        TotalsCount.Text = $"Всего использований: {_totals.Sum(t => t.Total)}";

        ExportDetailBtn.IsEnabled = _detail.Count > 0;
        ExportTotalsBtn.IsEnabled = _totals.Count > 0;
    }

    static string? AskPath(string baseName)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "Книга Excel (*.xlsx)|*.xlsx",
            FileName = $"{baseName}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    void ExportDetail_Click(object sender, RoutedEventArgs e)
    {
        var path = AskPath("Детальный_отчёт");
        if (path == null) return;
        try
        {
            ReportExporter.ExportDetail(path, _applied, _detail);
            Dialogs.Info($"Детальный отчёт сохранён:\n{path}");
        }
        catch (Exception ex)
        {
            Dialogs.Warn("Не удалось сохранить файл. Возможно, он открыт в Excel.\n" + ex.Message);
        }
    }

    void ExportTotals_Click(object sender, RoutedEventArgs e)
    {
        var path = AskPath("Общий_итог");
        if (path == null) return;
        try
        {
            ReportExporter.ExportTotals(path, _applied, _totals);
            Dialogs.Info($"Общий итог сохранён:\n{path}");
        }
        catch (Exception ex)
        {
            Dialogs.Warn("Не удалось сохранить файл. Возможно, он открыт в Excel.\n" + ex.Message);
        }
    }
}
