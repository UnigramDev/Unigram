using Telegram.Td.Api;
using Unigram.Common;
using Unigram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Unigram.Controls.Chats
{
    public class ChatListViewItem : LazoListViewItem
    {
        public readonly ChatListView Messages;

        public ChatListViewItem(ChatListView messages)
            : base(messages)
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
            DependencyProperty.Register("ContentMargin", typeof(Thickness), typeof(ChatListViewItem), new PropertyMetadata(default(Thickness)));

        #endregion

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            if (Messages.SelectionMode == ListViewSelectionMode.Multiple && !IsSelected)
            {
                e.Handled = CantSelect();

                //if (Content is TLMessageService serviceMessage)
                //{
                //    e.Handled = true;
                //}
                //else if (Content is TLMessage message)
                //{
                //    if (message.Media is TLMessageMediaDocument documentMedia && documentMedia.HasTTLSeconds)
                //    {
                //        e.Handled = true;
                //    }
                //}
            }

            base.OnPointerPressed(e);
        }

        public override bool CantSelect()
        {
            return ContentTemplateRoot is FrameworkElement element && element.Tag is MessageViewModel message && message.IsService();
        }
    }
}
