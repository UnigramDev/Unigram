using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.TL;
using Unigram.Controls;
using Unigram.Controls.Views;
using Unigram.Strings;
using Unigram.Themes;
using Unigram.ViewModels.Channels;
using Unigram.Views.Users;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using LinqToVisualTree;
using Unigram.Common;
using Telegram.Api.Helpers;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Views.Channels
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ChannelAdminLogPage : Page
    {
        public ChannelAdminLogViewModel ViewModel => DataContext as ChannelAdminLogViewModel;

        public ChannelAdminLogPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<ChannelAdminLogViewModel>();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _panel = (ItemsStackPanel)Messages.ItemsPanelRoot;

            var scroll = Messages.Descendants<ScrollViewer>().FirstOrDefault() as ScrollViewer;
            if (scroll != null)
            {
                scroll.ViewChanged += OnViewChanged;
            }
        }

        private void Photo_Click(object sender, RoutedEventArgs e)
        {
            var control = sender as FrameworkElement;
            var message = control.DataContext as TLMessage;
            if (message != null && message.HasFromId)
            {
                ViewModel.NavigationService.Navigate(typeof(UserDetailsPage), new TLPeerUser { UserId = message.FromId.Value });
            }
        }

        private void Download_Click(object sender, TransferCompletedEventArgs e)
        {
            Media.Download_Click(sender as FrameworkElement, e);
        }

        private async void StickerSet_Click(object sender, RoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            var message = element.DataContext as TLMessage;

            if (message?.Media is TLMessageMediaDocument documentMedia && documentMedia.Document is TLDocument document)
            {
                var stickerAttribute = document.Attributes.OfType<TLDocumentAttributeSticker>().FirstOrDefault();
                if (stickerAttribute != null && stickerAttribute.StickerSet.TypeId != TLType.InputStickerSetEmpty)
                {
                    await StickerSetView.Current.ShowAsync(stickerAttribute.StickerSet);
                }
            }
        }

        private async void Help_Click(object sender, RoutedEventArgs e)
        {
            var channel = ViewModel.Item as TLChannel;
            if (channel == null)
            {
                return;
            }

            await TLMessageDialog.ShowAsync(channel.IsMegaGroup ? Strings.Android.EventLogInfoDetail : Strings.Android.EventLogInfoDetailChannel, Strings.Android.EventLogInfoTitle, Strings.Android.OK);
        }

        private async void Settings_Click(object sender, RoutedEventArgs e)
        {
            var channel = ViewModel.Item as TLChannel;
            if (channel == null)
            {
                return;
            }

            await ChannelAdminLogFilterView.Current.ShowAsync(channel.ToPeer());
        }

        #region Binding

        private string ConvertType(string broadcast, string mega)
        {
            if (ViewModel.Item is TLChannel channel)
            {
                return LocaleHelper.GetString(channel.IsBroadcast ? broadcast : mega);
            }

            return null;
        }

        #endregion
    }
}
