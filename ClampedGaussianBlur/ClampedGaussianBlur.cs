using System;
using System.Drawing;
using System.Reflection;
using System.Collections.Generic;
using PaintDotNet;
using PaintDotNet.Effects;
using PaintDotNet.IndirectUI;
using PaintDotNet.PropertySystem;
using DistanceTransformation;

namespace ClampedGaussianBlurEffect
{
    public class PluginSupportInfo : IPluginSupportInfo
    {
        public string Author => base.GetType().Assembly.GetCustomAttribute<AssemblyCopyrightAttribute>().Copyright;
        public string Copyright => base.GetType().Assembly.GetCustomAttribute<AssemblyDescriptionAttribute>().Description;
        public string DisplayName => base.GetType().Assembly.GetCustomAttribute<AssemblyProductAttribute>().Product;
        public Version Version => base.GetType().Assembly.GetName().Version;
        public Uri WebsiteUri => new Uri("https://forums.getpaint.net/index.php?showtopic=106935");
    }

    [PluginSupportInfo(typeof(PluginSupportInfo), DisplayName = "Gaussian Blur (Clamped)")]
    public class ClampedGaussianBlurEffectPlugin : PropertyBasedEffect
    {
        private const string StaticName = "Gaussian Blur (Clamped)";
        private readonly static Image StaticIcon = new Bitmap(typeof(ClampedGaussianBlurEffectPlugin), "ClampedGaussianBlur.png");

        public ClampedGaussianBlurEffectPlugin()
            : base(StaticName, StaticIcon, SubmenuNames.Blurs, EffectFlags.Configurable)
        {
        }

        private enum PropertyNames
        {
            Radius
        }

        protected override PropertyCollection OnCreatePropertyCollection()
        {
            List<Property> props = new List<Property>();

            props.Add(new Int32Property(PropertyNames.Radius, 2, 1, 200));

            return new PropertyCollection(props);
        }

        protected override ControlInfo OnCreateConfigUI(PropertyCollection props)
        {
            ControlInfo configUI = CreateDefaultConfigUI(props);

            configUI.SetPropertyControlValue(PropertyNames.Radius, ControlInfoPropertyNames.DisplayName, "Radius");

            return configUI;
        }

        protected override void OnSetRenderInfo(PropertyBasedEffectConfigToken newToken, RenderArgs dstArgs, RenderArgs srcArgs)
        {
            int radius = newToken.GetProperty<Int32Property>(PropertyNames.Radius).Value;

            ColorBgra colorMarker = ColorBgra.FromBgra(46, 106, 84, 0);

            if (selectionSurface == null)
            {
                selectionSurface = new Surface(srcArgs.Surface.Size);
                selectionSurface.Clear(colorMarker);
                PdnRegion selectionRegion = EnvironmentParameters.GetSelection(srcArgs.Bounds);
                selectionSurface.CopySurface(srcArgs.Surface, selectionRegion);
            }

            Rectangle selection = EnvironmentParameters.GetSelection(srcArgs.Surface.Bounds).GetBoundsInt();
            Rectangle clampingBounds = Rectangle.FromLTRB(
                Math.Max(0, selection.Left - 200),
                Math.Max(0, selection.Top - 200),
                Math.Min(srcArgs.Surface.Width, selection.Right + 200),
                Math.Min(srcArgs.Surface.Height, selection.Bottom + 200)
            );

            if (nearestPixels == null)
            {
                nearestPixels = new NearestPixelTransform(clampingBounds.Left, clampingBounds.Top, clampingBounds.Width, clampingBounds.Height);
                nearestPixels.Include((x, y) => selectionSurface[x, y] != colorMarker);
                nearestPixels.Transform();
            }

            if (clampedSurface == null)
            {
                clampedSurface = new Surface(srcArgs.Surface.Size);
            }

            ColorBgra cp;
            for (int y = clampingBounds.Top; y < clampingBounds.Bottom; y++)
            {
                if (IsCancelRequested) return;
                for (int x = clampingBounds.Left; x < clampingBounds.Right; x++)
                {
                    cp = selectionSurface[x, y];

                    if (cp == colorMarker)
                        cp = selectionSurface[nearestPixels[x, y]];

                    clampedSurface[x, y] = cp;
                }
            }

            // Setup for calling the Gaussian Blur effect
            PropertyCollection blurProps = blurEffect.CreatePropertyCollection();
            PropertyBasedEffectConfigToken BlurParameters = new PropertyBasedEffectConfigToken(blurProps);
            BlurParameters.SetPropertyValue(GaussianBlurEffect.PropertyNames.Radius, radius);
            blurEffect.SetRenderInfo(BlurParameters, dstArgs, new RenderArgs(clampedSurface));

            base.OnSetRenderInfo(newToken, dstArgs, srcArgs);
        }

        protected override void OnRender(Rectangle[] renderRects, int startIndex, int length)
        {
            if (length == 0) return;

            blurEffect.Render(renderRects, startIndex, length);
        }

        private Surface selectionSurface;
        private Surface clampedSurface;
        private NearestPixelTransform nearestPixels;
        private readonly GaussianBlurEffect blurEffect = new GaussianBlurEffect();

        protected override void OnDispose(bool disposing)
        {
            selectionSurface?.Dispose();
            clampedSurface?.Dispose();
            blurEffect?.Dispose();

            base.OnDispose(disposing);
        }
    }
}
