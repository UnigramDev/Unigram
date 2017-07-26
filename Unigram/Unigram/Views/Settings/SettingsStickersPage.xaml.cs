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
using System.Diagnostics;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsStickersPage : Page
    {
        public SettingsStickersViewModel ViewModel => DataContext as SettingsStickersViewModel;

        public SettingsStickersPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<SettingsStickersViewModel>();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            while (Frame.BackStackDepth > 1)
            {
                Frame.BackStack.RemoveAt(1);
            }
        }

        private void FeaturedStickers_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsStickersFeaturedPage));
        }

        private void ArchivedStickers_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsStickersArchivedPage));
        }

        private void Masks_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsMasksPage));
        }

        private async void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            await StickerSetView.Current.ShowAsync((TLMessagesStickerSet)e.ClickedItem);
        }

        private void ListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            if (args.DropResult == Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move)
            {
                ViewModel.ReorderCommand.Execute(args.Items.FirstOrDefault());
            }
        }
    }
}
