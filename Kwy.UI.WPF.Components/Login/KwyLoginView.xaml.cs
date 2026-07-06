using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Kwy.UI.WPF.Components.Login;

public partial class KwyLoginView : UserControl
{
    public static readonly DependencyProperty TitleTextProperty =
        DependencyProperty.Register(
            nameof(TitleText),
            typeof(string),
            typeof(KwyLoginView),
            new PropertyMetadata("用户登录"));

    public static readonly DependencyProperty LoginButtonTextProperty =
        DependencyProperty.Register(
            nameof(LoginButtonText),
            typeof(string),
            typeof(KwyLoginView),
            new PropertyMetadata("登录"));

    public static readonly DependencyProperty UserNameProperty =
        DependencyProperty.Register(
            nameof(UserName),
            typeof(string),
            typeof(KwyLoginView),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty PasswordProperty =
        DependencyProperty.Register(
            nameof(Password),
            typeof(string),
            typeof(KwyLoginView),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(
            nameof(Message),
            typeof(string),
            typeof(KwyLoginView),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty UserNameItemsSourceProperty =
        DependencyProperty.Register(
            nameof(UserNameItemsSource),
            typeof(IEnumerable),
            typeof(KwyLoginView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty LoginCommandProperty =
        DependencyProperty.Register(
            nameof(LoginCommand),
            typeof(ICommand),
            typeof(KwyLoginView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty LoginCommandParameterProperty =
        DependencyProperty.Register(
            nameof(LoginCommandParameter),
            typeof(object),
            typeof(KwyLoginView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty FormWidthProperty =
        DependencyProperty.Register(
            nameof(FormWidth),
            typeof(double),
            typeof(KwyLoginView),
            new PropertyMetadata(500.0));

    public static readonly DependencyProperty FormMinHeightProperty =
        DependencyProperty.Register(
            nameof(FormMinHeight),
            typeof(double),
            typeof(KwyLoginView),
            new PropertyMetadata(500.0));

    public static readonly DependencyProperty FormPaddingProperty =
        DependencyProperty.Register(
            nameof(FormPadding),
            typeof(Thickness),
            typeof(KwyLoginView),
            new PropertyMetadata(new Thickness(70, 48, 70, 48)));

    public static readonly DependencyProperty InputWidthProperty =
        DependencyProperty.Register(
            nameof(InputWidth),
            typeof(double),
            typeof(KwyLoginView),
            new PropertyMetadata(300.0));

    public static readonly DependencyProperty InputHeightProperty =
        DependencyProperty.Register(
            nameof(InputHeight),
            typeof(double),
            typeof(KwyLoginView),
            new PropertyMetadata(45.0));

    public static readonly DependencyProperty ButtonHeightProperty =
        DependencyProperty.Register(
            nameof(ButtonHeight),
            typeof(double),
            typeof(KwyLoginView),
            new PropertyMetadata(50.0));

    public KwyLoginView()
    {
        InitializeComponent();
    }

    public string TitleText
    {
        get => (string)GetValue(TitleTextProperty);
        set => SetValue(TitleTextProperty, value);
    }

    public string LoginButtonText
    {
        get => (string)GetValue(LoginButtonTextProperty);
        set => SetValue(LoginButtonTextProperty, value);
    }

    public string UserName
    {
        get => (string)GetValue(UserNameProperty);
        set => SetValue(UserNameProperty, value);
    }

    public string Password
    {
        get => (string)GetValue(PasswordProperty);
        set => SetValue(PasswordProperty, value);
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public IEnumerable? UserNameItemsSource
    {
        get => (IEnumerable?)GetValue(UserNameItemsSourceProperty);
        set => SetValue(UserNameItemsSourceProperty, value);
    }

    public ICommand? LoginCommand
    {
        get => (ICommand?)GetValue(LoginCommandProperty);
        set => SetValue(LoginCommandProperty, value);
    }

    public object? LoginCommandParameter
    {
        get => GetValue(LoginCommandParameterProperty);
        set => SetValue(LoginCommandParameterProperty, value);
    }

    public double FormWidth
    {
        get => (double)GetValue(FormWidthProperty);
        set => SetValue(FormWidthProperty, value);
    }

    public double FormMinHeight
    {
        get => (double)GetValue(FormMinHeightProperty);
        set => SetValue(FormMinHeightProperty, value);
    }

    public Thickness FormPadding
    {
        get => (Thickness)GetValue(FormPaddingProperty);
        set => SetValue(FormPaddingProperty, value);
    }

    public double InputWidth
    {
        get => (double)GetValue(InputWidthProperty);
        set => SetValue(InputWidthProperty, value);
    }

    public double InputHeight
    {
        get => (double)GetValue(InputHeightProperty);
        set => SetValue(InputHeightProperty, value);
    }

    public double ButtonHeight
    {
        get => (double)GetValue(ButtonHeightProperty);
        set => SetValue(ButtonHeightProperty, value);
    }
}
