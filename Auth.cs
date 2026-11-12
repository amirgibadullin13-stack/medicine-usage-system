namespace DrugUsageSimple;

/// <summary>Текущий вошедший пользователь.</summary>
public static class Auth
{
    public static AppUser? Current { get; set; }
    public static bool IsAdmin => Current?.IsAdmin == true;
}
