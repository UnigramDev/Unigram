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

        public NavigationService NavigationService { get; private set; }
        public Frame ParentFrame { get; private set; }

        private readonly LinkedList<BackStackType> _backStack = new LinkedList<BackStackType>();

        public MasterDetailView()
        {
            DefaultStyleKey = typeof(MasterDetailView);

            Loaded += OnLoaded;
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

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (CurrentState != MasterDetailState.Minimal)
            {
                OnViewStateChanged();
            }
        }

        private void UpdateMasterVisibility()
        {
            if (CurrentState == MasterDetailState.Minimal && DetailFrame?.CurrentSourcePageType == BlankPageType)
            {
                MasterPresenter.Visibility = Visibility.Visible;
                MasterHeader.Visibility = Visibility.Visible;
            }
            else if (CurrentState is MasterDetailState.Compact or MasterDetailState.Expanded)
            {
                MasterPresenter.Visibility = Visibility.Visible;
                MasterHeader.Visibility = Visibility.Visible;
            }
            else
            {
                MasterPresenter.Visibility = Visibility.Collapsed;
                MasterHeader.Visibility = Visibility.Collapsed;
            }
        }

        protected override void OnApplyTemplate()
        {
            VisualStateManager.GoToState(this, "ResetState", false);

            MasterPresenter = (ContentPresenter)GetTemplateChild("MasterFrame");
            DetailPresenter = (Grid)GetTemplateChild("DetailPresenter");
            AdaptivePanel = (MasterDetailPanel)GetTemplateChild("AdaptivePanel");
            AdaptivePanel.ViewStateChanged += OnViewStateChanged;

            MasterPresenter.RegisterPropertyChangedCallback(VisibilityProperty, OnVisibilityChanged);

            if (DetailFrame != null)
            {
                var detailVisual = ElementCompositionPreview.GetElementVisual(DetailFrame);
                detailVisual.Clip = Window.Current.Compositor.CreateInsetClip();

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

            if (ActualWidth > 0 && CurrentState != MasterDetailState.Minimal)
            {
                OnViewStateChanged();
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

            if (e.NavigationMode == NavigationMode.New && DetailFrame.CanGoBack)
            {
                Push(false);
            }

            UpdateMasterVisibility();
        }

        private void OnViewStateChanged(object sender, EventArgs e)
        {
            OnViewStateChanged();
        }

        private void OnViewStateChanged()
        {
            VisualStateManager.GoToState(this, AdaptivePanel.CurrentState == MasterDetailState.Minimal ? "Minimal" : "Expanded", false);
            ViewStateChanged?.Invoke(this, EventArgs.Empty);

            UpdateMasterVisibility();
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
            get => (Type)GetValue(BlankPageTypeProperty);
            set => SetValue(BlankPageTypeProperty, value);
        }

        public static readonly DependencyProperty BlankPageTypeProperty =
            DependencyProperty.Register("BlankPageType", typeof(Type), typeof(MasterDetailView), new PropertyMetadata(typeof(BlankPage)));
        #endregion

        #region AllowCompact

        public bool AllowCompact
        {
            get => AdaptivePanel?.AllowCompact ?? true;
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
            get => (UIElement)GetValue(PageHeaderProperty);
            set => SetValue(PageHeaderProperty, value);
        }

        public static readonly DependencyProperty PageHeaderProperty =
            DependencyProperty.Register("Banner", typeof(UIElement), typeof(MasterDetailView), new PropertyMetadata(null));

        #endregion

        #region MasterHeader

        public UIElement MasterHeader
        {
            get => (UIElement)GetValue(MasterHeaderProperty);
            set => SetValue(MasterHeaderProperty, value);
        }

        public static readonly DependencyProperty MasterHeaderProperty =
            DependencyProperty.Register("MasterHeader", typeof(UIElement), typeof(MasterDetailView), new PropertyMetadata(null));

        #endregion

        #region DetailHeader

        public UIElement DetailHeader
        {
            get => (UIElement)GetValue(DetailHeaderProperty);
            set => SetValue(DetailHeaderProperty, value);
        }

        public static readonly DependencyProperty DetailHeaderProperty =
            DependencyProperty.Register("DetailHeader", typeof(UIElement), typeof(MasterDetailView), new PropertyMetadata(null));

        #endregion

        #region BackgroundOpacity

        public double BackgroundOpacity
        {
            get => (double)GetValue(BackgroundOpacityProperty);
            set => SetValue(BackgroundOpacityProperty, value);
        }

        public static readonly DependencyProperty BackgroundOpacityProperty =
            DependencyProperty.Register("BackgroundOpacity", typeof(double), typeof(MasterDetailView), new PropertyMetadata(1d));

        #endregion

        #region IsBlank

        public bool IsBlank
        {
            get => (bool)GetValue(IsBlankProperty);
            set => SetValue(IsBlankProperty, value);
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
