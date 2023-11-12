//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Controls;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels.Supergroups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Supergroups.Popups
{
    public sealed partial class SupergroupReactionsPopup : ContentPopup
    {
        public SupergroupReactionsViewModel ViewModel => DataContext as SupergroupReactionsViewModel;

        public SupergroupReactionsPopup()
        {
            InitializeComponent();

            Title = Strings.Reactions;

            PrimaryButtonText = Strings.Save;
            SecondaryButtonText = Strings.Cancel;
        }

        private void OnContainerContentChanged(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var animated = args.ItemContainer.ContentTemplateRoot as AnimatedImage;
            var reaction = args.Item as EmojiReaction;

            if (animated != null)
            {
                var file = reaction.CenterAnimation.StickerValue;
                animated.Source = new DelayedFileSource(ViewModel.ClientService, file);

                if (file.Local.IsDownloadingCompleted)
                {
                }
                else
                {
                }
            }

            Automation.SetToolTip(args.ItemContainer, reaction.Title);
        }

        private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is AnimatedImage player)
            {
                player.Play();
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is AnimatedImage player)
            {
                player.FrameSize = e.NewSize;
            }
        }

        #region Binding

        private bool ConvertType(ChatType type, bool channel)
        {
            if (type is ChatTypeSupergroup supergroup)
            {
                return supergroup.IsChannel == channel;
            }

            return channel;
        }

        private bool? ConvertAvailable(SupergroupAvailableReactions value)
        {
            return value != SupergroupAvailableReactions.None;
        }

        private void ConvertAvailableBack(bool? value)
        {
            ViewModel.Available = value == false
                ? SupergroupAvailableReactions.None
                : SupergroupAvailableReactions.Some;
        }

        private string ConvertFooter(SupergroupAvailableReactions value)
        {
            return value switch
            {
                SupergroupAvailableReactions.All => Strings.EnableAllReactionsInfo,
                SupergroupAvailableReactions.None => Strings.DisableReactionsInfo,
                _ => Strings.EnableSomeReactionsInfo
            };
        }

        #endregion

        private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            ViewModel.Execute();
        }
    }
}
