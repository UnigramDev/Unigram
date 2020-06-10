using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.ViewModels;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views
{
    public sealed partial class ChatsNearbyPage : HostedPage
    {
        public ChatsNearbyViewModel ViewModel => DataContext as ChatsNearbyViewModel;

        public ChatsNearbyPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<ChatsNearbyViewModel>();
        }

        private void OnElementPrepared(Microsoft.UI.Xaml.Controls.ItemsRepeater sender, Microsoft.UI.Xaml.Controls.ItemsRepeaterElementPreparedEventArgs args)
        {
            var button = args.Element as Button;
            var content = button.Content as Grid;

            var nearby = button.DataContext as ChatNearby;

            var chat = ViewModel.CacheService.GetChat(nearby.ChatId);
            if (chat == null)
            {
                return;
            }

            var title = content.Children[1] as TextBlock;
            title.Text = ViewModel.ProtoService.GetTitle(chat);

            if (ViewModel.CacheService.TryGetSupergroup(chat, out Supergroup supergroup))
            {
                var subtitle = content.Children[2] as TextBlock;
                subtitle.Text = string.Format("{0}, {1}", BindConvert.Distance(nearby.Distance), Locale.Declension("Members", supergroup.MemberCount));
            }
            else
            {
                var subtitle = content.Children[2] as TextBlock;
                subtitle.Text = BindConvert.Distance(nearby.Distance);
            }

            var photo = content.Children[0] as ProfilePicture;
            photo.Source = PlaceholderHelper.GetChat(ViewModel.ProtoService, chat, 36);

            button.Command = ViewModel.OpenChatCommand;
            button.CommandParameter = nearby;
        }
    }
}
