using Sandbox.Graphics.GUI;
using System;
using System.Collections.Generic;
using GradientHelper.Settings.Elements;
using VRageMath;

using Control = GradientHelper.Settings.Elements.Control;

namespace GradientHelper.Settings.Layouts;

internal abstract class Layout
{
    public abstract Vector2 SettingsPanelSize { get; }

    protected readonly Func<List<List<Control>>> GetControls;

    protected Layout(Func<List<List<Control>>> getControls)
    {
        GetControls = getControls;
    }

    public abstract List<MyGuiControlBase> RecreateControls();
    public abstract void LayoutControls();
}
