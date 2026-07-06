using Kwy.UI.WPF.Behaviors;
using Kwy.UI.Enums;
using Microsoft.Xaml.Behaviors;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Kwy.UI.WPF.Controls;

/// <summary>
/// 自定义摆盘控件
/// </summary>
public class KwyTray : ItemsControl
{
    private UniformGrid? itemsPanel;
    private Canvas? selectionCanvas;

    public const string SelectionCanvasPartName = "PART_SelectionCanvas";

    static KwyTray()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(KwyTray),
            new FrameworkPropertyMetadata(typeof(KwyTray)));
    }

    public static readonly DependencyProperty RowsProperty =
        DependencyProperty.Register(nameof(Rows), typeof(int), typeof(KwyTray),
            new PropertyMetadata(5, OnRowsOrColumnsChanged));

    public static readonly DependencyProperty ColumnsProperty =
        DependencyProperty.Register(nameof(Columns), typeof(int), typeof(KwyTray),
            new PropertyMetadata(10, OnRowsOrColumnsChanged));

    public int Rows
    {
        get => (int)GetValue(RowsProperty);
        set => SetValue(RowsProperty, value);
    }

    public int Columns
    {
        get => (int)GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    private static void OnRowsOrColumnsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is KwyTray tray)
        {
            tray.UpdateUniformGrid();
        }
    }

    #region 数据结构定义

    /// <summary>
    /// 盘孔项接口，用于 KwyTray 控件
    /// </summary>
    public interface ITrayItem
    {
        /// <summary>
        /// 盘孔状态
        /// </summary>
        TrayItemStatus Status { get; set; }

        /// <summary>
        /// 是否被选中
        /// </summary>
        bool IsSelected { get; set; }

        /// <summary>
        /// 是否有料（兼容旧代码，基于 Status 属性）
        /// </summary>
        bool HasMaterial { get; set; }
    }

    public class TrayItem : INotifyPropertyChanged, ITrayItem
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private TrayItemStatus status = TrayItemStatus.NoMaterial;

        public TrayItemStatus Status
        {
            get => status;
            set
            {
                if (status != value)
                {
                    status = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasMaterial));
                }
            }
        }

        private bool isSelected;

        public bool IsSelected
        {
            get => isSelected;
            set
            {
                if (isSelected != value)
                {
                    isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        // 兼容旧代码：HasMaterial 基于 Status
        public bool HasMaterial
        {
            get => Status == TrayItemStatus.OK || Status == TrayItemStatus.NG;
            set
            {
                if (value)
                {
                    // 如果设置为有料，默认设为 OK
                    if (Status == TrayItemStatus.NoMaterial)
                    {
                        Status = TrayItemStatus.OK;
                    }
                }
                else
                {
                    // 如果设置为无料
                    Status = TrayItemStatus.NoMaterial;
                }
            }
        }

        /// <summary>
        /// 触发属性变更事件
        /// </summary>
        /// <param name="propertyName">属性名</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    #endregion 数据结构定义

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        // 查找选择框装饰层 Canvas
        selectionCanvas = GetTemplateChild(SelectionCanvasPartName) as Canvas;

        // 添加选择行为
        var behaviors = Interaction.GetBehaviors(this);
        if (behaviors.OfType<KwyTraySelectBehavior>().FirstOrDefault() == null)
        {
            behaviors.Add(new KwyTraySelectBehavior());
        }

        // 延迟查找，等待面板创建
        Dispatcher.BeginInvoke(new Action(() => UpdateUniformGrid()),
            System.Windows.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>
    /// 获取选择框装饰层 Canvas（供行为使用）
    /// </summary>
    public Canvas? GetSelectionCanvas() => selectionCanvas;

    protected override void OnItemsPanelChanged(ItemsPanelTemplate oldItemsPanel, ItemsPanelTemplate newItemsPanel)
    {
        base.OnItemsPanelChanged(oldItemsPanel, newItemsPanel);
        itemsPanel = null; // 重置缓存
        // 延迟查找，等待面板创建
        Dispatcher.BeginInvoke(new Action(() => UpdateUniformGrid()),
            System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void UpdateUniformGrid()
    {
        if (itemsPanel == null)
        {
            // 查找 ItemsPresenter 中的 UniformGrid
            var itemsPresenter = FindVisualChild<ItemsPresenter>(this);
            if (itemsPresenter != null)
            {
                itemsPanel = FindVisualChild<UniformGrid>(itemsPresenter);
            }
        }

        if (itemsPanel != null)
        {
            itemsPanel.Rows = Rows;
            itemsPanel.Columns = Columns;
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null) return null;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T result)
            {
                return result;
            }
            var childOfChild = FindVisualChild<T>(child);
            if (childOfChild != null)
            {
                return childOfChild;
            }
        }
        return null;
    }

    protected override DependencyObject GetContainerForItemOverride() => new ContentPresenter();

    protected override bool IsItemItsOwnContainerOverride(object item) => false;
}