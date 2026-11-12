using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DrugUsageSimple.Views;

/// <summary>Редактор одного справочника: список слева, поле названия и кнопки.</summary>
public partial class ReferenceEditor : UserControl
{
    RefKind _kind;
    string _noun = "";

    public ReferenceEditor() => InitializeComponent();

    public void Init(RefKind kind, string title, string noun)
    {
        _kind = kind;
        _noun = noun;
        TitleText.Text = title;
        InputLabel.Text = $"Название ({noun}). Выберите запись в списке, чтобы изменить или удалить её";
        Reload();
    }

    RefItem? Selected => RefList.SelectedItem as RefItem;

    void Reload() => RefList.ItemsSource = Repo.GetRefs(_kind);

    void Say(string text, bool error)
    {
        StatusText.Text = text;
        StatusText.Foreground = (Brush)FindResource(error ? "DangerBrush" : "SuccessBrush");
    }

    void RefList_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        NameBox.Text = Selected?.Name ?? "";

    void Add_Click(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        try
        {
            Repo.AddRef(_kind, name);
            Reload();
            NameBox.Clear();
            Say($"Добавлено: «{name}».", false);
        }
        catch (UserException ex)
        {
            Say(ex.Message, true);
        }
    }

    void Rename_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } item)
        {
            Say($"Выберите {_noun} в списке, чтобы изменить его название.", true);
            return;
        }
        var name = NameBox.Text.Trim();
        try
        {
            Repo.RenameRef(_kind, item.Id, name);
            Reload();
            NameBox.Clear();
            Say($"Сохранено: «{name}».", false);
        }
        catch (UserException ex)
        {
            Say(ex.Message, true);
        }
    }

    void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } item)
        {
            Say($"Выберите {_noun} в списке, чтобы удалить его.", true);
            return;
        }
        if (!Dialogs.Confirm($"Удалить «{item.Name}» из справочника?")) return;

        try
        {
            Repo.DeleteRef(_kind, item.Id);
            Reload();
            NameBox.Clear();
            Say($"Удалено: «{item.Name}».", false);
        }
        catch (UserException ex)
        {
            Say(ex.Message, true);
        }
    }
}
