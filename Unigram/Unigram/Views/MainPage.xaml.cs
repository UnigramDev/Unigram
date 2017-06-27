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
using Unigram.Views;
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
using Unigram.Core.Services;
using Telegram.Api.Aggregator;
using Template10.Common;
using Windows.Foundation.Metadata;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Composition;
using Unigram.Views.Users;
using Windows.System;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Automation.Provider;

namespace Unigram.Views
{
    public sealed partial class MainPage : Page, IMasterDetailPage, IHandle<string>
    {
        public MainViewModel ViewModel => DataContext as MainViewModel;

        private object _lastSelected;

        public MainPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<MainViewModel>();

            NavigationCacheMode = NavigationCacheMode.Required;

            ViewModel.Aggregator.Subscribe(this);

            Loaded += OnLoaded;

            //Theme.RegisterPropertyChangedCallback(Border.BackgroundProperty, OnThemeChanged);

            searchInit();

            InputPane.GetForCurrentView().Showing += (s, args) => args.EnsuredFocusedElementInView = true;
        }

        public void OnBackRequested(HandledEventArgs args)
        {
            if (MasterDetail.CurrentState == MasterDetailState.Narrow && rpMasterTitlebar.SelectedIndex != 0)
            {
                rpMasterTitlebar.SelectedIndex = 0;
                args.Handled = true;
            }
            else if (!string.IsNullOrEmpty(SearchDialogs.Text))
            {
                SearchDialogs.Text = string.Empty;
                args.Handled = true;
            }
        }

        public void Handle(string message)
        {
            if (message.Equals("move_up") || message.Equals("move_down"))
            {
                var index = DialogsListView.SelectedIndex;
                if (index == -1)
                {
                    return;
                }

                if (message.Equals("move_up"))
                {
                    index--;
                }
                else if (message.Equals("move_down"))
                {
                    index++;
                }

                if (index >= 0 && index < ViewModel.Dialogs.Items.Count)
                {
                    DialogsListView.SelectedIndex = index;
                    Navigate(DialogsListView.SelectedItem);
                }
            }
            else if (message.Equals("Search"))
            {
                if (MasterDetail.CurrentState == MasterDetailState.Narrow && MasterDetail.NavigationService.CanGoBack)
                {
                    MasterDetail.NavigationService.GoBack();
                }
            }
        }

        //private async void OnThemeChanged(DependencyObject sender, DependencyProperty dp)
        //{
        //    if (_canvas != null)
        //    {
        //        _backgroundImage = await CanvasBitmap.LoadAsync(_canvas, new Uri("ms-appx:///Assets/Images/DefaultBackground.png"));
        //        _backgroundBrush = new CanvasImageBrush(_canvas, _backgroundImage);
        //        _backgroundBrush.ExtendX = _backgroundBrush.ExtendY = CanvasEdgeBehavior.Wrap;
        //        _canvas.Invalidate();
        //    }
        //}

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            OnStateChanged(null, null);
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            Frame.BackStack.Clear();

            if (MasterDetail.NavigationService == null)
            {
                MasterDetail.Initialize("Main", Frame);
                MasterDetail.NavigationService.Frame.Navigated += OnNavigated;
            }

            ViewModel.NavigationService = MasterDetail.NavigationService;
            ViewModel.Dialogs.NavigationService = MasterDetail.NavigationService;
            ViewModel.Contacts.NavigationService = MasterDetail.NavigationService;
            ViewModel.Calls.NavigationService = MasterDetail.NavigationService;
            SettingsView.ViewModel.NavigationService = MasterDetail.NavigationService;

