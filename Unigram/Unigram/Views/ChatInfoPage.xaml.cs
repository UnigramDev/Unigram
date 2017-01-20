using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.TL;
using Unigram.Core.Dependency;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views
{
    public sealed partial class ChatInfoPage : Page
    {
        public ChatInfoViewModel ViewModel => DataContext as ChatInfoViewModel;

        public ChatInfoPage()
        {
            this.InitializeComponent();

            DataContext = UnigramContainer.Instance.ResolveType<ChatInfoViewModel>();

            SizeChanged += OnSizeChanged;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            VisualStateManager.GoToState(this, e.NewSize.Width < 500 ? "NarrowState" : "FilledState", false);
        }

        private void UsersListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ViewModel.NavigationService.Navigate(typeof(UserInfoPage), new TLPeerUser { UserId = ((TLUser)UsersListView.SelectedItem).Id });
        }
    }
}
