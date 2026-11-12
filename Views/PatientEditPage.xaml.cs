using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MedicineUsageSystem.Views;

/// <summary>Карточка пациента: добавление (patientId = 0) и редактирование.</summary>
public partial class PatientEditPage : UserControl
{
    readonly long _patientId;
    readonly Action _back;

    public ObservableCollection<DrugRow> Rows { get; } = new();
    public List<RefItem> Drugs { get; } = new();

    public PatientEditPage(long patientId, Action back)
    {
        InitializeComponent();
        _patientId = patientId;
        _back = back;
        DataContext = this;

        Drugs.AddRange(Repo.GetRefs(RefKind.Drugs));
        var departments = Repo.GetRefs(RefKind.Departments);
        DepartmentBox.ItemsSource = departments;

        if (patientId == 0)
        {
            TitleText.Text = "Новый пациент";
            ExecPicker.SelectedDate = DateTime.Today;
            Rows.Add(NewRow());
        }
        else
        {
            TitleText.Text = "Редактирование пациента";
            DeleteBtn.Visibility = Visibility.Visible;

            var p = Repo.GetPatient(patientId);
            if (p == null)
            {
                Dialogs.Warn("Карточка не найдена — возможно, она уже удалена.");
                _back();
                return;
            }

            NameBox.Text = p.FullName;
            BirthPicker.SelectedDate = p.BirthDate;
            DepartmentBox.SelectedValue = p.DepartmentId;
            ExecPicker.SelectedDate = p.ExecutionDate;
            foreach (var it in p.Items)
            {
                Rows.Add(new DrugRow
                {
                    Drug = Drugs.FirstOrDefault(d => d.Id == it.DrugId),
                    Dosage = it.Dosage,
                    UsageDate = it.UsageDate
                });
            }
            if (Rows.Count == 0) Rows.Add(NewRow());
        }

        if (departments.Count == 0 || Drugs.Count == 0)
            Show("Справочник отделений или препаратов пуст. Заполните его в разделе «Справочники».", true);
    }

    /// <summary>Новая строка препарата: дата по умолчанию — дата исполнения.</summary>
    DrugRow NewRow() => new DrugRow { UsageDate = ExecPicker.SelectedDate ?? DateTime.Today };

    void Show(string text, bool error)
    {
        StatusText.Text = text;
        StatusText.Foreground = (Brush)FindResource(error ? "DangerBrush" : "SuccessBrush");
    }

    void Back_Click(object sender, RoutedEventArgs e) => _back();

    void AddRow_Click(object sender, RoutedEventArgs e) => Rows.Add(NewRow());

    void RemoveRow_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not DrugRow row) return;

        bool filled = row.Drug != null || !string.IsNullOrWhiteSpace(row.Dosage);
        if (filled && !Dialogs.Confirm("Удалить эту строку препарата из карточки?")) return;

        Rows.Remove(row);
    }

    void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (!Dialogs.Confirm($"Удалить пациента «{NameBox.Text.Trim()}» вместе со всеми строками препаратов?\nЭто действие нельзя отменить."))
            return;
        Repo.DeletePatient(_patientId);
        _back();
    }

    static DateTime? ReadDate(DatePicker picker, string label, List<string> errors)
    {
        if (picker.SelectedDate != null) return picker.SelectedDate;
        if (string.IsNullOrWhiteSpace(picker.Text))
            errors.Add($"Укажите {label}.");
        else
            errors.Add($"Дата «{label}» введена неверно — используйте формат ДД.ММ.ГГГГ.");
        return null;
    }

    void Save_Click(object sender, RoutedEventArgs e)
    {
        var errors = new List<string>();

        var name = NameBox.Text.Trim();
        if (name.Length == 0) errors.Add("Укажите ФИО пациента.");

        var birth = ReadDate(BirthPicker, "дату рождения", errors);
        var exec = ReadDate(ExecPicker, "дату исполнения", errors);

        if (birth != null && birth.Value > DateTime.Today)
            errors.Add("Дата рождения не может быть в будущем.");
        if (birth != null && exec != null && exec.Value < birth.Value)
            errors.Add("Дата исполнения не может быть раньше даты рождения.");

        if (DepartmentBox.SelectedValue is not long departmentId)
        {
            errors.Add("Выберите отделение.");
            departmentId = 0;
        }

        var items = new List<PatientDrugItem>();
        int line = 0;
        foreach (var row in Rows)
        {
            line++;
            var dosage = (row.Dosage ?? "").Trim();
            if (row.Drug == null && dosage.Length == 0) continue; // пустую строку пропускаем

            if (row.Drug == null || dosage.Length == 0)
            {
                errors.Add($"Строка {line}: выберите препарат и укажите дозировку.");
                continue;
            }
            if (row.UsageDate == null)
            {
                errors.Add($"Строка {line}: укажите дату использования в формате ДД.ММ.ГГГГ.");
                continue;
            }
            if (birth != null && row.UsageDate.Value < birth.Value)
            {
                errors.Add($"Строка {line}: дата использования раньше даты рождения.");
                continue;
            }
            items.Add(new PatientDrugItem { DrugId = row.Drug.Id, Dosage = dosage, UsageDate = row.UsageDate.Value });
        }
        if (items.Count == 0 && !errors.Any(x => x.StartsWith("Строка")))
            errors.Add("Добавьте хотя бы один препарат.");

        if (errors.Count > 0)
        {
            Show(string.Join("\n", errors), true);
            return;
        }

        var patient = new Patient
        {
            Id = _patientId,
            FullName = name,
            BirthDate = birth!.Value,
            DepartmentId = departmentId,
            ExecutionDate = exec!.Value
        };
        patient.Items.AddRange(items);

        try
        {
            Repo.SavePatient(patient);
        }
        catch (Exception ex)
        {
            Show("Не удалось сохранить: " + ex.Message, true);
            return;
        }

        if (_patientId != 0)
        {
            _back(); // после правки возвращаемся к списку
            return;
        }

        // Новый пациент: очищаем форму для следующего, отделение и дату оставляем
        NameBox.Clear();
        BirthPicker.SelectedDate = null;
        Rows.Clear();
        Rows.Add(NewRow());
        Show($"Пациент «{name}» сохранён. Можно вводить следующего.", false);
        NameBox.Focus();
    }
}