            if (e.Parameter is string)
            {
                if (SerializationService.Json.Deserialize((string)e.Parameter) is string parameter)
                {
                    if (Uri.TryCreate(parameter, UriKind.Absolute, out Uri scheme))
                    {
                        if (MessageHelper.IsTelegramUrl(scheme))
                        {
                            MessageHelper.HandleTelegramUrl(parameter);
                        }
                        else if (scheme.Scheme.Equals("ms-ipmessaging"))
                        {
                            var query = scheme.Query.ParseQueryString();
                            if (query.TryGetValue("ContactRemoteIds", out string remote) && int.TryParse(remote.Substring(1), out int from_id))
                            {
                                var user = ViewModel.CacheService.GetUser(from_id);
                                if (user != null)
                                {
                                    MasterDetail.NavigationService.NavigateToDialog(user);
                                }
                            }
                        }
                        else if (scheme.Scheme.Equals("ms-contact-profile"))
                        {
                            var query = scheme.Query.ParseQueryString();
                            if (query.TryGetValue("ContactRemoteIds", out string remote) && int.TryParse(remote.Substring(1), out int from_id))
                            {
                                var user = ViewModel.CacheService.GetUser(from_id);
                                if (user != null)
                                {
                                    MasterDetail.NavigationService.Navigate(typeof(UserDetailsPage), user.ToPeer());
                                }
                            }
                        }
                        else
                        {
                            string username = null;
                            string group = null;
                            string sticker = null;
                            string botUser = null;
                            string botChat = null;
                            string message = null;
                            string phone = null;
                            string game = null;
                            string phoneHash = null;
                            string post = null;
                            string server = null;
                            string port = null;
                            string user = null;
                            string pass = null;
                            bool hasUrl = false;

                            var query = scheme.Query.ParseQueryString();
                            if (scheme.AbsoluteUri.StartsWith("tg:resolve") || scheme.AbsoluteUri.StartsWith("tg://resolve"))
                            {
                                username = query.GetParameter("domain");
                                botUser = query.GetParameter("start");
                                botChat = query.GetParameter("startgroup");
                                game = query.GetParameter("game");
                                post = query.GetParameter("post");
                            }
                            else if (scheme.AbsoluteUri.StartsWith("tg:join") || scheme.AbsoluteUri.StartsWith("tg://join"))
                            {
                                group = query.GetParameter("invite");
                            }
                            else if (scheme.AbsoluteUri.StartsWith("tg:addstickers") || scheme.AbsoluteUri.StartsWith("tg://addstickers"))
                            {
                                sticker = query.GetParameter("set");
                            }
                            else if (scheme.AbsoluteUri.StartsWith("tg:msg") || scheme.AbsoluteUri.StartsWith("tg://msg") || scheme.AbsoluteUri.StartsWith("tg://share") || scheme.AbsoluteUri.StartsWith("tg:share"))
                            {
                                message = query.GetParameter("url");
                                if (message == null)
                                {
                                    message = "";
                                }
                                if (query.GetParameter("text") != null)
                                {
                                    if (message.Length > 0)
                                    {
                                        hasUrl = true;
                                        message += "\n";
                                    }
                                    message += query.GetParameter("text");
                                }
                                if (message.Length > 4096 * 4)
                                {
                                    message = message.Substring(0, 4096 * 4);
                                }
                                while (message.EndsWith("\n"))
                                {
                                    message = message.Substring(0, message.Length - 1);
                                }
                            }
                            else if (scheme.AbsoluteUri.StartsWith("tg:confirmphone") || scheme.AbsoluteUri.StartsWith("tg://confirmphone"))
                            {
                                phone = query.GetParameter("phone");
                                phoneHash = query.GetParameter("hash");
                            }
                            else if (scheme.AbsoluteUri.StartsWith("tg:socks") || scheme.AbsoluteUri.StartsWith("tg://socks"))
                            {
                                server = query.GetParameter("server");
                                port = query.GetParameter("port");
                                user = query.GetParameter("user");
                                pass = query.GetParameter("pass");
                            }

                            if (message != null && message.StartsWith("@"))
                            {
                                message = " " + message;
                            }

                            if (phone != null || phoneHash != null)
                            {
                                MessageHelper.NavigateToConfirmPhone(ViewModel.ProtoService, phone, phoneHash);
                            }
                            if (server != null && int.TryParse(port, out int portCode))
                            {
                                MessageHelper.NavigateToSocks(server, portCode, user, pass);
                            }
                            else if (group != null)
                            {
                                MessageHelper.NavigateToInviteLink(group);
                            }
                            else if (sticker != null)
                            {
                                MessageHelper.NavigateToStickerSet(sticker);
                            }
                            else if (username != null)
                            {
                                MessageHelper.NavigateToUsername(ViewModel.ProtoService, username, botUser ?? botChat, post, game);
                            }
                            else if (message != null)
                            {
                                MessageHelper.NavigateToShare(message, hasUrl);
                            }
                        }
                    }
                    else
                    {
                        var data = Toast.SplitArguments(parameter);
                        if (data.ContainsKey("from_id") && int.TryParse(data["from_id"], out int from_id))
                        {
                            var user = ViewModel.CacheService.GetUser(from_id);
                            if (user != null)
                            {
                                MasterDetail.NavigationService.NavigateToDialog(user);
                            }
                        }
                        else if (data.ContainsKey("chat_id") && int.TryParse(data["chat_id"], out int chat_id))
                        {
                            var chat = ViewModel.CacheService.GetChat(chat_id);
                            if (chat != null)
                            {
                                MasterDetail.NavigationService.NavigateToDialog(chat);
                            }
                        }
                        else if (data.ContainsKey("channel_id") && int.TryParse(data["channel_id"], out int channel_id))
                        {
                            var channel = ViewModel.CacheService.GetChat(channel_id);
                            if (channel != null)
                            {
                                MasterDetail.NavigationService.NavigateToDialog(channel);
                            }
                        }
                    }
                }
            }

