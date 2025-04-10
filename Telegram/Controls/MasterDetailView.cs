//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.Interactions;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Numerics;
using Telegram.Assets.Icons;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Composition;
using Telegram.Controls.Chats;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Views;

namespace Telegram.Controls
{
    public enum BackgroundKind
    {
        None,
        Material,
        Background
    }

    public sealed partial class MasterDetailView : ContentControl, IDisposable
    {
        private MasterDetailPanel AdaptivePanel;
        private Frame DetailFrame;
        private Grid DetailHeaderPresenter2;
        private Grid DetailPresenter;
        private BreadcrumbBar DetailHeaderPresenter;
        private BackButton BackButton;
        private Border DetailHeaderBackground;
        private ChatBackgroundControl BackgroundPart;
        private ContentControl DetailAction;
        private Border BorderPart;
        private Border MaterialPart;

        private Border GoBackIcon;
        private Border GoBackGlyph;

        public ViewModelBase ViewModel { get; private set; }
        public NavigationService NavigationService { get; private set; }
        public Frame ParentFrame { get; private set; }

        private readonly MvxObservableCollection<NavigationStackItem> _backStack = new();
        private readonly NavigationStackItem _currentPage = new(null, null, null, HostedNavigationMode.Child);

        private long _titleToken;

        private bool _templateApplied;

