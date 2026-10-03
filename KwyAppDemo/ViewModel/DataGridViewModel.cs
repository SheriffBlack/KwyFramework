using Kwy.UI.DataGrids;
using System.Collections.ObjectModel;

namespace KwyAppDemo.ViewModel;

public class DataGridViewModel
{
    public ObservableCollection<IDataGridColumnDescriptor> ColumnNames { get; } =
    [
        new DataGridColumnDescriptor
        {
            Key = nameof(User.Id),
            Header = "Id",
            BindingPath = nameof(User.Id)
        },
        new DataGridColumnDescriptor
        {
            Key = nameof(User.Name),
            Header = "Name",
            BindingPath = nameof(User.Name)
        },
        new DataGridColumnDescriptor
        {
            Key = nameof(User.Age),
            Header = "Age",
            BindingPath = nameof(User.Age)
        },
        new DataGridColumnDescriptor
        {
            Key = nameof(User.Email),
            Header = "Email",
            BindingPath = nameof(User.Email)
        }
    ];

    public ObservableCollection<User> DataGridItems { get; } =
    [
        new User(1, "Alice", 25, "alice@example.com"),
        new User(2, "Bob", 30, "bob@example.com"),
        new User(3, "Charlie", 35, "charlie@example.com"),
        new User(4, "Diana", 28, "diana@example.com"),
        new User(5, "Eve", 32, "eve@example.com"),
        new User(6, "Frank", 27, "frank@example.com"),
        new User(7, "Grace", 29, "grace@example.com"),
        new User(8, "Henry", 31, "henry@example.com"),
    ];

}

public record User(int Id, string Name, int Age, string Email);