            //var config = ViewModel.CacheService.GetConfig();
            //if (config != null)
            //{
            //    if (config.IsPhoneCallsEnabled)
            //    {

            //    }
            //    else if (rpMasterTitlebar.Items.Count > 2)
            //    {
            //        rpMasterTitlebar.Items.RemoveAt(2);
            //    }
            //}

            await SettingsView.ViewModel.OnNavigatedToAsync(null, e.NavigationMode, null);
        }

        private void OnNavigated(object sender, NavigationEventArgs e)
        {
            if (e.SourcePageType == typeof(BlankPage))
            {
                Grid.SetRow(Separator, 0);
            }
            else
            {
                Grid.SetRow(Separator, 1);
            }

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
            else
            {
                UpdateListViewsSelectedItem(MasterDetail.NavigationService.GetPeerFromBackStack());
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
            if (MasterDetail.CurrentState == MasterDetailState.Narrow)
            {
                //DialogsListView.IsItemClickEnabled = true;
                DialogsListView.SelectionMode = ListViewSelectionMode.None;
                DialogsListView.SelectedItem = null;
                //DialogsSearchListView.IsItemClickEnabled = true;
                DialogsSearchListView.SelectionMode = ListViewSelectionMode.None;
                DialogsSearchListView.SelectedItem = null;
                //UsersListView.IsItemClickEnabled = true;
                UsersListView.SelectionMode = ListViewSelectionMode.None;
                UsersListView.SelectedItem = null;
                Separator.BorderThickness = new Thickness(0);
            }
            else
            {
                //DialogsListView.IsItemClickEnabled = false;
                DialogsListView.SelectionMode = ListViewSelectionMode.Single;
                DialogsListView.SelectedItem = _lastSelected;
                //DialogsSearchListView.IsItemClickEnabled = false;
                DialogsSearchListView.SelectionMode = ListViewSelectionMode.Single;
                DialogsSearchListView.SelectedItem = _lastSelected;
                //UsersListView.IsItemClickEnabled = false;
                UsersListView.SelectionMode = ListViewSelectionMode.Single;
                UsersListView.SelectedItem = _lastSelected;
                Separator.BorderThickness = new Thickness(0, 0, 1, 0);
            }
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            Navigate(e.ClickedItem);
        }

        private void Navigate(object item)
        {
            _lastSelected = item;

            if (item is TLDialog dialog)
            {
                if (dialog.IsSearchResult)
                {
                    MasterDetail.NavigationService.NavigateToDialog(dialog.With, dialog.TopMessage);
                }
                else
                {
                    MasterDetail.NavigationService.NavigateToDialog(dialog.With);
                }
            }

            if (item is TLMessageCommonBase message)
            {
                var with = default(ITLDialogWith);
                var peer = message.IsOut || message.ToId is TLPeerChannel || message.ToId is TLPeerChat ? message.ToId : new TLPeerUser { UserId = message.FromId.Value };
                if (peer is TLPeerUser)
                {
                    with = ViewModel.CacheService.GetUser(peer.Id);
                }
                else
                {
                    with = ViewModel.CacheService.GetChat(peer.Id);
                }

                if (with != null)
                {
                    MasterDetail.NavigationService.NavigateToDialog(with, message.Id);
                }
            }
            else
            {
                SearchDialogs.Text = string.Empty;
            }

            if (item is TLUser user)
            {
                MasterDetail.NavigationService.NavigateToDialog(user);
            }

            if (item is TLChannel channel)
            {
                MasterDetail.NavigationService.NavigateToDialog(channel);
            }
        }

        private async void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listView = sender as ListView;
            if (listView.SelectedItem != null)
            {
                listView.ScrollIntoView(listView.SelectedItem);
            }
            else
            {
                // Find another solution
                await Task.Delay(500);
                UpdateListViewsSelectedItem(MasterDetail.NavigationService.GetPeerFromBackStack());
            }
        }

        private void cbtnMasterAbout_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(AboutPage));
        }

        private void cbtnMasterSearch_Click(object sender, RoutedEventArgs e)
        {
            //PLEASE REMOVE THE BELOW LINE ONCE THE CHATPAGE HAS BEEN IMPLEMENTED
            MasterDetail.NavigationService.Navigate(typeof(DialogSharedMediaPage));
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
                    //dialogs.LoadFirstSlice();
                    contacts.LoadContacts();
                });

                //ViewModel.Contacts.getTLContacts();
                ViewModel.Contacts.GetSelfAsync();
            }
            catch { }
        }

        private void Self_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.Contacts.Self != null)
            {
                MasterDetail.NavigationService.NavigateToDialog(ViewModel.Contacts.Self);
            }
        }

        private async void cbtnMasterSettings_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsPage));
        }

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

        private void txtSearch_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (DialogsSearchListView.Visibility == Visibility.Collapsed)
            {
                return;
            }

            if (e.Key == VirtualKey.Up || e.Key == VirtualKey.Down)
            {
                var index = e.Key == VirtualKey.Up ? -1 : 1;
                var next = DialogsSearchListView.SelectedIndex + index;
                if (next >= 0 && next < SearchResults.View.Count)
                {
                    DialogsSearchListView.SelectedIndex = next;
                    DialogsSearchListView.ScrollIntoView(DialogsSearchListView.SelectedItem);
                }

                //var index = Math.Max(DialogsSearchListView.SelectedIndex, 0);
                //var container = DialogsSearchListView.ContainerFromIndex(index) as ListViewItem;
                //if (container != null)
                //{
                //    DialogsSearchListView.SelectedIndex = index;
                //    container.Focus(FocusState.Keyboard);
                //}

                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Enter)
            {
                var index = Math.Max(DialogsSearchListView.SelectedIndex, 0);
                var container = DialogsSearchListView.ContainerFromIndex(index) as ListViewItem;
                if (container != null)
                {
                    var peer = new ListViewItemAutomationPeer(container);
                    var invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                    invokeProv.Invoke();
                }

                e.Handled = true;
            }
        }

        #region Context menu

        private void MenuFlyout_Opening(object sender, object e)
        {
            var flyout = sender as MenuFlyout;

            foreach (var item in flyout.Items)
            {
                item.Visibility = Visibility.Visible;
            }
        }

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
                        // TODO: 06/05/2017
                        //element.Visibility = user.IsBot && !user.IsBlocked ? Visibility.Visible : Visibility.Collapsed;
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
                    element.Visibility = dialog.Peer is TLPeerChat && dialog.With is TLChat ? Visibility.Visible : Visibility.Collapsed;
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

        private void Pivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            NewChatItem.Visibility = NewChannelItem.Visibility = rpMasterTitlebar.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
            EditNameItem.Visibility = LogoutItem.Visibility = rpMasterTitlebar.SelectedIndex == 3 ? Visibility.Visible : Visibility.Collapsed;
            FlyoutSeperator.Visibility = (rpMasterTitlebar.SelectedIndex == 1 || rpMasterTitlebar.SelectedIndex == 2) ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}
