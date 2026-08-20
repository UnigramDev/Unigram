using System;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;

namespace WalletCardDemo
{
    /// <summary>
    /// Procedural textures for the card: the static content overlay (texts,
    /// icons, QR button) and the stars map. Port of CardTextures.swift.
    ///
    /// Two of the four Swift textures are gone: the brushed-metal noise is
    /// generated in the shader instead (see CardFront.hlsl), and the studio
    /// environment has no consumer without SceneKit's image-based lighting -
    /// its two visible lobes are baked into the shader's specular term.
    ///
    /// Built once and kept; nothing here runs per frame.
    /// </summary>
    public sealed class CardTextures : IDisposable
    {
        // Rasterized at the control's own DPI, NOT at the iOS build's fixed 3x.
        //
        // These two bitmaps are the shader's inputs, and a shader input whose DPI
        // differs from the output gets a DPI-compensation pass inserted in front
        // of it. That pass renders into a padded intermediate, and
        // D2DGetInputCoordinate normalizes over the *padded texture*, not the
        // image - so uv reaches 0 at the top-left but stops short of 1 at the
        // bottom-right, and every uv-derived shape drifts. Matching the DPI keeps
        // the bitmap itself as the input and uv exactly 0..1 across the card.
        private readonly float _dpi;

        /// <summary>Static content, premultiplied, transparent where the gradient shows through.</summary>
        public CanvasRenderTarget Overlay { get; private set; }

        /// <summary>A = star coverage, G = that star's twinkle phase (0..1).</summary>
        public CanvasRenderTarget Stars { get; private set; }

        private CardTextures(float dpi)
        {
            _dpi = dpi;
        }

        public static async Task<CardTextures> CreateAsync(ICanvasResourceCreator device, WalletCardModel model, float dpi)
        {
            var textures = new CardTextures(dpi);

            var usdtIcon = await CanvasBitmap.LoadAsync(device, new Uri("ms-appx:///Assets/card-usdt-diamond.png"));
            var gramIcon = await CanvasBitmap.LoadAsync(device, new Uri("ms-appx:///Assets/card-gram-diamond.png"));
            var qrGlyph = await CanvasBitmap.LoadAsync(device, new Uri("ms-appx:///Assets/card-qr-glyph.png"));

            using (usdtIcon)
            using (gramIcon)
            using (qrGlyph)
            {
                textures.Overlay = textures.DrawOverlay(device, model, usdtIcon, gramIcon, qrGlyph);
            }

            textures.Stars = textures.DrawStars(device);
            return textures;
        }

        // MARK: - Static content overlay

        private CanvasRenderTarget DrawOverlay(ICanvasResourceCreator device, WalletCardModel model,
            CanvasBitmap usdtIcon, CanvasBitmap gramIcon, CanvasBitmap qrGlyph)
        {
            var target = new CanvasRenderTarget(device, CardDesign.Width, CardDesign.Height, _dpi);

            using (var ds = target.CreateDrawingSession())
            {
                ds.Clear(Colors.Transparent);
                ds.TextAntialiasing = CanvasTextAntialiasing.Grayscale;

                DrawAmountRow(ds, model.UsdtAmount, "USDT", usdtIcon, new Vector2(20, 71));
                DrawAmountRow(ds, model.GramAmount, "GRAM", gramIcon, new Vector2(20, 103));

                using (var name = Mono(12, FontWeights.Medium))
                {
                    DrawTracked(ds, model.HolderName.ToUpperInvariant(), name, 1.25f, Colors.White,
                        new Vector2(24, 185.5f));

                    using (var title = Mono(10, FontWeights.Medium))
                    {
                        DrawTrackedRight(ds, model.BalanceTitle, title, 1.1f, CardDesign.BalanceCyan, 322, 169.5f);
                    }

                    DrawTrackedRight(ds, model.BalanceValue.ToUpperInvariant(), name, 1.25f, Colors.White,
                        322, 185.5f);
                }

                DrawQRButton(ds, qrGlyph);
                DrawVerticalAddress(ds, model);
                DrawBottomShade(ds);
            }

            return target;
        }

        // Figma uses Martian Mono; the iOS sample stands in with the system
        // monospaced face, and Cascadia Mono is the equivalent here. Swap both
        // if Martian Mono is ever bundled.
        private static CanvasTextFormat Mono(float size, FontWeight weight)
        {
            return new CanvasTextFormat
            {
                FontFamily = CardDesign.MonoFont,
                FontSize = size,
                FontWeight = weight,
                WordWrapping = CanvasWordWrapping.NoWrap,
                Options = CanvasDrawTextOptions.Default
            };
        }