        public MasterDetailView()
        {
            DefaultStyleKey = typeof(MasterDetailView);

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        #region Initialize

        public void Initialize(string key, Frame parent, ViewModelBase viewModel)
        {
            var service = WindowContext.Current.NavigationServices.GetByFrameId(key + viewModel.SessionId) as NavigationService;
            if (service == null)
            {
                service = BootStrapper.Current.NavigationServiceFactory(viewModel.NavigationService.Window, BootStrapper.BackButton.Ignore, viewModel.SessionId, key + viewModel.SessionId, false) as NavigationService;
                service.Frame.DataContext = new object();
                service.FrameFacade.BackRequested += OnBackRequested;
                service.BackStackChanged += OnBackStackChanged;
                service.Navigated += OnNavigated;
            }

            Initialize(service, parent, viewModel, true);
        }

        public void Initialize(NavigationService service, Frame parent, ViewModelBase viewModel, bool hasMaster)
        {
            NavigationService = service;
            ViewModel = viewModel;
            DetailFrame = service.Frame;
            ParentFrame = parent;

            HasMaster = hasMaster;
        }

        public bool HasMaster { get; private set; }

        public void Dispose()
        {
            Loaded -= OnLoaded;

            if (NavigationService != null)
            {
                NavigationService.FrameFacade.BackRequested -= OnBackRequested;
                NavigationService.BackStackChanged -= OnBackStackChanged;
                NavigationService.Navigated -= OnNavigated;
            }

            if (AdaptivePanel != null)
            {
                AdaptivePanel.ViewStateChanged -= OnViewStateChanged;
            }

            if (DetailHeaderPresenter != null)
            {
                DetailHeaderPresenter.ItemsSource = null;
                DetailHeaderPresenter.ItemClicked -= DetailHeaderPresenter_ItemClicked;
            }

            NavigationService = null;
            ViewModel = null;
            DetailFrame = null;
            ParentFrame = null;
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
            OnLoaded2(sender, e);

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
                _backgroundType = show;
                return;
            }

            var type = _backgroundType;

            _backgroundType = show;

            var visual = ElementComposition.GetElementVisual(BackgroundPart);
            var border = ElementComposition.GetElementVisual(BorderPart);
            var material = ElementComposition.GetElementVisual(MaterialPart);
            var bread = ElementComposition.GetElementVisual(DetailHeaderPresenter);
            var button = ElementComposition.GetElementVisual(BackButton);

            ShowHideDetailHeader(show == BackgroundKind.Material, true);

            if (animate)
            {
                BackgroundPart.Visibility = Visibility.Visible;
                BorderPart.Visibility = Visibility.Visible;
                MaterialPart.Visibility = Visibility.Visible;
                DetailHeaderPresenter.Visibility = Visibility.Visible;
                BackButton.Visibility = Visibility.Visible;

                var batch = visual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
                batch.Completed += (s, args) =>
                {
                    BackgroundPart.Visibility = show == BackgroundKind.Background ? Visibility.Visible : Visibility.Collapsed;
                    BorderPart.Visibility = show != BackgroundKind.None ? Visibility.Visible : Visibility.Collapsed;
                    MaterialPart.Visibility = show == BackgroundKind.Material ? Visibility.Visible : Visibility.Collapsed;
                    DetailHeaderPresenter.Visibility = show == BackgroundKind.Material ? Visibility.Visible : Visibility.Collapsed;
                    BackButton.Visibility = show == BackgroundKind.Material && _showDetailHeader ? Visibility.Visible : Visibility.Collapsed;
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
                        button.StartAnimation("Opacity", fadeOut);
                    }
                }
                else if (show == BackgroundKind.Material)
                {
                    material.StartAnimation("Opacity", fadeIn);
                    bread.StartAnimation("Opacity", fadeIn);
                    button.StartAnimation("Opacity", fadeIn);

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
                        button.StartAnimation("Opacity", fadeOut);
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

            DetailHeaderPresenter2 = GetTemplateChild(nameof(DetailHeaderPresenter2)) as Grid;
            DetailPresenter = GetTemplateChild(nameof(DetailPresenter)) as Grid;
            DetailHeaderPresenter = GetTemplateChild(nameof(DetailHeaderPresenter)) as BreadcrumbBar;
            BackButton = GetTemplateChild(nameof(BackButton)) as BackButton;
            DetailHeaderBackground = GetTemplateChild(nameof(DetailHeaderBackground)) as Border;
            DetailAction = GetTemplateChild(nameof(DetailAction)) as ContentControl;
            BackgroundPart = GetTemplateChild(nameof(BackgroundPart)) as ChatBackgroundControl;
            BorderPart = GetTemplateChild(nameof(BorderPart)) as Border;
            MaterialPart = GetTemplateChild(nameof(MaterialPart)) as Border;
            GoBackIcon = GetTemplateChild(nameof(GoBackIcon)) as Border;
            GoBackGlyph = GetTemplateChild(nameof(GoBackGlyph)) as Border;
            AdaptivePanel = GetTemplateChild(nameof(AdaptivePanel)) as MasterDetailPanel;
            AdaptivePanel.ViewStateChanged += OnViewStateChanged;
            AdaptivePanel.HasMaster = HasMaster;

            DetailHeaderPresenter.ItemsSource = _backStack;
            DetailHeaderPresenter.ItemClicked += DetailHeaderPresenter_ItemClicked;

            BackgroundPart.Update(ViewModel.ClientService, ViewModel.Aggregator);
            BackgroundPart.Visibility = _backgroundType == BackgroundKind.Background ? Visibility.Visible : Visibility.Collapsed;
            BorderPart.Visibility = _backgroundType != BackgroundKind.None ? Visibility.Visible : Visibility.Collapsed;
            MaterialPart.Visibility = _backgroundType == BackgroundKind.Material ? Visibility.Visible : Visibility.Collapsed;
            DetailHeaderPresenter.Visibility = _backgroundType == BackgroundKind.Material ? Visibility.Visible : Visibility.Collapsed;
            BackButton.Visibility = _backgroundType == BackgroundKind.Material && _showDetailHeader ? Visibility.Visible : Visibility.Collapsed;

            InitializeGoBack();

            _templateApplied = true;

            ElementCompositionPreview.SetIsTranslationEnabled(DetailAction, true);
            ElementCompositionPreview.SetIsTranslationEnabled(BackButton, true);

            var detailVisual = ElementComposition.GetElementVisual(DetailPresenter);
            detailVisual.Clip = detailVisual.Compositor.CreateInsetClip();

            var detailVisual2 = ElementComposition.GetElementVisual(DetailHeaderPresenter2);
            detailVisual2.Clip = detailVisual.Compositor.CreateInsetClip();

            var visual1 = ElementComposition.GetElementVisual(DetailHeaderBackground);
            var visual2 = ElementComposition.GetElementVisual(DetailHeaderPresenter);
            var visual4 = ElementComposition.GetElementVisual(BackButton);

            visual4.CenterPoint = new Vector3(24, 16, 0);
            visual2.CenterPoint = new Vector3(0, -20, 0);
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
                    DetailPresenter.Children.Add(DetailFrame);

                    if (DetailFrame.Content is Page page)
                    {
                        OnNavigated(null, new NavigatedEventArgs
                        {
                            Content = page
                        });
                    }

                    if (HasMaster)
                    {
                        if (DetailFrame.CurrentSourcePageType == null)
                        {
                            DetailFrame.Navigate(BlankPageType);
                        }
                        else
                        {
                            NavigationService.InsertToBackStack(0, BlankPageType);
                        }
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

        private void OnNavigated(object sender, NavigatedEventArgs e)
        {
            // OnNavigated is then manually invoked in OnApplyTemplate
            if (!_templateApplied)
            {
                return;
            }

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
                        _backStack.ReplaceWith(BuildBackStack(hosted.NavigationMode == HostedNavigationMode.Root || (hosted.NavigationMode == HostedNavigationMode.RootWhenParameterless && e.Parameter == null)));
                    }

                    if (e.NavigationMode == NavigationMode.Back)
                    {
                        SetScrollingHost(_showDetailHeader, e.VerticalOffset);
                    }
                    else
                    {
                        SetScrollingHost(_showDetailHeader, 0);
                    }

                    ShowHideDetailHeader(true, hosted.ShowHeaderBackground);

                    //if (AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged))
                    //{
                    //    VisualUtilities.QueueCallbackForCompositionRendering(() => DetailHeaderPresenter.Focus(FocusState.Keyboard));
                    //}
                }
                else
                {
                    _backStack.Clear();
                    ShowHideDetailHeader(false, false);

                    SetParent(hosted);
                }
            }
            else
            {
                DetailHeader = null;
                DetailFooter = null;

                _backStack.Clear();
                ShowHideDetailHeader(false, false);
            }

            if (AdaptivePanel == null)
            {
                return;
            }

            OnViewStateChanged();
        }

