using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.TL;
using Unigram.Core.Dependency;
using Unigram.ViewModels;
using Unigram.ViewModels.Chats;
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

namespace Unigram.Views.Chats
{
    public sealed partial class ChatDetailsPage : Page
    {
        public ChatDetailsViewModel ViewModel => DataContext as ChatDetailsViewModel;

        public ChatDetailsPage()
        {
            this.InitializeComponent();

            DataContext = UnigramContainer.Current.ResolveType<ChatDetailsViewModel>();

            SizeChanged += OnSizeChanged;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            VisualStateManager.GoToState(this, e.NewSize.Width < 500 ? "NarrowState" : "FilledState", false);
        }

        private void UsersListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ViewModel.NavigationService.Navigate(typeof(UserDetailsPage), new TLPeerUser { UserId = ((TLUser)UsersListView.SelectedItem).Id });
        }
    }
}
