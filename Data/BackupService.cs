using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace MedicineUsageSystem;

public class BackupFile
{
    public string Format { get; set; } = "";
    public int Version { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<string> Departments { get; set; } = new();
    public List<string> Drugs { get; set; } = new();
    public List<BackupPatient> Patients { get; set; } = new();
}

public class BackupPatient
{
    public string FullName { get; set; } = "";
    public string BirthDate { get; set; } = "";      // ГГГГ-ММ-ДД
    public string Department { get; set; } = "";
    public string ExecutionDate { get; set; } = "";  // ГГГГ-ММ-ДД
    public List<BackupDrug> Items { get; set; } = new();
}

public class BackupDrug
{
    public string Drug { get; set; } = "";
    public string Dosage { get; set; } = "";
    public string Date { get; set; } = "";           // ГГГГ-ММ-ДД, с версии 2
}

/// <summary>Выгрузка и загрузка базы в файл резервной копии (JSON).</summary>
public static class BackupService
{
    public const string Magic = "DrugUsageBackup";

    static readonly JsonSerializerOptions Opts = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    // ---------- Выгрузка ----------

    public static void Create(string path)
    {
        var file = new BackupFile { Format = Magic, Version = 2, CreatedAt = DateTime.Now };
        file.Departments = Repo.GetRefs(RefKind.Departments).Select(x => x.Name).ToList();
        file.Drugs = Repo.GetRefs(RefKind.Drugs).Select(x => x.Name).ToList();

        using var c = Db.Open();
        var map = new Dictionary<long, BackupPatient>();

        using (var cmd = Db.Cmd(c, @"
            SELECT p.Id, p.FullName, p.BirthDate, dep.Name, p.ExecutionDate
              FROM Patients p JOIN Departments dep ON dep.Id = p.DepartmentId
             ORDER BY p.Id"))
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
            {
                var patient = new BackupPatient
                {
                    FullName = r.GetString(1),
                    BirthDate = r.GetString(2),
                    Department = r.GetString(3),
                    ExecutionDate = r.GetString(4)
                };
                map[r.GetInt64(0)] = patient;
                file.Patients.Add(patient);
            }
        }

        using (var cmd = Db.Cmd(c, @"
            SELECT pd.PatientId, dr.Name, pd.Dosage, pd.UsageDate
              FROM PatientDrugs pd JOIN Drugs dr ON dr.Id = pd.DrugId
             ORDER BY pd.Id"))
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
            {
                if (map.TryGetValue(r.GetInt64(0), out var owner))
                    owner.Items.Add(new BackupDrug { Drug = r.GetString(1), Dosage = r.GetString(2), Date = r.GetString(3) });
            }
        }

        File.WriteAllText(path, JsonSerializer.Serialize(file, Opts), new UTF8Encoding(false));
    }

    // ---------- Чтение и проверка структуры ----------

    public static BackupFile Read(string path)
    {
        BackupFile? f;
        try
        {
            f = JsonSerializer.Deserialize<BackupFile>(File.ReadAllText(path, Encoding.UTF8), Opts);
        }
        catch (JsonException)
        {
            throw new UserException("Файл повреждён или не является резервной копией этой программы.");
        }

        if (f == null || f.Format != Magic)
            throw new UserException("Файл несовместим: это не резервная копия программы учёта препаратов.");
        if (f.Version != 1 && f.Version != 2)
            throw new UserException($"Файл несовместим: версия формата {f.Version} не поддерживается (нужна версия 1 или 2).");
        if (f.Departments == null || f.Drugs == null || f.Patients == null)
            throw new UserException("Файл несовместим: в нём не хватает разделов данных.");

        int n = 0;
        foreach (var p in f.Patients)
        {
            n++;
            if (p == null || string.IsNullOrWhiteSpace(p.FullName) || string.IsNullOrWhiteSpace(p.Department)
                || p.Items == null)
                throw new UserException($"Файл несовместим: ошибка в записи пациента №{n}.");

            p.BirthDate = NormalizeDate(p.BirthDate, n);
            p.ExecutionDate = NormalizeDate(p.ExecutionDate, n);

            foreach (var it in p.Items)
            {
                if (it == null || string.IsNullOrWhiteSpace(it.Drug) || string.IsNullOrWhiteSpace(it.Dosage))
                    throw new UserException($"Файл несовместим: ошибка в строке препарата у пациента №{n}.");

                // в копиях старой версии даты у препарата нет — берём дату исполнения пациента
                it.Date = string.IsNullOrWhiteSpace(it.Date) ? p.ExecutionDate : NormalizeDate(it.Date, n);
            }
        }
        return f;
    }

    static string NormalizeDate(string? s, int patientNo)
    {
        try
        {
            return Db.D(Db.ParseD(s ?? ""));
        }
        catch (FormatException)
        {
            throw new UserException($"Файл несовместим: неверная дата у пациента №{patientNo}.");
        }
    }

    // ---------- Загрузка ----------

    /// <summary>replace = true — полностью заменить данные; false — объединить с существующими.</summary>
    public static (int added, int skipped) Restore(BackupFile f, bool replace)
    {
        using var c = Db.Open();
        using var tx = c.BeginTransaction();

        if (replace)
        {
            foreach (var t in new[] { "PatientDrugs", "Patients", "Drugs", "Departments" })
            {
                using var d = Db.Cmd(c, tx, $"DELETE FROM {t}");
                d.ExecuteNonQuery();
            }
        }

        var deps = LoadMap(c, tx, "Departments");
        var drugs = LoadMap(c, tx, "Drugs");

        foreach (var n in f.Departments.Concat(f.Patients.Select(p => p.Department)))
            EnsureName(c, tx, "Departments", deps, n);
        foreach (var n in f.Drugs.Concat(f.Patients.SelectMany(p => p.Items.Select(i => i.Drug))))
            EnsureName(c, tx, "Drugs", drugs, n);

        var existing = new HashSet<string>();
        if (!replace)
        {
            using var cmd = Db.Cmd(c, tx, "SELECT FullName, BirthDate, DepartmentId, ExecutionDate FROM Patients");
            using var r = cmd.ExecuteReader();
            while (r.Read())
                existing.Add(Key(r.GetString(0), r.GetString(1), r.GetInt64(2), r.GetString(3)));
        }

        int added = 0, skipped = 0;
        foreach (var p in f.Patients)
        {
            long depId = deps[p.Department.Trim()];
            if (!existing.Add(Key(p.FullName, p.BirthDate, depId, p.ExecutionDate)))
            {
                skipped++;
                continue;
            }

            long id;
            using (var ins = Db.Cmd(c, tx,
                @"INSERT INTO Patients(FullName, BirthDate, DepartmentId, ExecutionDate)
                  VALUES(@n, @b, @d, @e); SELECT last_insert_rowid();",
                ("@n", p.FullName.Trim()), ("@b", p.BirthDate), ("@d", depId), ("@e", p.ExecutionDate)))
            {
                id = (long)ins.ExecuteScalar()!;
            }

            foreach (var it in p.Items)
            {
                using var ic = Db.Cmd(c, tx,
                    "INSERT INTO PatientDrugs(PatientId, DrugId, Dosage, UsageDate) VALUES(@p, @d, @s, @u)",
                    ("@p", id), ("@d", drugs[it.Drug.Trim()]), ("@s", it.Dosage.Trim()), ("@u", it.Date));
                ic.ExecuteNonQuery();
            }
            added++;
        }

        tx.Commit();
        return (added, skipped);
    }

    static string Key(string name, string birth, long depId, string exec) =>
        $"{name.Trim().ToLowerInvariant()}|{birth}|{depId}|{exec}";

    static Dictionary<string, long> LoadMap(SqliteConnection c, SqliteTransaction tx, string table)
    {
        var map = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        using var cmd = Db.Cmd(c, tx, $"SELECT Id, Name FROM {table}");
        using var r = cmd.ExecuteReader();
        while (r.Read()) map[r.GetString(1)] = r.GetInt64(0);
        return map;
    }

    static void EnsureName(SqliteConnection c, SqliteTransaction tx, string table,
        Dictionary<string, long> map, string name)
    {
        name = name.Trim();
        if (name.Length == 0 || map.ContainsKey(name)) return;
        using var ins = Db.Cmd(c, tx, $"INSERT INTO {table}(Name) VALUES(@n); SELECT last_insert_rowid();", ("@n", name));
        map[name] = (long)ins.ExecuteScalar()!;
    }
}
