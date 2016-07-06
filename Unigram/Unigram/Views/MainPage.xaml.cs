using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.TL;
using Template10.Services.SerializationService;
using Unigram.Controls;
using Unigram.Core.Dependency;
using Unigram.Core.Notifications;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainViewModel ViewModel => DataContext as MainViewModel;

        private object _lastSelected;

        public MainPage()
        {
            InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Required;

            DataContext = UnigramContainer.Instance.ResolverType<MainViewModel>();

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            OnStateChanged(null, null);
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            Frame.BackStack.Clear();

            if (MasterDetail.NavigationService == null)
            {
                MasterDetail.Initialize("Main");
                ViewModel.NavigationService = MasterDetail.NavigationService;
            }

            if (e.Parameter is string)
            {
                var parameter = SerializationService.Json.Deserialize((string)e.Parameter) as string;
                if (parameter != null)
                {
                    var data = Toast.SplitArguments((string)parameter);
                    if (data.ContainsKey("from_id"))
                    {
                        var user = ViewModel.CacheService.GetUser(int.Parse(data["from_id"]));
                        if (user == null)
                        {
                            // TODO: ViewModel.ProtoService.GetUsersAsync
                            var users = await ViewModel.ProtoService.GetUsersAsync(new TLVector<TLInputUserBase>(new[] { new TLInputUser { UserId = int.Parse(data["from_id"]) } }));
                            if (users.IsSucceeded)
                            {
                                user = users.Value[0];
                            }
                        }

                        if (user != null)
                        {
                            ViewModel.NavigationService.Navigate(typeof(UserInfoPage), user);
                        }
                    }
                }
            }
        }

        private void OnStateChanged(object sender, EventArgs e)
        {
            if (lvMasterChats.SelectionMode == ListViewSelectionMode.Multiple)
            {
                ChangeListState();
            }

            if (MasterDetail.CurrentState == MasterDetailState.Narrow)
            {
                lvMasterChats.IsItemClickEnabled = true;
                lvMasterChats.SelectionMode = ListViewSelectionMode.None;
                lvMasterChats.SelectedItem = null;
            }
            else
            {
                lvMasterChats.IsItemClickEnabled = false;
                lvMasterChats.SelectionMode = ListViewSelectionMode.Single;
                lvMasterChats.SelectedItem = _lastSelected;
            }
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (lvMasterChats.SelectionMode != ListViewSelectionMode.Multiple)
            {
                _lastSelected = e.ClickedItem;

                var dialog = e.ClickedItem as TLDialog;
                if (dialog.With is TLUserBase)
                {
                    ViewModel.NavigationService.Navigate(typeof(UserInfoPage), dialog.With);
                }
            }
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lvMasterChats.SelectedItem != null && _lastSelected != lvMasterChats.SelectedItem && lvMasterChats.SelectionMode != ListViewSelectionMode.Multiple)
            {
                _lastSelected = lvMasterChats.SelectedItem;

                var dialog = lvMasterChats.SelectedItem as TLDialog;
                if (dialog.With is TLUserBase)
                {
                    ViewModel.NavigationService.Navigate(typeof(UserInfoPage), dialog.With);
                }
                else if (dialog.With is TLChat)
                {
                    var ciccio = dialog.With as TLChat;
                    ViewModel.NavigationService.Navigate(typeof(DialogSharedMediaPage), new TLInputPeerChat { ChatId = ciccio.Id });
                }
                else if (dialog.With is TLChannel)
                {
                    var ciccio = dialog.With as TLChannel;
                    ViewModel.NavigationService.Navigate(typeof(DialogSharedMediaPage), new TLInputPeerChannel { ChannelId = ciccio.Id, AccessHash = ciccio.AccessHash.Value });
                }
            }
        }

        private void cbtnMasterSelectAll_Click(object sender, RoutedEventArgs e)
        {
            lvMasterChats.SelectionMode = ListViewSelectionMode.Multiple;
            cbtnMasterDeleteSelected.Visibility = Visibility.Visible;
            cbtnMasterMuteSelected.Visibility = Visibility.Visible;
            cbtnMasterSelectAll.Visibility = Visibility.Collapsed;
            cbtnMasterNewChat.Visibility = Visibility.Collapsed;

            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
            SystemNavigationManager.GetForCurrentView().BackRequested += (s, _) =>
            {
                ChangeListState();
            };
        }

        private void cbtnMasterAbout_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.NavigationService.Navigate(typeof(AboutPage));
        }

        private void cbtnMasterSearch_Click(object sender, RoutedEventArgs e)
        {
            //PLEASE REMOVE THE BELOW LINE ONCE THE CHATPAGE HAS BEEN IMPLEMENTED
            ViewModel.NavigationService.Navigate(typeof(DialogSharedMediaPage));
        }

        private void ChangeListState()
        {
            Visibility act = Visibility.Collapsed;
            Visibility act1 = Visibility.Visible;

            cbtnMasterDeleteSelected.Visibility = act;
            cbtnMasterMuteSelected.Visibility = act;
            cbtnMasterSelectAll.Visibility = act1;
            cbtnMasterNewChat.Visibility = act1;
            lvMasterChats.SelectionMode = ListViewSelectionMode.Single;
            if (!ViewModel.NavigationService.CanGoBack)
            {
                SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed;
            }
        }
    }
}
