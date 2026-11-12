using Microsoft.Data.Sqlite;

namespace DrugUsageSimple;

/// <summary>Все операции с данными: справочники, пациенты, отчёты.</summary>
public static class Repo
{
    // ---------- Справочники ----------

    static string Table(RefKind k) => k == RefKind.Departments ? "Departments" : "Drugs";

    public static List<RefItem> GetRefs(RefKind kind)
    {
        using var c = Db.Open();
        using var cmd = Db.Cmd(c, $"SELECT Id, Name FROM {Table(kind)}");
        using var r = cmd.ExecuteReader();
        var list = new List<RefItem>();
        while (r.Read())
            list.Add(new RefItem { Id = r.GetInt64(0), Name = r.GetString(1) });
        return list.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    static string CleanName(string? name)
    {
        var n = (name ?? "").Trim();
        if (n.Length == 0) throw new UserException("Название не может быть пустым.");
        if (n.Length > 100) throw new UserException("Название слишком длинное (максимум 100 символов).");
        return n;
    }

    static bool NameExists(SqliteConnection c, RefKind kind, string name, long exceptId)
    {
        using var cmd = Db.Cmd(c, $"SELECT Id, Name FROM {Table(kind)}");
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            if (r.GetInt64(0) != exceptId &&
                string.Equals(r.GetString(1), name, StringComparison.CurrentCultureIgnoreCase))
                return true;
        }
        return false;
    }

    public static void AddRef(RefKind kind, string? name)
    {
        var n = CleanName(name);
        using var c = Db.Open();
        if (NameExists(c, kind, n, 0))
            throw new UserException($"Запись «{n}» уже есть в справочнике.");
        using var cmd = Db.Cmd(c, $"INSERT INTO {Table(kind)}(Name) VALUES(@n)", ("@n", n));
        cmd.ExecuteNonQuery();
    }

    public static void RenameRef(RefKind kind, long id, string? name)
    {
        var n = CleanName(name);
        using var c = Db.Open();
        if (NameExists(c, kind, n, id))
            throw new UserException($"Запись «{n}» уже есть в справочнике.");
        using var cmd = Db.Cmd(c, $"UPDATE {Table(kind)} SET Name=@n WHERE Id=@id", ("@n", n), ("@id", id));
        cmd.ExecuteNonQuery();
    }

    public static void DeleteRef(RefKind kind, long id)
    {
        using var c = Db.Open();
        var usedSql = kind == RefKind.Departments
            ? "SELECT COUNT(*) FROM Patients WHERE DepartmentId=@id"
            : "SELECT COUNT(*) FROM PatientDrugs WHERE DrugId=@id";
        long used;
        using (var cmd = Db.Cmd(c, usedSql, ("@id", id)))
            used = (long)cmd.ExecuteScalar()!;
        if (used > 0)
            throw new UserException(kind == RefKind.Departments
                ? $"Нельзя удалить отделение: в нём числятся пациенты ({used}). Сначала переведите или удалите их."
                : $"Нельзя удалить препарат: он использован в карточках пациентов ({used} строк.).");

        using var del = Db.Cmd(c, $"DELETE FROM {Table(kind)} WHERE Id=@id", ("@id", id));
        del.ExecuteNonQuery();
    }

    // ---------- Пациенты ----------

    public static List<PatientRow> GetPatientRows()
    {
        using var c = Db.Open();
        using var cmd = Db.Cmd(c, @"
            SELECT p.Id, p.FullName, p.BirthDate, dep.Name, p.ExecutionDate,
                   (SELECT GROUP_CONCAT(dr.Name || ' — ' || pd.Dosage || ' (' || substr(pd.UsageDate, 9, 2) || '.' || substr(pd.UsageDate, 6, 2) || '.' || substr(pd.UsageDate, 1, 4) || ')', '; ')
                      FROM PatientDrugs pd JOIN Drugs dr ON dr.Id = pd.DrugId
                     WHERE pd.PatientId = p.Id)
              FROM Patients p JOIN Departments dep ON dep.Id = p.DepartmentId
             ORDER BY p.ExecutionDate DESC, p.FullName");
        using var r = cmd.ExecuteReader();
        var list = new List<PatientRow>();
        while (r.Read())
        {
            list.Add(new PatientRow
            {
                Id = r.GetInt64(0),
                FullName = r.GetString(1),
                BirthDate = Db.ParseD(r.GetString(2)),
                Department = r.GetString(3),
                ExecutionDate = Db.ParseD(r.GetString(4)),
                DrugsSummary = r.IsDBNull(5) ? "" : r.GetString(5)
            });
        }
        return list;
    }

    public static Patient? GetPatient(long id)
    {
        using var c = Db.Open();
        Patient? p = null;

        using (var cmd = Db.Cmd(c,
            "SELECT Id, FullName, BirthDate, DepartmentId, ExecutionDate FROM Patients WHERE Id=@id", ("@id", id)))
        using (var r = cmd.ExecuteReader())
        {
            if (r.Read())
            {
                p = new Patient
                {
                    Id = r.GetInt64(0),
                    FullName = r.GetString(1),
                    BirthDate = Db.ParseD(r.GetString(2)),
                    DepartmentId = r.GetInt64(3),
                    ExecutionDate = Db.ParseD(r.GetString(4))
                };
            }
        }
        if (p == null) return null;

        using (var cmd = Db.Cmd(c,
            "SELECT DrugId, Dosage, UsageDate FROM PatientDrugs WHERE PatientId=@id ORDER BY Id", ("@id", id)))
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
                p.Items.Add(new PatientDrugItem { DrugId = r.GetInt64(0), Dosage = r.GetString(1), UsageDate = Db.ParseD(r.GetString(2)) });
        }
        return p;
    }

