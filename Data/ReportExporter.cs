using ClosedXML.Excel;

namespace MedicineUsageSystem;

/// <summary>Выгрузка отчётов в XLSX. Применённые фильтры записываются в шапку листа.</summary>
public static class ReportExporter
{
    public static void ExportDetail(string path, ReportFilter f, IReadOnlyList<DetailRow> rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Детальный отчёт");
        int r = WriteHeader(ws, "Детальный отчёт по использованию препаратов", f);
        ws.Cell(5, 1).Value = "Сортировка:";
        ws.Cell(5, 2).Value = f.SortName;
        ws.Cell(5, 1).Style.Font.Bold = true;

        string[] heads =
        {
            "ФИО пациента", "Отделение", "Дата использования",
            "Название препарата", "Дозировка", "Количество использований"
        };
        for (int i = 0; i < heads.Length; i++) ws.Cell(r, i + 1).Value = heads[i];
        StyleHeader(ws.Range(r, 1, r, heads.Length));

        foreach (var row in rows)
        {
            r++;
            ws.Cell(r, 1).Value = row.FullName;
            ws.Cell(r, 2).Value = row.Department;
            ws.Cell(r, 3).Value = row.Date;
            ws.Cell(r, 3).Style.DateFormat.Format = "dd.MM.yyyy";
            ws.Cell(r, 4).Value = row.Drug;
            ws.Cell(r, 5).Value = row.Dosage;
            ws.Cell(r, 6).Value = row.Count;
        }

        double[] widths = { 34, 20, 20, 24, 16, 26 };
        for (int i = 0; i < widths.Length; i++) ws.Column(i + 1).Width = widths[i];

        wb.SaveAs(path);
    }

    public static void ExportTotals(string path, ReportFilter f, IReadOnlyList<TotalRow> rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Общий итог");
        int r = WriteHeader(ws, "Общий итог по использованию препаратов", f);

        ws.Cell(r, 1).Value = "Название препарата";
        ws.Cell(r, 2).Value = "Суммарное количество за период";
        StyleHeader(ws.Range(r, 1, r, 2));

        int sum = 0;
        foreach (var row in rows)
        {
            r++;
            ws.Cell(r, 1).Value = row.Drug;
            ws.Cell(r, 2).Value = row.Total;
            sum += row.Total;
        }

        r++;
        ws.Cell(r, 1).Value = "Всего";
        ws.Cell(r, 2).Value = sum;
        ws.Range(r, 1, r, 2).Style.Font.Bold = true;

        ws.Column(1).Width = 34;
        ws.Column(2).Width = 34;

        wb.SaveAs(path);
    }

    /// <summary>Заголовок и блок фильтров. Возвращает номер строки, где должна быть шапка таблицы.</summary>
    static int WriteHeader(IXLWorksheet ws, string title, ReportFilter f)
    {
        ws.Cell(1, 1).Value = title;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        ws.Cell(2, 1).Value = "Период:";
        ws.Cell(2, 2).Value = f.PeriodText;
        ws.Cell(3, 1).Value = "Отделение:";
        ws.Cell(3, 2).Value = f.DepartmentName;
        ws.Cell(4, 1).Value = "Сформирован:";
        ws.Cell(4, 2).Value = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
        ws.Range(2, 1, 4, 1).Style.Font.Bold = true;

        return 6;
    }

    static void StyleHeader(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#E5E7EB");
    }
}
