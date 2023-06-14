//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Text;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Messages
{
    public sealed class MessageReference : MessageReferenceBase
    {
        public MessageReference()
        {
            DefaultStyleKey = typeof(MessageReference);
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new MessageReferenceAutomationPeer(this);
        }

        public string GetNameCore()
        {
            var builder = new StringBuilder();

            if (TitleLabel != null)
            {
                builder.Append(TitleLabel.Text);
                builder.Append(": ");
            }

            if (ServiceLabel != null)
            {
                builder.Append(ServiceLabel.Text);
            }

            if (MessageLabel != null)
            {
                foreach (var entity in MessageLabel.Inlines)
                {
                    if (entity is Run run)
                    {
                        builder.Append(run.Text);
                    }
                }
            }

            return builder.ToString();
        }

        #region InitializeComponent

        private Grid LayoutRoot;
        private Run TitleLabel;
        private Run ServiceLabel;
        private Span MessageLabel;

        // Lazy loaded
        private Border ThumbRoot;
        private Border ThumbEllipse;
        private ImageBrush ThumbImage;

        protected override void OnApplyTemplate()
        {
            LayoutRoot = GetTemplateChild(nameof(LayoutRoot)) as Grid;
            TitleLabel = GetTemplateChild(nameof(TitleLabel)) as Run;
            ServiceLabel = GetTemplateChild(nameof(ServiceLabel)) as Run;
            MessageLabel = GetTemplateChild(nameof(MessageLabel)) as Span;

            _templateApplied = true;

            if (_light)
            {
                VisualStateManager.GoToState(this, "LightState", false);
            }

            if (_messageReply != null)
            {
                UpdateMessageReply(_messageReply);
            }
            else if (_message != null)
            {
                UpdateMessage(_message, _loading, _title);
            }
            else if (Message != null)
            {
                OnMessageChanged(Message as MessageComposerHeader);
            }
        }

        #endregion

        private bool _light;
        private bool _tinted;

        private string _currentState;

        public void ToLightState()
        {
            if (_currentState != "LightState")
            {
                _light = true;
                _currentState = "LightState";
                VisualStateManager.GoToState(this, "LightState", false);
            }
        }

        public void ToNormalState()
        {
            if (_currentState != (_tinted ? "TintedState" : "NormalState"))
            {
                _light = false;
                _currentState = _tinted ? "TintedState" : "NormalState";
                VisualStateManager.GoToState(this, _tinted ? "TintedState" : "NormalState", false);
            }
        }

        #region Overrides

        private static readonly CornerRadius _defaultRadius = new(2);

        protected override void HideThumbnail()
        {
            if (ThumbRoot != null)
            {
                ThumbRoot.Visibility = Visibility.Collapsed;
            }
        }

        protected override void ShowThumbnail(CornerRadius radius = default)
        {
            if (ThumbRoot == null)
            {
                ThumbRoot = GetTemplateChild(nameof(ThumbRoot)) as Border;
                ThumbEllipse = GetTemplateChild(nameof(ThumbEllipse)) as Border;
                ThumbImage = GetTemplateChild(nameof(ThumbImage)) as ImageBrush;
            }

            ThumbRoot.Visibility = Visibility.Visible;
            ThumbRoot.CornerRadius =
                ThumbEllipse.CornerRadius = radius == default ? _defaultRadius : radius;
        }

        protected override void SetThumbnail(ImageSource value)
        {
            if (ThumbImage != null)
            {
                ThumbImage.ImageSource = value;
            }
        }

        protected override void SetText(MessageViewModel message, MessageSender sender, string title, string service, FormattedText text)
        {
            if (TitleLabel != null)
            {
                TitleLabel.Text = title ?? string.Empty;
                ServiceLabel.Text = service ?? string.Empty;

                if (!string.IsNullOrEmpty(text?.Text) && !string.IsNullOrEmpty(service))
                {
                    ServiceLabel.Text += ", ";
                }

                if (_light || sender is null)
                {
                    ClearValue(BorderBrushProperty);
                    TitleLabel.ClearValue(TextElement.ForegroundProperty);

                    _tinted = false;
                    VisualStateManager.GoToState(this, _light ? "LightState" : "NormalState", false);
                }
                else
                {
                    _tinted = true;
                    VisualStateManager.GoToState(this, "TintedState", false);

                    if (sender is MessageSenderUser user)
                    {
                        BorderBrush = PlaceholderImage.GetBrush(user.UserId);
                        TitleLabel.Foreground = PlaceholderImage.GetBrush(user.UserId);
                    }
                    else if (sender is MessageSenderChat chat)
                    {
                        BorderBrush = PlaceholderImage.GetBrush(chat.ChatId);
                        TitleLabel.Foreground = PlaceholderImage.GetBrush(chat.ChatId);
                    }
                }

                MessageLabel.Inlines.Clear();

                if (text != null)
                {
                    var clean = text.ReplaceSpoilers();
                    var previous = 0;

                    if (text.Entities != null)
                    {
                        foreach (var entity in text.Entities)
                        {
                            if (entity.Type is not TextEntityTypeCustomEmoji customEmoji)
                            {
                                continue;
                            }

                            if (entity.Offset > previous)
                            {
                                MessageLabel.Inlines.Add(new Run { Text = clean.Substring(previous, entity.Offset - previous) });
                            }

                            var player = new CustomEmojiIcon();
                            player.Source = new CustomEmojiFileSource(message.ClientService, customEmoji.CustomEmojiId);
                            player.Margin = new Thickness(0, -4, 0, -4);
                            player.IsHitTestVisible = false;

                            var inline = new InlineUIContainer();
                            inline.Child = player;

                            MessageLabel.Inlines.Add(inline);

                            previous = entity.Offset + entity.Length;
                        }
                    }

                    if (clean.Length > previous)
                    {
                        MessageLabel.Inlines.Add(new Run { Text = clean.Substring(previous) });
                    }
                }
            }
        }

        #endregion

        public double ContentWidth { get; set; }

        protected override Size MeasureOverride(Size availableSize)
        {
            Logger.Debug();

            if (ContentWidth > 0 && ContentWidth <= availableSize.Width)
            {
                LayoutRoot.Measure(new Size(Math.Max(144, ContentWidth), availableSize.Height));
                return LayoutRoot.DesiredSize;
            }

            return base.MeasureOverride(availableSize);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            Logger.Debug();

            if (ContentWidth > 0 && ContentWidth <= finalSize.Width)
            {
                LayoutRoot.Arrange(new Rect(0, 0, finalSize.Width, LayoutRoot.DesiredSize.Height));
                return new Size(finalSize.Width, LayoutRoot.DesiredSize.Height);
            }

            return base.ArrangeOverride(finalSize);
        }
    }

    public class MessageReferenceAutomationPeer : HyperlinkButtonAutomationPeer
    {
        private readonly MessageReference _owner;

        public MessageReferenceAutomationPeer(MessageReference owner)
            : base(owner)
        {
            _owner = owner;
        }

        protected override string GetNameCore()
        {
            return _owner.GetNameCore();
        }
    }
}
