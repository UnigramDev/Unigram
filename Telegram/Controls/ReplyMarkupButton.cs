//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;

namespace Telegram.Controls
{
    public partial class ReplyMarkupButton : GlyphButton
    {
        public ReplyMarkupButton(KeyboardButton button)
        {
            DefaultStyleKey = typeof(ReplyMarkupButton);
            Button = button;
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new ReplyMarkupButtonAutomationPeer(this);
        }

        public KeyboardButton Button { get; }

        private UIElement IconPresenter;
        private UIElement EmojiPresenter;

        protected override void OnApplyTemplate()
        {
            if (!string.IsNullOrEmpty(Icon))
            {
                IconPresenter = GetTemplateChild(nameof(IconPresenter)) as UIElement;
                IconPresenter.Visibility = Visibility.Visible;
            }

            if (Source != null)
            {
                EmojiPresenter = GetTemplateChild(nameof(EmojiPresenter)) as UIElement;
                EmojiPresenter.Visibility = Visibility.Visible;
            }

            base.OnApplyTemplate();
        }

        #region Icon

        public string Icon
        {
            get { return (string)GetValue(IconProperty); }
            set { SetValue(IconProperty, value); }
        }

        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register("Icon", typeof(string), typeof(ReplyMarkupButton), new PropertyMetadata(string.Empty, OnIconChanged));

        private static void OnIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as ReplyMarkupButton;
            if (sender?.IconPresenter != null || !string.IsNullOrEmpty((string)e.NewValue))
            {
                sender.IconPresenter ??= sender.GetTemplateChild(nameof(sender.IconPresenter)) as UIElement;
                sender.IconPresenter?.Visibility = string.IsNullOrEmpty((string)e.NewValue)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
        }

        #endregion

        #region Source

        public AnimatedImageSource Source
        {
            get { return (AnimatedImageSource)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register(nameof(Source), typeof(AnimatedImageSource), typeof(ReplyMarkupButton), new PropertyMetadata(null, OnSourceChanged));

        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as ReplyMarkupButton;
            if (sender?.EmojiPresenter != null || e.NewValue != null)
            {
                sender.EmojiPresenter ??= sender.GetTemplateChild(nameof(sender.EmojiPresenter)) as UIElement;
                sender.EmojiPresenter?.Visibility = e.NewValue == null
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
        }

        #endregion
    }

    public partial class ReplyMarkupButtonAutomationPeer : ButtonAutomationPeer
    {
        private readonly ReplyMarkupButton _owner;

        public ReplyMarkupButtonAutomationPeer(ReplyMarkupButton owner)
            : base(owner)
        {
            _owner = owner;
        }

        protected override string GetNameCore()
        {
            return Automation.GetNameCore(_owner);
        }
    }
}
