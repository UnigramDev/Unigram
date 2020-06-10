using System;
using Unigram.Common;
using Unigram.ViewModels.Chats;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Chats
{
    public sealed partial class ChatInviteLinkPage : HostedPage
    {
        public ChatInviteLinkViewModel ViewModel => DataContext as ChatInviteLinkViewModel;

        public ChatInviteLinkPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<ChatInviteLinkViewModel>();

            Transitions = ApiInfo.CreateSlideTransition();
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
                args.Request.Data.Properties.Title = ViewModel.Chat.Title;
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
            //if (ViewModel.Item is TLChannel channel)
            //{
            //    return Locale.GetString(channel.IsBroadcast ? broadcast : mega);
            //}

            return Locale.GetString(mega);
        }

        #endregion

    }
}
