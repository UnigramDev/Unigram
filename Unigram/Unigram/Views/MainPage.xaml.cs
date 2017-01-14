using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reactive.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Template10.Services.SerializationService;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Core.Dependency;
using Unigram.Core.Notifications;
using Unigram.ViewModels;
using Unigram.Views.Settings;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Display;
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
        private object _lastSelectedContact;

        public MainPage()
        {
            InitializeComponent();

            _logicalDpi = DisplayInformation.GetForCurrentView().LogicalDpi;

            NavigationCacheMode = NavigationCacheMode.Required;

            DataContext = UnigramContainer.Instance.ResolveType<MainViewModel>();

            Loaded += OnLoaded;

            searchInit();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            OnStateChanged(null, null);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Frame.BackStack.Clear();

            if (MasterDetail.NavigationService == null)
            {
                MasterDetail.Initialize("Main");
            }

            ViewModel.NavigationService = MasterDetail.NavigationService;

            if (e.Parameter is string)
            {
                var parameter = SerializationService.Json.Deserialize((string)e.Parameter) as string;
                if (parameter != null)
                {
                    var data = Toast.SplitArguments((string)parameter);
                    if (data.ContainsKey("from_id"))
                    {
                        var user = ViewModel.CacheService.GetUser(int.Parse(data["from_id"]));
                        if (user != null)
                        {
                            ClearNavigation();
                            MasterDetail.NavigationService.Navigate(typeof(DialogPage), new TLPeerUser { UserId = user.Id });
                        }
                    }
                    else if (data.ContainsKey("chat_id"))
                    {
                        var chat = ViewModel.CacheService.GetChat(int.Parse(data["chat_id"]));
                        if (chat != null)
                        {
                            ClearNavigation();
                            MasterDetail.NavigationService.Navigate(typeof(DialogPage), new TLPeerChat { ChatId = chat.Id });
                        }
                    }
                    else if (data.ContainsKey("channel_id"))
                    {
                        var chat = ViewModel.CacheService.GetChat(int.Parse(data["channel_id"]));
                        if (chat != null)
                        {
                            ClearNavigation();
                            MasterDetail.NavigationService.Navigate(typeof(DialogPage), new TLPeerChannel { ChannelId = chat.Id });
                        }
                    }
                }
            }
        }

        private void ClearNavigation()
        {
            while (MasterDetail.NavigationService.Frame.BackStackDepth > 1)
            {
                MasterDetail.NavigationService.Frame.BackStack.RemoveAt(1);
            }

            if (MasterDetail.NavigationService.CanGoBack)
            {
                MasterDetail.NavigationService.GoBack();
                MasterDetail.NavigationService.Frame.ForwardStack.Clear();
            }
        }

        private void OnStateChanged(object sender, EventArgs e)
        {
            if (DialogsListView.SelectionMode == ListViewSelectionMode.Multiple)
            {
                ChangeListState();
            }

            if (MasterDetail.CurrentState == MasterDetailState.Narrow)
            {
                DialogsListView.IsItemClickEnabled = true;
                DialogsListView.SelectionMode = ListViewSelectionMode.None;
                DialogsListView.SelectedItem = null;
                Separator.BorderThickness = new Thickness(0);
            }
            else
            {
                DialogsListView.IsItemClickEnabled = false;
                DialogsListView.SelectionMode = ListViewSelectionMode.Single;
                DialogsListView.SelectedItem = _lastSelected;
                Separator.BorderThickness = new Thickness(0, 0, 1, 0);
            }
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (DialogsListView.SelectionMode != ListViewSelectionMode.Multiple)
            {
                _lastSelected = e.ClickedItem;

                var dialog = e.ClickedItem as TLDialog;
                MasterDetail.NavigationService.Navigate(typeof(DialogPage), dialog.Peer);
            }
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DialogsListView.SelectedItem != null && _lastSelected != DialogsListView.SelectedItem && DialogsListView.SelectionMode != ListViewSelectionMode.Multiple)
            {
                _lastSelected = DialogsListView.SelectedItem;

                var dialog = DialogsListView.SelectedItem as TLDialog;
                MasterDetail.NavigationService.Navigate(typeof(DialogPage), dialog.Peer);
            }
        }

        private void cbtnMasterSelect_Click(object sender, RoutedEventArgs e)
        {
            DialogsListView.SelectionMode = ListViewSelectionMode.Multiple;
            cbtnMasterDeleteSelected.Visibility = Visibility.Visible;
            cbtnMasterMuteSelected.Visibility = Visibility.Visible;
            cbtnCancelSelection.Visibility = Visibility.Visible;
            cbtnMasterSelect.Visibility = Visibility.Collapsed;
            cbtnMasterNewChat.Visibility = Visibility.Collapsed;

            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
            SystemNavigationManager.GetForCurrentView().BackRequested += Select_BackRequested;
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

        private void Select_BackRequested(object sender, BackRequestedEventArgs e)
        {
            // Mark event as handled so we don't get bounced out of the app.
            e.Handled = true;
            ChangeListState();
        }

        private void ChangeListState()
        {
            cbtnMasterDeleteSelected.Visibility = Visibility.Collapsed;
            cbtnMasterMuteSelected.Visibility = Visibility.Collapsed;
            cbtnCancelSelection.Visibility = Visibility.Collapsed;
            cbtnMasterSelect.Visibility = Visibility.Visible;
            cbtnMasterNewChat.Visibility = Visibility.Visible;
            DialogsListView.SelectionMode = ListViewSelectionMode.Single;
            SystemNavigationManager.GetForCurrentView().BackRequested -= Select_BackRequested;

            if (!ViewModel.NavigationService.CanGoBack)
            {
                SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed;
            }
        }

        private void cbtnCancelSelection_Click(object sender, RoutedEventArgs e)
        {
            ChangeListState();
        }

        private void searchInit()
        {
            var observable = Observable.FromEventPattern<TextChangedEventArgs>(SearchDialogs, "TextChanged");
            var throttled = observable.Throttle(TimeSpan.FromMilliseconds(500)).ObserveOnDispatcher().Subscribe(x => 
            {
                if (string.IsNullOrWhiteSpace(SearchDialogs.Text))
                {
                    return;
                }

                ViewModel.Dialogs.SearchAsync(SearchDialogs.Text);
            });
        }

        private void PivotItem_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Execute.BeginOnUIThread(async () =>
                {
                    ViewModel.Dialogs.LoadFirstSlice();
                    //await ViewModel.Contacts.getTLContacts(); 
                    await ViewModel.Contacts.GetSelfAsync();
                });
            }
            catch { }
        }

        private void UsersListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (UsersListView.SelectedItem != null && _lastSelectedContact != UsersListView.SelectedItem && UsersListView.SelectionMode != ListViewSelectionMode.Multiple)
            {
                var user = UsersListView.SelectedItem as TLUser;
                MasterDetail.NavigationService.Navigate(typeof(DialogPage), new TLPeerUser { UserId = user.Id });
            }
        }

        private void Self_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(DialogPage), new TLPeerUser { UserId = ViewModel.Contacts.Self?.Id ?? 0 });
        }

        private void cbtnMasterSettings_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsPage));
        }

        #region Background

        private float _logicalDpi;
        private CanvasBitmap _backgroundImage;
        private CanvasImageBrush _backgroundBrush;

        private void BackgroundCanvas_CreateResources(CanvasControl sender, CanvasCreateResourcesEventArgs args)
        {
            args.TrackAsyncAction(Task.Run(async () =>
            {
                _backgroundImage = await CanvasBitmap.LoadAsync(sender, new Uri("ms-appx:///Assets/Images/DefaultBackground.png"));
                _backgroundBrush = new CanvasImageBrush(sender, _backgroundImage);
                _backgroundBrush.ExtendX = _backgroundBrush.ExtendY = CanvasEdgeBehavior.Wrap;
                //_backgroundBrush.Transform = Matrix3x2.CreateScale(_logicalDpi / 96f);
            }).AsAsyncAction());
        }

        private void BackgroundCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            args.DrawingSession.FillRectangle(new Rect(new Point(), sender.RenderSize), _backgroundBrush);
        }

        #endregion

        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (SearchDialogs.Text != "")
            {
                DialogsSearchListView.Visibility = Visibility.Visible;
            }
            else
            {
                //  lvMasterChats.Visibility = Visibility.Visible;
                DialogsSearchListView.Visibility = Visibility.Collapsed;
                // lvMasterChats.ItemsSource = ViewModel.Dialogs;
            }
        }

        #region Context menu

        private void DialogPin_Loaded(object sender, RoutedEventArgs e)
        {
            var element = sender as MenuFlyoutItem;
            if (element != null)
            {
                var dialog = element.DataContext as TLDialog;
                if (dialog != null)
                {
                    element.Text = dialog.IsPinned ? "Unpin from top" : "Pin to top";
                }
            }
        }

        private void DialogClear_Loaded(object sender, RoutedEventArgs e)
        {
            var element = sender as MenuFlyoutItem;
            if (element != null)
            {
                var dialog = element.DataContext as TLDialog;
                if (dialog != null)
                {
                    element.Visibility = dialog.Peer is TLPeerChannel ? Visibility.Collapsed : Visibility.Visible;
                }
            }
        }

        private void DialogDelete_Loaded(object sender, RoutedEventArgs e)
        {
            var element = sender as MenuFlyoutItem;
            if (element != null)
            {
                var dialog = element.DataContext as TLDialog;
                if (dialog != null)
                {
                    var channelPeer = dialog.Peer as TLPeerChannel;
                    if (channelPeer != null)
                    {
                        var channel = dialog.With as TLChannel;
                        if (channel != null)
                        {
                            if (channel.IsCreator)
                            {
                                element.Text = channel.IsMegagroup ? "Delete group" : "Delete channel";
                            }
                            else
                            {
                                element.Text = channel.IsMegagroup ? "Leave group" : "Leave channel";
                            }
                        }

                        element.Visibility = Visibility.Visible;
                        return;
                    }

                    var userPeer = dialog.Peer as TLPeerUser;
                    if (userPeer != null)
                    {
                        element.Text = "Delete conversation";
                        element.Visibility = Visibility.Visible;
                        return;
                    }

                    var chatPeer = dialog.Peer as TLPeerChat;
                    if (chatPeer != null)
                    {
                        element.Text = "Delete conversation";
                        element.Visibility = dialog.With is TLChatForbidden || dialog.With is TLChatEmpty ? Visibility.Visible : Visibility.Collapsed;
                        return;
                    }
                }
            }
        }

        private void DialogDeleteAndStop_Loaded(object sender, RoutedEventArgs e)
        {
            var element = sender as MenuFlyoutItem;
            if (element != null)
            {
                var dialog = element.DataContext as TLDialog;
                if (dialog != null)
                {
                    var user = dialog.With as TLUser;
                    if (user != null)
                    {
                        element.Visibility = user.IsBot && !user.Blocked ? Visibility.Visible : Visibility.Collapsed;
                    }
                    else
                    {
                        element.Visibility = Visibility.Collapsed;
                    }
                }
            }
        }

        private void DialogDeleteAndExit_Loaded(object sender, RoutedEventArgs e)
        {
            var element = sender as MenuFlyoutItem;
            if (element != null)
            {
                var dialog = element.DataContext as TLDialog;
                if (dialog != null)
                {
                    element.Visibility = dialog.Peer is TLPeerChat ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        #endregion
    }
}
