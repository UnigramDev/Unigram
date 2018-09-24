using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.ViewModels.Chats;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Chats
{
    public sealed partial class ChatInvitePage : Page
    {
        public ChatInviteViewModel ViewModel => DataContext as ChatInviteViewModel;

        public ChatInvitePage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<ChatInviteViewModel>();
        }

        private void Invite_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.NavigationService.Navigate(typeof(ChatInviteLinkPage), ViewModel.Chat.Id);
        }
    }
}
