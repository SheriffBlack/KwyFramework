using Kwy.UI.DataGrids;
using Kwy.UI.WPF.Controls;
using Kwy.UI.WPF.Controls.Helpers;
using Kwy.UI.WPF.Input;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Xunit;

namespace Kwy.UI.WPF.Tests;

public sealed class BehaviorTests
{
    [Fact]
    public void TextBoxKeyboardTarget_ReplacesSelectionAndEditsWithoutSystemInput()
        => RunInSta(() =>
        {
            var textBox = new TextBox { Text = "abcd" };
            textBox.Select(1, 2);
            var target = new TextBoxKeyboardInputTarget(textBox);

            target.HandleKey(KeyInput(Key.X), KeyboardLayout.Qwerty);
            Assert.Equal("axd", textBox.Text);
            Assert.Equal(2, textBox.CaretIndex);

            target.HandleKey(KeyInput(Key.Back), KeyboardLayout.Qwerty);
            Assert.Equal("ad", textBox.Text);
        });

    [Fact]
    public void TextBoxKeyboardTarget_AppliesShiftCapsAndLayout()
        => RunInSta(() =>
        {
            var textBox = new TextBox();
            var target = new TextBoxKeyboardInputTarget(textBox);

            target.HandleKey(KeyInput(Key.A, shift: true), KeyboardLayout.Qwerty);
            target.HandleKey(KeyInput(Key.B, capsLock: true), KeyboardLayout.Qwerty);
            target.HandleKey(KeyInput(Key.Q), KeyboardLayout.Azerty);

            Assert.Equal("ABa", textBox.Text);
        });

    [Fact]
    public void TextBoxKeyboardTarget_ReportsCommitAndCancel()
        => RunInSta(() =>
        {
            var target = new TextBoxKeyboardInputTarget(new TextBox());

            Assert.Equal(KeyboardInputResult.Commit, target.HandleKey(KeyInput(Key.Enter), KeyboardLayout.Qwerty));
            Assert.Equal(KeyboardInputResult.Cancel, target.HandleKey(KeyInput(Key.Escape), KeyboardLayout.Qwerty));
        });

    [Fact]
    public void NumericKeyboardTarget_UsesSeparateSignsAndEnforcesDecimalPlaces()
        => RunInSta(() =>
        {
            var textBox = new TextBox { Text = "12.3" };
            textBox.CaretIndex = textBox.Text.Length;
            var target = new TextBoxKeyboardInputTarget(
                textBox,
                new NumericKeyboardOptions(AllowDecimal: true, AllowNegative: true, DecimalPlaces: 2));

            target.HandleKey(KeyInput(Key.Subtract), KeyboardLayout.Qwerty);
            Assert.Equal("-12.3", textBox.Text);

            target.HandleKey(KeyInput(Key.Add), KeyboardLayout.Qwerty);
            Assert.Equal("+12.3", textBox.Text);

            target.HandleKey(KeyInput(Key.D4), KeyboardLayout.Qwerty);
            Assert.Equal("+12.34", textBox.Text);
            target.HandleKey(KeyInput(Key.D5), KeyboardLayout.Qwerty);
            Assert.Equal("+12.34", textBox.Text);
        });

    [Fact]
    public void IntegerKeyboardTarget_RejectsDecimalInput()
        => RunInSta(() =>
        {
            var textBox = new TextBox();
            var target = new TextBoxKeyboardInputTarget(
                textBox,
                new NumericKeyboardOptions(AllowDecimal: false, AllowNegative: false));

            target.HandleKey(KeyInput(Key.D1), KeyboardLayout.Qwerty);
            target.HandleKey(KeyInput(Key.Decimal), KeyboardLayout.Qwerty);
            target.HandleKey(KeyInput(Key.D5), KeyboardLayout.Qwerty);
            target.HandleKey(KeyInput(Key.Subtract), KeyboardLayout.Qwerty);

            Assert.Equal("15", textBox.Text);
        });

    [Fact]
    public void SoftKeyboardService_AcceptsTextBoxAndNumberBoxHosts()
        => RunInSta(() =>
        {
            var textBox = new TextBox();
            var numberBox = new KwyNumberBox();

            SoftKeyboardService.SetIsEnabled(textBox, true);
            SoftKeyboardService.SetMode(textBox, SoftKeyboardMode.Numeric);
            SoftKeyboardService.SetIsEnabled(numberBox, true);

            Assert.True(SoftKeyboardService.GetIsEnabled(textBox));
            Assert.Equal(SoftKeyboardMode.Numeric, SoftKeyboardService.GetMode(textBox));
            Assert.True(SoftKeyboardService.GetIsEnabled(numberBox));
        });

