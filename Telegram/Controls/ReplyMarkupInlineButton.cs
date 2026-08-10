//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Controls.Messages;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls
{
    public partial class ReplyMarkupInlineButton : GlyphButton
    {
        public readonly ReplyMarkupInlinePanel _owner;

        public ReplyMarkupInlineButton(ReplyMarkupInlinePanel owner, InlineKeyboardButton button)
        {
            _owner = owner;

            DefaultStyleKey = typeof(ReplyMarkupInlineButton);
            Button = button;
        }

        public ReplyMarkupInlineButton()
        {
            DefaultStyleKey = typeof(ReplyMarkupInlineButton);
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new ReplyMarkupInlineButtonAutomationPeer(this);
        }

        /// <summary>
        /// Applies a button's appearance — label, emoji icon, type glyph and style — from
        /// the <c>InlineKeyboardButtonType</c> / <c>ButtonStyle</c> vocabulary.
        ///
        /// Shared because an inline keyboard under a message and a pageBlockButtonRow in
        /// an instant view describe their buttons with exactly the same two enums: they
        /// should look identical, and a new button type should only have to be drawn once.
        /// Callers still own layout, Click and (for a keyboard) the Button payload.
        /// </summary>
        /// <param name="text">
        /// The label, or null when the caller has already supplied its own content — an
        /// instant-view button keeps its RichText label in a FormattedTextBlock, which a
        /// plain string would overwrite.
        /// </param>
        /// <param name="receipt">An already-paid invoice: the Buy button reads as a receipt.</param>
        public void SetButton(IClientService clientService, string text, long iconCustomEmojiId, ButtonStyle style, InlineKeyboardButtonType type, bool receipt = false)
        {
            if (text != null)
            {
                Content = text.Replace('\n', ' ');
            }

            if (iconCustomEmojiId != 0)
            {
                Source = new CustomEmojiFileSource(clientService, iconCustomEmojiId);
            }

            var disabled = false;

            switch (type)
            {
                case InlineKeyboardButtonTypeUrl typeUrl:
                    Glyph = "\uE9B7";
                    Extensions.SetToolTip(this, typeUrl.Url);
                    break;
                case InlineKeyboardButtonTypeLoginUrl:
                    Glyph = "\uE9B7";
                    break;
                case InlineKeyboardButtonTypeSwitchInline:
                    Glyph = "\uEE35";
                    break;
                case InlineKeyboardButtonTypeBuy:
                    if (text != null)
                    {
                        Content = receipt ? Strings.PaymentReceipt : text.ReplaceStar(Icons.Premium);
                    }
                    break;
                case InlineKeyboardButtonTypeWebApp:
                    Glyph = Icons.Window16;
                    break;
                case InlineKeyboardButtonTypeCopyText:
                    Glyph = Icons.CopyFilled16;
                    break;

                case InlineKeyboardButtonTypeSuggestionDecline suggestionDecline:
                    IsEnabled = suggestionDecline.IsEnabled;
                    Icon = Icons.DismissCircleFilled;
                    break;
                case InlineKeyboardButtonTypeSuggestionApprove suggestionApprove:
                    IsEnabled = suggestionApprove.IsEnabled;
                    Icon = Icons.CheckmarkCircleFilled;
                    break;
                case InlineKeyboardButtonTypeSuggestionEdit:
                    Icon = Icons.EditFilled;
                    break;
            }

            if (text != null)
            {
                switch (style)
                {
                    case ButtonStylePrimary:
                        Background = new SolidColorBrush(Color.FromArgb(0xB2, 0x22, 0x9a, 0xf0));
                        break;
                    case ButtonStyleDanger:
                        Background = new SolidColorBrush(Color.FromArgb(0xB2, 0xdb, 0x46, 0x46));
                        break;
                    case ButtonStyleSuccess:
                        Background = new SolidColorBrush(Color.FromArgb(0xB2, 0x40, 0xb1, 0x35));
                        break;
                }
            }
            else
            {
                switch (style)
                {
                    case ButtonStylePrimary:
                        Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x7a, 0xff));
                        Foreground = new SolidColorBrush(disabled ? Color.FromArgb(0xFF, 0x66, 0xaf, 0xff) : Color.FromArgb(0xFF, 0xff, 0xff, 0xff));
                        break;
                    case ButtonStyleDanger:
                        Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xff, 0xe2, 0xe0));
                        Foreground = new SolidColorBrush(disabled ? Color.FromArgb(0xFF, 0xff, 0x8e, 0x88) : Color.FromArgb(0xFF, 0xff, 0x3b, 0x30));
                        break;
                    case ButtonStyleSuccess:
                        Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xe8, 0xf6, 0xec));
                        Foreground = new SolidColorBrush(disabled ? Color.FromArgb(0xFF, 0x83, 0xce, 0x96) : Color.FromArgb(0xFF, 0x1e, 0xa6, 0x41));
                        break;
                    default:
                        Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xe4, 0xe4, 0xe6));
                        Foreground = new SolidColorBrush(disabled ? Color.FromArgb(0xFF, 0x94, 0x94, 0x95) : Color.FromArgb(0xFF, 0x00, 0x00, 0x00));
                        break;
                }
            }
        }

        public InlineKeyboardButton Button { get; }

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
            DependencyProperty.Register("Icon", typeof(string), typeof(ReplyMarkupInlineButton), new PropertyMetadata(string.Empty, OnIconChanged));

        private static void OnIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as ReplyMarkupInlineButton;
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
            DependencyProperty.Register(nameof(Source), typeof(AnimatedImageSource), typeof(ReplyMarkupInlineButton), new PropertyMetadata(null, OnSourceChanged));

        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as ReplyMarkupInlineButton;
            if (sender?.EmojiPresenter != null || e.NewValue != null)
            {
                sender.EmojiPresenter ??= sender.GetTemplateChild(nameof(sender.EmojiPresenter)) as UIElement;
                sender.EmojiPresenter?.Visibility = e.NewValue == null
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
        }

        #endregion

        protected override void OnKeyDown(KeyRoutedEventArgs e)
        {
            if (e.Key is VirtualKey.Left or VirtualKey.Right && Parent is Panel panel)
            {
                e.Handled = true;

                var index = panel.Children.IndexOf(this);

                Control control = null;
                if (e.Key == VirtualKey.Left && index > 0)
                {
                    control = panel.Children[index - 1] as Control;
                }
                else if (e.Key == VirtualKey.Right && index < panel.Children.Count - 1)
                {
                    control = panel.Children[index + 1] as Control;
                }

                control?.Focus(Windows.UI.Xaml.FocusState.Keyboard);
            }
            if (e.Key is >= VirtualKey.Left and <= VirtualKey.Down && false)
            {
                e.Handled = true;

                var direction = e.Key switch
                {
                    VirtualKey.Left => FocusNavigationDirection.Left,
                    VirtualKey.Up => FocusNavigationDirection.Up,
                    VirtualKey.Right => FocusNavigationDirection.Right,
                    VirtualKey.Down => FocusNavigationDirection.Down,
                    _ => FocusNavigationDirection.Next
                };

                FocusManager.TryMoveFocus(direction, new FindNextElementOptions { SearchRoot = Parent });
            }

            base.OnKeyDown(e);
        }
    }

    public partial class ReplyMarkupInlineButtonAutomationPeer : ButtonAutomationPeer
    {
        private readonly ReplyMarkupInlineButton _owner;

        public ReplyMarkupInlineButtonAutomationPeer(ReplyMarkupInlineButton owner)
            : base(owner)
        {
            _owner = owner;
        }

        protected override string GetNameCore()
        {
            return Automation.GetNameCore(_owner.ContentTemplateRoot ?? _owner) ?? base.GetNameCore();
        }

        protected override AutomationControlType GetAutomationControlTypeCore()
        {
            if (_owner._owner != null)
            {
                return AutomationControlType.ListItem;
            }

            return AutomationControlType.Button;
        }

        protected override int GetPositionInSetCore()
        {
            if (_owner._owner != null)
            {
                return 1 + _owner._owner.Children.IndexOf(_owner);
            }

            return base.GetPositionInSetCore();
        }

        protected override int GetSizeOfSetCore()
        {
            if (_owner._owner != null)
            {
                return _owner._owner.Children.Count;
            }

            return base.GetSizeOfSetCore();
        }
    }
}
