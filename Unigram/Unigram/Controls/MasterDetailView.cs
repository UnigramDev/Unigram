using System;
using Template10.Services.NavigationService;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using Unigram.Views;
using Template10.Common;
using Windows.UI.Xaml.Media;
using Unigram.Core.Services;
using Unigram.Views.Users;

// The Templated Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234235

namespace Unigram.Controls
{
    public sealed class MasterDetailView : ContentControl
    {
        private Frame DetailFrame;
        private ContentPresenter MasterPresenter;
        private Grid DetailPresenter;
        private VisualStateGroup AdaptiveStates;
        private bool IsMasterHidden;
        private const string NarrowState = "NarrowState";

        public NavigationService NavigationService { get; private set; }

        public MasterDetailView()
        {
            DefaultStyleKey = typeof(MasterDetailView);
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            SizeChanged += OnSizeChanged;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateVisualState();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateVisualState();

            if (CurrentState != MasterDetailState.Narrow && ViewStateChanged != null)
            {
                ViewStateChanged(this, EventArgs.Empty);
            }

            Window.Current.CoreWindow.Dispatcher.AcceleratorKeyActivated += OnAcceleratorKeyActivated;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Window.Current.CoreWindow.Dispatcher.AcceleratorKeyActivated -= OnAcceleratorKeyActivated;
        }

        private void UpdateVisualState()
        {
            // If there are previous pages, see the state of the MD.
            // If it's narrow, show the back button in the titlebar,
            // else hide it.
            if (DetailFrame != null && DetailFrame.CanGoBack && !Windows.ApplicationModel.DesignMode.DesignModeEnabled)
            {
                if (CurrentState != MasterDetailState.Narrow)
                {
                    //if (DetailFrame.SourcePageType == typeof(DialogPage) ||
                    //    DetailFrame.SourcePageType == typeof(AboutPage) ||
                    //    DetailFrame.SourcePageType == typeof(SettingsPage))
                    //{
                    //    SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                    //        AppViewBackButtonVisibility.Collapsed;
                    //}else
                    //{
                    //    SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                    //    AppViewBackButtonVisibility.Visible;
                    //}
                }
                else
                {
                    //SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                    //    AppViewBackButtonVisibility.Visible;
                }
            }
        }

        private void OnAcceleratorKeyActivated(CoreDispatcher sender, AcceleratorKeyEventArgs args)
        {
            if ((args.EventType == CoreAcceleratorKeyEventType.SystemKeyDown || args.EventType == CoreAcceleratorKeyEventType.KeyDown) && (args.VirtualKey == VirtualKey.Escape))
            {
                if (DetailFrame.CanGoBack && CurrentState == MasterDetailState.Narrow)
                {
                    DetailFrame.GoBack();
                    args.Handled = true;
                }
            }
        }

        protected override void OnApplyTemplate()
        {
            if (Windows.ApplicationModel.DesignMode.DesignModeEnabled) return;

            MasterPresenter = (ContentPresenter)GetTemplateChild("MasterFrame");
            DetailPresenter = (Grid)GetTemplateChild("DetailPresenter");
            AdaptiveStates = (VisualStateGroup)GetTemplateChild("AdaptiveStates");
            AdaptiveStates.CurrentStateChanged += OnCurrentStateChanged;

            if (DetailFrame != null)
            {
                var parent = VisualTreeHelper.GetParent(DetailFrame) as UIElement;
                if (parent != null && parent != DetailPresenter)
                {
                    VisualTreeHelper.DisconnectChildrenRecursive(parent);
                }

                DetailFrame.Navigated += OnNavigated;
                DetailPresenter.Children.Add(DetailFrame);

                if (DetailFrame.CurrentSourcePageType == null)
                {
                    DetailFrame.Navigate(BlankPageType);
                }
                else
                {
                    DetailFrame.BackStack.Insert(0, new PageStackEntry(BlankPageType, null, null));
                }
            }

            if (ActualWidth > 0)
            {
                UpdateVisualState();

                if (CurrentState != MasterDetailState.Narrow && ViewStateChanged != null)
                {
                    ViewStateChanged(this, EventArgs.Empty);
                }
            }
        }

