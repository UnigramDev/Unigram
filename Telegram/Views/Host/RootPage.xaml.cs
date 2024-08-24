//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using LinqToVisualTree;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Composition;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Settings;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.Views.Authorization;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.UI.ViewManagement;
using Point = Windows.Foundation.Point;

namespace Telegram.Views.Host
{
    public interface IToastHost
    {
        void Connect(TeachingTip toast);
        void Disconnect(TeachingTip toast);
    }

    public sealed partial class RootPage : Page, IToastHost
    {
        private readonly ILifetimeService _lifetime;
        private NavigationService _navigationService;

        private RootDestination _navigationViewSelected;
        private readonly MvxObservableCollection<object> _navigationViewItems;

        private long _attachmentMenuBots;

        public RootPage(NavigationService service)
        {
            RequestedTheme = SettingsService.Current.Appearance.GetCalculatedElementTheme();
            InitializeComponent();

            _lifetime = TypeResolver.Current.Lifetime;

            _navigationViewSelected = RootDestination.Chats;
            _navigationViewItems = new MvxObservableCollection<object>
            {
                RootDestination.ShowAccounts,
                RootDestination.Status,
                RootDestination.MyProfile,
                // ------------
                RootDestination.Separator,
                // ------------
                RootDestination.ArchivedChats,
                RootDestination.SavedMessages,
                // ------------
                RootDestination.Separator,
                // ------------
                RootDestination.Chats,
                RootDestination.Contacts,
                RootDestination.Calls,
                RootDestination.Settings,
                // ------------
                RootDestination.Separator,
                // ------------
                RootDestination.Tips,
                RootDestination.News
            };

            NavigationViewList.ItemsSource = _navigationViewItems;

            service.Frame.Navigating += OnNavigating;
            service.Frame.Navigated += OnNavigated;

            _navigationService = service;
            InitializeNavigation(service.Frame);

            Navigation.Content = _navigationService.Frame;

            VisualUtilities.DropShadow(ThemeShadow);

            //if (ApiInfo.IsXbox)
            //{
            //    var application = ApplicationView.GetForCurrentView();
            //    application.VisibleBoundsChanged += OnVisibleBoundsChanged;
            //    application.SetDesiredBoundsMode(ApplicationViewBoundsMode.UseCoreWindow);

            //    OnVisibleBoundsChanged(application, null);
            //}
        }

        private void OnVisibleBoundsChanged(ApplicationView sender, object args)
        {
            LayoutRoot.Margin = new Thickness(
                sender.VisibleBounds.Left,
                sender.VisibleBounds.Top,
                sender.VisibleBounds.Left,
                sender.VisibleBounds.Top);
        }

        public INavigationService NavigationService
        {
            get
            {
                if (_navigationService?.Frame.Content is MainPage mainPage)
                {
                    return mainPage.NavigationService;
                }

                return null;
            }
        }

        public void PopupOpened()
        {
            if (_navigationService.Frame.Content is IRootContentPage content)
            {
                content.PopupOpened();
            }
        }

        public void PopupClosed()
        {
            if (_navigationService.Frame.Content is IRootContentPage content)
            {
                content.PopupClosed();
            }
        }

        public void Connect(TeachingTip toast)
        {
            if (_navigationService?.Frame != null)
            {
                _navigationService.Frame.Resources.Remove("TeachingTip");
                _navigationService.Frame.Resources.Add("TeachingTip", toast);
            }
        }

        public void Disconnect(TeachingTip toast)
        {
            if (_navigationService?.Frame != null && _navigationService.Frame.Resources.TryGetValue("TeachingTip", out object cached))
            {
                if (cached == toast)
                {
                    _navigationService.Frame.Resources.Remove("TeachingTip");
                }
            }
        }

        public void UpdateComponent()
        {
            _contentLoaded = false;
            Resources.Clear();
            InitializeComponent();

            _navigationViewSelected = RootDestination.Chats;
            NavigationViewList.ItemsSource = _navigationViewItems;

            InitializeNavigation(_navigationService.Frame);

            Switch(_lifetime.ActiveItem);
        }

        public void Create()
        {
            var premium = 0;
            var count = 0;

            foreach (var session in TypeResolver.Current.Lifetime.Items)
            {
                if (session.Settings.UseTestDC)
                {
                    continue;
                }

                if (session.ClientService.Options.IsPremium)
                {
                    premium++;
                }

                count++;
            }

            var limit = 3;

            if (count >= limit + premium)
            {
                _navigationService.ShowLimitReached(new PremiumLimitTypeConnectedAccounts());
                return;
            }

            Switch(_lifetime.Create());
        }

