using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.TL;
using Unigram.Controls.Views;
using Unigram.ViewModels.Channels;
using Unigram.ViewModels.Users;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Views.Channels
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ChannelBannedRightsPage : Page
    {
        public ChannelBannedRightsViewModel ViewModel => DataContext as ChannelBannedRightsViewModel;

        public ChannelBannedRightsPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<ChannelBannedRightsViewModel>();
        }

        private async void Photo_Click(object sender, RoutedEventArgs e)
        {
            var user = ViewModel.Item.User as TLUser;
            var userFull = ViewModel.Full as TLUserFull;
            if (userFull != null && userFull.ProfilePhoto is TLPhoto && user != null)
            {
                var viewModel = new UserPhotosViewModel(ViewModel.ProtoService, userFull, user);
                await GalleryView.Current.ShowAsync(viewModel, () => Picture);
            }
        }
    }
}