        private void OnNavigated(object sender, NavigationEventArgs e)
        {
            if (AdaptiveStates.CurrentState == null) return;

            if (CurrentState == MasterDetailState.Narrow && e.SourcePageType == BlankPageType)
            {
                DetailPresenter.Visibility = Visibility.Collapsed;
            }
            else
            {
                DetailPresenter.Visibility = Visibility.Visible;
            }

            if (CurrentState == MasterDetailState.Narrow)
            {
                if (e.NavigationMode == NavigationMode.New && DetailFrame.BackStackDepth == 1)
                {
                    IsMasterHidden = true;

                    // Now that there is a backstack, show the back button in titlebar
                    //SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                    //AppViewBackButtonVisibility.Visible;

                    var anim = new DrillInThemeAnimation();
                    anim.EntranceTarget = new Border();
                    anim.ExitTarget = MasterPresenter;

                    var board = new Storyboard();
                    board.Children.Add(anim);
                    board.Begin();
                }
                else if (e.NavigationMode == NavigationMode.Back && DetailFrame.BackStackDepth == 0)
                {
                    IsMasterHidden = false;

                    // No navigation backstack, hide back button in titlebar
                    //SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                    //AppViewBackButtonVisibility.Collapsed;

                    var anim = new DrillOutThemeAnimation();
                    anim.EntranceTarget = MasterPresenter;
                    anim.ExitTarget = new Border();

                    var board = new Storyboard();
                    board.Children.Add(anim);
                    board.Begin();
                }
            }
            else
            {
                // While in Filled (wide) state, if a page is already open remove it from backstack in
                // some particular cases. 
                if (e.NavigationMode == NavigationMode.New && DetailFrame.BackStackDepth > 1)
                {

                    // When the new page is a settings or about page.
                    if (e.SourcePageType == typeof(AboutPage) || e.SourcePageType == typeof(SettingsPage))
                    {
                        // The user opened first a chat, then the userinfo. Remove them from backstack.
                        if (DetailFrame.BackStackDepth == 3)
                        {
                            DetailFrame.BackStack.RemoveAt(DetailFrame.BackStackDepth - 2);
                            DetailFrame.BackStack.RemoveAt(DetailFrame.BackStackDepth - 1);
                        }
                        // Simple case of about or settings page
                        else
                        {
                            DetailFrame.BackStack.RemoveAt(DetailFrame.BackStackDepth - 1);
                        }
                        UpdateVisualState();

                    }
                    // When the new page is a chat
                    else if (e.SourcePageType == typeof(DialogPage))
                    {
                        // The user opened first a chat, then the userinfo. Remove them from backstack.
                        if (DetailFrame.BackStackDepth == 3)
                        {
                            DetailFrame.BackStack.RemoveAt(DetailFrame.BackStackDepth - 2);
                            DetailFrame.BackStack.RemoveAt(DetailFrame.BackStackDepth - 1);
                        }
                        // Simple case of consecutive chats open
                        else
                        {
                            DetailFrame.BackStack.RemoveAt(DetailFrame.BackStackDepth - 1);
                        }
                        UpdateVisualState();
                    }
                    // When the new page is user info show the back button in titlebar.
                    else if (e.SourcePageType == typeof(UserDetailsPage))
                    {
                        UpdateVisualState();
                    }
                }
                // Hide back button in filled (wide) state
                else if (e.NavigationMode == NavigationMode.Back && DetailFrame.BackStackDepth <= 1)
                {
                    UpdateVisualState();
                }
            }
        }

        #region Initialize
        public void Initialize(string key)
        {
            var service = WindowWrapper.Current().NavigationServices.GetByFrameId(key) as NavigationService;
            if (service == null)
            {
                service = BootStrapper.Current.NavigationServiceFactory(BootStrapper.BackButton.Ignore, BootStrapper.ExistingContent.Exclude) as NavigationService;
                service.SerializationService = TLSerializationService.Current;
                service.FrameFacade.FrameId = key;
                service.FrameFacade.BackRequested += (s, args) =>
                {
                    if (CanGoBack)
                    {
                        DetailFrame.GoBack();
                        args.Handled = true;
                    }
                };
            }

            NavigationService = service;
            DetailFrame = NavigationService.Frame;
        }
        #endregion

        private void OnCurrentStateChanged(object sender, VisualStateChangedEventArgs e)
        {
            ViewStateChanged?.Invoke(this, EventArgs.Empty);

            if (CurrentState == MasterDetailState.Filled && IsMasterHidden)
            {
                var anim = new DrillOutThemeAnimation();
                anim.EntranceTarget = MasterPresenter;
                anim.ExitTarget = new Border();

                var board = new Storyboard();
                board.Children.Add(anim);
                board.Begin();
            }

            if (CurrentState == MasterDetailState.Narrow && BlankPageType == DetailFrame?.CurrentSourcePageType)
            {
                DetailPresenter.Visibility = Visibility.Collapsed;
            }
            else
            {
                DetailPresenter.Visibility = Visibility.Visible;
            }
        }

        #region Public methods
        public bool CanGoBack
        {
            get
            {
                return DetailFrame.CanGoBack;
                
                // BEFORE BACK NAVIGATION IN FILLED (WIDE) STATE FIX.
                // return DetailFrame.CanGoBack && AdaptiveStates.CurrentState.Name == NarrowState;
            }
        }

        public MasterDetailState CurrentState
        {
            get
            {
                return AdaptiveStates.CurrentState.Name == NarrowState ? MasterDetailState.Narrow : MasterDetailState.Filled;
            }
        }

        public event EventHandler ViewStateChanged;
        #endregion

        #region BlankType
        public Type BlankPageType
        {
            get { return (Type)GetValue(BlankPageTypeProperty); }
            set { SetValue(BlankPageTypeProperty, value); }
        }

        public static readonly DependencyProperty BlankPageTypeProperty =
            DependencyProperty.Register("BlankPageType", typeof(Type), typeof(MasterDetailView), new PropertyMetadata(typeof(BlankPage)));
        #endregion
    }

    public enum MasterDetailState
    {
        Narrow,
        Filled
    }
}