        public void Switch(ISessionService session)
        {
            _lifetime.ActiveItem = session;

            if (_navigationService != null)
            {
                Destroy(_navigationService);
            }

            Navigation.IsPaneOpen = false;

            var service = WindowContext.Current.NavigationServices.GetByFrameId($"{session.Id}") as NavigationService;
            if (service == null)
            {
                service = BootStrapper.Current.NavigationServiceFactory(BootStrapper.BackButton.Attach, new Frame { CacheSize = 0 }, session.Id, $"{session.Id}", true) as NavigationService;
                service.Frame.Navigating += OnNavigating;
                service.Frame.Navigated += OnNavigated;

                switch (session.ClientService.AuthorizationState)
                {
                    case AuthorizationStateReady:
                        service.Navigate(typeof(MainPage));
                        break;
                    case AuthorizationStateWaitPhoneNumber:
                    case AuthorizationStateWaitOtherDeviceConfirmation:
                        service.Navigate(typeof(AuthorizationPage));
                        service.AddToBackStack(typeof(BlankPage));
                        break;
                    case AuthorizationStateWaitCode:
                        service.Navigate(typeof(AuthorizationCodePage), navigationStackEnabled: false);
                        break;
                    case AuthorizationStateWaitEmailAddress:
                        service.Navigate(typeof(AuthorizationEmailAddressPage), navigationStackEnabled: false);
                        break;
                    case AuthorizationStateWaitEmailCode:
                        service.Navigate(typeof(AuthorizationEmailCodePage), navigationStackEnabled: false);
                        break;
                    case AuthorizationStateWaitRegistration:
                        service.Navigate(typeof(AuthorizationRegistrationPage), navigationStackEnabled: false);
                        break;
                    case AuthorizationStateWaitPassword:
                        service.Navigate(typeof(AuthorizationPasswordPage), navigationStackEnabled: false);
                        break;
                }

                //if (service is TLRootNavigationService rootService)
                //{
                //    rootService.Handle(session.ClientService.GetAuthorizationState());
                //}

                var counters = session.ClientService.GetUnreadCount(new ChatListMain());
                if (counters != null)
                {
                    session.Aggregator.Publish(counters.UnreadChatCount);
                    session.Aggregator.Publish(counters.UnreadMessageCount);
                }

                session.Aggregator.Publish(new UpdateConnectionState(session.ClientService.ConnectionState));
            }
            else
            {
                // TODO: This should actually __never__ happen.
            }

            _navigationService = service;
            Navigation.Content = service.Frame;
        }

        private void Destroy(NavigationService master)
        {
            if (master.Frame.Content is IRootContentPage content)
            {
                content.Root = null;
                content.Dispose();
            }

            var detail = WindowContext.Current.NavigationServices.GetByFrameId($"Main{master.FrameFacade.FrameId}");
            if (detail != null)
            {
                //detail.Navigate(typeof(BlankPage));
                //detail.ClearCache();
                detail.Suspend();
            }

            master.Frame.Navigating -= OnNavigating;
            master.Frame.Navigated -= OnNavigated;
            //master.Frame.Navigate(typeof(BlankPage));
            master.Suspend();

            WindowContext.Current.NavigationServices.Remove(master);
            WindowContext.Current.NavigationServices.Remove(detail);
        }

        private void OnNavigating(object sender, NavigatingCancelEventArgs e)
        {
            if (_navigationService?.Frame?.Content is IRootContentPage content)
            {
                content.Root = null;
            }
        }

        private void OnNavigated(object sender, NavigationEventArgs e)
        {
            if (e.Content is IRootContentPage content)
            {
                content.Root = this;
                InitializeNavigation(sender as Frame);
            }
        }

        private void InitializeNavigation(Frame frame)
        {
            if (frame?.Content is MainPage page && page.ViewModel != null)
            {
                InitializeUser(page.ViewModel.ClientService);
                InitializeSessions(page.ViewModel.ClientService, SettingsService.Current.IsAccountsSelectorExpanded, _lifetime.Items);
            }
        }

        private async void InitializeUser(IClientService clientService)
        {
            var user = clientService.GetUser(clientService.Options.MyId);
            user ??= await clientService.SendAsync(new GetMe()) as User;

            if (user == null)
            {
                return;
            }

            Photo.SetUser(clientService, user, 48);
            NameLabel.Text = user.FullName();

            if (SettingsService.Current.Diagnostics.HidePhoneNumber)
            {
                PhoneLabel.Text = "+42 --- --- ----";
            }
            else
            {
                PhoneLabel.Text = PhoneNumber.Format(user.PhoneNumber);
            }

            Expanded.IsChecked = SettingsService.Current.IsAccountsSelectorExpanded;
        }

        private void InitializeSessions(bool show, IList<ISessionService> items)
        {
            if (_navigationService.Content is MainPage page)
            {
                InitializeSessions(page.ViewModel.ClientService, SettingsService.Current.IsAccountsSelectorExpanded, _lifetime.Items);
            }
        }