        private static CanvasTextFormat Rounded(float size, FontWeight weight)
        {
            return new CanvasTextFormat
            {
                FontFamily = CardDesign.RoundedFont,
                FontSize = size,
                FontWeight = weight,
                WordWrapping = CanvasWordWrapping.NoWrap,
                Options = CanvasDrawTextOptions.Default
            };
        }

        /// <summary>
        /// UIKit's `.kern` attribute as DirectWrite character spacing: both add
        /// the value to every advance, so the trailing side is the direct
        /// equivalent and the leading side stays 0.
        /// </summary>
        private static CanvasTextLayout Track(ICanvasResourceCreator device, string text,
            CanvasTextFormat format, float tracking)
        {
            var layout = new CanvasTextLayout(device, text, format, 0, 0);
            layout.SetCharacterSpacing(0, text.Length, 0, tracking, 0);
            return layout;
        }

        private static void DrawTracked(CanvasDrawingSession ds, string text, CanvasTextFormat format,
            float tracking, Color color, Vector2 at)
        {
            using var layout = Track(ds, text, format, tracking);
            ds.DrawTextLayout(layout, at, color);
        }

        private static void DrawTrackedRight(CanvasDrawingSession ds, string text, CanvasTextFormat format,
            float tracking, Color color, float right, float top)
        {
            using var layout = Track(ds, text, format, tracking);
            ds.DrawTextLayout(layout, new Vector2(right - (float)layout.LayoutBounds.Width, top), color);
        }

        /// <summary>One amount row: 28pt icon + rounded semibold 22 label on a 28pt line.</summary>
        private static void DrawAmountRow(CanvasDrawingSession ds, string value, string unit,
            CanvasBitmap icon, Vector2 rowOrigin)
        {
            // Icon glyph box inside the 28x28 slot (Figma insets 14.34% / 20%).
            ds.DrawImage(icon, new Rect(rowOrigin.X + 4.02, rowOrigin.Y + 5.60, 19.97, 16.88),
                icon.Bounds, 1f, CanvasImageInterpolation.HighQualityCubic);

            using var format = Rounded(22, FontWeights.SemiBold);

            // Two layouts rather than one with a colored range: the value and the
            // unit differ only in color, and measuring the first gives the exact
            // advance the single attributed string would have produced.
            using var head = Track(ds, value + " ", format, 0.352f);
            using var tail = Track(ds, unit, format, 0.352f);

            var y = rowOrigin.Y + (28 - (float)head.LayoutBounds.Height) / 2;
            ds.DrawTextLayout(head, new Vector2(rowOrigin.X + 30, y), Colors.White);

            // ...IncludingTrailingWhitespace, or the separating space contributes
            // nothing and the unit runs straight into the value ("200USDT"):
            // DirectWrite's plain layout width stops at the last visible glyph.
            ds.DrawTextLayout(tail,
                new Vector2(rowOrigin.X + 30 + (float)head.LayoutBoundsIncludingTrailingWhitespace.Width, y),
                CardDesign.AccentCyan);
        }

        /// <summary>Silver QR button, 48x36 r8 at (274, 76).</summary>
        private static void DrawQRButton(CanvasDrawingSession ds, CanvasBitmap glyph)
        {
            var rect = new Rect(274, 76, 48, 36);

            // CSS 0deg points up and increases clockwise; the Y axis here points
            // down, so the direction is (sin A, -cos A). CSS also extends the
            // gradient line to cover the box, hence the half-extent below.
            const float angle = 127.2782f * MathF.PI / 180;
            var dir = new Vector2(MathF.Sin(angle), -MathF.Cos(angle));
            var center = new Vector2((float)(rect.X + rect.Width / 2), (float)(rect.Y + rect.Height / 2));
            var half = (MathF.Abs((float)rect.Width * dir.X) + MathF.Abs((float)rect.Height * dir.Y)) / 2;

            using (var brush = new CanvasLinearGradientBrush(ds, new[]
            {
                new CanvasGradientStop { Position = 0.0799f, Color = Color.FromArgb(0xE0, 0xFF, 0xFF, 0xFF) },
                new CanvasGradientStop { Position = 0.9220f, Color = Color.FromArgb(0xE0, 0xBB, 0xBD, 0xC0) },
            }, CanvasEdgeBehavior.Clamp, CanvasAlphaMode.Premultiplied))
            {
                brush.StartPoint = center - dir * half;
                brush.EndPoint = center + dir * half;
                ds.FillRoundedRectangle(rect, 8, 8, brush);
            }

            var border = new Rect(rect.X + 0.5, rect.Y + 0.5, rect.Width - 1, rect.Height - 1);
            ds.DrawRoundedRectangle(border, 7.5f, 7.5f, Color.FromArgb(0x29, 0, 0, 0), 1f);

            // 24x24 icon slot centered in the button, glyph inset 11.23%; the
            // asset carries its own shadow, which extends 5.37% below.
            ds.DrawImage(glyph, new Rect(288.70, 84.70, 18.61, 19.61), glyph.Bounds, 0.5f,
                CanvasImageInterpolation.HighQualityCubic);
        }

