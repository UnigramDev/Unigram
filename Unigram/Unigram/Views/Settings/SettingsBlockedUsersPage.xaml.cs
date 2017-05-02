using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
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

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsBlockedUsersPage : Page
    {
        public SettingsBlockedUsersViewModel ViewModel => DataContext as SettingsBlockedUsersViewModel;

        public SettingsBlockedUsersPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<SettingsBlockedUsersViewModel>();
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            ViewModel.UnblockCommand.Execute(e.ClickedItem);
        }
    }
}
