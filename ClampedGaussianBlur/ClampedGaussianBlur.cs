using System;
using System.Drawing;
using System.Reflection;
using System.Collections.Generic;
using PaintDotNet;
using PaintDotNet.Effects;
using PaintDotNet.IndirectUI;
using PaintDotNet.PropertySystem;

namespace ClampedGaussianBlurEffect
{
    public class PluginSupportInfo : IPluginSupportInfo
    {
        public string Author
        {
            get
            {
                return ((AssemblyCopyrightAttribute)base.GetType().Assembly.GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false)[0]).Copyright;
            }
        }
        public string Copyright
        {
            get
            {
                return ((AssemblyDescriptionAttribute)base.GetType().Assembly.GetCustomAttributes(typeof(AssemblyDescriptionAttribute), false)[0]).Description;
            }
        }

        public string DisplayName
        {
            get
            {
                return ((AssemblyProductAttribute)base.GetType().Assembly.GetCustomAttributes(typeof(AssemblyProductAttribute), false)[0]).Product;
            }
        }

        public Version Version
        {
            get
            {
                return base.GetType().Assembly.GetName().Version;
            }
        }

        public Uri WebsiteUri
        {
            get
            {
                return new Uri("http://www.getpaint.net/redirect/plugins.html");
            }
        }
    }

    [PluginSupportInfo(typeof(PluginSupportInfo), DisplayName = "Gaussian Blur (Clamped)")]
    public class ClampedGaussianBlurEffectPlugin : PropertyBasedEffect
    {
        public static string StaticName
        {
            get
            {
                return "Gaussian Blur (Clamped)";
            }
        }

        public static Image StaticIcon
        {
            get
            {
                return new Bitmap(typeof(ClampedGaussianBlurEffectPlugin), "ClampedGaussianBlur.png");
            }
        }

        public static string SubmenuName
        {
            get
            {
                return SubmenuNames.Blurs;  // Programmer's chosen default
            }
        }

        public ClampedGaussianBlurEffectPlugin()
            : base(StaticName, StaticIcon, SubmenuName, EffectFlags.Configurable)
        {
        }

        public enum PropertyNames
        {
            Amount1
        }


        protected override PropertyCollection OnCreatePropertyCollection()
        {
            List<Property> props = new List<Property>();

            props.Add(new Int32Property(PropertyNames.Amount1, 2, 1, 200));

            return new PropertyCollection(props);
        }

        protected override ControlInfo OnCreateConfigUI(PropertyCollection props)
        {
            ControlInfo configUI = CreateDefaultConfigUI(props);

            configUI.SetPropertyControlValue(PropertyNames.Amount1, ControlInfoPropertyNames.DisplayName, "Radius");

            return configUI;
        }

        protected override void OnSetRenderInfo(PropertyBasedEffectConfigToken newToken, RenderArgs dstArgs, RenderArgs srcArgs)
        {
            Amount1 = newToken.GetProperty<Int32Property>(PropertyNames.Amount1).Value;


            Rectangle selection = EnvironmentParameters.GetSelection(srcArgs.Surface.Bounds).GetBoundsInt();

            if (selectionSurface == null)
            {
                selectionSurface = new Surface(selection.Size);
                selectionSurface.CopySurface(srcArgs.Surface, selection);
            }

            if (clampedSurface == null)
                clampedSurface = new Surface(srcArgs.Surface.Size);

            for (int y = Math.Max(0, selection.Top - 200); y < Math.Min(clampedSurface.Height, selection.Bottom + 200); y++)
            {
                if (IsCancelRequested) return;
                for (int x = Math.Max(0, selection.Left - 200); x < Math.Min(clampedSurface.Width, selection.Right + 200); x++)
                {
                    clampedSurface[x, y] = selectionSurface.GetBilinearSampleClamped(x - selection.Left, y - selection.Top);
                }
            }


            base.OnSetRenderInfo(newToken, dstArgs, srcArgs);
        }

        protected override void OnRender(Rectangle[] renderRects, int startIndex, int length)
        {
            if (length == 0) return;
            for (int i = startIndex; i < startIndex + length; ++i)
            {
                Render(DstArgs.Surface, SrcArgs.Surface, renderRects[i]);
            }
        }

        #region CodeLab
        int Amount1 = 2; // [1,200] Radius
        #endregion

        Surface selectionSurface;
        Surface clampedSurface;

        void Render(Surface dst, Surface src, Rectangle rect)
        {
            // Setup for calling the Gaussian Blur effect
            GaussianBlurEffect blurEffect = new GaussianBlurEffect();
            PropertyCollection blurProps = blurEffect.CreatePropertyCollection();
            PropertyBasedEffectConfigToken BlurParameters = new PropertyBasedEffectConfigToken(blurProps);
            BlurParameters.SetPropertyValue(GaussianBlurEffect.PropertyNames.Radius, Amount1);
            blurEffect.SetRenderInfo(BlurParameters, new RenderArgs(dst), new RenderArgs(clampedSurface));
            // Call the Gaussian Blur function
            blurEffect.Render(new Rectangle[1] { rect }, 0, 1);
        }
    }
}
