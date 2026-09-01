//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls
{
    /// <summary>
    /// A toast that counts down to an action, with a button to call it off.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="ToastPopup.ShowCountdownAsync"/> this one stays addressable, so a repeat
    /// of the same action can rewrite the text and start the countdown over on the toast already
    /// up rather than stacking a second one.
    /// </remarks>
    public partial class UndoToastPopup
    {
        private readonly ToastPopup _toast;
        private readonly TextBlock _label;
        private readonly SelfDestructTimer _slice;
        private readonly AnimatedTextBlock _value;
        private readonly DispatcherTimer _timer;
        private readonly Button _undo;

        private readonly int _seconds;
        private int _remaining;

        /// <summary>
        /// The countdown ran out: whatever was deferred is to be carried out.
        /// </summary>
        public event TypedEventHandler<UndoToastPopup, object> Committed;

        /// <summary>
        /// The button was pressed: whatever was deferred is to be called off.
        /// </summary>
        public event TypedEventHandler<UndoToastPopup, object> Undone;

        public bool IsOpen => _toast.IsOpen;

        /// <summary>
        /// Answers null when the toast could not be shown, which a caller has to treat as the
        /// action never having been offered.
        /// </summary>
        public static UndoToastPopup Show(XamlRoot xamlRoot, FormattedText text, ToastPopupIcon? icon, string action, TimeSpan duration)
        {
            var toast = icon is ToastPopupIcon value
                ? ToastPopup.Show(xamlRoot, text, value, dismissAfter: TimeSpan.Zero)
                : ToastPopup.Show(xamlRoot, text, dismissAfter: TimeSpan.Zero);
            if (toast?.Content is Grid content && content.Children.Count > 0 && content.Children[0] is TextBlock label)
            {
                return new UndoToastPopup(toast, content, label, action, duration);
            }

            return null;
        }

        private UndoToastPopup(ToastPopup toast, Grid content, TextBlock label, string action, TimeSpan duration)
        {
            _toast = toast;
            _label = label;

            _seconds = (int)duration.TotalSeconds;
            _remaining = _seconds;

            toast.MaxWidth = 500;
            toast.MinWidth = 336;

            _undo = new Button
            {
                Content = action,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Style = BootStrapper.Current.Resources["AccentTextButtonStyle"] as Style,
                Margin = new Thickness(8, -4, -4, -4),
                Padding = new Thickness(4, 5, 4, 6)
            };

            _slice = new SelfDestructTimer
            {
                Background = new SolidColorBrush(Colors.Transparent),
                Foreground = _undo.Foreground,
                Width = 22,
                Height = 22,
                Center = 11,
                Radius = 9.5
            };

            _value = new AnimatedTextBlock
            {
                Foreground = _undo.Foreground,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 1, 0, 1),
                TextStyle = BootStrapper.Current.Resources["CaptionTextBlockStyle"] as Style,
                Text = _remaining.ToString()
            };

            _slice.Maximum = _seconds;
            _slice.Value = DateTime.Now.AddSeconds(_seconds);

            var animated = new Grid
            {
                Height = 32,
                Margin = new Thickness(8, -12, -4, -12)
            };

            animated.ColumnDefinitions.Add(1, GridUnitType.Auto);
            animated.ColumnDefinitions.Add(32, GridUnitType.Pixel);

            Grid.SetColumn(_slice, 1);
            Grid.SetColumn(_value, 1);

            animated.Children.Add(_slice);
            animated.Children.Add(_value);
            animated.Children.Add(_undo);

            // Column 2 of the toast's own grid: the label is at 1 and the icon at 0.
            Grid.SetColumn(animated, 2);
            content.Children.Add(animated);

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };

            _undo.Click += OnClick;
            _timer.Tick += OnTick;
            _timer.Start();
        }

        /// <summary>
        /// Rewrites the text and starts the countdown over, for an action joining the one already
        /// pending.
        /// </summary>
        public void Extend(FormattedText text)
        {
            TextBlockHelper.SetFormattedText(_label, text);

            _remaining = _seconds;
            _value.Text = _remaining.ToString();

            _slice.Maximum = _seconds;
            _slice.Value = DateTime.Now.AddSeconds(_seconds);

            _timer.Stop();
            _timer.Start();
        }

        private void OnTick(object sender, object e)
        {
            _remaining--;
            _value.Text = _remaining.ToString();

            if (_remaining <= 0)
            {
                Close();
                Committed?.Invoke(this, null);
            }
        }

        private void OnClick(object sender, RoutedEventArgs e)
        {
            Close();
            Undone?.Invoke(this, null);
        }

        private void Close()
        {
            _undo.Click -= OnClick;
            _timer.Tick -= OnTick;
            _timer.Stop();

            _toast.IsOpen = false;
        }
    }
}
