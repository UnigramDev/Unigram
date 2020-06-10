using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Popups
{
    public sealed partial class JoinChatPopup : ContentPopup
    {
        private IProtoService _protoService;

        public JoinChatPopup(IProtoService protoService, ChatInviteLinkInfo info)
        {
            InitializeComponent();

            _protoService = protoService;

            Photo.Source = PlaceholderHelper.GetChat(protoService, info, 72);

            Title.Text = info.Title;
            Subtitle.Text = ConvertCount(info.MemberCount, info.MemberUserIds.Count == 0);

            PrimaryButtonText = Strings.Resources.ChannelJoin;
            SecondaryButtonText = Strings.Resources.Cancel;

            if (info.MemberUserIds.Count > 0)
            {
                FooterPanel.Visibility = ConvertMoreVisibility(info.MemberCount, info.MemberUserIds.Count);
                Footer.Text = ConvertMore(info.MemberCount, info.MemberUserIds.Count);

                Members.Visibility = Visibility.Visible;
                Members.ItemsSource = protoService.GetUsers(info.MemberUserIds);
            }
            else
            {
                Members.Visibility = Visibility.Collapsed;
            }
        }

        public string ConvertCount(int total, bool broadcast)
        {
            return Locale.Declension(broadcast ? "Subscribers" : "Members", total);
        }

        public Visibility ConvertMoreVisibility(int total, int count)
        {
            return total - count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        public string ConvertMore(int total, int count)
        {
            return string.Format("+{0}", total - count);
        }

        private void Join_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as StackPanel;
            var user = args.Item as User;

            if (args.Phase == 0)
            {
                var title = content.Children[1] as TextBlock;
                title.Text = user.GetFullName();
            }
            else if (args.Phase == 2)
            {
                var photo = content.Children[0] as ProfilePicture;
                photo.Source = PlaceholderHelper.GetUser(_protoService, user, 48);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(OnContainerContentChanging);
            }

            args.Handled = true;
        }
    }
}
