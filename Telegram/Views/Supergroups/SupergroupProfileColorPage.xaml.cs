//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Controls.Messages;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels.Drawers;
using Telegram.ViewModels.Settings;
using Telegram.ViewModels.Supergroups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views.Supergroups
{
    public sealed partial class SupergroupProfileColorPage : HostedPage
    {
        public SupergroupProfileColorViewModel ViewModel => DataContext as SupergroupProfileColorViewModel;

        public SupergroupProfileColorPage()
        {
            InitializeComponent();
            Title = Strings.Appearance;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            NameView.Initialize(ViewModel.ClientService, new MessageSenderChat(ViewModel.Chat.Id));
            ProfileView.Initialize(ViewModel.ClientService, new MessageSenderChat(ViewModel.Chat.Id));
        }

        private void EmojiStatus_Click(object sender, RoutedEventArgs e)
        {
            var flyout = EmojiMenuFlyout.ShowAt(ViewModel.ClientService, EmojiDrawerMode.ChatEmojiStatus, Animated, EmojiFlyoutAlignment.TopRight);
            flyout.EmojiSelected += Flyout_EmojiSelected;
        }

        private void Flyout_EmojiSelected(object sender, EmojiSelectedEventArgs e)
        {
            ViewModel.SelectedEmojiStatus = new EmojiStatus(e.CustomEmojiId, 0);

            if (e.CustomEmojiId != 0)
            {
                Animated.Source = new CustomEmojiFileSource(ViewModel.ClientService, e.CustomEmojiId);
                EmojiStatus.Badge = string.Empty;
            }
            else
            {
                Animated.Source = null;
                EmojiStatus.Badge = Strings.UserReplyIconOff;
            }
        }

        #region Recycle

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new GridViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is ChatThemeCell content && args.Item is ChatThemeViewModel theme)
            {
                content.Update(theme);
                args.Handled = true;
            }
        }

        #endregion

        #region Binding

        private string ConvertRequiredLevel(int value, UIElement element)
        {
            if (value > 0)
            {
                element.Visibility = Visibility.Visible;
                return Icons.LockClosedFilled14 + Icons.Spacing + string.Format(Strings.BoostLevel, value);
            }
            else
            {
                element.Visibility = Visibility.Collapsed;
                return string.Empty;
            }
        }

        #endregion

    }
}
