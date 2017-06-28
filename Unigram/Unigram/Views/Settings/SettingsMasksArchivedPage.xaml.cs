using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.TL;
using Unigram.Controls.Views;
using Unigram.Views;
using Unigram.ViewModels.Settings;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Telegram.Api.TL.Messages;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsMasksArchivedPage : Page
    {
        public SettingsMasksArchivedViewModel ViewModel => DataContext as SettingsMasksArchivedViewModel;

        public SettingsMasksArchivedPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<SettingsMasksArchivedViewModel>();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            while (Frame.BackStackDepth > 3)
            {
                Frame.BackStack.RemoveAt(3);
            }
        }

        private async void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TLMessagesStickerSet stickerSet)
            {
                // WARNING: we are using TLMessagesStickerSet only because we already expanded it to supply "Cover" property.
                // but this IS NOT a full sticker set.
                await StickerSetView.Current.ShowAsync(new TLInputStickerSetID { Id = stickerSet.Set.Id, AccessHash = stickerSet.Set.AccessHash });
            }
        }
    }
}
