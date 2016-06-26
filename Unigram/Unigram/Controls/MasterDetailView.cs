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

// The Templated Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234235

namespace Unigram.Controls
{
    public sealed class MasterDetailView : ContentControl
    {
        private Frame DetailFrame;
        private ContentPresenter MasterPresenter;
        private Border DetailPresenter;
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
            VisualStateManager.GoToState(this, ActualWidth >= 820 ? "FilledState" : "NarrowState", false);

            // If there are previous pages, see the state of the MD.
            // If it's narrow, show the back button in the titlebar,
            // else hide it.
            if (DetailFrame != null && DetailFrame.CanGoBack && !Windows.ApplicationModel.DesignMode.DesignModeEnabled)
            {
                if (CurrentState != MasterDetailState.Narrow)
                {
                    SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                        AppViewBackButtonVisibility.Collapsed;
                }
                else
                {
                    SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                        AppViewBackButtonVisibility.Visible;
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
            MasterPresenter = (ContentPresenter)GetTemplateChild("MasterFrame");
            DetailPresenter = (Border)GetTemplateChild("DetailPresenter");
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
                DetailPresenter.Child = DetailFrame;

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
                    SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                    AppViewBackButtonVisibility.Visible;

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
                    SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                    AppViewBackButtonVisibility.Collapsed;

                    var anim = new DrillOutThemeAnimation();
                    anim.EntranceTarget = MasterPresenter;
                    anim.ExitTarget = new Border();

                    var board = new Storyboard();
                    board.Children.Add(anim);
                    board.Begin();
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
            if (ViewStateChanged != null)
            {
                ViewStateChanged(this, EventArgs.Empty);
            }

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
                return DetailFrame.CanGoBack && AdaptiveStates.CurrentState.Name == NarrowState;
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
