using GradientHelper.Settings;
using GradientHelper.Settings.Elements;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using VRage.Input;

using Binding = GradientHelper.Settings.Tools.Binding;

namespace GradientHelper;

public class Config : INotifyPropertyChanged
{
    #region Options

    private Binding toggleGradient = new Binding(MyKeys.G, ctrl: true, shift: false);
    private Binding toggleRadialGradient = new Binding(MyKeys.R, ctrl: true, shift: false);
    private Binding cycleInterpolation = new Binding(MyKeys.T, ctrl: true, shift: false);
    private Binding toggleDebugDraw = new Binding(MyKeys.None);
    private Binding toggleApplySkin = new Binding(MyKeys.K, ctrl: true);
    private float maxDistance = 200f;
    private bool applySkin = true;
    private bool debugDraw = false;
    private InterpolationMode interpolation = InterpolationMode.RGB;

    #endregion

    #region User interface

    public readonly string Title = "Gradient Helper";

    [Separator("Gradient Settings")]

    [Slider(10f, 500f, label: "Auto-Reset Distance")]
    public float MaxDistance
    {
        get => maxDistance;
        set => SetField(ref maxDistance, value);
    }

    [Dropdown(label: "Interpolation Mode")]
    public InterpolationMode Interpolation
    {
        get => interpolation;
        set => SetField(ref interpolation, value);
    }

    [Checkbox(label: "Apply Skin")]
    public bool ApplySkin
    {
        get => applySkin;
        set => SetField(ref applySkin, value);
    }

    [Checkbox(label: "Debug Draw")]
    public bool DebugDraw
    {
        get => debugDraw;
        set => SetField(ref debugDraw, value);
    }

    [Separator("Hotkeys")]

    [Keybind(description: "Toggle gradient painting mode")]
    public Binding ToggleGradient
    {
        get => toggleGradient;
        set => SetField(ref toggleGradient, value);
    }

    [Keybind(description: "Toggle radial gradient painting mode")]
    public Binding ToggleRadialGradient
    {
        get => toggleRadialGradient;
        set => SetField(ref toggleRadialGradient, value);
    }

    [Keybind(description: "Next interpolation mode")]
    public Binding CycleInterpolation
    {
        get => cycleInterpolation;
        set => SetField(ref cycleInterpolation, value);
    }

    [Keybind(description: "Toggle debug drawing")]
    public Binding ToggleDebugDraw
    {
        get => toggleDebugDraw;
        set => SetField(ref toggleDebugDraw, value);
    }

    [Keybind(description: "Toggle apply skin")]
    public Binding ToggleApplySkin
    {
        get => toggleApplySkin;
        set => SetField(ref toggleApplySkin, value);
    }

    #endregion

    #region Property change notification boilerplate

    public static readonly Config Default = new Config();
    public static readonly Config Current = ConfigStorage.Load();

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    #endregion
}
