//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Numerics;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Services.Settings;
using Telegram.Td.Api;
using Telegram.ViewModels.Settings;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Cells
{
    public sealed partial class BaseThemeCell : UserControl, IMultipleElement
    {
        public BaseThemeCell()
        {
            InitializeComponent();
        }

        private ThemeData _theme;

        private ThemeSettings _settings;
        public ThemeSettings Settings
        {
            get => _settings;
            set => Update(_theme, value);
        }

        public void Update(ThemeData theme, ThemeSettings settings)
        {
            _theme = theme;
            _settings = settings;

            if (theme == null)
            {
                return;
            }

            _backgroundBrush = ActualTheme == ElementTheme.Dark ? Colors.Black : Colors.White;
            _innerShape?.StrokeBrush = BootStrapper.Current.Compositor.CreateColorBrush(_backgroundBrush);

            NameText.Text = theme.Info.Name;
            Preview.Visibility = Visibility.Visible;

            ThemeInfoBase info;
            if (settings != null)
            {
                info = ThemeAccentInfo.FromSettings(ActualTheme == ElementTheme.Light ? TelegramTheme.Light : TelegramTheme.Dark, settings);
            }
            else
            {
                info = theme.Info;
                settings = info is ThemeCustomInfo custom ? custom.Settings : null;
            }

            if (settings?.Background != null)
            {
                Preview.UpdateSource(theme.ClientService, settings.Background, true);
            }
            else
            {
                Preview.UpdateSource(theme.ClientService, AppearanceSettings.GetDefaultBackground(info is ThemeCustomInfo { Parent: TelegramTheme.Dark }), true);
            }

            if (settings != null)
            {
                // Before Fill: it decides the order of the stops.
                Outgoing.IsAnimated = settings.AnimateOutgoingMessageFill;
                Outgoing.Fill = settings.OutgoingMessageFill switch
                {
                    BackgroundFillGradient => settings.OutgoingMessageFill,
                    BackgroundFillFreeformGradient => settings.OutgoingMessageFill,
                    _ => new BackgroundFillSolid(info.MessageBackgroundOutColor.ToValue())
                };
            }
            else
            {
                Outgoing.IsAnimated = false;
                Outgoing.Fill = new BackgroundFillSolid(info.MessageBackgroundOutColor.ToValue());
            }

            Incoming.Fill = new SolidColorBrush(info.MessageBackgroundColor);

            _selectionBrush = info.AccentColor;
            _outerShape?.StrokeBrush = BootStrapper.Current.Compositor.CreateColorBrush(info.AccentColor);
        }

        private void OnActualThemeChanged(FrameworkElement sender, object args)
        {
            if (_theme != null)
            {
                Update(_theme, _settings);
            }

            _backgroundBrush = ActualTheme == ElementTheme.Dark ? Colors.Black : Colors.White;
            _innerShape?.StrokeBrush = BootStrapper.Current.Compositor.CreateColorBrush(_backgroundBrush);
        }

        private CompositionRoundedRectangleGeometry _outer;
        private CompositionRoundedRectangleGeometry _inner;

        private CompositionSpriteShape _outerShape;
        private CompositionSpriteShape _innerShape;

        private ShapeVisual _visual;

        private Color _selectionBrush;
        private Color _backgroundBrush;

        public void UpdateState(bool selected, bool animate, bool multiple)
        {
            if (!selected && !animate && _visual == null)
            {
                return;
            }

            EnsureVisuals();

            if (animate)
            {
                var compositor = BootStrapper.Current.Compositor;
                var duration = Constants.FastAnimation;

                var scaleAnim = compositor.CreateVector2KeyFrameAnimation();
                scaleAnim.InsertKeyFrame(selected ? 0 : 1, new Vector2((RootGrid.ActualSize.X + 4) / RootGrid.ActualSize.X, (RootGrid.ActualSize.Y + 4) / RootGrid.ActualSize.Y));
                scaleAnim.InsertKeyFrame(selected ? 1 : 0, new Vector2(1));
                scaleAnim.Duration = duration;

                var fadeIn = compositor.CreateScalarKeyFrameAnimation();
                fadeIn.InsertKeyFrame(selected ? 0 : 1, 0);
                fadeIn.InsertKeyFrame(selected ? 1 : 0, 1);
                fadeIn.Duration = duration;

                _outerShape.StartAnimation("Scale", scaleAnim);
                _innerShape.StartAnimation("Scale", scaleAnim);

                _visual.StartAnimation("Opacity", fadeIn);
            }
            else
            {
                //outerShape.Scale = new Vector2(selected ? 1 : size / 28);
                //innerShape1.Offset = new Vector2(selected ? -6 : 0, 0);
                //innerShape3.Offset = new Vector2(selected ? 6 : 0, 0);

                _visual.Opacity = selected ? 1 : 0;
            }
        }

        private void EnsureVisuals()
        {
            var compositor = BootStrapper.Current.Compositor;

            _outer = compositor.CreateRoundedRectangleGeometry();
            _outer.CornerRadius = new Vector2(4);
            _outer.Offset = new Vector2(1);

            _inner = compositor.CreateRoundedRectangleGeometry();
            _inner.CornerRadius = new Vector2(4);
            _inner.Offset = new Vector2(2);

            _outerShape = compositor.CreateSpriteShape(_outer);
            _outerShape.StrokeBrush = compositor.CreateColorBrush(_selectionBrush);
            _outerShape.StrokeThickness = 2;
            _outerShape.IsStrokeNonScaling = true;

            _innerShape = compositor.CreateSpriteShape(_inner);
            _innerShape.StrokeBrush = compositor.CreateColorBrush(_backgroundBrush);
            _innerShape.StrokeThickness = 4;
            _innerShape.IsStrokeNonScaling = true;

            _visual = compositor.CreateShapeVisual();
            _visual.Shapes.Add(_innerShape);
            _visual.Shapes.Add(_outerShape);

            ElementCompositionPreview.SetElementChildVisual(RootGrid, _visual);

            if (RootGrid.ActualSize.X > 0 && RootGrid.ActualSize.Y > 0)
            {
                var final = RootGrid.ActualSize;

                _outerShape.CenterPoint = final / 2;
                _innerShape.CenterPoint = final / 2;

                _outer.Size = new Vector2(final.X - 2, final.Y - 2);
                _inner.Size = new Vector2(final.X - 4, final.Y - 4);
                _visual.Size = final;
            }
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var size = base.ArrangeOverride(finalSize);
            var final = RootGrid.ActualSize;

            _outerShape?.CenterPoint = final / 2;
            _innerShape?.CenterPoint = final / 2;

            _outer?.Size = new Vector2(final.X - 2, final.Y - 2);
            _inner?.Size = new Vector2(final.X - 4, final.Y - 4);
            _visual?.Size = final;

            return size;
        }
    }
}
