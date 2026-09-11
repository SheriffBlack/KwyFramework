using KwyAppDemo.Views;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace KwyAppDemo;

public class MainViewModel : INotifyPropertyChanged
{

    public MainViewModel()
    {
        SelectedItem = BasicControlCatalog[0];
    }

    public IReadOnlyList<NavItem> BasicControlCatalog { get; } = [
            new("Button",  new ButtonView()),
            new("CheckBox", new CheckBoxView()),
            new("ComboBox",  new ComboBoxView()),
            new("DataGrid",  new DataGridView())
        ];

    private NavItem? selectedItem;

    public NavItem? SelectedItem
    {
        get => selectedItem;
        set
        {
            if (ReferenceEquals(selectedItem, value))
            {
                return;
            }

            selectedItem = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}


public sealed class NavItem
{
    public NavItem(string title, UserControl view, object? icon = null)
    {
        Title = title;
        View = view;
        Icon = icon;
    }

    public string Title { get; }
    public UserControl View { get; }
    public object? Icon { get; }
}