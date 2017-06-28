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
    public sealed partial class SettingsMasksPage : Page
    {
        public SettingsMasksViewModel ViewModel => DataContext as SettingsMasksViewModel;

        public SettingsMasksPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<SettingsMasksViewModel>();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            while (Frame.BackStackDepth > 2)
            {
                Frame.BackStack.RemoveAt(2);
            }
        }

        private void ArchivedStickers_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsMasksArchivedPage));
        }

        private async void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            await StickerSetView.Current.ShowAsync((TLMessagesStickerSet)e.ClickedItem);
        }
    }
}
