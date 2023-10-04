using System;
using System.Reactive.Linq;
using System.Windows.Input;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

using DynamicData.Binding;

namespace TestingEO.Views;

public partial class ControlPTZView : UserControl
{
    /// <summary>
    /// Identifies the <seealso cref="Command"/> avalonia property.
    /// </summary>
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<ControlPTZView, ICommand?>(nameof(Command));

    /// <summary>
    /// Gets or sets the command this action should invoke. This is a avalonia property.
    /// </summary>
    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public static readonly DirectProperty<ControlPTZView, String> TextProperty =
        AvaloniaProperty.RegisterDirect<ControlPTZView, String>(nameof(Text), o => o.Text, (o, v) => o.Text = v);
    private String _text;
    public String Text
    {
        get => _text;
        set => SetAndRaise(TextProperty, ref _text, value);
    }

    public static readonly DirectProperty<ControlPTZView, String> CmdNProperty =
        AvaloniaProperty.RegisterDirect<ControlPTZView, String>(nameof(CmdN), o => o.CmdN, (o, v) => o.CmdN = v);
    private String _cmdN;
    public String CmdN
    {
        get => _cmdN;
        set => SetAndRaise(CmdNProperty, ref _cmdN, value);
    }

    public static readonly DirectProperty<ControlPTZView, String> CmdPProperty =
        AvaloniaProperty.RegisterDirect<ControlPTZView, String>(nameof(CmdP), o => o.CmdP, (o, v) => o.CmdP = v);
    private String _cmdP;
    public String CmdP
    {
        get => _cmdP;
        set => SetAndRaise(CmdPProperty, ref _cmdP, value);
    }

    public static readonly DirectProperty<ControlPTZView, String> CmdSProperty =
        AvaloniaProperty.RegisterDirect<ControlPTZView, String>(nameof(CmdS), o => o.CmdS, (o, v) => o.CmdS = v);
    private String _cmdS;
    public String CmdS
    {
        get => _cmdS;
        set => SetAndRaise(CmdSProperty, ref _cmdS, value);
    }

    public static readonly DirectProperty<ControlPTZView, int> CameraProperty =
        AvaloniaProperty.RegisterDirect<ControlPTZView, int>(nameof(Camera), o => o.Camera, (o, v) => o.Camera = v,
            defaultBindingMode: Avalonia.Data.BindingMode.OneWay);
    private int _camera;
    public int Camera
    {
        get => _camera;
        set => SetAndRaise(CameraProperty, ref _camera, value + 1);
    }

    public static readonly DirectProperty<ControlPTZView, int> MinProperty =
        AvaloniaProperty.RegisterDirect<ControlPTZView, int>(nameof(Min), o => o.Min, (o, v) => o.Min = v,
            defaultBindingMode: Avalonia.Data.BindingMode.OneWay);
    private int _min;
    public int Min
    {
        get => _min;
        set => SetAndRaise(MinProperty, ref _min, value);
    }

    public static readonly DirectProperty<ControlPTZView, int> MaxProperty =
        AvaloniaProperty.RegisterDirect<ControlPTZView, int>(nameof(Max), o => o.Max, (o, v) => o.Max = v,
            defaultBindingMode: Avalonia.Data.BindingMode.OneWay);
    private int _max;
    public int Max
    {
        get => _max;
        set => SetAndRaise(MaxProperty, ref _max, value);
    }

    public ControlPTZView()
    {
        InitializeComponent();

        // monitor the changes
        Slider.WhenPropertyChanged(t => t.Value, notifyOnInitialValue: false)
            .Throttle(TimeSpan.FromSeconds(0.2))
            .ObserveOn(ReactiveUI.RxApp.MainThreadScheduler)
            .Subscribe(v => SliderValueChanged(v.Value));
    }

    private void SliderValueChanged(double value)
    {
        if (Command is null) return;
        Command.Execute($"{CmdS}@CamId={Camera}@{value}");
    }
}