        private void InitializeSessions(IClientService clientService, bool show, IList<ISessionService> items)
        {
            var bots = clientService.GetBotsForMenu(out long botsHash);

            for (int i = 0; i < _navigationViewItems.Count; i++)
            {
                if (_navigationViewItems[i] is ISessionService || _navigationViewItems[i] is RootDestination.AddAccount)
                {
                    _navigationViewItems.RemoveAt(i);

                    if (i < _navigationViewItems.Count && _navigationViewItems[i] is RootDestination.Separator)
                    {
                        _navigationViewItems.RemoveAt(i);
                    }

                    i--;
                }
                else if (_navigationViewItems[i] is AttachmentMenuBot && _attachmentMenuBots != botsHash)
                {
                    _navigationViewItems.RemoveAt(i);
                    i--;
                }
            }

            var index = 4;

            if (clientService.IsPremium is false)
            {
                if (_navigationViewItems[1] is RootDestination.Status)
                {
                    _navigationViewItems.RemoveAt(1);
                }

                index = 3;
            }
            else if (_navigationViewItems[1] is not RootDestination.Status)
            {
                _navigationViewItems.Insert(1, RootDestination.Status);
            }

            if (_attachmentMenuBots != botsHash)
            {
                for (int i = bots.Count - 1; i >= 0; i--)
                {
                    _navigationViewItems.Insert(index - 1, bots[i]);
                    index++;
                }
            }
            else
            {
                index += bots.Count;
            }

            if (SettingsService.Current.HideArchivedChats is false)
            {
                if (_navigationViewItems[index] is RootDestination.ArchivedChats)
                {
                    _navigationViewItems.RemoveAt(index);
                }
            }
            else if (_navigationViewItems[index] is not RootDestination.ArchivedChats)
            {
                _navigationViewItems.Insert(index, RootDestination.ArchivedChats);
            }

            if (show && items != null)
            {
                _navigationViewItems.Insert(1, RootDestination.Separator);

#if !DEBUG
                if (items.Count < 4)
#endif
                {
                    _navigationViewItems.Insert(1, RootDestination.AddAccount);
                }

                if (items.Count > 1)
                {
                    foreach (var item in items.OrderByDescending(x => { int index = Array.IndexOf(SettingsService.Current.AccountsSelectorOrder, x.Id); return index < 0 ? x.Id : index; }))
                    {
                        _navigationViewItems.Insert(1, item);
                    }
                }
            }

            _attachmentMenuBots = botsHash;
        }

        #region Recycling

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.Item is ISessionService && args.ItemContainer is null or Controls.NavigationViewItem or Controls.NavigationViewItemSeparator)
            {
                args.ItemContainer = new ListViewItem();
                args.ItemContainer.Style = NavigationViewList.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = Resources["SessionItemTemplate"] as DataTemplate;
                args.ItemContainer.ContextRequested += OnContextRequested;
            }
            else if (args.Item is AttachmentMenuBot && args.ItemContainer is null or Controls.NavigationViewItemSeparator or ListViewItem)
            {
                args.ItemContainer = new Controls.NavigationViewItem();
                args.ItemContainer.ContextRequested += OnContextRequested;
            }
            else if (args.Item is RootDestination destination)
            {
                if (destination == RootDestination.Separator && args.ItemContainer is not Controls.NavigationViewItemSeparator)
                {
                    args.ItemContainer = new Controls.NavigationViewItemSeparator();
                }
                else if (destination != RootDestination.Separator)
                {
                    if (args.ItemContainer is not Controls.NavigationViewItem)
                    {
                        args.ItemContainer = new Controls.NavigationViewItem();
                        args.ItemContainer.ContextRequested += OnContextRequested;
                    }

                    if (destination is RootDestination.ShowAccounts)
                    {
                        AutomationProperties.SetName(args.ItemContainer, SettingsService.Current.IsAccountsSelectorExpanded ? Strings.AccDescrHideAccounts : Strings.AccDescrShowAccounts);
                        args.ItemContainer.FocusVisualMargin = new Thickness(0, -22, 0, 2);
                    }
                    else
                    {
                        AutomationProperties.SetName(args.ItemContainer, string.Empty);
                        args.ItemContainer.FocusVisualMargin = new Thickness();
                    }
                }
            }