    [Fact]
    public void DynamicRow_ReadDoesNotCreateCell_AndCellsAreReadOnly()
    {
        var row = new DisplayRowItem();

        Assert.Null(row["missing"]);
        Assert.Empty(row.Cells);

        CellState cell = row.GetOrAddCell("result");
        cell.VisualState = CellValidationState.Success;

        Assert.Same(cell, row["result"]);
        Assert.Equal(CellValidationState.Success, row.Cells["result"].VisualState);
        Assert.IsAssignableFrom<IReadOnlyDictionary<string, CellState>>(row.Cells);
        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<string, CellState>)row.Cells).Add("blocked", new CellState()));
    }

    [Fact]
    public void ColumnsSource_CanBeSharedByTwoDataGrids()
        => RunInSta(() =>
        {
            var columns = new ObservableCollection<IDataGridColumnDescriptor> { Column("A") };
            var first = new DataGrid();
            var second = new DataGrid();

            DataGridColumnsHelper.SetColumnsSource(first, columns);
            DataGridColumnsHelper.SetColumnsSource(second, columns);
            columns.Add(Column("B"));
            DrainDispatcher();

            Assert.Equal(2, first.Columns.Count);
            Assert.Equal(2, second.Columns.Count);
        });

    [Fact]
    public void ColumnsSource_ReplacementAndUnbind_StopOldCollectionUpdates()
        => RunInSta(() =>
        {
            var oldColumns = new ObservableCollection<IDataGridColumnDescriptor> { Column("A") };
            var newColumns = new ObservableCollection<IDataGridColumnDescriptor> { Column("B") };
            var dataGrid = new DataGrid();

            DataGridColumnsHelper.SetColumnsSource(dataGrid, oldColumns);
            DataGridColumnsHelper.SetColumnsSource(dataGrid, newColumns);
            oldColumns.Add(Column("Old"));
            newColumns.Add(Column("New"));
            DrainDispatcher();
            Assert.Equal(2, dataGrid.Columns.Count);

            DataGridColumnsHelper.SetColumnsSource(dataGrid, null);
            newColumns.Add(Column("Detached"));
            DrainDispatcher();
            Assert.Empty(dataGrid.Columns);
        });

    [Fact]
    public void ColumnsSource_DoesNotRequireApplicationCurrent()
        => RunInSta(() =>
        {
            Assert.Null(Application.Current);
            var dataGrid = new DataGrid();
            DataGridColumnsHelper.SetColumnsSource(
                dataGrid,
                new ObservableCollection<IDataGridColumnDescriptor> { Column("A") });
            Assert.Single(dataGrid.Columns);
        });

    [Fact]
    public void ColumnsSource_WeakSubscription_DoesNotKeepClosedViewAlive()
        => RunInSta(() =>
        {
            var columns = new ObservableCollection<IDataGridColumnDescriptor> { Column("A") };
            WeakReference dataGridReference = CreateAndReleaseDataGrid(columns);

            for (int i = 0; i < 3 && dataGridReference.IsAlive; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                DrainDispatcher();
            }

            Assert.False(dataGridReference.IsAlive);
            columns.Add(Column("B"));
            DrainDispatcher();
        });

    [Fact]
    public void NumberBox_PreservesNegativeAndDecimalEditingStates()
        => RunInSta(() =>
        {
            var numberBox = CreateNumberBox(out TextBox editor);
            numberBox.Value = 12d;

            editor.Text = "-";
            Assert.Equal("-", editor.Text);
            Assert.Equal(12d, numberBox.Value);

            string separator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            editor.Text = $"-1{separator}";
            Assert.Equal($"-1{separator}", editor.Text);
        });

    [Theory]
    [InlineData("en-US", "-12.5", -12.5)]
    [InlineData("de-DE", "-12,5", -12.5)]
    public void NumberBox_ParsesCurrentCulture(string cultureName, string text, double expected)
        => RunInSta(() =>
        {
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
                var numberBox = CreateNumberBox(out TextBox editor);
                editor.Text = text;
                Invoke(numberBox, "CommitText");
                Assert.Equal(expected, Convert.ToDouble(numberBox.Value, CultureInfo.InvariantCulture), 8);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        });

    [Fact]
    public void NumberBox_ValueContract_IsNullableDouble()
        => RunInSta(() =>
        {
            Assert.Equal(typeof(double?), KwyNumberBox.ValueProperty.PropertyType);
            var numberBox = CreateNumberBox(out TextBox editor);
            editor.Text = "42.5";
            Assert.Equal(42.5d, numberBox.Value);
        });

    [Fact]
    public void NumberBox_RejectsInvalidPasteCandidate()
        => RunInSta(() =>
        {
            var numberBox = CreateNumberBox(out TextBox editor);
            editor.Text = "12";
            editor.SelectAll();

            var invalidPaste = new DataObjectPastingEventArgs(new DataObject(DataFormats.Text, "not-a-number"), false, DataFormats.Text);
            Invoke(numberBox, "OnPaste", editor, invalidPaste);
            Assert.True(invalidPaste.CommandCancelled);

            var validPaste = new DataObjectPastingEventArgs(new DataObject(DataFormats.Text, "-3.5"), false, DataFormats.Text);
            Invoke(numberBox, "OnPaste", editor, validPaste);
            Assert.False(validPaste.CommandCancelled);
        });

    [Fact]
    public void ToastOverflowAndUnload_ReleasePendingRemovalTasks()
        => RunInSta(() =>
        {
            var host = new KwyToastHost { MaxItems = 5, Duration = TimeSpan.FromHours(1) };
            for (int i = 0; i < 100; i++)
            {
                host.Show(i);
            }

            Assert.Equal(5, host.Items.Count);
            Assert.Equal(5, GetPendingRemovalCount(host));

            Invoke(host, "OnHostUnloaded", host, new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            Assert.Equal(0, GetPendingRemovalCount(host));
        });

    [Theory]
    [InlineData("Themes/LightTheme.xaml")]
    [InlineData("Themes/DarkTheme.xaml")]
    public void Theme_HasRequiredSemanticResources_AndLoadsControlTemplates(string source)
        => RunInSta(() =>
        {
            var resources = new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/Kwy.UI.WPF;component/{source}", UriKind.Absolute)
            };
            resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/Kwy.UI.WPF;component/Themes/Generic.xaml", UriKind.Absolute)
            });

            string[] requiredKeys =
            [
                "ControlBackgroundBrush", "ControlBorderBrush", "ControlForegroundBrush",
                "ControlHoverBackgroundBrush", "ControlPressedBackgroundBrush",
                "ControlDisabledBackgroundBrush", "ControlDisabledForegroundBrush"
            ];
            foreach (string key in requiredKeys)
            {
                Assert.NotNull(resources[key]);
            }

            var numberBox = new KwyNumberBox { Resources = resources };
            numberBox.ApplyTemplate();
            Assert.NotNull(numberBox.Template);

            var toastHost = new KwyToastHost { Resources = resources };
            toastHost.ApplyTemplate();
            Assert.NotNull(toastHost.Template);

            var numericKeyboard = new KwyKeyboard
            {
                Resources = resources,
                Mode = SoftKeyboardMode.Numeric
            };
            numericKeyboard.ApplyTemplate();
            var numericPanel = (Grid?)numericKeyboard.Template.FindName("PART_NumericKeysRoot", numericKeyboard);
            Assert.NotNull(numericPanel);
            Assert.Contains(numericPanel.Children.OfType<Button>(), button => Equals(button.Content, "+"));
            Assert.Contains(numericPanel.Children.OfType<Button>(), button => Equals(button.Content, "−"));
        });

    private static DataGridColumnDescriptor Column(string id)
        => new() { Key = id, Header = id, BindingPath = id };

    private static KeyboardKeyInvokedEventArgs KeyInput(Key key, bool shift = false, bool capsLock = false)
        => new(KwyKeyboard.KeyInvokedEvent, key, shift, false, false, capsLock);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateAndReleaseDataGrid(ObservableCollection<IDataGridColumnDescriptor> columns)
    {
        var dataGrid = new DataGrid();
        DataGridColumnsHelper.SetColumnsSource(dataGrid, columns);
        dataGrid.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        return new WeakReference(dataGrid);
    }

    private static KwyNumberBox CreateNumberBox(out TextBox editor)
    {
        var numberBox = new KwyNumberBox();
        var textBox = new TextBox();
        editor = textBox;
        typeof(KwyNumberBox).GetField("textBox", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(numberBox, textBox);
        textBox.TextChanged += (_, _) => Invoke(numberBox, "OnTextChanged", textBox, new TextChangedEventArgs(TextBox.TextChangedEvent, UndoAction.None));
        return numberBox;
    }

    private static int GetPendingRemovalCount(KwyToastHost host)
    {
        object dictionary = typeof(KwyToastHost)
            .GetField("pendingRemovals", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(host)!;
        return (int)dictionary.GetType().GetProperty("Count")!.GetValue(dictionary)!;
    }

    private static object? Invoke(object target, string method, params object?[] arguments)
        => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(target, arguments);

    private static void DrainDispatcher()
        => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);

    private static void RunInSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                error = exception is TargetInvocationException { InnerException: not null }
                    ? exception.InnerException
                    : exception;
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (error != null)
        {
            ExceptionDispatchInfo.Capture(error).Throw();
        }
    }
}
