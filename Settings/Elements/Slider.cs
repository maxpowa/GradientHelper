using Sandbox.Graphics.GUI;
using System;
using System.Collections.Generic;
using System.Text;
using VRageMath;

namespace GradientHelper.Settings.Elements;

internal class SliderAttribute : Attribute, IElement
{
    public readonly string Label;
    public readonly float Min;
    public readonly float Max;

    public SliderAttribute(float min, float max, string label = null)
    {
        Label = label;
        Min = min;
        Max = max;
    }

    public List<Type> SupportedTypes => new List<Type> { typeof(float) };

    public List<Control> GetControls(string name, Func<object> propertyGetter, Action<object> propertySetter)
    {
        var label = new MyGuiControlLabel(text: Tools.Tools.GetLabelOrDefault(name, Label));

        var value = (float)propertyGetter();
        var slider = new MyGuiControlSlider(
            minValue: Min,
            maxValue: Max,
            defaultValue: value);
        slider.Value = value;

        var valueLabel = new MyGuiControlLabel(text: value.ToString("F0"));

        slider.ValueChanged += (s) =>
        {
            propertySetter(s.Value);
            valueLabel.Text = s.Value.ToString("F0");
        };

        return new List<Control>
        {
            new Control(label, minWidth: Control.LabelMinWidth),
            new Control(slider, fillFactor: 1f),
            new Control(valueLabel, fixedWidth: 0.05f),
        };
    }
}
