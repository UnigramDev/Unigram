//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Controls;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels.Supergroups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Supergroups
{
    public sealed partial class SupergroupReactionsPage : HostedPage
    {
        public SupergroupReactionsViewModel ViewModel => DataContext as SupergroupReactionsViewModel;

        public SupergroupReactionsPage()
        {
            InitializeComponent();
            Title = Strings.Reactions;
        }

        private void OnContainerContentChanged(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var element = args.ItemContainer.ContentTemplateRoot as FrameworkElement;
            var reaction = args.Item as SupergroupReactionOption;

            var animated = element.FindName("Player") as AnimatedImage;
            if (animated != null)
            {
                var file = reaction.Reaction.CenterAnimation.StickerValue;
                animated.Source = new DelayedFileSource(ViewModel.ClientService, file);

                if (file.Local.IsDownloadingCompleted)
                {
                }
                else
                {
                }
            }
        }

        private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                var player = element.FindName("Player") as AnimatedImage;
                player?.Play();
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

    }
}
