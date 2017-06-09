using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.TL;
using Unigram.Views;
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
using Unigram.Common;

namespace Unigram.Views.Users
{
    public sealed partial class UserCommonChatsPage : Page
    {
        public UserCommonChatsViewModel ViewModel => DataContext as UserCommonChatsViewModel;

        public UserCommonChatsPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<UserCommonChatsViewModel>();
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ITLDialogWith with)
            {
                ViewModel.NavigationService.NavigateToDialog(with);
            }
        }
    }
}
