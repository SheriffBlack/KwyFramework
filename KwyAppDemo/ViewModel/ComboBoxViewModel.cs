using Kwy.UI;
using System.Collections.ObjectModel;

namespace KwyAppDemo.ViewModel;

public class ComboBoxViewModel
{
    public ObservableCollection<string> ComboBoxItems { get; } = new ObservableCollection<string>
    {
        "Item 1",
        "Item 2",
        "Item 3",
        "Item 4",
        "Item 5"
    };
    public ObservableCollection<ComboBoxItem> ComboBoxIconItems { get; } = new ObservableCollection<ComboBoxItem>
    {
        new ComboBoxItem("Item 1", IconNames.IconAirspay),
        new ComboBoxItem("Item 2", IconNames.IconAnticlockwise),
        new ComboBoxItem("Item 3", IconNames.IconCalendar),
        new ComboBoxItem("Item 4", IconNames.IconCamera),
        new ComboBoxItem("Item 5", IconNames.IconChart),
    };
}

public record ComboBoxItem(string Text, string Icon);

