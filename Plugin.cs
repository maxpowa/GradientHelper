using System.Reflection;
using HarmonyLib;
using Sandbox.Game.Entities;
using Sandbox.Graphics.GUI;
using Sandbox.ModAPI;
using GradientHelper.Settings;
using GradientHelper.Settings.Layouts;
using VRage.Game.ModAPI;
using VRage.Input;
using VRage.Plugins;
using VRage.Utils;
using VRageMath;

namespace GradientHelper;

public class Plugin : IPlugin
{
    public const string Name = "GradientHelper";
    public static Plugin Instance { get; private set; }
    private SettingsGenerator settingsGenerator;
    private static Harmony harmony;

    public void Init(object gameInstance)
    {
        Instance = this;
        Instance.settingsGenerator = new SettingsGenerator();
        PatchColorBlocks();

        GradientHelper.RecolorBlock = (grid, block, hsvOffset) =>
        {
            var skin = Config.Current.ApplySkin ? GradientHelper.SelectedSkin : (MyStringHash?)null;
            changeColorAndSkinMethod?.Invoke(grid,
                new object[] { block, (Vector3?)hsvOffset, skin });
        };
    }

    public static bool Patched { get; private set; }
    private static MethodInfo changeColorAndSkinMethod;

    private static void PatchColorBlocks()
    {
        harmony = new Harmony("com.gradienthelper.plugin");

        var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        var gridType = typeof(MyCubeGrid);

        // Find ChangeColorAndSkin for direct invocation during gradient painting
        foreach (var method in gridType.GetMethods(flags))
        {
            if (method.Name == "ChangeColorAndSkin")
            {
                var parms = method.GetParameters();
                if (parms.Length >= 2 && parms[0].ParameterType.Name == "MySlimBlock")
                {
                    changeColorAndSkinMethod = method;
                    break;
                }
            }
        }

        // Patch SkinBlocks — called in the paint flow with typed Vector3I min/max and Nullable<Vector3> newHSV
        foreach (var method in gridType.GetMethods(flags))
        {
            if (method.Name != "SkinBlocks")
                continue;

            var parms = method.GetParameters();
            if (parms.Length >= 3 && parms[0].ParameterType == typeof(Vector3I) && parms[1].ParameterType == typeof(Vector3I))
            {
                try
                {
                    harmony.Patch(method, prefix: new HarmonyMethod(
                        typeof(Plugin).GetMethod(nameof(SkinBlocksPrefix),
                            BindingFlags.NonPublic | BindingFlags.Static)));
                    Patched = true;
                    VRage.Utils.MyLog.Default.WriteLineAndConsole(
                        "[GradientHelper] Patched SkinBlocks successfully");
                }
                catch (System.Exception ex)
                {
                    VRage.Utils.MyLog.Default.WriteLineAndConsole(
                        $"[GradientHelper] Failed to patch SkinBlocks: {ex}");
                }
                break;
            }
        }
    }

    private static bool SkinBlocksPrefix(MyCubeGrid __instance, Vector3I min, Vector3I max, ref Vector3? newHSV, ref MyStringHash? newSkin)
    {
        try
        {
            if (GradientHelper.State == GradientState.Idle || newHSV == null)
                return true;

            GradientHelper.SelectedSkin = newSkin;

            // Reset if painting a different grid
            if (GradientHelper.ActiveGrid != null && (IMyCubeGrid)__instance != GradientHelper.ActiveGrid)
            {
                GradientHelper.Reset();
                GradientHelper.NotifyDifferentGrid();
                return true;
            }

            switch (GradientHelper.State)
            {
                case GradientState.WaitingForStart:
                    GradientHelper.OnBlockPicked(min, __instance);
                    return false; // block the paint — just picking

                case GradientState.WaitingForEnd:
                    GradientHelper.OnBlockPicked(min, __instance);
                    return false; // block the paint — just picking

                case GradientState.Painting:
                    // Apply gradient colors per-block in the range
                    if (GradientHelper.RecolorBlock != null)
                    {
                        for (var pos = min; pos.X <= max.X; pos.X++)
                        for (pos.Y = min.Y; pos.Y <= max.Y; pos.Y++)
                        for (pos.Z = min.Z; pos.Z <= max.Z; pos.Z++)
                        {
                            var block = __instance.GetCubeBlock(pos);
                            if (block == null)
                                continue;

                            float t = GradientHelper.ComputeGradientParameter(pos);
                            var gradientHSV = GradientHelper.LerpHSV(
                                GradientHelper.StartColorHSV, GradientHelper.EndColorHSV, t);

                            GradientHelper.RecolorBlock(__instance, block, gradientHSV);
                        }
                    }
                    return false; // skip the original — we've applied per-block colors
            }
        }
        catch (System.Exception ex)
        {
            VRage.Utils.MyLog.Default.WriteLineAndConsole(
                $"[GradientHelper] SkinBlocksPrefix error: {ex}");
        }
        return true;
    }

    public void Update()
    {
        var input = MyInput.Static;
        if (input == null)
            return;

        if (MyAPIGateway.Session == null)
        {
            if (GradientHelper.State != GradientState.Idle)
                GradientHelper.Reset();
            return;
        }

        GradientHelper.Update();

        if (MyAPIGateway.Gui.IsCursorVisible || MyAPIGateway.Gui.ChatEntryVisible)
            return;

        var config = Config.Current;

        if (config.ToggleGradient.HasPressed(input))
            GradientHelper.ToggleGradientMode();

        if (config.ToggleRadialGradient.HasPressed(input))
            GradientHelper.ToggleRadialGradientMode();

        if (config.CycleInterpolation.HasPressed(input))
            GradientHelper.CycleInterpolationMode();

        if (config.ToggleDebugDraw.HasPressed(input))
            GradientHelper.ToggleDebugDraw();

        if (config.ToggleApplySkin.HasPressed(input))
            GradientHelper.ToggleApplySkin();
    }

    public void Dispose()
    {
        harmony?.UnpatchAll("com.gradienthelper.plugin");
        GradientHelper.Reset();
        Instance = null;
    }

    public void OpenConfigDialog()
    {
        Instance.settingsGenerator.SetLayout<Simple>();
        MyGuiSandbox.AddScreen(Instance.settingsGenerator.Dialog);
    }

}
