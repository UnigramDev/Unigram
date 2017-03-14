using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reactive.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Template10.Services.SerializationService;
using Unigram.Common;
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
using Windows.UI.ViewManagement;
using Unigram.Views.Channels;
using Unigram.ViewModels.Chats;
using Unigram.Views.Chats;
using Windows.System.Profile;
using Windows.ApplicationModel.Core;

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
            DataContext = UnigramContainer.Current.ResolveType<MainViewModel>();

            _logicalDpi = DisplayInformation.GetForCurrentView().LogicalDpi;

            Loaded += OnLoaded;

            Theme.RegisterPropertyChangedCallback(Border.BackgroundProperty, OnThemeChanged);

            searchInit();

            InputPane.GetForCurrentView().Showing += (s, args) => args.EnsuredFocusedElementInView = true;
        }

        private async void OnThemeChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (_canvas != null)
            {
                _backgroundImage = await CanvasBitmap.LoadAsync(_canvas, new Uri("ms-appx:///Assets/Images/DefaultBackground.png"));
                _backgroundBrush = new CanvasImageBrush(_canvas, _backgroundImage);
                _backgroundBrush.ExtendX = _backgroundBrush.ExtendY = CanvasEdgeBehavior.Wrap;
                _canvas.Invalidate();
            }
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
                MasterDetail.NavigationService.Frame.Navigated += OnNavigated;
            }

            ViewModel.NavigationService = MasterDetail.NavigationService;

            if (e.Parameter is string)
            {
                if (SerializationService.Json.Deserialize((string)e.Parameter) is string parameter)
                {
                    var data = Toast.SplitArguments(parameter);
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

            var config = ViewModel.CacheService.GetConfig();
            if (config != null)
            {
                if (config.IsPhoneCallsEnabled)
                {

                }
                else if (rpMasterTitlebar.Items.Count > 2)
                {
                    rpMasterTitlebar.Items.RemoveAt(2);
                }
            }
        }

        private void OnNavigated(object sender, NavigationEventArgs e)
        {
            if (e.SourcePageType == typeof(DialogPage))
            {
                var parameter = MasterDetail.NavigationService.SerializationService.Deserialize((string)e.Parameter);
                var tuple = parameter as Tuple<TLPeerBase, int>;
                if (tuple != null)
                {
                    parameter = tuple.Item1;
                }

                UpdateListViewsSelectedItem(parameter as TLPeerBase);
            }
        }

        private void UpdateListViewsSelectedItem(TLPeerBase peer)
        {
            if (peer == null)
            {
                _lastSelected = null;
                DialogsListView.SelectedItem = null;

                _lastSelected = null;
                UsersListView.SelectedItem = null;
                return;
            }

            var dialog = ViewModel.Dialogs.Items.FirstOrDefault(x => x.Peer.Equals(peer));
            if (dialog != null)
            {
                _lastSelected = dialog;
                DialogsListView.SelectedItem = dialog;
            }
            else
            {
                _lastSelected = null;
                DialogsListView.SelectedItem = null;
            }

            var user = ViewModel.Contacts.Items.FirstOrDefault(x => x.Id == peer.Id);
            if (user != null)
            {
                _lastSelected = user;
                UsersListView.SelectedItem = user;
            }
            else
            {
                _lastSelected = null;
                UsersListView.SelectedItem = null;
            }
        }

        private TLPeerBase GetPeerFromBackStack()
        {
            if (MasterDetail.NavigationService.CurrentPageType == typeof(DialogPage))
            {
                if (TryGetPeerFromParameter(MasterDetail.NavigationService.CurrentPageParam, out TLPeerBase peer))
                {
                    return peer;
                }
            }

            for (int i = MasterDetail.NavigationService.Frame.BackStackDepth - 1; i >= 0; i--)
            {
                var entry = MasterDetail.NavigationService.Frame.BackStack[i];
                if (entry.SourcePageType == typeof(DialogPage))
                {
                    if (TryGetPeerFromParameter(entry.Parameter, out TLPeerBase peer))
                    {
                        return peer;
                    }
                }
            }

            return null;
        }

        private bool TryGetPeerFromParameter(object parameter, out TLPeerBase peer)
        {
            if (parameter is string)
            {
                parameter = MasterDetail.NavigationService.SerializationService.Deserialize((string)parameter);
            }

            var tuple = parameter as Tuple<TLPeerBase, int>;
            if (tuple != null)
            {
                parameter = tuple.Item1;
            }

            peer = parameter as TLPeerBase;
            return peer != null;
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
                DialogsSearchListView.IsItemClickEnabled = true;
                DialogsSearchListView.SelectionMode = ListViewSelectionMode.None;
                DialogsSearchListView.SelectedItem = null;
                UsersListView.IsItemClickEnabled = true;
                UsersListView.SelectionMode = ListViewSelectionMode.None;
                UsersListView.SelectedItem = null;
                Separator.BorderThickness = new Thickness(0);
            }
            else
            {
                DialogsListView.IsItemClickEnabled = false;
                DialogsListView.SelectionMode = ListViewSelectionMode.Single;
                DialogsListView.SelectedItem = _lastSelected;
                DialogsSearchListView.IsItemClickEnabled = false;
                DialogsSearchListView.SelectionMode = ListViewSelectionMode.Single;
                DialogsSearchListView.SelectedItem = _lastSelected;
                UsersListView.IsItemClickEnabled = false;
                UsersListView.SelectionMode = ListViewSelectionMode.Single;
                UsersListView.SelectedItem = _lastSelected;
                Separator.BorderThickness = new Thickness(0, 0, 1, 0);
            }
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var listView = sender as ListView;
            if (listView.SelectionMode != ListViewSelectionMode.Multiple)
            {
                _lastSelected = e.ClickedItem;

                if (e.ClickedItem is TLDialog dialog)
                {
                    if (dialog.IsSearchResult)
                    {
                        MasterDetail.NavigationService.Navigate(typeof(DialogPage), Tuple.Create(dialog.Peer, dialog.TopMessage));
                    }
                    else
                    {
                        MasterDetail.NavigationService.Navigate(typeof(DialogPage), dialog.Peer);
                    }
                }

                if (e.ClickedItem is TLMessageCommonBase message)
                {
                    var peer = message.IsOut || message.ToId is TLPeerChannel || message.ToId is TLPeerChat ? message.ToId : new TLPeerUser { UserId = message.FromId.Value };
                    MasterDetail.NavigationService.Navigate(typeof(DialogPage), Tuple.Create(peer, message.Id));
                }

                if (e.ClickedItem is TLUser user)
                {
                    MasterDetail.NavigationService.Navigate(typeof(DialogPage), new TLPeerUser { UserId = user.Id });
                }

                if (e.ClickedItem is TLChannel channel)
                {
                    MasterDetail.NavigationService.Navigate(typeof(DialogPage), new TLPeerChannel { ChannelId = channel.Id });
                }
            }
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listView = sender as ListView;
            if (listView.SelectedItem != null)
            {
                listView.ScrollIntoView(listView.SelectedItem);
            }
            else
            {
                UpdateListViewsSelectedItem(GetPeerFromBackStack());
            }

            if (listView.SelectedItem != null && _lastSelected != listView.SelectedItem)
            {
                _lastSelected = listView.SelectedItem;

                if (listView.SelectedItem is TLDialog dialog)
                {
                    if (dialog.IsSearchResult)
                    {
                        MasterDetail.NavigationService.Navigate(typeof(DialogPage), Tuple.Create(dialog.Peer, dialog.TopMessage));
                    }
                    else
                    {
                        MasterDetail.NavigationService.Navigate(typeof(DialogPage), dialog.Peer);
                    }
                }

                if (listView.SelectedItem is TLMessageCommonBase message)
                {
                    var peer = message.IsOut || message.ToId is TLPeerChannel || message.ToId is TLPeerChat ? message.ToId : new TLPeerUser { UserId = message.FromId.Value };
                    MasterDetail.NavigationService.Navigate(typeof(DialogPage), Tuple.Create(peer, message.Id));
                }

                if (listView.SelectedItem is TLUser user)
                {
                    MasterDetail.NavigationService.Navigate(typeof(DialogPage), new TLPeerUser { UserId = user.Id });
                }

                if (listView.SelectedItem is TLChannel channel)
                {
                    MasterDetail.NavigationService.Navigate(typeof(DialogPage), new TLPeerChannel { ChannelId = channel.Id });
                }
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

            //SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
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

            //if (!ViewModel.NavigationService.CanGoBack)
            //{
            //    SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed;
            //}
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
                    ViewModel.Dialogs.Search.Clear();
                    return;
                }

                ViewModel.Dialogs.SearchAsync(SearchDialogs.Text);
            });
        }

        private void PivotItem_Loaded(object sender, RoutedEventArgs e)
        {
            var dialogs = ViewModel.Dialogs;
            var contacts = ViewModel.Contacts;

            try
            {
                Execute.BeginOnThreadPool(() =>
                {
                    dialogs.LoadFirstSlice();
                    contacts.LoadContacts();
                });

                //ViewModel.Contacts.getTLContacts();
                ViewModel.Contacts.GetSelfAsync();
            }
            catch { }
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

        private CanvasControl _canvas;

        private void BackgroundCanvas_CreateResources(CanvasControl sender, CanvasCreateResourcesEventArgs args)
        {
            _canvas = sender;

            args.TrackAsyncAction(Task.Run(async () =>
            {
                _backgroundImage = await CanvasBitmap.LoadAsync(sender, new Uri("ms-appx:///Assets/Images/DefaultBackground.png"));
                _backgroundBrush = new CanvasImageBrush(sender, _backgroundImage);
                _backgroundBrush.ExtendX = _backgroundBrush.ExtendY = CanvasEdgeBehavior.Wrap;
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
                                element.Text = channel.IsMegaGroup ? "Delete group" : "Delete channel";
                            }
                            else
                            {
                                element.Text = channel.IsMegaGroup ? "Leave group" : "Leave channel";
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
                        element.Visibility = user.IsBot && !user.IsBlocked ? Visibility.Visible : Visibility.Collapsed;
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

        private void NewChat_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(CreateChatStep1Page));
        }

        private void NewChannel_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(CreateChannelStep1Page));
        }
    }
}
