﻿using System;
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


            if (selectionSurface == null)
            {
                selectionSurface = new Surface(srcArgs.Surface.Size);
                PdnRegion selectionRegion = EnvironmentParameters.GetSelection(srcArgs.Bounds);
                selectionSurface.CopySurface(srcArgs.Surface, selectionRegion);

                // Increase absolute Transparency within selection to 1 to ensure clamping happens at selection edge
                ColorBgra alphaTest;
                foreach (Rectangle r in selectionRegion.GetRegionScansInt())
                {
                    for (int y = r.Top; y < r.Bottom; y++)
                    {
                        if (IsCancelRequested) return;
                        for (int x = r.Left; x < r.Right; x++)
                        {
                            alphaTest = selectionSurface[x, y];
                            if (alphaTest.A == 0)
                                alphaTest.A = 1;
                            selectionSurface[x, y] = alphaTest;
                        }
                    }
                }
            }

            Rectangle selection = EnvironmentParameters.GetSelection(srcArgs.Surface.Bounds).GetBoundsInt();
            int left = Math.Max(0, selection.Left - 200);
            int right = Math.Min(srcArgs.Surface.Width, selection.Right + 200);
            int top = Math.Max(0, selection.Top - 200);
            int bottom = Math.Min(srcArgs.Surface.Height, selection.Bottom + 200);

            if (nearestPixels == null)
            {
                nearestPixels = new NearestPixelTransform(left, top, right - left, bottom - top);
                nearestPixels.Include((x, y) => selectionSurface[x, y].A >= 1);
                nearestPixels.Transform();
            }

            if (clampedSurface == null)
                clampedSurface = new Surface(srcArgs.Surface.Size);

            ColorBgra cp;
            for (int y = top; y < bottom; y++)
            {
                if (IsCancelRequested) return;
                for (int x = left; x < right; x++)
                {
                    cp = selectionSurface[x, y];

                    if (cp.A <= 1)
                        cp = selectionSurface[nearestPixels[x, y]];

                    clampedSurface[x, y] = cp;
                }
            }

            // Setup for calling the Gaussian Blur effect
            PropertyCollection blurProps = blurEffect.CreatePropertyCollection();
            PropertyBasedEffectConfigToken BlurParameters = new PropertyBasedEffectConfigToken(blurProps);
            BlurParameters.SetPropertyValue(GaussianBlurEffect.PropertyNames.Radius, Amount1);
            blurEffect.SetRenderInfo(BlurParameters, dstArgs, new RenderArgs(clampedSurface));

            base.OnSetRenderInfo(newToken, dstArgs, srcArgs);
        }

        protected override void OnRender(Rectangle[] renderRects, int startIndex, int length)
        {
            if (length == 0) return;

            blurEffect.Render(renderRects, startIndex, length);
        }

        #region CodeLab
        int Amount1 = 2; // [1,200] Radius
        #endregion

        Surface selectionSurface;
        Surface clampedSurface;
        NearestPixelTransform nearestPixels;
        readonly GaussianBlurEffect blurEffect = new GaussianBlurEffect();
    }
}
