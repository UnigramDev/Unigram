using Telegram.Td.Api;
using Unigram.Charts;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Cells;
using Unigram.ViewModels.Chats;
using Unigram.ViewModels.Delegates;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Chats
{
    public sealed partial class MessageStatisticsPage : HostedPage, IChatDelegate
    {
        public MessageStatisticsViewModel ViewModel => DataContext as MessageStatisticsViewModel;

        public MessageStatisticsPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<MessageStatisticsViewModel, IChatDelegate>(this);
        }

        #region Delegate

        public void UpdateChat(Chat chat)
        {
            UpdateChatTitle(chat);
            UpdateChatPhoto(chat);
        }

        public void UpdateChatTitle(Chat chat)
        {
            Title.Text = ViewModel.CacheService.GetTitle(chat);
        }

        public void UpdateChatPhoto(Chat chat)
        {
            Photo.Source = PlaceholderHelper.GetChat(ViewModel.ProtoService, chat, 36);
        }

        #endregion

        #region Binding

        private string ConvertViews(Message message)
        {
            if (message?.InteractionInfo != null)
            {
                return message.InteractionInfo.ViewCount.ToString("N0");
            }

            return string.Empty;
        }

        private string ConvertPublicShares(Message message, int totalCount)
        {
            return totalCount.ToString("N0");
        }

        private string ConvertPrivateShares(Message message, int totalCount)
        {
            if (message?.InteractionInfo != null)
            {
                return string.Format("≈{0:N0}", message.InteractionInfo.ForwardCount - totalCount);
            }

            return string.Empty;
        }

        #endregion

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var button = args.ItemContainer.ContentTemplateRoot as Button;
            var message = args.Item as Message;

            var content = button.Content as Grid;

            var title = content.Children[1] as TextBlock;
            var subtitle = content.Children[2] as TextBlock;

            var photo = content.Children[0] as ProfilePicture;

            var chat = ViewModel.CacheService.GetChat(message.ChatId);
            if (chat == null)
            {
                return;
            }

            title.Text = chat.Title;
            subtitle.Text = Locale.Declension("Views", message.InteractionInfo?.ViewCount ?? 0);

            photo.Source = PlaceholderHelper.GetChat(ViewModel.ProtoService, chat, 36);

            button.CommandParameter = message;
            button.Command = ViewModel.OpenPostCommand;
        }

        private void OnElementPrepared(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            var root = sender as ChartCell;
            var data = args.NewValue as ChartViewData;

            var header = root.Items[0] as ChartHeaderView;
            var border = root.Items[1] as AspectView;
            var checks = root.Items[2] as WrapPanel;

            root.Header = data.title;
            border.Children.Clear();
            border.Constraint = data;

            root.UpdateData(data);
        }
    }
}
