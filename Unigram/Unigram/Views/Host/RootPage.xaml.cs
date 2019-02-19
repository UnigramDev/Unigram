using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Template10.Common;
using Template10.Services.NavigationService;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Services;
using Unigram.Services.Settings;
using Unigram.ViewModels;
using Unigram.Views.SignIn;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System.Profile;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Host
{
    public sealed partial class RootPage : Page
    {
        private ILifetimeService _lifetime;
        private NavigationService _navigationService;

        private const string NavigationAdd = "NavigationAdd";
        private const string NavigationAddSeparator = "NavigationAddSeparator";

        public RootPage(NavigationService service)
        {
            if (!SettingsService.Current.Appearance.RequestedTheme.HasFlag(TelegramTheme.Default))
            {
                RequestedTheme = SettingsService.Current.Appearance.GetCalculatedElementTheme();
            }

            InitializeComponent();

            _lifetime = TLContainer.Current.Lifetime;

            service.Frame.Navigating += OnNavigating;
            service.Frame.Navigated += OnNavigated;
            _navigationService = service;

            InitializeTitleBar();
            InitializeNavigation(service.Frame);
            InitializeLocalization();

            Navigation.Content = _navigationService.Frame;
        }

        private void InitializeTitleBar()
        {
            var sender = CoreApplication.GetCurrentView().TitleBar;

            if (string.Equals(AnalyticsInfo.VersionInfo.DeviceFamily, "Windows.Desktop") && UIViewSettings.GetForCurrentView().UserInteractionMode == UserInteractionMode.Mouse)
            {
                // If running on PC and tablet mode is disabled, then titlebar is most likely visible
                // So we're going to force it
                Navigation.Padding = new Thickness(0, 32, 0, 0);
            }
            else
            {
                Navigation.Padding = new Thickness(0, sender.IsVisible ? sender.Height : 0, 0, 0);
            }

            sender.ExtendViewIntoTitleBar = true;
            sender.IsVisibleChanged += CoreTitleBar_LayoutMetricsChanged;
            sender.LayoutMetricsChanged += CoreTitleBar_LayoutMetricsChanged;
        }

        private void CoreTitleBar_LayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args)
        {
            Navigation.Padding = new Thickness(0, sender.IsVisible ? sender.Height : 0, 0, 0);

            var popups = VisualTreeHelper.GetOpenPopups(Window.Current);
            foreach (var popup in popups)
            {
                if (popup.Child is OverlayPage contentDialog)
                {
                    contentDialog.Padding = new Thickness(0, sender.IsVisible ? sender.Height : 0, 0, 0);
                }
            }
        }

        public void Create()
        {
            Switch(_lifetime.Create());
        }

        public void Switch(ISessionService session)
        {
            _lifetime.ActiveItem = session;

            if (_navigationService != null)
            {
                Destroy(_navigationService);
            }

            var service = WindowContext.GetForCurrentView().NavigationServices.GetByFrameId($"{session.Id}") as NavigationService;
            if (service == null)
            {
                service = BootStrapper.Current.NavigationServiceFactory(BootStrapper.BackButton.Attach, BootStrapper.ExistingContent.Exclude, new Frame(), session.Id, $"{session.Id}", true) as NavigationService;
                service.SerializationService = TLSerializationService.Current;
                service.Frame.Navigating += OnNavigating;
                service.Frame.Navigated += OnNavigated;

                switch (session.ProtoService.GetAuthorizationState())
                {
                    case AuthorizationStateReady ready:
                        service.Navigate(typeof(MainPage));
                        break;
                    case AuthorizationStateWaitPhoneNumber waitPhoneNumber:
                        service.Navigate(typeof(SignInPage));
                        service.Frame.BackStack.Add(new PageStackEntry(typeof(BlankPage), null, null));
                        break;
                    case AuthorizationStateWaitCode waitCode:
                        service.Navigate(waitCode.IsRegistered ? typeof(SignInSentCodePage) : typeof(SignUpPage));
                        break;
                    case AuthorizationStateWaitPassword waitPassword:
                        service.Navigate(typeof(SignInPasswordPage));
                        break;
                }

                //WindowContext.GetForCurrentView().Handle(session, new UpdateConnectionState(session.ProtoService.GetConnectionState()));
                session.Aggregator.Publish(new UpdateConnectionState(session.ProtoService.GetConnectionState()));
            }
            else
            {
                // TODO: This should actually __never__ happen.
            }

            _navigationService = service;
            Navigation.Content = service.Frame;
        }

        private void Destroy(NavigationService service)
        {
            if (service.Frame.Content is IRootContentPage content)
            {
                content.Root = null;
            }

            service.Frame.Navigating -= OnNavigating;
            service.Frame.Navigated -= OnNavigated;

            WindowContext.GetForCurrentView().NavigationServices.Remove(service);
            WindowContext.GetForCurrentView().NavigationServices.RemoveByFrameId($"Main{service.FrameFacade.FrameId}");
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
                Navigation.PaneToggleButtonVisibility = content.EvalutatePaneToggleButtonVisibility();
                InitializeNavigation(sender as Frame);
            }
            else
            {
                Navigation.PaneToggleButtonVisibility = Visibility.Collapsed;
            }
        }

        private void InitializeLocalization()
        {
            NavigationChats.Text = Strings.Additional.Chats;
            //NavigationAbout.Content = Strings.Additional.About;
            NavigationNews.Text = Strings.Additional.News;
        }

        private void InitializeNavigation(Frame frame)
        {
            if (frame?.Content is MainPage main)
            {
                InitializeUser(main.ViewModel);
            }

            InitializeSessions(SettingsService.Current.IsAccountsSelectorExpanded, _lifetime.Items);

            foreach (var item in NavigationViewItems)
            {
                if (item is Controls.NavigationViewItem viewItem)
                {
                    viewItem.Content = viewItem.Name;
                }
            }
        }

        private async void InitializeUser(MainViewModel viewModel)
        {
            var user = viewModel.CacheService.GetUser(viewModel.CacheService.Options.MyId);
            if (user == null)
            {
                user = await viewModel.ProtoService.SendAsync(new GetMe()) as User;
            }

            if (user == null)
            {
                return;
            }

            NameLabel.Text = user.GetFullName();
#if DEBUG
            PhoneLabel.Text = "+39 --- --- ----";
#else
            PhoneLabel.Text = PhoneNumber.Format(user.PhoneNumber);
#endif
            Expanded.IsChecked = SettingsService.Current.IsAccountsSelectorExpanded;
        }

        private void InitializeSessions(bool show, IList<ISessionService> items)
        {
            for (int i = 0; i < NavigationViewItems.Count; i++)
            {
                if (NavigationViewItems[i] is ISessionService)
                {
                    NavigationViewItems.RemoveAt(i);
                    i--;
                }
                else if (NavigationViewItems[i] is Controls.NavigationViewItem viewItem && string.Equals(viewItem.Name, NavigationAdd))
                {
                    NavigationViewItems.RemoveAt(i);
                    i--;
                }
                else if (NavigationViewItems[i] is Controls.NavigationViewItemSeparator viewItemSeparator && string.Equals(viewItemSeparator.Name, NavigationAddSeparator))
                {
                    NavigationViewItems.RemoveAt(i);
                    i--;
                }
            }

            if (show && items != null)
            {
                NavigationViewItems.Insert(1, new Controls.NavigationViewItemSeparator { Name = NavigationAddSeparator });
                NavigationViewItems.Insert(1, new Controls.NavigationViewItem { Name = NavigationAdd, Content = NavigationAdd, Text = Strings.Resources.AddAccount, Glyph = "\uE109" });

                for (int k = items.Count - 1; k >= 0; k--)
                {
                    NavigationViewItems.Insert(1, items[k]);
                }
            }
        }

        #region Recycling

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as Grid;
            if (content == null)
            {
                return;
            }

            var session = args.Item as ISessionService;
            if (session == null)
            {
                return;
            }

            var user = session.ProtoService.GetUser(session.UserId);
            if (user == null)
            {
                return;
            }

            if (args.Phase == 0)
            {
                var title = content.Children[2] as TextBlock;
                title.Text = user.GetFullName();
            }
            else if (args.Phase == 2)
            {
                var photo = content.Children[0] as ProfilePicture;
                photo.Source = PlaceholderHelper.GetUser(session.ProtoService, user, 28);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(OnContainerContentChanging);
            }
        }

        #endregion

        private void Expand_Click(object sender, RoutedEventArgs e)
        {
            SettingsService.Current.IsAccountsSelectorExpanded = !SettingsService.Current.IsAccountsSelectorExpanded;

            InitializeSessions(SettingsService.Current.IsAccountsSelectorExpanded, _lifetime.Items);
            Expanded.IsChecked = SettingsService.Current.IsAccountsSelectorExpanded;
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem as string == "NavigationAdd")
            {
                Switch(_lifetime.Create());
            }
            else if (e.ClickedItem is ISessionService session)
            {
                if (session.IsActive)
                {
                    return;
                }

                Switch(session);
            }
            else if (_navigationService?.Frame?.Content is IRootContentPage content)
            {
                if (e.ClickedItem as string == NavigationNewChat.Name)
                {
                    content.NavigationView_ItemClick(RootDestination.NewChat);
                }
                else if (e.ClickedItem as string == NavigationNewSecretChat.Name)
                {
                    content.NavigationView_ItemClick(RootDestination.NewSecretChat);
                }
                else if (e.ClickedItem as string == NavigationNewChannel.Name)
                {
                    content.NavigationView_ItemClick(RootDestination.NewChannel);
                }
                else if (e.ClickedItem as string == NavigationChats.Name)
                {
                    content.NavigationView_ItemClick(RootDestination.Chats);
                }
                else if (e.ClickedItem as string == NavigationContacts.Name)
                {
                    content.NavigationView_ItemClick(RootDestination.Contacts);
                }
                else if (e.ClickedItem as string == NavigationCalls.Name)
                {
                    content.NavigationView_ItemClick(RootDestination.Calls);
                }
                else if (e.ClickedItem as string == NavigationSettings.Name)
                {
                    content.NavigationView_ItemClick(RootDestination.Settings);
                }
                //else if (e.ClickedItem as string == NavigationInviteFriends.Name)
                //{
                //    content.NavigationView_ItemClick(RootDestination.InviteFriends);
                //}
                else if (e.ClickedItem as string == NavigationSavedMessages.Name)
                {
                    content.NavigationView_ItemClick(RootDestination.SavedMessages);
                }
                else if (e.ClickedItem as string == NavigationNews.Name)
                {
                    content.NavigationView_ItemClick(RootDestination.News);
                }
            }

            Navigation.IsPaneOpen = false;

            var scroll = NavigationViewList.GetScrollViewer();
            if (scroll != null)
            {
                scroll.ChangeView(null, 0, null, true);
            }
        }

        #region Exposed

        public void SetPaneToggleButtonVisibility(Visibility value)
        {
            Navigation.PaneToggleButtonVisibility = value;
        }

        public void SetSelectedIndex(int value)
        {
            NavigationChats.IsChecked = value == 0;
            NavigationContacts.IsChecked = value == 1;
            NavigationCalls.IsChecked = value == 2;
            NavigationSettings.IsChecked = value == 3;
        }

        #endregion
    }

    public interface IRootContentPage
    {
        RootPage Root { get; set; }

        void NavigationView_ItemClick(RootDestination destination);

        //Visibility PaneToggleButtonVisibility { get; }

        Visibility EvalutatePaneToggleButtonVisibility();
    }

    public enum RootDestination
    {
        NewChat,
        NewSecretChat,
        NewChannel,

        Chats,
        Contacts,
        Calls,
        Settings,

        InviteFriends,

        SavedMessages,
        News
    }
}
