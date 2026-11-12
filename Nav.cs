using System.Windows;
using System.Windows.Controls;

namespace DrugUsageSimple;

/// <summary>Простая навигация: в окне всегда показана одна «страница».</summary>
public static class Nav
{
    public static Grid Host { get; set; } = null!;

    public static void Go(UserControl page)
    {
        Host.Children.Clear();
        Host.Children.Add(page);
    }

    public static void Menu() => Go(new Views.MainMenuPage());
}

public static class Dialogs
{
    public static bool Confirm(string text, string title = "Подтверждение") =>
        MessageBox.Show(text, title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No)
        == MessageBoxResult.Yes;

    public static void Info(string text, string title = "Готово") =>
        MessageBox.Show(text, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public static void Warn(string text, string title = "Внимание") =>
        MessageBox.Show(text, title, MessageBoxButton.OK, MessageBoxImage.Warning);
}
