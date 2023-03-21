//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Controls;
using Telegram.Td.Api;
using Telegram.ViewModels.Supergroups;
using Windows.Foundation;
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

            var player = element.FindName("Player") as LottieView;
            if (player != null)
            {
                player.FrameSize = new Size(48, 48);

                var file = reaction.Reaction.CenterAnimation.StickerValue;
                if (file.Local.IsDownloadingCompleted)
                {
                    player.Source = UriEx.ToLocal(file.Local.Path);
                }
                else
                {
                    player.Source = null;

                    UpdateManager.Subscribe(player, ViewModel.ClientService, file, UpdateFile, true);

                    if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                    {
                        ViewModel.ClientService.DownloadFile(file.Id, 16);
                    }
                }
            }
        }

        private void UpdateFile(object target, File file)
        {
            if (target is LottieView player && player.IsLoaded)
            {
                player.Source = UriEx.ToLocal(file.Local.Path);
            }
        }

        private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                var player = element.FindName("Player") as LottieView;
                if (player != null)
                {
                    player.Play();
                }
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
