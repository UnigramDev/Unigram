//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Numerics;
using Telegram.Common;
using Telegram.Composition;
using Telegram.Native.Controls;
using Telegram.Navigation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls
{
    public partial class Ribbon : ControlEx
    {
        private CompositionColorSource _foregroundBrush;
        private long _foregroundToken;

        public Ribbon()
        {
            DefaultStyleKey = typeof(Ribbon);
        }

        protected override void OnApplyTemplate()
        {
            this.RegisterPropertyChangedCallback(ForegroundProperty, OnForegroundChanged, ref _foregroundToken);
        }

        protected override void OnLoaded()
        {
            _foregroundBrush?.Register();
        }

        protected override void OnUnloaded()
        {
            _foregroundBrush?.Unregister();
        }

        private void OnForegroundChanged(DependencyObject sender, DependencyProperty dp)
        {
            _foregroundBrush?.PropertyChanged(Background as SolidColorBrush, IsConnected);
        }

        #region Text

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(Ribbon), new PropertyMetadata(null, OnTextChanged));

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((Ribbon)d).OnTextChanged((string)e.NewValue);
        }

        private void OnTextChanged(string newValue)
        {
            _foregroundBrush = new CompositionColorSource(Foreground, IsConnected);

            var path = Direct2D.Current.GetTextOutline(newValue, "Segoe UI", 500, 12, 64, out Vector2 size);
            var geometry = BootStrapper.Current.Compositor.CreatePathGeometry(path);

            var sprite = BootStrapper.Current.Compositor.CreateSpriteShape(geometry);
            sprite.StrokeThickness = 0;
            sprite.FillBrush = _foregroundBrush;

            var center = new Vector2(37, 23);

            var shape = Window.Current.Compositor.CreateShapeVisual();
            shape.Shapes.Add(sprite);
            shape.Size = size;
            shape.Offset = new Vector3(center - size / 2, 0);
            shape.CenterPoint = new Vector3(size / 2, 0);
            shape.RotationAngleInDegrees = 45;

            ElementCompositionPreview.SetElementChildVisual(this, shape);
        }

        #endregion
    }
}
