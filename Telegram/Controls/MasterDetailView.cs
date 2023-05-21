//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Numerics;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls.Chats;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Views;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Controls
{
    public enum BackgroundKind
    {
        None,
        Material,
        Background
    }

    public sealed class MasterDetailView : ContentControl, IDisposable
    {
        private MasterDetailPanel AdaptivePanel;
        private Frame DetailFrame;
        private ContentControl MasterPresenter;
        private Grid DetailPresenter;
        private BreadcrumbBar DetailHeaderPresenter;
        private Border DetailHeaderBackground;
        private ChatBackgroundControl BackgroundPart;
        private ContentControl DetailAction;
        private Border BorderPart;
        private Border MaterialPart;

        public ViewModelBase ViewModel { get; private set; }
        public NavigationService NavigationService { get; private set; }
        public Frame ParentFrame { get; private set; }

        private readonly MvxObservableCollection<NavigationStackItem> _backStack = new();
        private readonly NavigationStackItem _currentPage = new(null, null, null, false);

        private long _titleToken;

        public MasterDetailView()
        {
            DefaultStyleKey = typeof(MasterDetailView);

            Loaded += OnLoaded;
        }

        #region Initialize

        public void Initialize(string key, Frame parent, ViewModelBase viewModel)
        {
            var service = WindowContext.Current.NavigationServices.GetByFrameId(key + viewModel.SessionId) as NavigationService;
            if (service == null)
            {
                service = BootStrapper.Current.NavigationServiceFactory(BootStrapper.BackButton.Ignore, BootStrapper.ExistingContent.Exclude, viewModel.SessionId, key + viewModel.SessionId, false) as NavigationService;
                service.Frame.DataContext = new object();
                service.FrameFacade.BackRequested += OnBackRequested;
                service.BackStackChanged += OnBackStackChanged;
            }

            NavigationService = service;
            ViewModel = viewModel;
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
                service.BackStackChanged -= OnBackStackChanged;
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

        private void OnBackRequested(object sender, BackRequestedRoutedEventArgs args)
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
                MasterVisibility = Visibility.Visible;
            }
            else if (CurrentState is MasterDetailState.Compact or MasterDetailState.Expanded)
            {
                MasterVisibility = Visibility.Visible;
            }
            else
            {
                MasterVisibility = Visibility.Collapsed;
            }
        }

        private BackgroundKind _backgroundType;

        public void ShowHideBackground(BackgroundKind show, bool animate)
        {
            if (_backgroundType == show || BackgroundPart == null)
            {
                return;
            }

            var type = _backgroundType;

            _backgroundType = show;

            var visual = ElementCompositionPreview.GetElementVisual(BackgroundPart);
            var border = ElementCompositionPreview.GetElementVisual(BorderPart);
            var material = ElementCompositionPreview.GetElementVisual(MaterialPart);
            var bread = ElementCompositionPreview.GetElementVisual(DetailHeaderPresenter);

            ShowHideDetailHeader(show == BackgroundKind.Material);

            if (animate)
            {
                BackgroundPart.Visibility = Visibility.Visible;
                BorderPart.Visibility = Visibility.Visible;
                MaterialPart.Visibility = Visibility.Visible;
                DetailHeaderPresenter.Visibility = Visibility.Visible;

                var batch = visual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
                batch.Completed += (s, args) =>
                {
                    BackgroundPart.Visibility = show == BackgroundKind.Background ? Visibility.Visible : Visibility.Collapsed;
                    BorderPart.Visibility = show != BackgroundKind.None ? Visibility.Visible : Visibility.Collapsed;
                    MaterialPart.Visibility = show == BackgroundKind.Material ? Visibility.Visible : Visibility.Collapsed;
                    DetailHeaderPresenter.Visibility = show == BackgroundKind.Material ? Visibility.Visible : Visibility.Collapsed;
                };

                var fadeOut = visual.Compositor.CreateScalarKeyFrameAnimation();
                fadeOut.InsertKeyFrame(0, 1);
                fadeOut.InsertKeyFrame(1, 0);

                var fadeIn = visual.Compositor.CreateScalarKeyFrameAnimation();
                fadeIn.InsertKeyFrame(0, 0);
                fadeIn.InsertKeyFrame(1, 1);

                if (show == BackgroundKind.Background)
                {
                    visual.StartAnimation("Opacity", fadeIn);

                    if (type == BackgroundKind.None)
                    {
                        border.StartAnimation("Opacity", fadeIn);
                    }
                    else if (type == BackgroundKind.Material)
                    {
                        material.StartAnimation("Opacity", fadeOut);
                        bread.StartAnimation("Opacity", fadeOut);
                    }
                }
                else if (show == BackgroundKind.Material)
                {
                    material.StartAnimation("Opacity", fadeIn);
                    bread.StartAnimation("Opacity", fadeIn);

                    if (type == BackgroundKind.None)
                    {
                        border.StartAnimation("Opacity", fadeIn);
                    }
                    else if (type == BackgroundKind.Background)
                    {
                        visual.StartAnimation("Opacity", fadeOut);
                    }
                }
                else if (show == BackgroundKind.None)
                {
                    border.StartAnimation("Opacity", fadeOut);

                    if (type == BackgroundKind.Background)
                    {
                        visual.StartAnimation("Opacity", fadeOut);
                    }
                    else
                    {
                        material.StartAnimation("Opacity", fadeOut);
                        bread.StartAnimation("Opacity", fadeOut);
                    }
                }

                batch.End();
            }
            else
            {
                visual.Opacity = show == BackgroundKind.Background ? 1 : 0;
                border.Opacity = show != BackgroundKind.None ? 1 : 0;
                material.Opacity = show == BackgroundKind.Material ? 1 : 0;
                bread.Opacity = show == BackgroundKind.Material ? 1 : 0;

                BackgroundPart.Visibility = show == BackgroundKind.Background ? Visibility.Visible : Visibility.Collapsed;
                BorderPart.Visibility = show != BackgroundKind.None ? Visibility.Visible : Visibility.Collapsed;
                MaterialPart.Visibility = show == BackgroundKind.Material ? Visibility.Visible : Visibility.Collapsed;
                DetailHeaderPresenter.Visibility = show == BackgroundKind.Material ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        protected override void OnApplyTemplate()
        {
            VisualStateManager.GoToState(this, "ResetState", false);

            MasterPresenter = GetTemplateChild("MasterFrame") as ContentControl;
            DetailPresenter = GetTemplateChild(nameof(DetailPresenter)) as Grid;
            DetailHeaderPresenter = GetTemplateChild(nameof(DetailHeaderPresenter)) as BreadcrumbBar;
            DetailHeaderBackground = GetTemplateChild(nameof(DetailHeaderBackground)) as Border;
            DetailAction = GetTemplateChild(nameof(DetailAction)) as ContentControl;
            BackgroundPart = GetTemplateChild(nameof(BackgroundPart)) as ChatBackgroundControl;
            BorderPart = GetTemplateChild(nameof(BorderPart)) as Border;
            MaterialPart = GetTemplateChild(nameof(MaterialPart)) as Border;
            AdaptivePanel = GetTemplateChild(nameof(AdaptivePanel)) as MasterDetailPanel;
            AdaptivePanel.ViewStateChanged += OnViewStateChanged;

            DetailHeaderPresenter.ItemsSource = _backStack;
            DetailHeaderPresenter.ItemClicked += DetailHeaderPresenter_ItemClicked;

            BackgroundPart.Update(ViewModel.ClientService, ViewModel.Aggregator);
            BackgroundPart.Visibility = _backgroundType == BackgroundKind.Background ? Visibility.Visible : Visibility.Collapsed;
            BorderPart.Visibility = _backgroundType != BackgroundKind.None ? Visibility.Visible : Visibility.Collapsed;
            MaterialPart.Visibility = _backgroundType == BackgroundKind.Material ? Visibility.Visible : Visibility.Collapsed;
            DetailHeaderPresenter.Visibility = _backgroundType == BackgroundKind.Material ? Visibility.Visible : Visibility.Collapsed;

            ElementCompositionPreview.SetIsTranslationEnabled(DetailAction, true);

            var detailVisual = ElementCompositionPreview.GetElementVisual(DetailPresenter);
            detailVisual.Clip = Window.Current.Compositor.CreateInsetClip();

            var visual1 = ElementCompositionPreview.GetElementVisual(DetailHeaderBackground);
            var visual2 = ElementCompositionPreview.GetElementVisual(DetailHeaderPresenter);

            visual2.CenterPoint = new Vector3(0, -16, 0);
            visual1.Opacity = 0;

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
                        NavigationService.InsertToBackStack(0, BlankPageType);
                    }
                }
                catch { }
            }

            if (ActualWidth > 0 && CurrentState != MasterDetailState.Minimal)
            {
                OnViewStateChanged();
            }
        }

        private void DetailHeaderPresenter_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
        {
            var index = args.Index + 1;
            var count = _backStack.Count - 1;

            while (count - index > 0)
            {
                NavigationService.RemoveFromBackStack(DetailFrame.BackStackDepth - 1);
                count--;
            }

            NavigationService.GoBack();
        }

        private void OnNavigating(object sender, NavigatingEventArgs e)
        {
            if (e.Content is HostedPage hosted)
            {
                hosted.UnregisterPropertyChangedCallback(HostedPage.TitleProperty, _titleToken);

                var scrollingHost = hosted.FindName("ScrollingHost");
                if (scrollingHost is ListViewBase list)
                {
                    list.Loaded -= SetScrollingHost;
                }
            }
        }

        private void OnNavigated(object sender, NavigationEventArgs e)
        {
            if (e.Content is HostedPage hosted)
            {
                DetailFooter = hosted.Action;

                if (hosted.ShowHeader)
                {
                    _titleToken = hosted.RegisterPropertyChangedCallback(HostedPage.TitleProperty, OnTitleChanged);

                    if (string.IsNullOrEmpty(hosted.Title))
                    {
                        _backStack.Clear();
                    }
                    else
                    {
                        _currentPage.Title = hosted.Title;

                        _backStack.ReplaceWith(BuildBackStack(hosted.IsNavigationRoot));
                        _backStack.Add(_currentPage);
                    }

                    var scrollingHost = hosted.FindName("ScrollingHost");
                    if (scrollingHost is ListViewBase list)
                    {
                        list.Loaded += SetScrollingHost;
                    }
                    else if (scrollingHost is ScrollViewer scroll)
                    {
                        SetScrollingHost(scroll);
                    }

                    ShowHideDetailHeader(true);
                }
                else
                {
                    _backStack.Clear();
                    ShowHideDetailHeader(false);
                }
            }
            else
            {
                DetailHeader = null;
                DetailFooter = null;

                _backStack.Clear();
                ShowHideDetailHeader(false);
            }

            if (AdaptivePanel == null)
            {
                return;
            }

            OnViewStateChanged();
        }

        private void ShowHideDetailHeader(bool show)
        {
            var detailVisual = ElementCompositionPreview.GetElementVisual(DetailPresenter);
            detailVisual.Clip = Window.Current.Compositor.CreateInsetClip();

            if (detailVisual.Clip is InsetClip clip)
            {
                clip.TopInset = show ? 2 : 0;
            }

            DetailHeaderBackground.Visibility = show
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void SetScrollingHost(object sender, RoutedEventArgs e)
        {
            if (sender is ListViewBase list)
            {
                var scroller = list.GetScrollViewer();
                if (scroller != null)
                {
                    SetScrollingHost(scroller);
                }
            }
        }

        private void SetScrollingHost(ScrollViewer scroller)
        {
            if (scroller == null)
            {
                return;
            }

            var visual1 = ElementCompositionPreview.GetElementVisual(DetailHeaderBackground);
            var visual2 = ElementCompositionPreview.GetElementVisual(DetailHeaderPresenter);
            var visual3 = ElementCompositionPreview.GetElementVisual(DetailAction);

            // min out: 0.583
            // max out: 1

            // min in:  1
            // max in:  1.714

            var properties = ElementCompositionPreview.GetScrollViewerManipulationPropertySet(scroller);

            var expOut = "clamp(1 - ((-scrollViewer.Translation.Y / 32) * 0.417), 0.583, 1)";
            var slideOut = visual1.Compositor.CreateExpressionAnimation($"vector3({expOut}, {expOut}, 1)");
            slideOut.SetReferenceParameter("scrollViewer", properties);

            var expOut3 = "-clamp(((-scrollViewer.Translation.Y / 32) * 16), 0, 16)";
            var slideOut3 = visual1.Compositor.CreateExpressionAnimation(expOut3);
            slideOut3.SetReferenceParameter("scrollViewer", properties);

            var expIn = "clamp(1.357 - ((-scrollViewer.Translation.Y / 32) * 0.357), 1, 1.357)";
            var slideIn = visual1.Compositor.CreateExpressionAnimation($"vector3({expIn}, {expIn}, 1)");
            slideIn.SetReferenceParameter("scrollViewer", properties);

            visual1.StartAnimation("Scale", slideIn);
            visual2.StartAnimation("Scale", slideOut);
            visual3.StartAnimation("Translation.Y", slideOut3);

            var fadeIn = visual1.Compositor.CreateExpressionAnimation("scrollViewer.Translation.Y < -16 ? -(scrollViewer.Translation.Y + 16) / 16 : 0");
            fadeIn.SetReferenceParameter("scrollViewer", properties);

            visual1.StartAnimation("Opacity", fadeIn);
        }

        private void OnTitleChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (sender is HostedPage hosted)
            {
                if (string.IsNullOrEmpty(hosted.Title))
                {
                    _backStack.Clear();
                }
                else if (_backStack.Count > 0)
                {
                    _currentPage.Title = hosted.Title;
                }
                else
                {
                    _currentPage.Title = hosted.Title;

                    _backStack.ReplaceWith(BuildBackStack(hosted.IsNavigationRoot));
                    _backStack.Add(_currentPage);
                }
            }
        }

        private void OnBackStackChanged(object sender, EventArgs e)
        {
            if (DetailFrame.Content is HostedPage hosted)
            {
                _backStack.ReplaceWith(BuildBackStack(hosted.IsNavigationRoot));
                _backStack.Add(_currentPage);
            }
            else if (_backStack.Count > 0)
            {
                _backStack.Clear();
            }
        }

        private IEnumerable<NavigationStackItem> BuildBackStack(bool root)
        {
            if (root)
            {
                yield break;
            }

            var index = NavigationService.BackStack.FindLastIndex(x => x.IsRoot);
            var k = Math.Max(index, 0);

            for (int i = k; i < NavigationService.BackStack.Count; i++)
            {
                var item = NavigationService.BackStack[i];
                if (item.Title != null)
                {
                    yield return item;
                }
            }
        }

        private void OnViewStateChanged(object sender, EventArgs e)
        {
            OnViewStateChanged();
        }

        private void OnViewStateChanged()
        {
            if (AdaptivePanel == null)
            {
                return;
            }

            if (_isMinimal != IsMinimal)
            {
                _isMinimal = IsMinimal;

                VisualStateManager.GoToState(this, IsMinimal ? "Minimal" : "Expanded", false);
                ViewStateChanged?.Invoke(this, EventArgs.Empty);
            }

            UpdateMasterVisibility();
        }

        private bool _isMinimal = false;
        private bool IsMinimal =>
            AdaptivePanel?.CurrentState == MasterDetailState.Minimal
            && IsOnlyChild;

        private bool _isOnlyChild = true;
        public bool IsOnlyChild
        {
            get => _isOnlyChild;
            set
            {
                if (_isOnlyChild != value)
                {
                    _isOnlyChild = value;
                    OnViewStateChanged();
                }
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

        #region DetailHeader

        public UIElement DetailHeader
        {
            get => (UIElement)GetValue(DetailHeaderProperty);
            set => SetValue(DetailHeaderProperty, value);
        }

        public static readonly DependencyProperty DetailHeaderProperty =
            DependencyProperty.Register("DetailHeader", typeof(UIElement), typeof(MasterDetailView), new PropertyMetadata(null));

        #endregion

        #region DetailFooter

        public UIElement DetailFooter
        {
            get { return (UIElement)GetValue(DetailFooterProperty); }
            set { SetValue(DetailFooterProperty, value); }
        }

        public static readonly DependencyProperty DetailFooterProperty =
            DependencyProperty.Register("DetailFooter", typeof(UIElement), typeof(MasterDetailView), new PropertyMetadata(null));

        #endregion

        #region BackgroundMargin

        public Thickness BackgroundMargin
        {
            get { return (Thickness)GetValue(BackgroundMarginProperty); }
            set { SetValue(BackgroundMarginProperty, value); }
        }

        public static readonly DependencyProperty BackgroundMarginProperty =
            DependencyProperty.Register("BackgroundMargin", typeof(Thickness), typeof(MasterDetailView), new PropertyMetadata(default(Thickness)));

        #endregion

        #region MasterVisibility

        public Visibility MasterVisibility
        {
            get { return (Visibility)GetValue(MasterVisibilityProperty); }
            set { SetValue(MasterVisibilityProperty, value); }
        }

        public static readonly DependencyProperty MasterVisibilityProperty =
            DependencyProperty.Register("MasterVisibility", typeof(Visibility), typeof(MasterDetailView), new PropertyMetadata(Visibility.Visible));

        #endregion
    }

    public enum MasterDetailState
    {
        Unknown,
        Minimal,
        Compact,
        Expanded
    }
}
