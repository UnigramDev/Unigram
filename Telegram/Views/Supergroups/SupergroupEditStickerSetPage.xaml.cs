//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels.Supergroups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Supergroups.Popups
{
    public sealed partial class SupergroupEditStickerSetPopup : ContentPopup
    {
        public SupergroupEditStickerSetViewModel ViewModel => DataContext as SupergroupEditStickerSetViewModel;

        private readonly TaskCompletionSource<object> _tsc;

        public SupergroupEditStickerSetPopup(TaskCompletionSource<object> tsc)
        {
            InitializeComponent();
            Title = Strings.GroupStickers;

            _tsc = tsc;

            PrimaryButtonText = Strings.OK;
            SecondaryButtonText = Strings.Cancel;
        }

        #region Recycle

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as Grid;
            var stickerSet = args.Item as StickerSetInfo;

            var title = content.Children[2] as TextBlock;
            title.Text = stickerSet.Title;

            var subtitle = content.Children[3] as TextBlock;
            subtitle.Text = Locale.Declension(Strings.R.Stickers, stickerSet.Size);

            var animated = content.Children[1] as AnimatedImage;
            var cross = content.Children[0];

            var source = DelayedFileSource.FromStickerSetInfo(ViewModel.ClientService, stickerSet);
            if (source == null)
            {
                animated.Source = null;
                cross.Visibility = Visibility.Visible;
                title.Margin = new Thickness(0, 8, 0, -8);
                subtitle.Text = string.Empty;
            }
            else
            {
                animated.Source = source;
                cross.Visibility = Visibility.Collapsed;
                title.Margin = new Thickness();
                subtitle.Text = Locale.Declension(Strings.R.Stickers, stickerSet.Size);
            }

            args.Handled = true;
        }

        #endregion

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            IsPrimaryButtonEnabled = ScrollingHost.SelectedItem != null;
        }

        private void OnClosing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            _tsc.TrySetResult(ScrollingHost.SelectedItem);
        }
    }
}