        private bool _showDetailHeader = true;

        private void ShowHideDetailHeader(bool show, bool showBackground)
        {
            var detailVisual = ElementComposition.GetElementVisual(DetailPresenter);
            detailVisual.Clip = detailVisual.Compositor.CreateInsetClip();

            if (detailVisual.Clip is InsetClip clip)
            {
                clip.TopInset = show ? 2 : 0;
            }

            _showDetailHeader = show;

            BackButton.Visibility = show
                ? Visibility.Visible
                : Visibility.Collapsed;

            DetailHeaderBackground.Visibility = show && showBackground
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private CompositionPropertySet _properties;

        private void InitializeScrollingHostAnimation()
        {
            var visual1 = ElementComposition.GetElementVisual(DetailHeaderBackground);
            var visual2 = ElementComposition.GetElementVisual(DetailHeaderPresenter);
            var visual3 = ElementComposition.GetElementVisual(DetailAction);
            var visual4 = ElementComposition.GetElementVisual(BackButton);

            // min out: 0.583
            // max out: 1

            // min in:  1
            // max in:  1.714

            _properties = visual1.Compositor.CreatePropertySet();
            _properties.InsertVector3("Translation", new Vector3(0, 32, 0));

            var properties = _properties;

            var expOut = "clamp(1 - ((-scrollViewer.Translation.Y / 32) * 0.417), 0.583, 1)";
            var slideOut = visual1.Compositor.CreateExpressionAnimation($"vector3({expOut}, {expOut}, 1)");
            slideOut.SetReferenceParameter("scrollViewer", properties);

            var expOut2 = "clamp(1 - ((-scrollViewer.Translation.Y / 32) * 0.2), 0.8, 1)";
            var slideOut2 = visual1.Compositor.CreateExpressionAnimation($"vector3({expOut2}, {expOut2}, 1)");
            slideOut2.SetReferenceParameter("scrollViewer", properties);

            var expOut3 = "-clamp(((-scrollViewer.Translation.Y / 32) * 16), 0, 16)";
            var slideOut3 = visual1.Compositor.CreateExpressionAnimation(expOut3);
            slideOut3.SetReferenceParameter("scrollViewer", properties);

            var expIn = "clamp(1.357 - ((-scrollViewer.Translation.Y / 32) * 0.357), 1, 1.357)";
            var slideIn = visual1.Compositor.CreateExpressionAnimation($"vector3({expIn}, {expIn}, 1)");
            slideIn.SetReferenceParameter("scrollViewer", properties);

            visual1.StartAnimation("Scale", slideIn);
            visual2.StartAnimation("Scale", slideOut);
            visual4.StartAnimation("Scale", slideOut2);
            visual3.StartAnimation("Translation.Y", slideOut3);
            visual4.StartAnimation("Translation.Y", slideOut3);

            var fadeIn = visual1.Compositor.CreateExpressionAnimation("scrollViewer.Translation.Y < -16 ? -(scrollViewer.Translation.Y + 16) / 16 : 0");
            fadeIn.SetReferenceParameter("scrollViewer", properties);

            visual1.StartAnimation("Opacity", fadeIn);
        }

        private void SetScrollingHost(bool animate, double offset)
        {
            if (_properties == null)
            {
                InitializeScrollingHostAnimation();
            }

            if (animate)
            {
                var batch = _properties.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
                batch.Completed += (s, args) =>
                {
                    SetScrollingHost();
                };

                var animation = _properties.Compositor.CreateScalarKeyFrameAnimation();
                animation.InsertKeyFrame(1, -Math.Min((float)offset, 32));
                animation.Duration = Constants.FastAnimation;

                _properties.StartAnimation("Translation.Y", animation);

                batch.End();
            }
            else
            {
                SetScrollingHost();
            }
        }

        private void SetScrollingHost()
        {
            var hosted = DetailFrame?.Content as HostedPage;

            var scrollingHost = hosted?.FindName("ScrollingHost");
            if (scrollingHost is ListViewBase listView)
            {
                var scrollViewer = listView.GetScrollViewer();
                if (scrollViewer == null)
                {
                    listView.Loaded += SetScrollingHost;
                }
                else
                {
                    SetScrollingHost(scrollViewer);
                }
            }
            else if (scrollingHost is ScrollViewer scroll)
            {
                SetScrollingHost(scroll);
            }
            else
            {
                SetParent(hosted);
            }
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

            var properties = ElementCompositionPreview.GetScrollViewerManipulationPropertySet(scroller);
            var animation = _properties.Compositor.CreateExpressionAnimation("scrollViewer.Translation");
            animation.SetReferenceParameter("scrollViewer", properties);

            _properties.StartAnimation("Translation", animation);

            SetParent(scroller.ContentTemplateRoot);
        }

        private UIElement _lastParent;

        private void SetParent(UIElement parent)
        {
            return;

            if (_lastParent != null)
            {
                ElementCompositionPreview.SetElementChildVisual(_lastParent, null);
            }

            if (parent is ChatPage or null)
            {
                return;
            }

            //if (parent is ChatPage page && !SettingsService.Current.SwipeToShare)
            //{
            //    var view = page.GetChild<ChatHistoryView>();
            //    var scroll = view.GetScrollViewer();
            //    parent = scroll.ContentTemplateRoot;
            //}

            ElementCompositionPreview.SetElementChildVisual(_lastParent = parent, _container);
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
                    _backStack.ReplaceWith(BuildBackStack(hosted.NavigationMode == HostedNavigationMode.Root || (hosted.NavigationMode == HostedNavigationMode.RootWhenParameterless && NavigationService.CurrentPageParam == null)));
                }
            }
        }

