using System;
using System.Collections.Generic;
using System.ComponentModel;
using Unigram.Navigation;
using Unigram.Navigation.Services;
using Unigram.Views;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Controls
{
    public sealed class MasterDetailView : ContentControl, IDisposable
    {
        private MasterDetailPanel AdaptivePanel;
        private Frame DetailFrame;
        private ContentPresenter MasterPresenter;
        private Grid DetailPresenter;
        private bool IsMasterHidden;

        public NavigationService NavigationService { get; private set; }
        public Frame ParentFrame { get; private set; }

        private readonly LinkedList<BackStackType> _backStack = new LinkedList<BackStackType>();

        public MasterDetailView()
        {
            DefaultStyleKey = typeof(MasterDetailView);

            Loaded += OnLoaded;
            SizeChanged += OnSizeChanged;
        }

        #region Initialize

        public void Initialize(string key, Frame parent, int session)
        {
            var service = WindowContext.GetForCurrentView().NavigationServices.GetByFrameId(key + session) as NavigationService;
            if (service == null)
            {
                service = BootStrapper.Current.NavigationServiceFactory(BootStrapper.BackButton.Ignore, BootStrapper.ExistingContent.Exclude, session, key + session, false) as NavigationService;
                service.Frame.DataContext = new object();
                service.FrameFacade.BackRequested += OnBackRequested;
            }

            NavigationService = service;
            DetailFrame = NavigationService.Frame;
            ParentFrame = parent;
        }

        public void Dispose()
        {
            Loaded -= OnLoaded;
            SizeChanged -= OnSizeChanged;

            var service = NavigationService;
            if (service != null)
            {
                service.FrameFacade.BackRequested -= OnBackRequested;
            }

            var panel = AdaptivePanel;
            if (panel != null)
            {
                panel.ViewStateChanged -= OnViewStateChanged;
            }

            var frame = DetailFrame;
            if (frame != null)
            {
                frame.Navigated -= OnNavigated;
            }
        }

        private void OnBackRequested(object sender, HandledEventArgs args)
        {
            //var type = BackStackType.Navigation;
            //if (_backStack.Count > 0)
            //{
            //    type = _backStack.Last.Value;
            //    _backStack.RemoveLast();
            //}

            if (ParentFrame.Content is INavigatingPage masterPaging && CurrentState != MasterDetailState.Minimal)
            {
                masterPaging.OnBackRequesting(args);
                if (args.Handled)
                {
                    return;
                }
            }

            if (DetailFrame.Content is INavigablePage detailPage /*&& type == BackStackType.Navigation*/)
            {
                detailPage.OnBackRequested(args);
                if (args.Handled)
                {
                    return;
                }
            }

            // TODO: maybe checking for the actual width is not the perfect way,
            // but if it is 0 it means that the control is not loaded, and the event shouldn't be handled
            if (CanGoBack && ActualWidth > 0 /*&& type == BackStackType.Navigation*/)
            {
                DetailFrame.GoBack();
                args.Handled = true;
            }
            else if (ParentFrame.Content is INavigablePage masterPage /*&& type == BackStackType.Hamburger*/)
            {
                masterPage.OnBackRequested(args);
                if (args.Handled)
                {
                    return;
                }
            }
            else if (ParentFrame.CanGoBack && ActualWidth > 0)
            {
                ParentFrame.GoBack();
                args.Handled = true;
            }
        }

        #endregion

        public void Push(bool hamburger)
        {
            //if (hamburger)
            //{
            //    while (_backStack.Contains(BackStackType.Hamburger))
            //    {
            //        _backStack.Remove(BackStackType.Hamburger);
            //    }
            //}

            //_backStack.AddLast(hamburger ? BackStackType.Hamburger : BackStackType.Navigation);
        }

        public bool Last()
        {
            if (_backStack.Count > 0)
            {
                return _backStack.Last.Value == BackStackType.Hamburger;
            }

            return false;
        }

        enum BackStackType
        {
            Hamburger,
            Navigation
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateVisualState();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateVisualState();

            if (CurrentState == MasterDetailState.Minimal && DetailFrame.CurrentSourcePageType == BlankPageType)
            {
                MasterPresenter.Visibility = Visibility.Visible;
            }
            else if (CurrentState == MasterDetailState.Compact || CurrentState == MasterDetailState.Expanded)
            {
                MasterPresenter.Visibility = Visibility.Visible;
            }
            else
            {
                MasterPresenter.Visibility = Visibility.Collapsed;
            }

            if (CurrentState != MasterDetailState.Minimal && ViewStateChanged != null)
            {
                ViewStateChanged(this, EventArgs.Empty);
            }
        }

        private void UpdateVisualState()
        {
            // If there are previous pages, see the state of the MD.
            // If it's narrow, show the back button in the titlebar,
            // else hide it.
            if (DetailFrame != null && DetailFrame.CanGoBack && !Windows.ApplicationModel.DesignMode.DesignModeEnabled)
            {
                if (CurrentState != MasterDetailState.Minimal)
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

        protected override void OnApplyTemplate()
        {
            if (Windows.ApplicationModel.DesignMode.DesignModeEnabled) return;

            VisualStateManager.GoToState(this, "ResetState", false);

            MasterPresenter = (ContentPresenter)GetTemplateChild("MasterFrame");
            DetailPresenter = (Grid)GetTemplateChild("DetailPresenter");
            AdaptivePanel = (MasterDetailPanel)GetTemplateChild("AdaptivePanel");
            AdaptivePanel.ViewStateChanged += OnViewStateChanged;

            MasterPresenter.RegisterPropertyChangedCallback(VisibilityProperty, OnVisibilityChanged);

            var detailVisual = ElementCompositionPreview.GetElementVisual(DetailPresenter);
            detailVisual.Clip = Window.Current.Compositor.CreateInsetClip();

            if (DetailFrame != null)
            {
                var parent = VisualTreeHelper.GetParent(DetailFrame) as UIElement;
                if (parent != null && parent != DetailPresenter)
                {
                    VisualTreeHelper.DisconnectChildrenRecursive(parent);
                }

                //Grid.SetRow(DetailFrame, 1);
                try
                {
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
                catch { }
            }

            if (ActualWidth > 0)
            {
                UpdateVisualState();

                if (CurrentState != MasterDetailState.Minimal && ViewStateChanged != null)
                {
                    ViewStateChanged(this, EventArgs.Empty);
                }
            }
        }

        private void OnVisibilityChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (MasterPresenter.Visibility == Visibility.Visible)
            {
                Update?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnNavigated(object sender, NavigationEventArgs e)
        {
            if (e.Content is HostedPage hosted)
            {
                DetailHeader = hosted.Header;
            }
            else
            {
                DetailHeader = null;
            }

            if (AdaptivePanel == null)
            {
                return;
            }

            //if (e.Content is Page page)
            //{
            //    PageHeader = GetHeader(page);
            //}
            //else
            //{
            //    PageHeader = null;
            //}

            if (e.NavigationMode == NavigationMode.New && DetailFrame.CanGoBack)
            {
                Push(false);
            }

            if (CurrentState == MasterDetailState.Minimal && e.SourcePageType == BlankPageType)
            {
                MasterPresenter.Visibility = Visibility.Visible;
            }
            else if (CurrentState == MasterDetailState.Compact || CurrentState == MasterDetailState.Expanded)
            {
                MasterPresenter.Visibility = Visibility.Visible;
            }
            else
            {
                MasterPresenter.Visibility = Visibility.Collapsed;
            }

            if (CurrentState == MasterDetailState.Minimal)
            {
                if (e.NavigationMode == NavigationMode.New && DetailFrame.BackStackDepth == 1)
                {
                    IsMasterHidden = true;

                    //// Now that there is a backstack, show the back button in titlebar
                    ////SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                    ////AppViewBackButtonVisibility.Visible;

                    //var anim = new DrillInThemeAnimation();
                    //anim.EntranceTarget = new Border();
                    //anim.ExitTarget = MasterPresenter;

                    //var board = new Storyboard();
                    //board.Children.Add(anim);
                    //board.Begin();
                }
                else if (e.NavigationMode == NavigationMode.Back && DetailFrame.BackStackDepth == 0)
                {
                    IsMasterHidden = false;

                    //// No navigation backstack, hide back button in titlebar
                    ////SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                    ////AppViewBackButtonVisibility.Collapsed;

                    //var anim = new DrillOutThemeAnimation();
                    //anim.EntranceTarget = MasterPresenter;
                    //anim.ExitTarget = new Border();

                    //var board = new Storyboard();
                    //board.Children.Add(anim);
                    //board.Begin();
                }
            }
            else
            {
                // While in Filled (wide) state, if a page is already open remove it from backstack in
                // some particular cases. 
                if (e.NavigationMode == NavigationMode.New && DetailFrame.BackStackDepth > 1)
                {

                    // When the new page is a settings or about page.
                    if (/*e.SourcePageType == typeof(AboutPage) ||*/ e.SourcePageType == typeof(SettingsPage))
                    {
                        // The user opened first a chat, then the userinfo. Remove them from backstack.
                        //if (DetailFrame.BackStackDepth == 3)
                        //{
                        //    DetailFrame.BackStack.RemoveAt(DetailFrame.BackStackDepth - 2);
                        //    DetailFrame.BackStack.RemoveAt(DetailFrame.BackStackDepth - 1);
                        //}
                        //// Simple case of about or settings page
                        //else
                        //{
                        //    DetailFrame.BackStack.RemoveAt(DetailFrame.BackStackDepth - 1);
                        //}
                        UpdateVisualState();

                    }
                    // When the new page is a chat
                    else if (e.SourcePageType == typeof(ChatPage))
                    {
                        // The user opened first a chat, then the userinfo. Remove them from backstack.
                        //if (DetailFrame.BackStackDepth == 3)
                        //{
                        //    DetailFrame.BackStack.RemoveAt(DetailFrame.BackStackDepth - 2);
                        //    DetailFrame.BackStack.RemoveAt(DetailFrame.BackStackDepth - 1);
                        //}
                        //// Simple case of consecutive chats open
                        //else
                        //{
                        //    DetailFrame.BackStack.RemoveAt(DetailFrame.BackStackDepth - 1);
                        //}
                        UpdateVisualState();
                    }
                    // When the new page is user info show the back button in titlebar.
                    else if (e.SourcePageType == typeof(ProfilePage))
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

        private void OnViewStateChanged(object sender, EventArgs e)
        {
            ViewStateChanged?.Invoke(this, EventArgs.Empty);

            if ((CurrentState == MasterDetailState.Compact || CurrentState == MasterDetailState.Expanded) && IsMasterHidden)
            {
                //var anim = new DrillOutThemeAnimation();
                //anim.EntranceTarget = MasterPresenter;
                //anim.ExitTarget = new Border();

                //var board = new Storyboard();
                //board.Children.Add(anim);
                //board.Begin();
            }

            if (CurrentState == MasterDetailState.Minimal && BlankPageType == DetailFrame?.CurrentSourcePageType)
            {
                MasterPresenter.Visibility = Visibility.Visible;
            }
            else if (CurrentState == MasterDetailState.Compact || CurrentState == MasterDetailState.Expanded)
            {
                MasterPresenter.Visibility = Visibility.Visible;
            }
            else
            {
                MasterPresenter.Visibility = Visibility.Collapsed;
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
                if (AdaptivePanel == null)
                {
                    return MasterDetailState.Expanded;
                }

                return AdaptivePanel.CurrentState;
            }
        }

        public event EventHandler ViewStateChanged;
        public event EventHandler Update;

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

        #region AllowCompact

        public bool AllowCompact
        {
            get
            {
                return AdaptivePanel?.AllowCompact ?? true;
            }
            set
            {
                if (AdaptivePanel != null)
                {
                    AdaptivePanel.AllowCompact = value;
                }
            }
        }

        #endregion

        #region Banner

        public UIElement Banner
        {
            get { return (UIElement)GetValue(PageHeaderProperty); }
            set { SetValue(PageHeaderProperty, value); }
        }

        public static readonly DependencyProperty PageHeaderProperty =
            DependencyProperty.Register("Banner", typeof(UIElement), typeof(MasterDetailView), new PropertyMetadata(null));

        #endregion

        #region MasterHeader

        public UIElement MasterHeader
        {
            get { return (UIElement)GetValue(MasterHeaderProperty); }
            set { SetValue(MasterHeaderProperty, value); }
        }

        public static readonly DependencyProperty MasterHeaderProperty =
            DependencyProperty.Register("MasterHeader", typeof(UIElement), typeof(MasterDetailView), new PropertyMetadata(null));

        #endregion

        #region DetailHeader

        public UIElement DetailHeader
        {
            get { return (UIElement)GetValue(DetailHeaderProperty); }
            set { SetValue(DetailHeaderProperty, value); }
        }

        public static readonly DependencyProperty DetailHeaderProperty =
            DependencyProperty.Register("DetailHeader", typeof(UIElement), typeof(MasterDetailView), new PropertyMetadata(null));

        #endregion

        #region BackgroundOpacity

        public double BackgroundOpacity
        {
            get { return (double)GetValue(BackgroundOpacityProperty); }
            set { SetValue(BackgroundOpacityProperty, value); }
        }

        public static readonly DependencyProperty BackgroundOpacityProperty =
            DependencyProperty.Register("BackgroundOpacity", typeof(double), typeof(MasterDetailView), new PropertyMetadata(1d));

        #endregion

        #region IsBlank

        public bool IsBlank
        {
            get { return (bool)GetValue(IsBlankProperty); }
            set { SetValue(IsBlankProperty, value); }
        }

        public static readonly DependencyProperty IsBlankProperty =
            DependencyProperty.Register("IsBlank", typeof(bool), typeof(MasterDetailView), new PropertyMetadata(true));

        #endregion
    }

    public enum MasterDetailState
    {
        Minimal,
        Compact,
        Expanded
    }
}
