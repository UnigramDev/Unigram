using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TdWindows;
using Unigram.Common;
using Unigram.Services;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Controls.Views
{
    public sealed partial class JoinChatView : BottomSheet
    {
        private IProtoService _protoService;

        public JoinChatView(IProtoService protoService, ChatInviteLinkInfo info)
        {
            InitializeComponent();

            _protoService = protoService;

            Photo.Source = PlaceholderHelper.GetChat(protoService, info, 72, 72);

            Title.Text = info.Title;
            Subtitle.Text = ConvertCount(info.MemberCount, info.MemberUserIds.Count == 0);

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
            Hide(ContentDialogBaseResult.OK);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Hide(ContentDialogBaseResult.Cancel);
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
                photo.Source = PlaceholderHelper.GetUser(_protoService, user, 48, 48);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(OnContainerContentChanging);
            }

            args.Handled = true;
        }
    }
}
