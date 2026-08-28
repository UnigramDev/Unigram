//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Delegates;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace Telegram.Controls.Messages
{
    public partial class MessageService : Button, IReactionsDelegate
    {
        private MessageViewModel _message;

        public MessageService()
        {
            DefaultStyleKey = typeof(MessageService);

            Telegram.Common.Instrumentation.Register(this);
        }

        public MessageViewModel Message => _message;

        #region ContentOpacity

        public double ContentOpacity
        {
            get { return (double)GetValue(ContentOpacityProperty); }
            set { SetValue(ContentOpacityProperty, value); }
        }

        public static readonly DependencyProperty ContentOpacityProperty =
            DependencyProperty.Register("ContentOpacity", typeof(double), typeof(MessageService), new PropertyMetadata(1d));

        #endregion

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            var content = FindName("Text") as FormattedTextBlock;
            if (content != null)
            {
                content.TextEntityClick += Message_TextEntityClick;
            }

            if (_message != null)
            {
                UpdateMessageInteractionInfo(_message);
            }
        }

        private void Message_TextEntityClick(object sender, TextEntityClickEventArgs e)
        {
            if (_message is not MessageViewModel message || message.Delegate == null)
            {
                return;
            }

            if (e.Type is TextEntityTypeMention && e.Text is string username)
            {
                message.Delegate.OpenUsername(username);
            }
            else if (e.Type is TextEntityTypeMentionName mentionName)
            {
                message.Delegate.OpenUser(mentionName.UserId);
            }
            else if (e.Type is TextEntityTypeTextUrl textUrl)
            {
                message.Delegate.OpenUrl(textUrl.Url, true, new OpenUrlSourceChat(message.ChatId, message.SenderId));
            }
            else if (e.Type is TextEntityTypeUrl && e.Text is string url)
            {
                message.Delegate.OpenUrl(url, false, new OpenUrlSourceChat(message.ChatId, message.SenderId));
            }
        }

        public void UpdateMessage(MessageViewModel message)
        {
            _message = message;

            var content = FindName("Text") as FormattedTextBlock;
            if (content == null)
            {
                UpdateContent(message);
                return;
            }

            var entities = MessageServiceText.GetEntities(message, true);
            if (entities.Text != null)
            {
                content.SetText(message.ClientService, entities);
                AutomationProperties.SetName(this, entities.Text);
            }

            UpdateContent(message);
            UpdateMessageInteractionInfo(message);
        }

        /// <summary>
        /// Called when the container goes back on the recycle queue: releases what the
        /// control holds, so a service message off screen doesn't keep its whole view model
        /// (and the inlines its text built) alive.
        ///
        /// Overrides must also reset any template state <see cref="UpdateContent"/> only
        /// sets conditionally — the same container comes back for another message of the
        /// same type, and whatever isn't reset is inherited by it.
        /// </summary>
        public virtual void Recycle()
        {
            if (FindName("Text") is FormattedTextBlock content)
            {
                content.Clear();
            }

            _message = null;
        }

        /// <summary>
        /// Fills in whatever the subclass's own template shows; the base control only
        /// knows how to render the message as text.
        /// </summary>
        protected virtual void UpdateContent(MessageViewModel message)
        {
        }

        public void UpdateMessageInteractionInfo(MessageViewModel message)
        {
            UpdateMessageReactions(message, false);
        }

        public void UpdateMessageReactions(MessageViewModel message, bool animate)
        {
            // TODO: Name
            var reactions = GetTemplateChild("Reactions") as ReactionsPanel;
            reactions?.UpdateMessageReactions(message, animate);
        }
    }

    public partial class DashedLine : Path
    {
        private readonly LineGeometry _geometry;

        public DashedLine()
        {
            _geometry = new LineGeometry();
            Data = _geometry;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var size = base.MeasureOverride(availableSize);
            return new Size(size.Width, 2);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            _geometry.StartPoint = new Windows.Foundation.Point(0, 1);
            _geometry.EndPoint = new Windows.Foundation.Point(finalSize.Width, 1);

            var size = base.ArrangeOverride(finalSize);
            return new Size(size.Width, 2);
        }
    }
}
