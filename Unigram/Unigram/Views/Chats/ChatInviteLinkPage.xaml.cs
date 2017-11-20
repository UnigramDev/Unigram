using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.ViewModels.Chats;
using Windows.ApplicationModel.DataTransfer;
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

namespace Unigram.Views.Chats
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ChatInviteLinkPage : Page
    {
        public ChatInviteLinkViewModel ViewModel => DataContext as ChatInviteLinkViewModel;

        public ChatInviteLinkPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<ChatInviteLinkViewModel>();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            DataTransferManager.GetForCurrentView().DataRequested += OnDataRequested;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            DataTransferManager.GetForCurrentView().DataRequested -= OnDataRequested;
        }

        private void OnDataRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            if (ViewModel.InviteLink != null)
            {
                args.Request.Data.Properties.Title = ViewModel.Item.DisplayName;
                args.Request.Data.SetWebLink(new Uri(ViewModel.InviteLink));
            }
        }

        private void Share_Click(object sender, RoutedEventArgs e)
        {
            DataTransferManager.ShowShareUI();
        }

        #region Binding

        private string ConvertType(string broadcast, string mega)
        {
            if (ViewModel.Item is TLChannel channel)
            {
                return LocaleHelper.GetString(channel.IsBroadcast ? broadcast : mega);
            }

            return LocaleHelper.GetString(mega);
        }

        #endregion

    }
}
