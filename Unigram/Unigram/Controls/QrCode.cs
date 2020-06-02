using System;
using Unigram.Common;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls
{
    [TemplatePart(Name = "Canvas", Type = typeof(Image))]
    public class QrCode : Control
    {
        private Image Canvas;
        private string CanvasPartName = "Canvas";

        private string _text;

        public QrCode()
        {
            DefaultStyleKey = typeof(QrCode);

            RegisterPropertyChangedCallback(ForegroundProperty, OnBrushChanged);
            RegisterPropertyChangedCallback(BackgroundProperty, OnBrushChanged);
        }

        private void OnBrushChanged(DependencyObject sender, DependencyProperty dp)
        {
            ((QrCode)sender).OnTextChanged(((QrCode)sender).Text, null);
        }

        protected override void OnApplyTemplate()
        {
            var canvas = GetTemplateChild(CanvasPartName) as Image;
            if (canvas == null)
            {
                return;
            }

            Canvas = canvas;
            OnTextChanged(_text, null);

            base.OnApplyTemplate();
        }

        private void OnTextChanged(string newValue, string oldValue)
        {
            var canvas = Canvas;
            if (canvas == null)
            {
                _text = newValue;
                return;
            }

            if (string.IsNullOrEmpty(newValue))
            {
                canvas.Source = null;
                return;
            }

            if (oldValue != null)
            {
                if (string.Equals(newValue, oldValue, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(newValue, _text, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            _text = newValue;

            var foreground = Colors.Black;
            var background = Colors.White;

            if (Foreground is SolidColorBrush foreBrush)
            {
                foreground = foreBrush.Color;
            }
            if (Background is SolidColorBrush backBrush)
            {
                background = backBrush.Color;
            }

            canvas.Source = PlaceholderHelper.GetQr(newValue, foreground, background);
        }

        #region Text

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(QrCode), new PropertyMetadata(null, OnTextChanged));

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((QrCode)d).OnTextChanged((string)e.NewValue, (string)e.OldValue);
        }

        #endregion
    }
}
