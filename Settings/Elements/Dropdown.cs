using Sandbox.Graphics.GUI;
using System;
using System.Collections.Generic;

namespace GradientHelper.Settings.Elements;

internal class DropdownAttribute : Attribute, IElement
{
    public readonly string Label;

    public DropdownAttribute(string label = null)
    {
        Label = label;
    }

    public List<Type> SupportedTypes => new List<Type> { typeof(Enum) };

    public List<Control> GetControls(string name, Func<object> propertyGetter, Action<object> propertySetter)
    {
        var label = new MyGuiControlLabel(text: Tools.Tools.GetLabelOrDefault(name, Label));

        var enumType = propertyGetter().GetType();
        var values = Enum.GetValues(enumType);

        var combobox = new MyGuiControlCombobox();
        foreach (Enum value in values)
        {
            combobox.AddItem(Convert.ToInt64(value), value.ToString());
        }
        combobox.SelectItemByKey(Convert.ToInt64(propertyGetter()));
        combobox.ItemSelected += () =>
        {
            propertySetter(Enum.ToObject(enumType, combobox.GetSelectedKey()));
        };

        return new List<Control>
        {
            new Control(label, minWidth: Control.LabelMinWidth),
            new Control(combobox),
        };
    }
}
