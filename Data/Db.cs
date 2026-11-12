using System.IO;
using System.Globalization;
using Microsoft.Data.Sqlite;

namespace DrugUsageSimple;

/// <summary>Доступ к файлу базы SQLite и создание схемы.</summary>
public static class Db
{
    public static string Dir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DrugUsageSimple");

    public static string FilePath { get; } = Path.Combine(Dir, "drugusagesimple.db");

    public static SqliteConnection Open()
    {
        var c = new SqliteConnection($"Data Source={FilePath}");
        c.Open();
        using var pragma = c.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();
        return c;
    }

    public static SqliteCommand Cmd(SqliteConnection c, string sql, params (string, object?)[] ps)
    {
        var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in ps)
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return cmd;
    }

    public static SqliteCommand Cmd(SqliteConnection c, SqliteTransaction tx, string sql, params (string, object?)[] ps)
    {
        var cmd = Cmd(c, sql, ps);
        cmd.Transaction = tx;
        return cmd;
    }

    /// <summary>Дата в базе хранится как ГГГГ-ММ-ДД (так работают сравнения и сортировка).</summary>
    public static string D(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static DateTime ParseD(string s) =>
        DateTime.ParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    static readonly string[] Schema =
    {
        @"CREATE TABLE IF NOT EXISTS Departments(
              Id INTEGER PRIMARY KEY AUTOINCREMENT,
              Name TEXT NOT NULL UNIQUE)",
        @"CREATE TABLE IF NOT EXISTS Drugs(
              Id INTEGER PRIMARY KEY AUTOINCREMENT,
              Name TEXT NOT NULL UNIQUE)",
        @"CREATE TABLE IF NOT EXISTS Patients(
              Id INTEGER PRIMARY KEY AUTOINCREMENT,
              FullName TEXT NOT NULL,
              BirthDate TEXT NOT NULL,
              DepartmentId INTEGER NOT NULL REFERENCES Departments(Id),
              ExecutionDate TEXT NOT NULL)",
        @"CREATE TABLE IF NOT EXISTS PatientDrugs(
              Id INTEGER PRIMARY KEY AUTOINCREMENT,
              PatientId INTEGER NOT NULL REFERENCES Patients(Id) ON DELETE CASCADE,
              DrugId INTEGER NOT NULL REFERENCES Drugs(Id),
              Dosage TEXT NOT NULL)",
        @"CREATE TABLE IF NOT EXISTS Users(
              Id INTEGER PRIMARY KEY AUTOINCREMENT,
              Login TEXT NOT NULL UNIQUE COLLATE NOCASE,
              Password TEXT NOT NULL,
              IsAdmin INTEGER NOT NULL DEFAULT 0)",
        "CREATE INDEX IF NOT EXISTS IX_Patients_Exec ON Patients(ExecutionDate)",
        "CREATE INDEX IF NOT EXISTS IX_PatientDrugs_Patient ON PatientDrugs(PatientId)"
    };

    static void Exec(SqliteConnection c, string sql)
    {
        using var cmd = Cmd(c, sql);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Добавляет дату использования к строкам препаратов (для баз, созданных старой версией).</summary>
    static void MigrateUsageDate(SqliteConnection c)
    {
        bool has = false;
        using (var info = Cmd(c, "PRAGMA table_info(PatientDrugs)"))
        using (var r = info.ExecuteReader())
        {
            while (r.Read())
            {
                if (string.Equals(r.GetString(1), "UsageDate", StringComparison.OrdinalIgnoreCase))
                    has = true;
            }
        }
        if (has) return;

        using var tx = c.BeginTransaction();
        using (var alter = Cmd(c, tx, "ALTER TABLE PatientDrugs ADD COLUMN UsageDate TEXT NOT NULL DEFAULT ''"))
            alter.ExecuteNonQuery();
        using (var fill = Cmd(c, tx,
            "UPDATE PatientDrugs SET UsageDate = (SELECT ExecutionDate FROM Patients WHERE Patients.Id = PatientDrugs.PatientId) WHERE UsageDate = ''"))
            fill.ExecuteNonQuery();
        tx.Commit();
    }

    public static void Init()
    {
        Directory.CreateDirectory(Dir);
        using var c = Open();

        foreach (var sql in Schema)
            Exec(c, sql);

        MigrateUsageDate(c);
        Exec(c, "CREATE INDEX IF NOT EXISTS IX_PatientDrugs_Date ON PatientDrugs(UsageDate)");

        long version;
        using (var v = Cmd(c, "PRAGMA user_version;"))
            version = (long)v.ExecuteScalar()!;

        // Начальные значения справочников — только при первом запуске
        if (version == 0)
        {
            using var tx = c.BeginTransaction();
            foreach (var n in new[] { "Хирургия", "Гинекология", "Реанимация" })
            {
                using var cmd = Cmd(c, tx, "INSERT OR IGNORE INTO Departments(Name) VALUES(@n)", ("@n", n));
                cmd.ExecuteNonQuery();
            }
            foreach (var n in new[] { "В6", "В12" })
            {
                using var cmd = Cmd(c, tx, "INSERT OR IGNORE INTO Drugs(Name) VALUES(@n)", ("@n", n));
                cmd.ExecuteNonQuery();
            }
            using (var cmd = Cmd(c, tx, "PRAGMA user_version = 1;"))
                cmd.ExecuteNonQuery();
            tx.Commit();
        }

        // Администратор по умолчанию, если пользователей ещё нет
        long users;
        using (var cnt = Cmd(c, "SELECT COUNT(*) FROM Users"))
            users = (long)cnt.ExecuteScalar()!;
        if (users == 0)
            Exec(c, "INSERT INTO Users(Login, Password, IsAdmin) VALUES('admin', 'admin', 1)");
    }
}