            args.IsContainerPrepared = true;
        }

        private void OnContextRequested(UIElement sender, Microsoft.UI.Xaml.Input.ContextRequestedEventArgs args)
        {
            var container = sender as ListViewItem;
            if (container.Content is ISessionService session && !session.IsActive)
            {

            }
            else if (container.Content is AttachmentMenuBot menuBot)
            {
                if (_navigationService.Content is MainPage page)
                {
                    var flyout = new MenuFlyout();

                    // TODO: list updates are not handled while the panel is open
                    flyout.CreateFlyoutItem(page.ViewModel.RemoveMiniApp, menuBot, Strings.BotWebViewDeleteBot, Icons.Delete);
                    flyout.ShowAt(sender, args);
                }
            }
            else if (container.Content is RootDestination.AddAccount)
            {
                var alt = WindowContext.IsKeyDown(Windows.System.VirtualKey.Menu);
                var ctrl = WindowContext.IsKeyDown(Windows.System.VirtualKey.Control);
                var shift = WindowContext.IsKeyDown(Windows.System.VirtualKey.Shift);

                if (alt && !ctrl && shift)
                {
                    var flyout = new MenuFlyout();
                    flyout.CreateFlyoutItem(() => Switch(_lifetime.Create(test: false)), "Production Server", Icons.Globe);
                    flyout.CreateFlyoutItem(() => Switch(_lifetime.Create(test: true)), "Test Server", Icons.Bug);
                    flyout.ShowAt(sender, args);
                }
            }
            else if (container.Content is RootDestination.ArchivedChats)
            {
                if (_navigationService.Content is MainPage page)
                {
                    var flyout = new MenuFlyout();
                    flyout.CreateFlyoutItem(ToggleArchive, Strings.ArchiveMoveToChatList, Icons.AddCircle);
                    flyout.CreateFlyoutItem(page.ViewModel.MarkFolderAsRead, ChatFolderViewModel.Archive, Strings.MarkAllAsRead, Icons.MarkAsRead);
                    flyout.ShowAt(sender, args);
                }
            }
        }

        private void ToggleArchive()
        {
            Navigation.IsPaneOpen = false;

            if (_navigationService.Content is MainPage page)
            {
                page.ToggleArchive();
            }
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            UpdateContainerContent(args.ItemContainer, args.Item);
        }

        private void UpdateContainerContent(SelectorItem container, object item)
        {
            container.Opacity = 1;

            if (item is ISessionService session)
            {
                var content = container.ContentTemplateRoot as Grid;
                if (content == null)
                {
                    return;
                }

                var user = session.ClientService.GetUser(session.UserId);
                if (user == null)
                {
                    session.ClientService.Send(new GetUser(session.UserId));
                    return;
                }

                var title = content.FindName("TitleLabel") as TextBlock;
                title.Text = user.FullName();

                var photo = content.Children[0] as ProfilePicture;
                photo.SetUser(session.ClientService, user, 28);

                var identity = content.FindName("Identity") as IdentityIcon;
                identity.SetStatus(session.ClientService, user);

                AutomationProperties.SetName(container, user.FullName());
            }
            else if (item is AttachmentMenuBot menuBot)
            {
                // TODO: properly support icons provided by the API

                var content = container as Controls.NavigationViewItem;
                content.IsChecked = false;
                content.Text = menuBot.Name;
                content.Glyph = menuBot.BotUserId == 1985737506 ? Icons.Wallet : Icons.Bot;
                content.BadgeVisibility = menuBot.ShowDisclaimerInSideMenu ? Visibility.Visible : Visibility.Collapsed;
            }
            else if (item is RootDestination destination && _navigationService.Content is MainPage page)
            {
                var content = container as Controls.NavigationViewItem;
                if (content != null)
                {
                    content.IsChecked = destination == _navigationViewSelected;
                    content.BadgeVisibility = Visibility.Collapsed;
                }

                switch (destination)
                {
                    case RootDestination.AddAccount:
                        content.Text = Strings.AddAccount;
                        content.Glyph = Icons.PersonAdd;
                        break;

                    case RootDestination.Chats:
                        content.Text = Strings.FilterChats;
                        content.Glyph = Icons.ChatMultiple;
                        break;
                    case RootDestination.Contacts:
                        content.Text = Strings.Contacts;
                        content.Glyph = Icons.People;
                        break;
                    case RootDestination.Calls:
                        content.Text = Strings.Calls;
                        content.Glyph = Icons.Call;
                        break;
                    case RootDestination.Settings:
                        content.Text = Strings.Settings;
                        content.Glyph = Icons.Settings;
                        break;

                    case RootDestination.ArchivedChats:
                        content.Text = Strings.ArchivedChats;
                        content.Glyph = Icons.Archive;
                        break;
                    case RootDestination.SavedMessages:
                        content.Text = Strings.SavedMessages;
                        content.Glyph = Icons.Bookmark;
                        break;

                    case RootDestination.Status:
                        if (page.ViewModel.ClientService.TryGetUser(page.ViewModel.ClientService.Options.MyId, out User user))
                        {
                            content.Text = user.EmojiStatus == null ? Strings.SetEmojiStatus : Strings.ChangeEmojiStatus;
                            content.Glyph = user.EmojiStatus == null ? Icons.EmojiAdd : Icons.EmojiEdit;
                        }
                        break;
                    case RootDestination.MyProfile:
                        content.Text = Strings.MyProfile;
                        content.Glyph = Icons.PersonCircle;
                        break;

                    case RootDestination.Tips:
                        content.Text = Strings.TelegramFeatures;
                        content.Glyph = Icons.QuestionCircle;
                        break;
                    case RootDestination.News:
                        content.Text = Strings.News;
                        content.Glyph = Icons.Megaphone;
                        break;

                    case RootDestination.ShowAccounts:
                        container.Opacity = 0;
                        break;
                }
            }
        }

        #endregion

        private void Expand_Click(object sender, RoutedEventArgs e)
        {
            SettingsService.Current.IsAccountsSelectorExpanded = !SettingsService.Current.IsAccountsSelectorExpanded;

            InitializeSessions(SettingsService.Current.IsAccountsSelectorExpanded, _lifetime.Items);
            Expanded.IsChecked = SettingsService.Current.IsAccountsSelectorExpanded;

            var selector = NavigationViewList.ContainerFromIndex(0);
            if (selector != null)
            {
                AutomationProperties.SetName(selector, SettingsService.Current.IsAccountsSelectorExpanded ? Strings.AccDescrHideAccounts : Strings.AccDescrShowAccounts);
            }
        }

        private void TestDestroy()
        {
            Destroy(_navigationService);

            var frames = this.Descendants<Frame>().ToList();

            foreach (var frame in frames)
            {
                if (frame.Content is MainPage main)
                {
                    main.LeakTest(true);
                }
                else if (frame.Content is Page page && page.Content is ChatView chat)
                {
                    chat.LeakTest(true);
                }
            }

            var butt = new RepeatButton();
            butt.HorizontalAlignment = HorizontalAlignment.Center;
            butt.Content = "GC";
            butt.Click += (s, args) =>
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            };

            var swit = new Button();
            swit.HorizontalAlignment = HorizontalAlignment.Center;
            swit.Content = "SW";
            swit.Click += async (s, args) =>
            {
                for (int i = 0; i < 10; i++)
                {
                    Switch(_lifetime.ActiveItem);
                    await Task.Delay(2000);
                }

                TestDestroy();
            };

            var clin = new Button();
            clin.HorizontalAlignment = HorizontalAlignment.Center;
            clin.Content = "CL";
            clin.Click += (s, args) =>
            {
                var butt = new RepeatButton();
                butt.HorizontalAlignment = HorizontalAlignment.Center;
                butt.Content = "GC";
                butt.Click += (s, args) =>
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                };

                Window.Current.Content = butt;
            };

            var rest = new Button();
            rest.HorizontalAlignment = HorizontalAlignment.Center;
            rest.Content = "RE";
            rest.Click += (s, args) =>
            {
                Switch(_lifetime.ActiveItem);
            };

            var panel = new StackPanel();
            panel.VerticalAlignment = VerticalAlignment.Center;
            panel.HorizontalAlignment = HorizontalAlignment.Center;
            panel.Children.Add(butt);
            panel.Children.Add(swit);
            panel.Children.Add(clin);
            panel.Children.Add(rest);

            Navigation.Content = panel;
            _navigationService = null;
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ISessionService session)
            {
                if (session.IsActive)
                {
                    var alt = WindowContext.IsKeyDown(Windows.System.VirtualKey.Menu);
                    var ctrl = WindowContext.IsKeyDown(Windows.System.VirtualKey.Control);
                    var shift = WindowContext.IsKeyDown(Windows.System.VirtualKey.Shift);

                    if (SettingsService.Current.Diagnostics.ShowMemoryUsage && alt && !ctrl && !shift)
                    {
                        TestDestroy();
                    }

                    return;
                }

                Switch(session);
            }
            else if (e.ClickedItem is AttachmentMenuBot menuBot)
            {
                if (_navigationService?.Frame?.Content is MainPage content)
                {
                    content.ViewModel.OpenMiniApp(menuBot, ContinueNavigation);
                    return;
                }
            }
            else if (e.ClickedItem is RootDestination destination)
            {
                if (destination is RootDestination.ShowAccounts)
                {
                    Expand_Click(null, null);
                    return;
                }
                else if (destination is RootDestination.AddAccount)
                {
                    Create();
                }
                else if (_navigationService?.Frame?.Content is IRootContentPage content)
                {
                    content.NavigationView_ItemClick(destination);
                }
            }

            ContinueNavigation();
        }

        private void ContinueNavigation(bool done = true)
        {
            if (done)
            {
                Navigation.IsPaneOpen = false;

                var scroll = NavigationViewList.GetScrollViewer();
                scroll?.ChangeView(null, 0, null, true);
            }
        }

        #region Exposed

        public void PresentContent(UIElement element)
        {
            if (Transition.Child is IDisposable disposable)
            {
                disposable.Dispose();
            }

            Transition.Child = element;
            Navigation.Visibility = element != null
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        public void UpdateSessions()
        {
            InitializeSessions(SettingsService.Current.IsAccountsSelectorExpanded, _lifetime.Items);
        }

        public void SetSelectedIndex(RootDestination value)
        {
            _navigationViewSelected = value;

            void SetChecked(RootDestination destination, RootDestination target)
            {
                var selector = NavigationViewList.ContainerFromItem(_navigationViewItems.FirstOrDefault(x => x is RootDestination y && y == destination)) as Controls.NavigationViewItem;
                if (selector != null)
                {
                    selector.IsChecked = destination == target;
                }
            }

            SetChecked(RootDestination.Chats, value);
            SetChecked(RootDestination.Contacts, value);
            SetChecked(RootDestination.Calls, value);
            SetChecked(RootDestination.Settings, value);
        }

        #endregion

        public void ShowEditor(ThemeCustomInfo theme)
        {
            var resize = ThemePage == null;

            FindName("ThemePage");
            ThemePage.Load(theme);

            if (resize)
            {
                MainColumn.Width = new GridLength(ActualWidth, GridUnitType.Pixel);

                var view = ApplicationView.GetForCurrentView();
                var size = view.VisibleBounds;

                view.TryResizeView(new Size(size.Width + 320, size.Height));
                ApplicationView.PreferredLaunchViewSize = new Size(size.Width, size.Height);

                MainColumn.Width = new GridLength(1, GridUnitType.Star);
            }
        }

        public void HideEditor()
        {
            UnloadObject(ThemePage);

            var view = ApplicationView.GetForCurrentView();
            view.TryResizeView(ApplicationView.PreferredLaunchViewSize);
        }

        private Task Test()
        {
            var tsc = new TaskCompletionSource<bool>();
            void handler(object sender, object e)
            {
                Microsoft.UI.Xaml.Media.CompositionTarget.Rendered -= handler;
                tsc.SetResult(true);
            }

            Microsoft.UI.Xaml.Media.CompositionTarget.Rendered += handler;
            return tsc.Task;
        }

        private async void Theme_Click(object sender, RoutedEventArgs e)
        {
            var animate = true;
            if (animate)
            {
                Theme.Visibility = Visibility.Collapsed;

                if (false)
                {
                    await Test();
                }

                var visual = BootStrapper.Current.Compositor.CreateRedirectVisual(this, Vector2.Zero, ActualSize, true);
                await VisualUtilities.WaitForCompositionRenderedAsync();

                ElementCompositionPreview.SetElementChildVisual(Transition, visual);

                //var bitmap = ScreenshotManager.Capture();
                //Transition.Background = new ImageBrush { ImageSource = bitmap, AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center, RelativeTransform = new ScaleTransform { ScaleY = -1, CenterY = 0.5 } };

                Theme.Visibility = Visibility.Visible;
                Theme.Foreground = new SolidColorBrush(ActualTheme != ElementTheme.Dark ? Microsoft.UI.Colors.White : Microsoft.UI.Colors.Black);
                //Theme.Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);

                var actualWidth = (float)ActualWidth;
                var actualHeight = (float)ActualHeight;

                var transform = Theme.TransformToVisual(this);
                var point = transform.TransformPoint(new Point()).ToVector2();

                var width = MathF.Max(actualWidth - point.X, actualHeight - point.Y);
                var diaginal = MathF.Sqrt((width * width) + (width * width));

                var device = CanvasDevice.GetSharedDevice();
                var expand = false; // ActualTheme == ElementTheme.Dark;

                var rect1 = CanvasGeometry.CreateRectangle(device, 0, 0, expand ? 0 : actualWidth, expand ? 0 : actualHeight);

                var elli1 = CanvasGeometry.CreateCircle(device, point.X + 24, point.Y + 24, expand ? 0 : diaginal);
                var group1 = CanvasGeometry.CreateGroup(device, new[] { elli1, rect1 }, CanvasFilledRegionDetermination.Alternate);

                var elli2 = CanvasGeometry.CreateCircle(device, point.X + 24, point.Y + 24, expand ? diaginal : 0);
                var group2 = CanvasGeometry.CreateGroup(device, new[] { elli2, rect1 }, CanvasFilledRegionDetermination.Alternate);

                //var visual = ElementComposition.GetElementVisual(Transition);
                var ellipse = visual.Compositor.CreatePathGeometry(new CompositionPath(group2));
                var clip = visual.Compositor.CreateGeometricClip(ellipse);

                visual.Clip = clip;

                var batch = visual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
                batch.Completed += (s, args) =>
                {
                    visual.Clip = null;
                    visual.Brush = visual.Compositor.CreateColorBrush(Microsoft.UI.Colors.Transparent);
                    //Transition.Background = null;

                    ElementCompositionPreview.SetElementChildVisual(Transition, visual.Compositor.CreateSpriteVisual());
                    Theme.Foreground = new SolidColorBrush(ActualTheme == ElementTheme.Dark ? Microsoft.UI.Colors.White : Microsoft.UI.Colors.Black);
                };

                CompositionEasingFunction ease;
                if (expand)
                {
                    ease = visual.Compositor.CreateCubicBezierEasingFunction(new Vector2(.42f, 0), new Vector2(1, 1));
                }
                else
                {
                    ease = visual.Compositor.CreateCubicBezierEasingFunction(new Vector2(0, 0), new Vector2(.58f, 1));
                }

                var anim = visual.Compositor.CreatePathKeyFrameAnimation();
                anim.InsertKeyFrame(0, new CompositionPath(group2), ease);
                anim.InsertKeyFrame(1, new CompositionPath(group1), ease);
                anim.Duration = TimeSpan.FromMilliseconds(500);

                ellipse.StartAnimation("Path", anim);
                batch.End();
            }

            if (SettingsService.Current.Appearance.NightMode != NightMode.Disabled)
            {
                SettingsService.Current.Appearance.NightMode = NightMode.Disabled;
                ToastPopup.Show(XamlRoot, Strings.AutoNightModeOff, ToastPopupIcon.AutoNightOff);
            }

            SettingsService.Current.Appearance.ForceNightMode = ActualTheme != ElementTheme.Dark;
            SettingsService.Current.Appearance.RequestedTheme = ActualTheme != ElementTheme.Dark
                ? TelegramTheme.Dark
                : TelegramTheme.Light;

            SettingsService.Current.Appearance.UpdateNightMode();
        }

        private void Theme_ActualThemeChanged(FrameworkElement sender, object args)
        {
            Theme.IsChecked = sender.ActualTheme == ElementTheme.Dark;
        }

        public bool IsPaneOpen
        {
            get => Navigation.IsPaneOpen;
            set => Navigation.IsPaneOpen = value;
        }

        private bool _isSidebarEnabled;

        public void SetSidebarEnabled(bool value)
        {
            if (_isSidebarEnabled != value)
            {
                _isSidebarEnabled = value;
            }
        }

        private void UpdateNavigation()
        {
            var clientService = TypeResolver.Current.Resolve<IClientService>(_navigationService.SessionId);
            if (clientService == null)
            {
                // TODO: this should never be happening
                return;
            }

            var bots = clientService.GetBotsForMenu(out long botsHash);
            var index = -1;

            if (_attachmentMenuBots != botsHash)
            {
                for (int i = 0; i < _navigationViewItems.Count; i++)
                {
                    if (_navigationViewItems[i] is AttachmentMenuBot)
                    {
                        _navigationViewItems.RemoveAt(i);
                        index = i--;
                    }
                }
            }

            NavigationViewList.ForEach(container =>
            {
                UpdateContainerContent(container, container.Content);

                if (container.Content is RootDestination.Status)
                {
                    return;
                }
            });

            if (_attachmentMenuBots != botsHash && index != -1)
            {
                for (int i = bots.Count - 1; i >= 0; i--)
                {
                    _navigationViewItems.Insert(index, bots[i]);
                }
            }

            _attachmentMenuBots = botsHash;
        }

        private void Navigation_PaneOpening(SplitView sender, object args)
        {
            UpdateNavigation();

            if (_navigationService?.Content is MainPage main)
            {
                InitializeUser(main.ViewModel.ClientService);
            }

            Theme.Visibility = Visibility.Visible;
            Accounts.Visibility = Visibility.Visible;

            ElementCompositionPreview.SetIsTranslationEnabled(Info, true);

            var theme = ElementComposition.GetElementVisual(Theme);
            var photo = ElementComposition.GetElementVisual(Photo);
            var info = ElementComposition.GetElementVisual(Info);
            var accounts = ElementComposition.GetElementVisual(Accounts);
            var compositor = theme.Compositor;

            var ease = compositor.CreateCubicBezierEasingFunction(new Vector2(0.1f, 0.9f), new Vector2(0.2f, 1.0f));

            var offset1 = compositor.CreateVector3KeyFrameAnimation();
            offset1.InsertKeyFrame(0, new Vector3(-40, 0, 0), ease);
            offset1.InsertKeyFrame(1, new Vector3(200, 0, 0), ease);
            offset1.Duration = TimeSpan.FromMilliseconds(350);

            var offset2 = compositor.CreateVector3KeyFrameAnimation();
            offset2.InsertKeyFrame(0, new Vector3(-8, 0, 0), ease);
            offset2.InsertKeyFrame(1, new Vector3(0, 0, 0), ease);
            offset2.Duration = TimeSpan.FromMilliseconds(350);

            var opacity = compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(0, 0, ease);
            opacity.InsertKeyFrame(1, 1, ease);
            opacity.Duration = TimeSpan.FromMilliseconds(350);

            var scale = compositor.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(0, new Vector3(0, 0, 0), ease);
            scale.InsertKeyFrame(1, new Vector3(1, 1, 0), ease);
            scale.Duration = TimeSpan.FromMilliseconds(350);

            var clip = compositor.CreateScalarKeyFrameAnimation();
            clip.InsertKeyFrame(0, 180, ease);
            clip.InsertKeyFrame(1, 0, ease);
            clip.Duration = TimeSpan.FromMilliseconds(350);

            theme.StartAnimation("Offset", offset1);
            theme.StartAnimation("Opacity", opacity);

            photo.CenterPoint = new Vector3(24, 24, 0);
            photo.StartAnimation("Scale", scale);
            photo.StartAnimation("Opacity", opacity);

            info.CenterPoint = new Vector3(0, 32, 0);
            info.StartAnimation("Scale", scale);
            info.StartAnimation("Opacity", opacity);
            info.StartAnimation("Translation", offset2);

            accounts.Clip = compositor.CreateInsetClip();
            accounts.Clip.StartAnimation("RightInset", clip);
        }

        private void Navigation_PaneClosing(SplitView sender, SplitViewPaneClosingEventArgs args)
        {
            Theme.Visibility = Visibility.Visible;
            Accounts.Visibility = Visibility.Visible;

            ElementCompositionPreview.SetIsTranslationEnabled(Info, true);

            var batch = BootStrapper.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                Theme.Visibility = Visibility.Collapsed;
                Accounts.Visibility = Visibility.Collapsed;
            };

            var theme = ElementComposition.GetElementVisual(Theme);
            var photo = ElementComposition.GetElementVisual(Photo);
            var info = ElementComposition.GetElementVisual(Info);
            var accounts = ElementComposition.GetElementVisual(Accounts);
            var compositor = theme.Compositor;

            var ease = compositor.CreateCubicBezierEasingFunction(new Vector2(0.1f, 0.9f), new Vector2(0.2f, 1.0f));
            var offset1 = compositor.CreateVector3KeyFrameAnimation();
            offset1.InsertKeyFrame(0, new Vector3(200, 0, 0), ease);
            offset1.InsertKeyFrame(1, new Vector3(-40, 0, 0), ease);
            offset1.Duration = TimeSpan.FromMilliseconds(120);

            var offset2 = compositor.CreateVector3KeyFrameAnimation();
            offset2.InsertKeyFrame(0, new Vector3(0, 0, 0), ease);
            offset2.InsertKeyFrame(1, new Vector3(-8, 0, 0), ease);
            offset2.Duration = TimeSpan.FromMilliseconds(120);

            var opacity = compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(0, 1, ease);
            opacity.InsertKeyFrame(1, 0, ease);
            opacity.Duration = TimeSpan.FromMilliseconds(120);

            var scale = compositor.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(0, new Vector3(1, 1, 0), ease);
            scale.InsertKeyFrame(1, new Vector3(0, 0, 0), ease);

            scale.Duration = TimeSpan.FromMilliseconds(120);

            var clip = compositor.CreateScalarKeyFrameAnimation();
            clip.InsertKeyFrame(0, 0, ease);
            clip.InsertKeyFrame(1, 180, ease);
            clip.Duration = TimeSpan.FromMilliseconds(120);

            theme.StartAnimation("Offset", offset1);
            theme.StartAnimation("Opacity", opacity);

            photo.CenterPoint = new Vector3(24, 24, 0);
            photo.StartAnimation("Scale", scale);
            photo.StartAnimation("Opacity", opacity);

            info.CenterPoint = new Vector3(0, 32, 0);
            info.StartAnimation("Scale", scale);
            info.StartAnimation("Opacity", opacity);
            info.StartAnimation("Translation", offset2);

            accounts.Clip = compositor.CreateInsetClip();
            accounts.Clip.StartAnimation("RightInset", clip);

            batch.End();
        }

        private void OnDragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            if (e.Items[0] is ISessionService)
            {
                NavigationViewList.CanReorderItems = true;
            }
            else
            {
                NavigationViewList.CanReorderItems = false;
                e.Cancel = true;
            }
        }

        private void OnDragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            NavigationViewList.CanReorderItems = false;

            if (args.DropResult == DataPackageOperation.Move && args.Items.Count == 1 && args.Items[0] is ISessionService session)
            {
                var items = _navigationViewItems;
                var index = items.IndexOf(session);

                var compare = items[index > 0 ? index - 1 : index + 1];
                if (compare is ISessionService)
                {
                    var sessions = _navigationViewItems.OfType<ISessionService>();
                    var ids = sessions.Select(x => x.Id);

                    SettingsService.Current.AccountsSelectorOrder = ids.ToArray();
                }
                else
                {
                    InitializeSessions(SettingsService.Current.IsAccountsSelectorExpanded, _lifetime.Items);
                }
            }
        }

        private void Photo_Click(object sender, RoutedEventArgs e)
        {
            IsPaneOpen = false;
        }
    }

    public interface IRootContentPage
    {
        RootPage Root { get; set; }

        void NavigationView_ItemClick(RootDestination destination);

        void Dispose();

        void PopupOpened();
        void PopupClosed();
    }

    public enum RootDestination
    {
        ShowAccounts,
        AddAccount,

        Status,

        MyProfile,
        ArchivedChats,
        SavedMessages,

        Chats,
        Contacts,
        Calls,
        Settings,

        Tips,
        News,

        Separator
    }
}