    /// <summary>Создаёт или обновляет карточку вместе со всеми строками препаратов (одной транзакцией).</summary>
    public static long SavePatient(Patient p)
    {
        using var c = Db.Open();
        using var tx = c.BeginTransaction();
        long id = p.Id;

        if (id == 0)
        {
            using var ins = Db.Cmd(c, tx,
                @"INSERT INTO Patients(FullName, BirthDate, DepartmentId, ExecutionDate)
                  VALUES(@n, @b, @d, @e); SELECT last_insert_rowid();",
                ("@n", p.FullName), ("@b", Db.D(p.BirthDate)), ("@d", p.DepartmentId), ("@e", Db.D(p.ExecutionDate)));
            id = (long)ins.ExecuteScalar()!;
        }
        else
        {
            using var upd = Db.Cmd(c, tx,
                @"UPDATE Patients SET FullName=@n, BirthDate=@b, DepartmentId=@d, ExecutionDate=@e WHERE Id=@id",
                ("@n", p.FullName), ("@b", Db.D(p.BirthDate)), ("@d", p.DepartmentId),
                ("@e", Db.D(p.ExecutionDate)), ("@id", id));
            upd.ExecuteNonQuery();

            using var del = Db.Cmd(c, tx, "DELETE FROM PatientDrugs WHERE PatientId=@id", ("@id", id));
            del.ExecuteNonQuery();
        }

        foreach (var it in p.Items)
        {
            using var ic = Db.Cmd(c, tx,
                "INSERT INTO PatientDrugs(PatientId, DrugId, Dosage, UsageDate) VALUES(@p, @d, @s, @u)",
                ("@p", id), ("@d", it.DrugId), ("@s", it.Dosage), ("@u", Db.D(it.UsageDate)));
            ic.ExecuteNonQuery();
        }

        tx.Commit();
        return id;
    }

    public static void DeletePatient(long id)
    {
        using var c = Db.Open();
        using var tx = c.BeginTransaction();
        using (var d1 = Db.Cmd(c, tx, "DELETE FROM PatientDrugs WHERE PatientId=@id", ("@id", id)))
            d1.ExecuteNonQuery();
        using (var d2 = Db.Cmd(c, tx, "DELETE FROM Patients WHERE Id=@id", ("@id", id)))
            d2.ExecuteNonQuery();
        tx.Commit();
    }

