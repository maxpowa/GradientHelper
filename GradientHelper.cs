using System;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace GradientHelper
{
    public enum GradientState
    {
        Idle,
        WaitingForStart,
        WaitingForEnd,
        Painting,
    }

    public enum GradientMode
    {
        Linear,
        Radial,
    }

    public enum InterpolationMode
    {
        RGB,
        HSV,
        Lab,
        CubicLab,
        Perceptual,
    }

    public static class GradientHelper
    {
        private static readonly int InterpolationModeCount =
            Enum.GetValues(typeof(InterpolationMode)).Length;
        public static GradientState State { get; private set; } = GradientState.Idle;
        public static GradientMode Mode { get; private set; } = GradientMode.Linear;

        public static IMyCubeGrid ActiveGrid { get; private set; }
        private static Vector3I startPos;
        private static Vector3I endPos;
        public static Vector3 StartColorHSV { get; private set; }
        public static Vector3 EndColorHSV { get; private set; }

        /// <summary>
        /// Callback set by Plugin to recolor a single block on a grid.
        /// Invoked via reflection on MyCubeGrid.ChangeColorAndSkin.
        /// </summary>
        public static Action<MyCubeGrid, IMySlimBlock, Vector3> RecolorBlock;

        private static int lastPickFrame = -1;
        public static bool DebugDrawEnabled => Config.Current.DebugDraw;
        public static MyStringHash? SelectedSkin { get; set; }

        public static void ToggleGradientMode()
        {
            ToggleMode(GradientMode.Linear);
        }

        public static void ToggleRadialGradientMode()
        {
            ToggleMode(GradientMode.Radial);
        }

        public static void ToggleDebugDraw()
        {
            Config.Current.DebugDraw = !Config.Current.DebugDraw;
            ShowMessage(Config.Current.DebugDraw ? "Debug draw ON." : "Debug draw OFF.");
        }

        public static void ToggleApplySkin()
        {
            Config.Current.ApplySkin = !Config.Current.ApplySkin;
            ShowMessage(Config.Current.ApplySkin ? "Apply skin ON." : "Apply skin OFF.");
        }

        public static void CycleInterpolationMode()
        {
            var current = Config.Current.Interpolation;
            var next = (InterpolationMode)(((int)current + 1) % InterpolationModeCount);
            Config.Current.Interpolation = next;
            ShowMessage($"Interpolation: {next}");
        }

        private static void ToggleMode(GradientMode mode)
        {
            if (State != GradientState.Idle)
            {
                Reset();
                ShowMessage("Gradient mode OFF.");
                return;
            }

            Mode = mode;
            State = GradientState.WaitingForStart;
            var label = mode == GradientMode.Radial ? "Radial gradient" : "Linear gradient";
            ShowMessage($"{label} mode ON. Click the START block.");
        }

        public static void Reset()
        {
            UnsubscribeBlockAdded();
            UnsubscribeGridClose();
            State = GradientState.Idle;
            ActiveGrid = null;
            SelectedSkin = null;
        }

        public static void Update()
        {
            if (State == GradientState.Idle)
                return;

            if (!ValidatePlayerNearGrid())
            {
                Reset();
                ShowMessage("Too far from grid - gradient mode OFF.");
                return;
            }

            if (State == GradientState.Painting && DebugDrawEnabled)
                DrawGradientLine();
        }

        /// <summary>
        /// Called from the Harmony prefix on MyCubeGrid.SkinBlocks when in
        /// WaitingForStart or WaitingForEnd state. Records the block position
        /// and reads its existing color (ignoring the player's palette selection).
        /// </summary>
        public static void OnBlockPicked(Vector3I blockPos, MyCubeGrid grid)
        {
            // Debounce: ignore duplicate calls in the same frame (e.g. from mirror/symmetry mode)
            var frame = MyAPIGateway.Session?.GameplayFrameCounter ?? -1;
            if (frame == lastPickFrame)
                return;
            lastPickFrame = frame;

            var block = grid.GetCubeBlock(blockPos);
            if (block == null)
                return;

            var colorHSV = block.ColorMaskHSV;

            switch (State)
            {
                case GradientState.WaitingForStart:
                    ActiveGrid = grid;
                    SubscribeGridClose();
                    startPos = blockPos;
                    StartColorHSV = colorHSV;
                    State = GradientState.WaitingForEnd;
                    ShowMessage("Start set. Click the END block.");
                    break;

                case GradientState.WaitingForEnd:
                    if (blockPos == startPos)
                        return; // silently ignore — same block as start
                    endPos = blockPos;
                    EndColorHSV = colorHSV;
                    State = GradientState.Painting;
                    SubscribeBlockAdded();
                    ShowMessage("End set. Paint or place blocks to apply gradient.");
                    break;
            }
        }

        public static float ComputeGradientParameter(Vector3I pos)
        {
            if (Mode == GradientMode.Radial)
                return ComputeRadialParameter(pos);
            return ComputeLinearParameter(pos);
        }

        private static float ComputeLinearParameter(Vector3I pos)
        {
            var ab = endPos - startPos;
            var ap = pos - startPos;

            float lengthSq = (float)(ab.X * ab.X + ab.Y * ab.Y + ab.Z * ab.Z);
            if (lengthSq < 0.0001f)
                return 0f;

            float dot = (float)(ap.X * ab.X + ap.Y * ab.Y + ap.Z * ab.Z);
            return MathHelper.Clamp(dot / lengthSq, 0f, 1f);
        }

        private static float ComputeRadialParameter(Vector3I pos)
        {
            var dp = pos - startPos;
            float dist = (float)Math.Sqrt(dp.X * dp.X + dp.Y * dp.Y + dp.Z * dp.Z);

            var de = endPos - startPos;
            float radius = (float)Math.Sqrt(de.X * de.X + de.Y * de.Y + de.Z * de.Z);
            if (radius < 0.0001f)
                return 0f;

            return MathHelper.Clamp(dist / radius, 0f, 1f);
        }

        public static Vector3 LerpHSV(Vector3 a, Vector3 b, float t)
        {
            var hsvA = MyColorPickerConstants.HSVOffsetToHSV(a);
            var hsvB = MyColorPickerConstants.HSVOffsetToHSV(b);

            Vector3 resultHSV;
            switch (Config.Current.Interpolation)
            {
                case InterpolationMode.HSV:
                    resultHSV = Vector3.Lerp(hsvA, hsvB, t);
                    break;
                case InterpolationMode.Lab:
                    resultHSV = LerpViaLab(hsvA, hsvB, t);
                    break;
                case InterpolationMode.CubicLab:
                    resultHSV = LerpViaCubicLab(hsvA, hsvB, t);
                    break;
                case InterpolationMode.Perceptual:
                    resultHSV = LerpPerceptual(hsvA, hsvB, t);
                    break;
                default: // RGB
                {
                    var rgbA = ColorExtensions.HSVtoColor(hsvA).ToVector3();
                    var rgbB = ColorExtensions.HSVtoColor(hsvB).ToVector3();
                    var rgb = Vector3.Lerp(rgbA, rgbB, t);
                    resultHSV = ColorExtensions.ColorToHSV(new VRageMath.Color(rgb));
                    break;
                }
            }

            return MyColorPickerConstants.HSVToHSVOffset(resultHSV);
        }

        #region Lab interpolation

        private static Vector3 LerpViaLab(Vector3 hsvA, Vector3 hsvB, float t)
        {
            var rgbA = ColorExtensions.HSVtoColor(hsvA).ToVector3();
            var rgbB = ColorExtensions.HSVtoColor(hsvB).ToVector3();

            var labA = RgbToLab(rgbA);
            var labB = RgbToLab(rgbB);
            var lab = Vector3.Lerp(labA, labB, t);

            var rgb = LabToRgb(lab);
            return ColorExtensions.ColorToHSV(new VRageMath.Color(rgb));
        }

        private static Vector3 LerpViaCubicLab(Vector3 hsvA, Vector3 hsvB, float t)
        {
            var rgbA = ColorExtensions.HSVtoColor(hsvA).ToVector3();
            var rgbB = ColorExtensions.HSVtoColor(hsvB).ToVector3();

            var labA = RgbToLab(rgbA);
            var labB = RgbToLab(rgbB);

            // Smoothstep cubic: 3t² - 2t³
            float s = t * t * (3f - 2f * t);
            var lab = Vector3.Lerp(labA, labB, s);

            var rgb = LabToRgb(lab);
            return ColorExtensions.ColorToHSV(new VRageMath.Color(rgb));
        }

        private static Vector3 RgbToLab(Vector3 rgb)
        {
            // sRGB (0-1) → linear RGB → XYZ → Lab
            float lr = InverseSrgbCompanding(rgb.X);
            float lg = InverseSrgbCompanding(rgb.Y);
            float lb = InverseSrgbCompanding(rgb.Z);

            // linear RGB → XYZ (D65)
            float x = lr * 0.4124564f + lg * 0.3575761f + lb * 0.1804375f;
            float y = lr * 0.2126729f + lg * 0.7151522f + lb * 0.0721750f;
            float z = lr * 0.0193339f + lg * 0.1191920f + lb * 0.9503041f;

            // XYZ → Lab (D65 white point: 0.95047, 1.0, 1.08883)
            float fx = LabF(x / 0.95047f);
            float fy = LabF(y / 1.00000f);
            float fz = LabF(z / 1.08883f);

            float L = 116f * fy - 16f;
            float A = 500f * (fx - fy);
            float B = 200f * (fy - fz);
            return new Vector3(L, A, B);
        }

        private static Vector3 LabToRgb(Vector3 lab)
        {
            // Lab → XYZ → linear RGB → sRGB
            float fy = (lab.X + 16f) / 116f;
            float fx = lab.Y / 500f + fy;
            float fz = fy - lab.Z / 200f;

            float x = 0.95047f * LabFInverse(fx);
            float y = 1.00000f * LabFInverse(fy);
            float z = 1.08883f * LabFInverse(fz);

            // XYZ → linear RGB
            float lr =  3.2404542f * x - 1.5371385f * y - 0.4985314f * z;
            float lg = -0.9692660f * x + 1.8760108f * y + 0.0415560f * z;
            float lb =  0.0556434f * x - 0.2040259f * y + 1.0572252f * z;

            return new Vector3(
                MathHelper.Clamp(SrgbCompanding(lr), 0f, 1f),
                MathHelper.Clamp(SrgbCompanding(lg), 0f, 1f),
                MathHelper.Clamp(SrgbCompanding(lb), 0f, 1f));
        }

        private static float LabF(float t)
        {
            const float d = 6f / 29f;
            return t > d * d * d
                ? (float)Math.Pow(t, 1.0 / 3.0)
                : t / (3f * d * d) + 4f / 29f;
        }

        private static float LabFInverse(float t)
        {
            const float d = 6f / 29f;
            return t > d
                ? t * t * t
                : 3f * d * d * (t - 4f / 29f);
        }

        #endregion

        #region Perceptual (Mark's method)

        private static Vector3 LerpPerceptual(Vector3 hsvA, Vector3 hsvB, float t)
        {
            var rgbA = ColorExtensions.HSVtoColor(hsvA).ToVector3();
            var rgbB = ColorExtensions.HSVtoColor(hsvB).ToVector3();

            // Convert to linear light
            float lrA = InverseSrgbCompanding(rgbA.X);
            float lgA = InverseSrgbCompanding(rgbA.Y);
            float lbA = InverseSrgbCompanding(rgbA.Z);
            float lrB = InverseSrgbCompanding(rgbB.X);
            float lgB = InverseSrgbCompanding(rgbB.Y);
            float lbB = InverseSrgbCompanding(rgbB.Z);

            // Perceptual brightness via gamma 0.43
            const double gamma = 0.43;
            double bright1 = Math.Pow(lrA + lgA + lbA, gamma);
            double bright2 = Math.Pow(lrB + lgB + lbB, gamma);

            // Lerp in linear RGB
            float lr = lrA * (1f - t) + lrB * t;
            float lg = lgA * (1f - t) + lgB * t;
            float lb = lbA * (1f - t) + lbB * t;

            // Lerp brightness and convert back to linear intensity
            double brightness = bright1 * (1.0 - t) + bright2 * t;
            double intensity = Math.Pow(brightness, 1.0 / gamma);

            // Adjust RGB to match target brightness
            float sum = lr + lg + lb;
            if (sum > 0.0001f)
            {
                float factor = (float)(intensity / sum);
                lr *= factor;
                lg *= factor;
                lb *= factor;
            }

            // Back to sRGB
            return ColorExtensions.ColorToHSV(new VRageMath.Color(
                MathHelper.Clamp(SrgbCompanding(lr), 0f, 1f),
                MathHelper.Clamp(SrgbCompanding(lg), 0f, 1f),
                MathHelper.Clamp(SrgbCompanding(lb), 0f, 1f)));
        }

        #endregion

        #region sRGB companding

        private static float InverseSrgbCompanding(float c)
        {
            return c <= 0.04045f
                ? c / 12.92f
                : (float)Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        private static float SrgbCompanding(float c)
        {
            return c <= 0.0031308f
                ? c * 12.92f
                : 1.055f * (float)Math.Pow(c, 1.0 / 2.4) - 0.055f;
        }

        #endregion

        private static void DrawGradientLine()
        {
            if (ActiveGrid == null)
                return;

            var worldStart = ActiveGrid.GridIntegerToWorld(startPos);
            var worldEnd = ActiveGrid.GridIntegerToWorld(endPos);

            var startColor = ColorExtensions.HSVtoColor(MyColorPickerConstants.HSVOffsetToHSV(StartColorHSV));
            var endColor = ColorExtensions.HSVtoColor(MyColorPickerConstants.HSVOffsetToHSV(EndColorHSV));

            VRageRender.MyRenderProxy.DebugDrawLine3D(
                worldStart,
                worldEnd,
                startColor,
                endColor,
                depthRead: false);

            VRageRender.MyRenderProxy.DebugDrawSphere(
                worldStart, 0.15f, startColor, alpha: 1f, depthRead: false);
            VRageRender.MyRenderProxy.DebugDrawSphere(
                worldEnd, 0.15f, endColor, alpha: 1f, depthRead: false);

            if (Mode == GradientMode.Radial)
            {
                var radius = (float)Vector3D.Distance(worldStart, worldEnd);
                VRageRender.MyRenderProxy.DebugDrawSphere(
                    worldStart,
                    radius,
                    Color.Yellow,
                    alpha: 0.3f,
                    depthRead: false);
            }
        }

        private static bool ValidatePlayerNearGrid()
        {
            if (ActiveGrid == null)
                return State == GradientState.WaitingForStart;

            if (ActiveGrid.MarkedForClose)
                return false;

            var player = MyAPIGateway.Session?.Player;
            if (player?.Character == null)
                return false;

            var characterPos = player.Character.GetPosition();
            var gridCenter = ActiveGrid.GetPosition();
            var maxDistance = (double)Config.Current.MaxDistance;

            return Vector3D.DistanceSquared(characterPos, gridCenter) < maxDistance * maxDistance;
        }

        private static void ShowMessage(string text)
        {
            MyAPIGateway.Utilities?.ShowNotification($"[{Plugin.Name}] {text}", 2000);
        }

        public static void NotifyDifferentGrid()
        {
            ShowMessage("Different grid — gradient mode OFF.");
        }

        #region Block-added subscription

        private static bool subscribedToBlockAdded;
        private static bool subscribedToGridClose;

        private static void SubscribeBlockAdded()
        {
            if (subscribedToBlockAdded || ActiveGrid == null)
                return;
            ActiveGrid.OnBlockAdded += OnBlockAdded;
            subscribedToBlockAdded = true;
        }

        private static void UnsubscribeBlockAdded()
        {
            if (!subscribedToBlockAdded || ActiveGrid == null)
                return;
            ActiveGrid.OnBlockAdded -= OnBlockAdded;
            subscribedToBlockAdded = false;
        }

        private static void SubscribeGridClose()
        {
            if (subscribedToGridClose || ActiveGrid == null)
                return;
            ActiveGrid.OnClose += OnGridClosed;
            subscribedToGridClose = true;
        }

        private static void UnsubscribeGridClose()
        {
            if (!subscribedToGridClose || ActiveGrid == null)
                return;
            ActiveGrid.OnClose -= OnGridClosed;
            subscribedToGridClose = false;
        }

        private static void OnGridClosed(VRage.ModAPI.IMyEntity entity)
        {
            Reset();
        }

        private static void OnBlockAdded(IMySlimBlock block)
        {
            if (State != GradientState.Painting || RecolorBlock == null)
                return;

            var pos = block.Position;
            float t = ComputeGradientParameter(pos);
            var gradientHSV = LerpHSV(StartColorHSV, EndColorHSV, t);
            RecolorBlock((MyCubeGrid)ActiveGrid, block, gradientHSV);
        }

        #endregion
    }
}
