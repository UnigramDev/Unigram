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
    }
}