    public static DbStats GetStats()
    {
        using var c = Db.Open();
        int Count(string table)
        {
            using var cmd = Db.Cmd(c, $"SELECT COUNT(*) FROM {table}");
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
        return new DbStats(Count("Patients"), Count("PatientDrugs"), Count("Departments"), Count("Drugs"));
    }

    // ---------- Отчёты ----------

    static (string clause, (string, object?)[] ps) BuildWhere(ReportFilter f)
    {
        var parts = new List<string>();
        var ps = new List<(string, object?)>();
        if (f.From != null)
        {
            parts.Add("pd.UsageDate >= @from");
            ps.Add(("@from", Db.D(f.From.Value)));
        }
        if (f.To != null)
        {
            parts.Add("pd.UsageDate <= @to");
            ps.Add(("@to", Db.D(f.To.Value)));
        }
        if (f.DepartmentId != null)
        {
            parts.Add("p.DepartmentId = @dep");
            ps.Add(("@dep", f.DepartmentId.Value));
        }
        var clause = parts.Count == 0 ? "" : "WHERE " + string.Join(" AND ", parts);
        return (clause, ps.ToArray());
    }

    public static List<DetailRow> GetDetail(ReportFilter f)
    {
        var (clause, ps) = BuildWhere(f);
        using var c = Db.Open();
        using var cmd = Db.Cmd(c, $@"
            SELECT p.FullName, dep.Name, pd.UsageDate, dr.Name, pd.Dosage, COUNT(*)
              FROM PatientDrugs pd
              JOIN Patients p ON p.Id = pd.PatientId
              JOIN Departments dep ON dep.Id = p.DepartmentId
              JOIN Drugs dr ON dr.Id = pd.DrugId
              {clause}
             GROUP BY p.Id, dr.Id, pd.Dosage, pd.UsageDate
             ORDER BY pd.UsageDate, p.FullName, dr.Name", ps);
        using var r = cmd.ExecuteReader();
        var list = new List<DetailRow>();
        while (r.Read())
        {
            list.Add(new DetailRow
            {
                FullName = r.GetString(0),
                Department = r.GetString(1),
                Date = Db.ParseD(r.GetString(2)),
                Drug = r.GetString(3),
                Dosage = r.GetString(4),
                Count = r.GetInt32(5)
            });
        }
        return list;
    }

    public static List<TotalRow> GetTotals(ReportFilter f)
    {
        var (clause, ps) = BuildWhere(f);
        using var c = Db.Open();
        using var cmd = Db.Cmd(c, $@"
            SELECT dr.Name, COUNT(*)
              FROM PatientDrugs pd
              JOIN Patients p ON p.Id = pd.PatientId
              JOIN Drugs dr ON dr.Id = pd.DrugId
              {clause}
             GROUP BY dr.Id
             ORDER BY dr.Name", ps);
        using var r = cmd.ExecuteReader();
        var list = new List<TotalRow>();
        while (r.Read())
            list.Add(new TotalRow { Drug = r.GetString(0), Total = r.GetInt32(1) });
        return list;
    }


    // ---------- Пользователи ----------

    static AppUser ReadUser(SqliteDataReader r) => new()
    {
        Id = r.GetInt64(0),
        Login = r.GetString(1),
        Password = r.GetString(2),
        IsAdmin = r.GetInt64(3) != 0
    };

    /// <summary>Возвращает пользователя, если логин и пароль верны; иначе null.</summary>
    public static AppUser? FindUser(string login, string password)
    {
        using var c = Db.Open();
        using var cmd = Db.Cmd(c,
            "SELECT Id, Login, Password, IsAdmin FROM Users WHERE Login=@l AND Password=@p",
            ("@l", login.Trim()), ("@p", password));
        using var r = cmd.ExecuteReader();
        return r.Read() ? ReadUser(r) : null;
    }

    public static List<AppUser> GetUsers()
    {
        using var c = Db.Open();
        using var cmd = Db.Cmd(c,
            "SELECT Id, Login, Password, IsAdmin FROM Users ORDER BY IsAdmin DESC, Login");
        using var r = cmd.ExecuteReader();
        var list = new List<AppUser>();
        while (r.Read()) list.Add(ReadUser(r));
        return list;
    }

    static string CleanLogin(string? login)
    {
        var l = (login ?? "").Trim();
        if (l.Length < 2) throw new UserException("Логин должен содержать не меньше 2 символов.");
        if (l.Length > 30) throw new UserException("Логин слишком длинный (максимум 30 символов).");
        if (l.Any(char.IsWhiteSpace)) throw new UserException("Логин не должен содержать пробелов.");
        return l;
    }

    static string CheckPassword(string? password)
    {
        var p = password ?? "";
        if (p.Length < 4) throw new UserException("Пароль должен содержать не меньше 4 символов.");
        if (p.Length > 50) throw new UserException("Пароль слишком длинный (максимум 50 символов).");
        if (p.Trim().Length != p.Length) throw new UserException("Пароль не должен начинаться или заканчиваться пробелом.");
        return p;
    }

    public static void AddUser(string? login, string? password, bool isAdmin)
    {
        var l = CleanLogin(login);
        var p = CheckPassword(password);
        using var c = Db.Open();

        using (var chk = Db.Cmd(c, "SELECT COUNT(*) FROM Users WHERE Login=@l", ("@l", l)))
        {
            if ((long)chk.ExecuteScalar()! > 0)
                throw new UserException($"Пользователь «{l}» уже существует.");
        }

        using var ins = Db.Cmd(c, "INSERT INTO Users(Login, Password, IsAdmin) VALUES(@l, @p, @a)",
            ("@l", l), ("@p", p), ("@a", isAdmin ? 1 : 0));
        ins.ExecuteNonQuery();
    }

    public static void ChangePassword(long userId, string? newPassword)
    {
        var p = CheckPassword(newPassword);
        using var c = Db.Open();
        using var cmd = Db.Cmd(c, "UPDATE Users SET Password=@p WHERE Id=@id", ("@p", p), ("@id", userId));
        if (cmd.ExecuteNonQuery() == 0)
            throw new UserException("Пользователь не найден.");
    }

    public static void DeleteUser(long userId, long currentUserId)
    {
        if (userId == currentUserId)
            throw new UserException("Нельзя удалить пользователя, под которым вы вошли.");

        using var c = Db.Open();
        bool isAdmin;
        using (var q = Db.Cmd(c, "SELECT IsAdmin FROM Users WHERE Id=@id", ("@id", userId)))
        {
            var v = q.ExecuteScalar();
            if (v == null) throw new UserException("Пользователь не найден.");
            isAdmin = Convert.ToInt64(v) != 0;
        }

        if (isAdmin)
        {
            long admins;
            using (var q2 = Db.Cmd(c, "SELECT COUNT(*) FROM Users WHERE IsAdmin=1"))
                admins = (long)q2.ExecuteScalar()!;
            if (admins <= 1)
                throw new UserException("Нельзя удалить последнего администратора.");
        }

        using var del = Db.Cmd(c, "DELETE FROM Users WHERE Id=@id", ("@id", userId));
        del.ExecuteNonQuery();
    }
}