        /// <summary>Wallet address rotated 90deg cw along the right edge, centered at (352, 110).</summary>
        private static void DrawVerticalAddress(CanvasDrawingSession ds, WalletCardModel model)
        {
            var restore = ds.Transform;
            ds.Transform = Matrix3x2.CreateRotation(MathF.PI / 2) * Matrix3x2.CreateTranslation(352, 110);

            using var format = Mono(8.2f, FontWeights.Normal);
            // Tracking scaled for 8.2pt so the line length matches Figma's 179pt.
            using var line1 = Track(ds, model.AddressLine1.ToUpperInvariant(), format, 0.72f);
            using var line2 = Track(ds, model.AddressLine2.ToUpperInvariant(), format, 0.72f);

            const float lineHeight = 9.84f; // 8.2pt * 1.2
            var x1 = -(float)line1.LayoutBounds.Width / 2;
            var x2 = -(float)line2.LayoutBounds.Width / 2;

            // Figma's `0px 1px 0px rgba(255,255,255,0.06)` engrave: a hard offset
            // copy, not a blur, so a second draw is the whole effect.
            var highlight = Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF);
            ds.DrawTextLayout(line1, new Vector2(x1, -lineHeight + 1), highlight);
            ds.DrawTextLayout(line2, new Vector2(x2, 1), highlight);
            ds.DrawTextLayout(line1, new Vector2(x1, -lineHeight), CardDesign.AddressBlue);
            ds.DrawTextLayout(line2, new Vector2(x2, 0), CardDesign.AddressBlue);

            ds.Transform = restore;
        }

        /// <summary>Figma: inset shadow 0 -1px 1px rgba(0,0,0,0.14) along the bottom edge.</summary>
        private static void DrawBottomShade(CanvasDrawingSession ds)
        {
            using var brush = new CanvasLinearGradientBrush(ds, new[]
            {
                new CanvasGradientStop { Position = 0f, Color = Color.FromArgb(0, 0, 0, 0) },
                new CanvasGradientStop { Position = 1f, Color = Color.FromArgb(0x24, 0, 0, 0) },
            }, CanvasEdgeBehavior.Clamp, CanvasAlphaMode.Premultiplied)
            {
                StartPoint = new Vector2(0, 217),
                EndPoint = new Vector2(0, CardDesign.Height)
            };

            // The card's rounded corners are clipped by the caller, so a plain
            // band is enough here - unlike CoreGraphics there is no path to push.
            ds.FillRectangle(new Rect(0, 217, CardDesign.Width, 3), brush);
        }

        // MARK: - Stars map

        private CanvasRenderTarget DrawStars(ICanvasResourceCreator device)
        {
            var target = new CanvasRenderTarget(device, CardDesign.Width, CardDesign.Height, _dpi);

            using (var ds = target.CreateDrawingSession())
            {
                ds.Clear(Colors.Transparent);

                for (int i = 0; i < CardDesign.StarCenters.Length; i++)
                {
                    // Phase rides in the green channel. The target is premultiplied,
                    // so an antialiased edge scales G and A together and the shader's
                    // `star.g / star.a` recovers it - the same trick the iOS build uses.
                    var color = Color.FromArgb(255, 255, (byte)(CardDesign.StarPhases[i] * 255), 0);
                    using var geometry = CreateStar(device, CardDesign.StarCenters[i],
                        CardDesign.StarOuterRadius, CardDesign.StarInnerRadius);
                    ds.FillGeometry(geometry, color);
                }
            }

            return target;
        }

        /// <summary>Four-pointed star (points up/right/down/left) as in the Figma asset.</summary>
        private static CanvasGeometry CreateStar(ICanvasResourceCreator device, Vector2 center,
            float outer, float inner)
        {
            using var builder = new CanvasPathBuilder(device);

            for (int k = 0; k < 8; k++)
            {
                var radius = (k % 2) == 0 ? outer : inner;
                var angle = -MathF.PI / 2 + k * MathF.PI / 4;
                var point = new Vector2(center.X + radius * MathF.Cos(angle), center.Y + radius * MathF.Sin(angle));

                if (k == 0)
                {
                    builder.BeginFigure(point);
                }
                else
                {
                    builder.AddLine(point);
                }
            }

            builder.EndFigure(CanvasFigureLoop.Closed);
            return CanvasGeometry.CreatePath(builder);
        }

        public void Dispose()
        {
            Overlay?.Dispose();
            Overlay = null;

            Stars?.Dispose();
            Stars = null;
        }
    }
}
