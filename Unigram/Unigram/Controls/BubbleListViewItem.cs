using Telegram.Api.TL;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Unigram.Controls
{
    public class BubbleListViewItem : ListViewItem
    {
        public readonly BubbleListView Messages;

        public BubbleListViewItem(BubbleListView messages)
        {
            Messages = messages;
        }

        #region ContentMargin

        public Thickness ContentMargin
        {
            get { return (Thickness)GetValue(ContentMarginProperty); }
            set { SetValue(ContentMarginProperty, value); }
        }

        public static readonly DependencyProperty ContentMarginProperty =
            DependencyProperty.Register("ContentMargin", typeof(Thickness), typeof(BubbleListViewItem), new PropertyMetadata(default(Thickness)));

        #endregion

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            if (Messages.SelectionMode == ListViewSelectionMode.Multiple)
            {
                if (Content is TLMessageService serviceMessage && !(serviceMessage.Action is TLMessageActionPhoneCall))
                {
                    e.Handled = true;
                }
                else if (Content is TLMessage message)
                {
                    if (message.Media is TLMessageMediaPhoto photoMedia && photoMedia.HasTTLSeconds)
                    {
                        e.Handled = true;
                    }
                    else if (message.Media is TLMessageMediaDocument documentMedia && documentMedia.HasTTLSeconds)
                    {
                        e.Handled = true;
                    }
                }
            }

            base.OnPointerPressed(e);
        }
    }
}