        private void OnBackStackChanged(object sender, EventArgs e)
        {
            if (DetailFrame.Content is HostedPage hosted)
            {
                _backStack.ReplaceWith(BuildBackStack(hosted.NavigationMode == HostedNavigationMode.Root || (hosted.NavigationMode == HostedNavigationMode.RootWhenParameterless && NavigationService.CurrentPageParam == null)));
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
                yield return _currentPage;
                yield break;
            }

            var index = NavigationService.BackStack.FindLastIndex(x => x.Mode != HostedNavigationMode.Child);
            var k = Math.Max(index, 0);

            for (int i = k; i < NavigationService.BackStack.Count; i++)
            {
                var item = NavigationService.BackStack[i];
                if (item.Title != null)
                {
                    yield return item;
                }
            }

            yield return _currentPage;
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
            }

            if (_prevState != CurrentState)
            {
                _prevState = CurrentState;
                ViewStateChanged?.Invoke(this, EventArgs.Empty);
            }

            UpdateMasterVisibility();
        }

        private MasterDetailState _prevState;

        private bool _isMinimal = false;
        private bool IsMinimal =>
            AdaptivePanel?.CurrentState == MasterDetailState.Minimal;

        #region Icon

        // This should be held in memory, or animation will stop
        private CompositionPropertySet _props;

        private IAnimatedVisual _previous;
        private IAnimatedVisualSource2 _source;

        private void InitializeGoBack()
        {
            return;

            var visual = GetVisual(BootStrapper.Current.Compositor, out var source, out _props);
            var container = visual.RootVisual.Compositor.CreateContainerVisual();

            _source = source;
            _previous = visual;

            _previous.RootVisual.Scale = new Vector3(72f / 512f, 72f / 512f, 1);
            _previous.RootVisual.RotationAngleInDegrees = -90;
            _previous.RootVisual.Offset = new Vector3(0, 72, 0);
            _previous.RootVisual.CenterPoint = new Vector3(0);

            ElementCompositionPreview.SetIsTranslationEnabled(GoBackIcon, true);
            ElementCompositionPreview.SetElementChildVisual(GoBackIcon, visual.RootVisual);

            ElementCompositionPreview.SetIsTranslationEnabled(GoBackGlyph, true);
        }

        private IAnimatedVisual GetVisual(Compositor compositor, out IAnimatedVisualSource2 source, out CompositionPropertySet properties)
        {
            source = new UserAvatarMask();

            if (source == null)
            {
                properties = null;
                return null;
            }

            var visual = source.TryCreateAnimatedVisual(compositor, out _);
            if (visual == null)
            {
                properties = null;
                return null;
            }

            properties = compositor.CreatePropertySet();
            properties.InsertScalar("Progress", 1.0F);

            var progressAnimation = compositor.CreateExpressionAnimation("_.Progress");
            progressAnimation.SetReferenceParameter("_", properties);
            visual.RootVisual.Properties.InsertScalar("Progress", 1.0F);
            visual.RootVisual.Properties.StartAnimation("Progress", progressAnimation);

            return visual;
        }

        #endregion

        #region Swipe

        public bool CanGoNext { get; set; } = true;
        public bool CanGoPrev { get; set; } = true;

        private SpriteVisual _hitTest;
        private ContainerVisual _container;
        private Visual _visual;
        private ContainerVisual _indicator;

        private bool _hasInitialLoadedEventFired;
        private WeakInteractionTrackerOwner _trackerOwner;
        private InteractionTracker _tracker;
        private VisualInteractionSource _interactionSource;

        private void OnLoaded2(object sender, RoutedEventArgs e)
        {
            return;

            if (!_hasInitialLoadedEventFired)
            {
                _hasInitialLoadedEventFired = true;

                _visual = ElementCompositionPreview.GetElementVisual(DetailPresenter);

                _hitTest = _visual.Compositor.CreateSpriteVisual();
                _hitTest.Brush = _visual.Compositor.CreateColorBrush(Colors.Transparent);
                _hitTest.RelativeSizeAdjustment = Vector2.One;

                _container = _visual.Compositor.CreateContainerVisual();
                _container.Children.InsertAtBottom(_hitTest);
                _container.RelativeSizeAdjustment = Vector2.One;

                ConfigureInteractionTracker();
                ConfigureAnimations(null);
            }

            if (_trackerOwner != null)
            {
                _trackerOwner.ValuesChanged += OnValuesChanged;
                _trackerOwner.InertiaStateEntered += OnInertiaStateEntered;
                _trackerOwner.InteractingStateEntered += OnInteractingStateEntered;
                _trackerOwner.IdleStateEntered += OnIdleStateEntered;
                _trackerOwner.CustomAnimationStateEntered += OnCustomAnimationStateEntered;
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_trackerOwner != null)
            {
                _trackerOwner.ValuesChanged -= OnValuesChanged;
                _trackerOwner.InertiaStateEntered -= OnInertiaStateEntered;
                _trackerOwner.InteractingStateEntered -= OnInteractingStateEntered;
                _trackerOwner.IdleStateEntered -= OnIdleStateEntered;
                _trackerOwner.CustomAnimationStateEntered -= OnCustomAnimationStateEntered;
            }
        }

        private void ConfigureInteractionTracker()
        {
            _interactionSource = VisualInteractionSource.Create(_hitTest);

            //Configure for x-direction panning
            _interactionSource.ManipulationRedirectionMode = VisualInteractionSourceRedirectionMode.CapableTouchpadOnly;
            _interactionSource.PositionXSourceMode = InteractionSourceMode.EnabledWithInertia;
            _interactionSource.PositionXChainingMode = InteractionChainingMode.Never;
            _interactionSource.IsPositionXRailsEnabled = true;

            _trackerOwner = new WeakInteractionTrackerOwner();

            //Create tracker and associate interaction source
            _tracker = InteractionTracker.CreateWithOwner(_visual.Compositor, _trackerOwner);
            _tracker.InteractionSources.Add(_interactionSource);

            _tracker.MaxPosition = new Vector3(72);
            _tracker.MinPosition = new Vector3(-72);

            _tracker.Properties.InsertBoolean("FromAnimation", false);
            _tracker.Properties.InsertBoolean("CanGoNext", CanGoNext);
            _tracker.Properties.InsertBoolean("CanGoPrev", CanGoPrev);

            //ConfigureAnimations(_visual, null);
            ConfigureRestingPoints();
        }

        private void ConfigureRestingPoints()
        {
            var neutralX = InteractionTrackerInertiaRestingValue.Create(_visual.Compositor);
            neutralX.Condition = _visual.Compositor.CreateExpressionAnimation("true");
            neutralX.RestingValue = _visual.Compositor.CreateExpressionAnimation("0");

            _tracker.ConfigurePositionXInertiaModifiers(new InteractionTrackerInertiaModifier[] { neutralX });
        }

        public void ConfigureAnimations(InteractionTracker tracker)
        {
            ConfigureAnimations(tracker, false);
        }

        private void ConfigureAnimations(InteractionTracker tracker, bool interacting)
        {
            if (tracker == null)
            {
                tracker = _tracker;

                if (interacting)
                {
                    _tracker.Properties.InsertBoolean("FromAnimation", /*_fromAnimation =*/ false);
                }

                _tracker.Properties.InsertBoolean("CanGoNext", CanGoNext);
                _tracker.Properties.InsertBoolean("CanGoPrev", CanGoPrev);
                _tracker.MaxPosition = new Vector3(CanGoNext ? 72 : 0);
                _tracker.MinPosition = new Vector3(CanGoPrev ? -72 : 0);
            }

            // This should be enough: tracker.FromAnimation ? -tracker.Position.X : Clamp(-tracker.Position.X, -72, 72)
            var offsetExp = _visual.Compositor.CreateExpressionAnimation("1 - (Clamp(-tracker.Position.X, -72, 72) / 72)");
            offsetExp.SetReferenceParameter("tracker", tracker);

            _props.StartAnimation("Progress", offsetExp);

            var progressExp = _visual.Compositor.CreateExpressionAnimation("(Clamp(-tracker.Position.X, -72, 72) / 72)");
            progressExp.SetReferenceParameter("tracker", tracker);

            var translateExp = _visual.Compositor.CreateExpressionAnimation("-(1 - (Clamp(-tracker.Position.X, -72, 72) / 72)) * 42");
            translateExp.SetReferenceParameter("tracker", tracker);

            var visual = ElementCompositionPreview.GetElementVisual(GoBackGlyph);

            visual.StartAnimation("Translation.X", translateExp);
            visual.StartAnimation("Scale.X", progressExp);
            visual.StartAnimation("Scale.Y", progressExp);
            visual.CenterPoint = new Vector3(21, 21, 0);
        }

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType != PointerDeviceType.Mouse)
            {
                try
                {
                    _interactionSource.TryRedirectForManipulation(e.GetCurrentPoint(this));
                }
                catch { }
            }
        }

        private void OnValuesChanged(InteractionTracker sender, InteractionTrackerValuesChangedArgs args)
        {
            return;

            if (_indicator == null && (_tracker.Position.X > 0.0001f || _tracker.Position.X < -0.0001f) /*&& Math.Abs(e.Cumulative.Translation.X) >= 45*/)
            {
                var sprite = _visual.Compositor.CreateSpriteVisual();
                sprite.Size = new Vector2(30, 30);
                sprite.CenterPoint = new Vector3(15);

                var surface = LoadedImageSurface.StartLoadFromUri(new Uri("ms-appx:///Assets/Images/ArrowLeft.png"));
                void handler(LoadedImageSurface s, LoadedImageSourceLoadCompletedEventArgs args)
                {
                    s.LoadCompleted -= handler;
                    sprite.Brush = _visual.Compositor.CreateSurfaceBrush(s);
                }

                surface.LoadCompleted += handler;

                var ellipse = _visual.Compositor.CreateEllipseGeometry();
                ellipse.Radius = new Vector2(15);

                var ellipseShape = _visual.Compositor.CreateSpriteShape(ellipse);
                ellipseShape.FillBrush = _visual.Compositor.CreateColorBrush((Windows.UI.Color)App.Current.Resources["MessageServiceBackgroundColor"]);
                ellipseShape.Offset = new Vector2(15);

                var shape = _visual.Compositor.CreateShapeVisual();
                shape.Shapes.Add(ellipseShape);
                shape.Size = new Vector2(30, 30);

                _indicator = _visual.Compositor.CreateContainerVisual();
                _indicator.Children.InsertAtBottom(shape);
                _indicator.Children.InsertAtTop(sprite);
                _indicator.Size = new Vector2(30, 30);
                _indicator.CenterPoint = new Vector3(15);
                _indicator.Scale = new Vector3();

                _container.Children.InsertAtTop(_indicator);
            }

            var offset = (_tracker.Position.X > 0 && !CanGoNext) || (_tracker.Position.X <= 0 && !CanGoPrev) ? 0 : Math.Clamp(Math.Abs(_tracker.Position.X), 0, 72);

            var abs = Math.Abs(offset);
            var percent = /*_fromAnimation ? 0 :*/ abs / 72f;

            var width = ActualSize.X;
            var height = ActualSize.Y;

            if (_indicator != null)
            {
                _indicator.Offset = new Vector3(_tracker.Position.X > 0 ? width - percent * 60 : -30 + percent * 55, (height - 30) / 2, 0);
                _indicator.Scale = new Vector3(_tracker.Position.X > 0 ? 0.8f + percent * 0.2f : -(0.8f + percent * 0.2f), 0.8f + percent * 0.2f, 1);
                _indicator.Opacity = percent;
            }
        }

        private void OnInertiaStateEntered(InteractionTracker sender, InteractionTrackerInertiaStateEnteredArgs args)
        {
            var position = _tracker.Position;
            if (position.X >= 72 && CanGoNext || position.X <= -72 && CanGoPrev)
            {
                //sender.TryUpdatePosition(sender.Position);

                var direction = position.X <= -72 && CanGoPrev
                    ? CarouselDirection.Previous
                    : CarouselDirection.Next;

                //ChangeView(direction, null);

                if (direction == CarouselDirection.Previous)
                {
                    NavigationService.GoBack();
                }
                else
                {
                    NavigationService.GoForward();
                }
            }
        }

        private void OnIdleStateEntered(InteractionTracker sender, InteractionTrackerIdleStateEnteredArgs args)
        {
            ConfigureAnimations(null, false);
        }

        private void OnInteractingStateEntered(InteractionTracker sender, InteractionTrackerInteractingStateEnteredArgs args)
        {
            ConfigureAnimations(null, true);
        }

        private void OnCustomAnimationStateEntered(InteractionTracker sender, InteractionTrackerCustomAnimationStateEnteredArgs args)
        {
            _tracker.Properties.InsertBoolean("FromAnimation", /*_fromAnimation =*/ true);
        }

        #endregion

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
            get => (UIElement)GetValue(BannerProperty);
            set => SetValue(BannerProperty, value);
        }

        public static readonly DependencyProperty BannerProperty =
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

        public event EventHandler MasterVisibilityChanged;

        public Visibility MasterVisibility
        {
            get { return (Visibility)GetValue(MasterVisibilityProperty); }
            set { SetValue(MasterVisibilityProperty, value); }
        }

        public static readonly DependencyProperty MasterVisibilityProperty =
            DependencyProperty.Register("MasterVisibility", typeof(Visibility), typeof(MasterDetailView), new PropertyMetadata(Visibility.Visible, OnMasterVisibilityChanged));

        private static void OnMasterVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((MasterDetailView)d).MasterVisibilityChanged?.Invoke(d, EventArgs.Empty);
        }

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
