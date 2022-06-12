using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Navigation;
using Unigram.Services;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls.Cells
{
    public sealed partial class UserCell : Grid
    {
        public UserCell()
        {
            InitializeComponent();
        }

        public void UpdateUser(IProtoService protoService, User user, ContainerContentChangingEventArgs args, TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> callback)
        {
            if (args.Phase == 0)
            {
                TitleLabel.Text = user.GetFullName();
                Premium.Visibility = user.IsPremium && protoService.IsPremiumAvailable
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            else if (args.Phase == 1)
            {
                SubtitleLabel.Text = LastSeenConverter.GetLabel(user, false);
                SubtitleLabel.Style = BootStrapper.Current.Resources[user.Status is UserStatusOnline ? "AccentCaptionTextBlockStyle" : "InfoCaptionTextBlockStyle"] as Style;
            }
            else if (args.Phase == 2)
            {
                Photo.SetUser(protoService, user, 36);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }

            args.Handled = true;
        }

        public void UpdateSupergroupPermissions(IProtoService protoService, ContainerContentChangingEventArgs args, TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> callback)
        {
            var member = args.Item as ChatMember;

            var user = protoService.GetMessageSender(member.MemberId) as User;
            if (user == null)
            {
                return;
            }

            if (args.Phase == 0)
            {
                TitleLabel.Text = user.GetFullName();
                Premium.Visibility = user.IsPremium && protoService.IsPremiumAvailable
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            else if (args.Phase == 1)
            {
                SubtitleLabel.Text = ChannelParticipantToTypeConverter.Convert(protoService, member);
            }
            else if (args.Phase == 2)
            {
                Photo.SetUser(protoService, user, 36);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }

            args.Handled = true;
        }

        public void UpdateSupergroupBanned(IProtoService protoService, ContainerContentChangingEventArgs args, TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> callback)
        {
            var member = args.Item as ChatMember;

            var messageSender = protoService.GetMessageSender(member.MemberId);
            if (messageSender == null)
            {
                return;
            }

            if (args.Phase == 0)
            {
                if (messageSender is User user)
                {
                    TitleLabel.Text = user.GetFullName();
                    Premium.Visibility = user.IsPremium && protoService.IsPremiumAvailable
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                }
                else if (messageSender is Chat chat)
                {
                    TitleLabel.Text = chat.Title;
                    Premium.Visibility = Visibility.Collapsed;
                }
            }
            else if (args.Phase == 1)
            {
                SubtitleLabel.Text = ChannelParticipantToTypeConverter.Convert(protoService, member);
            }
            else if (args.Phase == 2)
            {
                if (messageSender is User user)
                {
                    Photo.SetUser(protoService, user, 36);
                }
                else if (messageSender is Chat chat)
                {
                    Photo.SetChat(protoService, chat, 36);
                }
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }

            args.Handled = true;
        }

    }
}
