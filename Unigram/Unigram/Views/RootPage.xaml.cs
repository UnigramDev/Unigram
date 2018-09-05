using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Template10.Common;
using Template10.Services.NavigationService;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Core.Common;
using Unigram.Core.Services;
using Unigram.Services;
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

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Unigram.Views
{
    public sealed partial class RootPage : UserControl
    {
        private ILifecycleService _lifecycle;
        private NavigationService _navigationService;

        private bool _showSessions;

        public RootPage(NavigationService service)
        {
            InitializeComponent();

            _lifecycle = TLContainer.Current.Lifecycle;

            service.Frame.Navigating += OnNavigating;
            service.Frame.Navigated += OnNavigated;
            _navigationService = service;

            InitializeNavigation(service.Frame);
            InitializeLocalization();

            Navigation.Content = _navigationService.Frame;
        }

        public void Switch(ISessionService session)
        {
            if (_navigationService != null)
            {
                if (_navigationService?.Frame?.Content is IRootContentPage content)
                {
                    content.Root = null;
                }

                _navigationService.Frame.Navigating -= OnNavigating;
                _navigationService.Frame.Navigated -= OnNavigated;
            }

            var service = WindowWrapper.Current().NavigationServices.GetByFrameId(session.Id.ToString()) as NavigationService;
            if (service == null)
            {
                service = BootStrapper.Current.NavigationServiceFactory(BootStrapper.BackButton.Attach, BootStrapper.ExistingContent.Exclude, new Frame(), session.Id) as NavigationService;
                service.SerializationService = TLSerializationService.Current;
                service.FrameFacade.FrameId = session.Id.ToString();
                service.Frame.Navigating += OnNavigating;
                service.Frame.Navigated += OnNavigated;
                service.Navigate(typeof(MainPage));
                //WindowContext.GetForCurrentView().Handle(new UpdateAuthorizationState(session.ProtoService.GetAuthorizationState()));
            }
            else
            {
                if (service.Frame.Content is IRootContentPage content)
                {
                    content.Root = this;
                    Navigation.PaneToggleButtonVisibility = content.EvalutatePaneToggleButtonVisibility();
                    InitializeNavigation(service.Frame);
                }

                service.Frame.Navigating += OnNavigating;
                service.Frame.Navigated += OnNavigated;
            }

            _navigationService = service;
            Navigation.Content = service.Frame;
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

            InitializeSessions(_showSessions, _lifecycle.Items);

            foreach (var item in NavigationViewItems)
            {
                if (item is Controls.NavigationViewItem viewItem)
                {
                    viewItem.Content = viewItem.Name;
                }
            }
        }

        private void InitializeUser(MainViewModel viewModel)
        {
            for (int i = 0; i < NavigationViewItems.Count; i++)
            {
                if (NavigationViewItems[i] is MainViewModel)
                {
                    NavigationViewItems.RemoveAt(i);
                    i--;
                }
            }

            return;

            if (viewModel != null)
            {
                NavigationViewItems.Insert(0, viewModel);
            }
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
                else if (NavigationViewItems[i] is Controls.NavigationViewItem viewItem && string.Equals(viewItem.Name, "NavigationAdd"))
                {
                    NavigationViewItems.RemoveAt(i);
                    i--;
                }
            }

            if (show && items != null)
            {
                NavigationViewItems.Insert(1, new Controls.NavigationViewItem { Name = "NavigationAdd", Content = "NavigationAdd", Text = Strings.Resources.AddAccount, Glyph = "\uE109" });

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

            if (args.Item is ISessionService session)
            {
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
                    photo.Source = PlaceholderHelper.GetUser(session.ProtoService, user, 28, 28);
                }

                if (args.Phase < 2)
                {
                    args.RegisterUpdateCallback(OnContainerContentChanging);
                }
            }
            else if (args.Item is MainViewModel viewModel)
            {
                var user = viewModel.ProtoService.GetUser(viewModel.ProtoService.GetMyId());
                if (user == null)
                {
                    return;
                }

                if (args.Phase == 0)
                {
                    var title = content.Children[0] as TextBlock;
                    var phoneNumber = content.Children[1] as TextBlock;
                    var check = content.Children[2] as CheckBox;

                    title.Text = user.GetFullName();
                    phoneNumber.Text = Telegram.Helpers.PhoneNumber.Format(user.PhoneNumber);
                    check.IsChecked = _showSessions;
                }
            }
        }

        #endregion

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is MainViewModel)
            {
                var container = NavigationViewList.ContainerFromItem(e.ClickedItem) as SelectorItem;
                if (container == null)
                {
                    return;
                }

                var content = container.ContentTemplateRoot as Grid;
                if (content == null)
                {
                    return;
                }

                _showSessions = !_showSessions;

                InitializeSessions(_showSessions, _lifecycle.Items);

                var check = content.Children[2] as CheckBox;
                check.IsChecked = _showSessions;

                //var items = NavigationViewItems.Source as AnyCollection;
                //if (items.Contains(ViewModel.Lifecycle.Items))
                //{
                //    items.RemoveAt(1);
                //}
                //else
                //{
                //    items.Insert(1, ViewModel.Lifecycle.Items);
                //}
            }
            else
            {
                Navigation.IsPaneOpen = false;

                var scroll = NavigationViewList.GetScrollViewer();
                if (scroll != null)
                {
                    scroll.ChangeView(null, 0, null, true);
                }

                if (e.ClickedItem as string == "NavigationAdd")
                {
                    if (_navigationService != null)
                    {
                        if (_navigationService?.Frame?.Content is IRootContentPage content)
                        {
                            content.Root = null;
                        }

                        _navigationService.Frame.Navigating -= OnNavigating;
                        _navigationService.Frame.Navigated -= OnNavigated;
                    }

                    var session = _lifecycle.Create();
                    var service = WindowWrapper.Current().NavigationServices.GetByFrameId(session.Id.ToString()) as NavigationService;
                    if (service == null)
                    {
                        service = BootStrapper.Current.NavigationServiceFactory(BootStrapper.BackButton.Attach, BootStrapper.ExistingContent.Exclude, new Frame(), session.Id) as NavigationService;
                        service.SerializationService = TLSerializationService.Current;
                        service.FrameFacade.FrameId = session.Id.ToString();
                        service.Frame.Navigating += OnNavigating;
                        service.Frame.Navigated += OnNavigated;
                        service.Navigate(typeof(SignIn.SignInPage));
                        service.Frame.BackStack.Add(new PageStackEntry(typeof(BlankPage), null, null));
                    }
                    else
                    {
                        service.Frame.Navigating += OnNavigating;
                        service.Frame.Navigated += OnNavigated;
                    }

                    _navigationService = service;
                    Navigation.Content = service.Frame;
                }
                else if (e.ClickedItem is ISessionService session)
                {
                    if (session.IsActive)
                    {
                        return;
                    }

                    _lifecycle.ActiveItem = session;
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
                    else if (e.ClickedItem as string == NavigationSavedMessages.Name)
                    {
                        content.NavigationView_ItemClick(RootDestination.SavedMessages);
                    }
                    else if (e.ClickedItem as string == NavigationNews.Name)
                    {
                        content.NavigationView_ItemClick(RootDestination.News);
                    }
                }
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

        SavedMessages,
        News
    }
}
