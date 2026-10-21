//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using LinqToVisualTree;
using System;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Authorization;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views.Host
{
    public interface IPopupHost
    {
        void PopupOpened();
        void PopupClosed();
    }

    public sealed partial class RootWindow : WindowContent, INavigationHost
    {
        private readonly ILifetimeService _lifetime;

        private NavigationService _navigationService;

        public RootWindow(WindowContext context, NavigationService service)
            : base(context, false)
        {
            InitializeComponent();

            _lifetime = LifetimeService.Current;

            service.Frame.Navigating += OnNavigating;
            service.Frame.Navigated += OnNavigated;
            service.FrameFacade.ShortcutInvoked += OnShortcutInvoked;

            _navigationService = service;
            Navigation.Child = _navigationService.Frame;

            VisualUtilities.DropShadow(ThemeShadow);
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

        // The root frame's own service, for the window to walk; NavigationService above is
        // where navigation should go instead.
        INavigationService INavigationHost.NavigationService => _navigationService;

        protected override void OnPopupOpened()
        {
            if (_navigationService.Frame.Content is IRootContentPage content)
            {
                content.PopupOpened();
            }
        }

        protected override void OnPopupClosed()
        {
            if (_navigationService.Frame.Content is IRootContentPage content)
            {
                content.PopupClosed();
            }
        }

        public void UpdateComponent()
        {
            _contentLoaded = false;
            Resources.Clear();
            InitializeComponent();

            Switch(_lifetime.ActiveItem);
        }

        public void Create()
        {
            var premium = 0;
            var count = 0;

            foreach (var session in _lifetime.Items)
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

        public void Switch(ISession session)
        {
            Logger.Info(session.Id);

            _lifetime.ActiveItem = session;

            if (_lifetime.ActiveItem != session)
            {
                return;
            }

            if (_navigationService != null)
            {
                Destroy(_navigationService);
            }

            var service = BootStrapper.Current.NavigationServiceFactory(session, Window, BootStrapper.BackButton.Attach, new Frame { CacheSize = 0 }, $"{session.Id}", true) as NavigationService;
            service.Frame.Navigating += OnNavigating;
            service.Frame.Navigated += OnNavigated;
            service.FrameFacade.ShortcutInvoked += OnShortcutInvoked;

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

            _navigationService = service;
            Navigation.Child = service.Frame;
        }

        private void Destroy(NavigationService master)
        {
            Logger.Info(master.Session.Id);

            if (master.Frame.Content is IRootContentPage content)
            {
                content.Root = null;
                content.Dispose();
            }

            master.Frame.Navigating -= OnNavigating;
            master.Frame.Navigated -= OnNavigated;
            master.FrameFacade.ShortcutInvoked -= OnShortcutInvoked;
            master.Suspend();
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

                Window.Content = butt;
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

            Navigation.Child = panel;
            _navigationService = null;
        }

        public void ShowEditor(ThemeCustomInfo theme)
        {
            var resize = ThemePage == null;

            FindName("ThemePage");
            ThemePage.ViewModel.NavigationService = _navigationService;
            ThemePage.Load(theme);

            if (resize)
            {
                MainColumn.Width = new GridLength(ActualWidth, GridUnitType.Pixel);

                var size = Window.VisibleBounds;

                Window.TryResizeView(new Size(size.Width + 320, size.Height));
                WindowContext.PreferredLaunchViewSize = new Size(size.Width, size.Height);

                MainColumn.Width = new GridLength(1, GridUnitType.Star);
            }
        }

        public void HideEditor()
        {
            UnloadObject(ThemePage);

            Window.TryResizeView(WindowContext.PreferredLaunchViewSize);
        }

        private void OnShortcutInvoked(object sender, ShortcutInvokedEventArgs args)
        {
            if (_navigationService?.Frame.Content is MainPage mainPage)
            {
                mainPage.ProcessKeyboardAccelerators(args);
            }
        }
    }

    public interface IRootContentPage : IPopupHost
    {
        RootWindow Root { get; set; }
        void Dispose();
    }
}
