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
    public sealed partial class SettingsStickersFeaturedPage : Page
    {
        public SettingsStickersFeaturedViewModel ViewModel => DataContext as SettingsStickersFeaturedViewModel;

        public SettingsStickersFeaturedPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<SettingsStickersFeaturedViewModel>();
        }

        private async void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            await StickerSetView.Current.ShowAsync((TLMessagesStickerSet)e.ClickedItem);
        }
    }
}
