using Sandbox.Graphics.GUI;
using System;
using System.Collections.Generic;

namespace GradientHelper.Settings.Elements;

internal class CheckboxAttribute : Attribute, IElement
{
    public readonly string Label;

    public CheckboxAttribute(string label = null)
    {
        Label = label;
    }

    public List<Type> SupportedTypes => new List<Type> { typeof(bool) };

    public List<Control> GetControls(string name, Func<object> propertyGetter, Action<object> propertySetter)
    {
        var label = new MyGuiControlLabel(text: Tools.Tools.GetLabelOrDefault(name, Label));

        var checkbox = new MyGuiControlCheckbox(isChecked: (bool)propertyGetter());
        checkbox.IsCheckedChanged += (cb) => propertySetter(cb.IsChecked);

        return new List<Control>
        {
            new Control(label, minWidth: Control.LabelMinWidth),
            new Control(checkbox),
        };
    }
}
