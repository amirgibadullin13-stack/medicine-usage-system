namespace MedicineUsageSystem;

public enum RefKind { Departments, Drugs }

/// <summary>Ошибка, текст которой можно показать пользователю как есть.</summary>
public class UserException : Exception
{
    public UserException(string message) : base(message) { }
}

/// <summary>Пользователь программы (логин и пароль).</summary>
public class AppUser
{
    public long Id { get; set; }
    public string Login { get; set; } = "";
    public string Password { get; set; } = "";
    public bool IsAdmin { get; set; }
    public string Role => IsAdmin ? "Администратор" : "Пользователь";
}

/// <summary>Запись справочника (отделение или препарат).</summary>
public class RefItem
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public override string ToString() => Name;
}

/// <summary>Строка списка пациентов.</summary>
public class PatientRow
{
    public long Id { get; set; }
    public string FullName { get; set; } = "";
    public DateTime BirthDate { get; set; }
    public string Department { get; set; } = "";
    public DateTime ExecutionDate { get; set; }
    public string DrugsSummary { get; set; } = "";
}

/// <summary>Карточка пациента с препаратами.</summary>
public class Patient
{
    public long Id { get; set; }
    public string FullName { get; set; } = "";
    public DateTime BirthDate { get; set; }
    public long DepartmentId { get; set; }
    public DateTime ExecutionDate { get; set; }
    public List<PatientDrugItem> Items { get; } = new();
}

public class PatientDrugItem
{
    public long DrugId { get; set; }
    public string Dosage { get; set; } = "";
    public DateTime UsageDate { get; set; }
}

/// <summary>Строка препарата в форме ввода.</summary>
public class DrugRow
{
    public RefItem? Drug { get; set; }
    public string Dosage { get; set; } = "";
    public DateTime? UsageDate { get; set; }
}

public class ReportFilter
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public long? DepartmentId { get; set; }
    public string DepartmentName { get; set; } = "Все отделения";
    public string SortName { get; set; } = "";

    public string PeriodText
    {
        get
        {
            const string fmt = "dd.MM.yyyy";
            if (From == null && To == null) return "весь период";
            if (To == null) return "с " + From!.Value.ToString(fmt);
            if (From == null) return "по " + To.Value.ToString(fmt);
            return From.Value.ToString(fmt) + " — " + To.Value.ToString(fmt);
        }
    }
}

public class DetailRow
{
    public string FullName { get; set; } = "";
    public string Department { get; set; } = "";
    public DateTime Date { get; set; }
    public string Drug { get; set; } = "";
    public string Dosage { get; set; } = "";
    public int Count { get; set; }
}

public class TotalRow
{
    public string Drug { get; set; } = "";
    public int Total { get; set; }
}

public record DbStats(int Patients, int Rows, int Departments, int Drugs);
