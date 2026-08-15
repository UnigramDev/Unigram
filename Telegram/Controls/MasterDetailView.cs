//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Numerics;
using Telegram.Common;
using Telegram.Composition;
using Telegram.Controls.Chats;
using Telegram.Controls.Media;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Views;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Composition.Interactions;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Controls
{
    public enum BackgroundKind
    {
        None,
        Material,
        Background
    }

    public sealed partial class MasterDetailView : ContentControl, INotifyPropertyChanged, IDisposable
    {
        private MasterDetailPanel AdaptivePanel;
        private ContentControl MasterFrame;
        private Frame DetailFrame;
        private Grid DetailHeaderPresenter2;
        private Grid DetailRoot;
        private Grid DetailPresenter;
        private TextBlock DetailHeaderPresenter;
        private BackButton BackButton;
        private Border DetailHeaderBackground;
        private ChatBackgroundControl BackgroundPart;
        private ContentControl DetailAction;
        private ContentControl BannerPresenter;
        private Border BorderPart;
        private Border MaterialPart;

        public ViewModelBase ViewModel { get; private set; }
        public NavigationService NavigationService { get; private set; }
        public Frame ParentFrame { get; private set; }

        private long _titleToken;

        private bool _templateApplied;

        public MasterDetailView()
        {
            DefaultStyleKey = typeof(MasterDetailView);

            Loaded += OnLoaded;
        }

        #region Initialize

        public void Initialize(string key, Frame parent, ViewModelBase viewModel)
        {
            var service = WindowContext.Current.NavigationServices.GetByFrameId(key + viewModel.Session.Id) as NavigationService;
            if (service == null)
            {
                service = BootStrapper.Current.NavigationServiceFactory(viewModel.Session, viewModel.NavigationService.Window, BootStrapper.BackButton.Ignore, key + viewModel.Session.Id, false) as NavigationService;
                service.Frame.DataContext = new object();
                service.Frame.CacheSize = 5;
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

            if (_backTrackerOwner != null)
            {
                _backTrackerOwner.InteractingStateEntered -= OnBackInteractingStateEntered;
                _backTrackerOwner.InertiaStateEntered -= OnBackInertiaStateEntered;
                _backTrackerOwner.IdleStateEntered -= OnBackIdleStateEntered;
            }

            DetachBackGesture(null);
            DetachBackGestureContent();

            if (DetailFrame?.Content is HostedPage hosted)
            {
                hosted.UnregisterPropertyChangedCallback(HostedPage.TitleProperty, _titleToken);

                var scrollingHost = hosted.FindName("ScrollingHost");
                if (scrollingHost is ListViewBase list)
                {
                    list.Loaded -= SetScrollingHost;
                }
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
                NavigationTransitionInfo transitionInfoOverride = null;
                if (CurrentState == MasterDetailState.Minimal && DetailFrame.BackStackDepth == 1)
                {
                    transitionInfoOverride = new SuppressNavigationTransitionInfo();
                }

                DetailFrame.GoBack(transitionInfoOverride);
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
            DetailRoot = GetTemplateChild(nameof(DetailRoot)) as Grid;
            DetailPresenter = GetTemplateChild(nameof(DetailPresenter)) as Grid;
            DetailHeaderPresenter = GetTemplateChild(nameof(DetailHeaderPresenter)) as TextBlock;
            BackButton = GetTemplateChild(nameof(BackButton)) as BackButton;
            DetailHeaderBackground = GetTemplateChild(nameof(DetailHeaderBackground)) as Border;
            DetailAction = GetTemplateChild(nameof(DetailAction)) as ContentControl;
            BannerPresenter = GetTemplateChild(nameof(BannerPresenter)) as ContentControl;
            BackgroundPart = GetTemplateChild(nameof(BackgroundPart)) as ChatBackgroundControl;
            BorderPart = GetTemplateChild(nameof(BorderPart)) as Border;
            MaterialPart = GetTemplateChild(nameof(MaterialPart)) as Border;
            MasterFrame = GetTemplateChild(nameof(MasterFrame)) as ContentControl;
            AdaptivePanel = GetTemplateChild(nameof(AdaptivePanel)) as MasterDetailPanel;
            AdaptivePanel.ViewStateChanged += OnViewStateChanged;
            AdaptivePanel.HasMaster = HasMaster;

            BannerPresenter.Visibility = _bannerCollapsed
                ? Visibility.Collapsed
                : Visibility.Visible;

            BackgroundPart.SizeChanged += BackgroundPart_SizeChanged;
            BackgroundPart.Update(ViewModel.ClientService, ViewModel.Aggregator);
            BackgroundPart.Visibility = _backgroundType == BackgroundKind.Background ? Visibility.Visible : Visibility.Collapsed;
            BorderPart.Visibility = _backgroundType != BackgroundKind.None ? Visibility.Visible : Visibility.Collapsed;
            MaterialPart.Visibility = _backgroundType == BackgroundKind.Material ? Visibility.Visible : Visibility.Collapsed;
            DetailHeaderPresenter.Visibility = _backgroundType == BackgroundKind.Material ? Visibility.Visible : Visibility.Collapsed;
            BackButton.Visibility = _backgroundType == BackgroundKind.Material && _showDetailHeader ? Visibility.Visible : Visibility.Collapsed;

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

                    if (HasMaster && !NavigationService.IsNavigating)
                    {
                        if (NavigationService.CurrentPageType == null)
                        {
                            NavigationService.Navigate(BlankPageType);
                        }
                        else
                        {
                            NavigationService.InsertToBackStack(0, BlankPageType);
                        }
                    }
                }
                catch { }
            }

            ConfigureBackGesture();

            if (ActualWidth > 0 && CurrentState != MasterDetailState.Minimal)
            {
                OnViewStateChanged();
            }
        }

        private bool _bannerCollapsed = true;

        public event EventHandler BannerCollapsed;

        public void ShowHideBanner(bool show)
        {
            if (_bannerCollapsed != show)
            {
                return;
            }

            if (BannerPresenter == null)
            {
                _bannerCollapsed = !show;
                return;
            }

            _bannerCollapsed = !show;
            BannerPresenter.Visibility = Visibility.Visible;

            var banner = ElementComposition.GetElementVisual(BannerPresenter);
            var detail = ElementComposition.GetElementVisual(DetailRoot);
            var master = ElementComposition.GetElementVisual(MasterFrame);

            ElementCompositionPreview.SetIsTranslationEnabled(BannerPresenter, true);
            ElementCompositionPreview.SetIsTranslationEnabled(DetailRoot, true);
            ElementCompositionPreview.SetIsTranslationEnabled(MasterFrame, true);

            var batch = banner.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                if (DetailFrame?.Content is IChatPage chatPage)
                {
                    chatPage.CompleteBannerAnimation();
                }

                detail.Properties.InsertVector3("Translation", Vector3.Zero);
                master.Properties.InsertVector3("Translation", Vector3.Zero);

                DetailRoot.Margin = new Thickness(0);
                MasterFrame.Margin = new Thickness(0);

                if (_bannerCollapsed)
                {
                    BannerPresenter.Visibility = Visibility.Collapsed;
                    BannerCollapsed?.Invoke(this, EventArgs.Empty);
                }
            };

            var translate = banner.Compositor.CreateScalarKeyFrameAnimation();
            translate.InsertKeyFrame(show ? 0 : 1, -40);
            translate.InsertKeyFrame(show ? 1 : 0, 0);
            translate.Duration = Constants.FastAnimation;

            banner.StartAnimation("Translation.Y", translate);

            if (CurrentState == MasterDetailState.Minimal && DetailFrame?.CurrentSourcePageType == BlankPageType)
            {
                translate.InsertKeyFrame(show ? 0 : 1, -48);

                MasterFrame.Margin = new Thickness(0, 0, 0, -48);
                master.StartAnimation("Translation.Y", translate);
            }
            else
            {
                var detailVisual = ElementComposition.GetElementVisual(DetailPresenter);
                detailVisual.Clip = detailVisual.Compositor.CreateInsetClip(0, -40, 0, 0);

                if (DetailFrame.Content is IChatPage chatPage)
                {
                    chatPage.StartBannerAnimation(translate);
                }
                else
                {
                    DetailRoot.Margin = new Thickness(0, 0, 0, -40);
                    detail.StartAnimation("Translation.Y", translate);
                }
            }

            batch.End();
        }

        private void BackgroundPart_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var newSize = e.NewSize.ToVector2();
            var visual = ElementComposition.GetElementVisual(BackgroundPart);
            var geometry = visual.Compositor.CreateRoundedRectangleGeometry();
            geometry.Size = new Vector2(newSize.X + 9, newSize.Y + 9);
            geometry.CornerRadius = new Vector2(9);
            visual.Clip = visual.Compositor.CreateGeometricClip(geometry);
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

            if (HasMaster && !NavigationService.CanGoBack && NavigationService.CurrentPageType != BlankPageType)
            {
                NavigationService.InsertToBackStack(0, BlankPageType);
            }

            // The container that was driving the chip may have gone with the page, in which case
            // its tracker never reaches idle and nothing else would release the binding.
            DetachBackGesture(null);
            ConfigureBackGesture();
            ConfigureBackGestureContent(e.Content);

            if (e.Content is HostedPage hosted)
            {
                DetailFooter = hosted.Action;

                if (hosted.ShowHeader)
                {
                    _titleToken = hosted.RegisterPropertyChangedCallback(HostedPage.TitleProperty, OnTitleChanged);

                    if (string.IsNullOrEmpty(hosted.Title))
                    {
                        DetailHeaderPresenter.Text = string.Empty;
                    }
                    else
                    {
                        DetailHeaderPresenter.Text = hosted.Title;
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
                    DetailHeaderPresenter.Text = string.Empty;
                    ShowHideDetailHeader(false, false);
                }
            }
            else
            {
                DetailHeader = null;
                DetailFooter = null;

                DetailHeaderPresenter.Text = string.Empty;
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
        }

        private void OnTitleChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (sender is HostedPage hosted && !string.IsNullOrEmpty(hosted.Title))
            {
                DetailHeaderPresenter.Text = hosted.Title;
            }
            else
            {
                DetailHeaderPresenter.Text = string.Empty;
            }
        }

        private void OnBackStackChanged(object sender, EventArgs e)
        {
            if (DetailFrame.Content is HostedPage hosted && hosted.ShowHeader && !string.IsNullOrEmpty(hosted.Title))
            {
                DetailHeaderPresenter.Text = hosted.Title;
            }
            else
            {
                DetailHeaderPresenter.Text = string.Empty;
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
            }

            if (_prevState != CurrentState)
            {
                _prevState = CurrentState;
                ViewStateChanged?.Invoke(this, EventArgs.Empty);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentState)));
            }

            UpdateMasterVisibility();
        }

        private MasterDetailState _prevState;

        private bool _isMinimal = false;
        private bool IsMinimal =>
            AdaptivePanel?.CurrentState == MasterDetailState.Minimal;

        #region Back gesture

        // Chrome's desktop back gesture: only the chip travels, the page stays where it is. That
        // is what makes this affordable here - the master is collapsed while a chat is open in
        // Minimal, so revealing it would force a measure of the whole chat list at the moment the
        // finger starts moving, and a Frame navigation cannot be scrubbed anyway.
        private const float BackGestureThreshold = 72;

        private const float BackIndicatorSize = 30;

        private VisualInteractionSource _backSource;
        private VisualInteractionSource _backContentSource;
        private ScrollViewer _backScrollingHost;
        private ScrollMode _backScrollingMode;
        private InteractionTracker _backTracker;
        private WeakInteractionTrackerOwner _backTrackerOwner;

        private ContainerVisual _backIndicator;
        private SpriteVisual _backIndicatorCircle;
        private Color _backIndicatorColor;
        private InteractionTracker _backDriver;

        /// <summary>
        /// Creates, then enables or disables, the source carrying the gesture everywhere the detail
        /// is not a message bubble: the chat header, the composer, profile and settings pages.
        /// </summary>
        private void ConfigureBackGesture()
        {
            var enabled = SettingsService.Current.SwipeToGoBack;

            if (_backTracker == null)
            {
                if (!enabled || DetailRoot == null)
                {
                    return;
                }

                var visual = ElementComposition.GetElementVisual(DetailRoot);
                var compositor = visual.Compositor;

                _backSource = VisualInteractionSource.Create(visual);
                _backSource.ManipulationRedirectionMode = VisualInteractionSourceRedirectionMode.CapableTouchpadOnly;
                _backSource.PositionXSourceMode = InteractionSourceMode.EnabledWithInertia;
                _backSource.PositionXChainingMode = InteractionChainingMode.Never;
                _backSource.IsPositionXRailsEnabled = true;

                _backTrackerOwner = new WeakInteractionTrackerOwner();
                _backTrackerOwner.InteractingStateEntered += OnBackInteractingStateEntered;
                _backTrackerOwner.InertiaStateEntered += OnBackInertiaStateEntered;
                _backTrackerOwner.IdleStateEntered += OnBackIdleStateEntered;

                _backTracker = InteractionTracker.CreateWithOwner(compositor, _backTrackerOwner);
                _backTracker.InteractionSources.Add(_backSource);

                // Only the back direction travels, and it runs negative to match the sign
                // MessageSelector's tracker already uses for a left-to-right drag.
                _backTracker.MaxPosition = Vector3.Zero;
                _backTracker.MinPosition = new Vector3(-BackGestureThreshold, 0, 0);

                var neutralX = InteractionTrackerInertiaRestingValue.Create(compositor);
                neutralX.Condition = compositor.CreateExpressionAnimation("true");
                neutralX.RestingValue = compositor.CreateExpressionAnimation("0");

                // A List, not an array: InteractionTrackerInertiaModifier is a WinRT runtimeclass,
                // and an array of one boxes through IReferenceArray, which NativeAOT cannot synthesise.
                _backTracker.ConfigurePositionXInertiaModifiers(new List<InteractionTrackerInertiaModifier> { neutralX });
            }

            // Re-read on every navigation, so switching the setting off takes effect at once.
            _backSource.PositionXSourceMode = enabled
                ? InteractionSourceMode.EnabledWithInertia
                : InteractionSourceMode.Disabled;
        }

        /// <summary>
        /// Adds a second source inside the page's scrolling host, if it has one.
        /// </summary>
        /// <remarks>
        /// The source on DetailRoot is an ancestor of the page, and a scroller can take the
        /// touchpad pan before an ancestor sees it - a source on a descendant of the scroller wins
        /// the contact instead, which is why MessageSelector's works inside the chat history. Rails
        /// and an X-only mode leave vertical panning to the ScrollViewer, as they already do there.
        ///
        /// It also forces the host's VerticalScrollMode on, for the reason given below.
        /// </remarks>
        private void ConfigureBackGestureContent(object content)
        {
            // Drops the previous page's source first: this is the only place it can happen, since
            // OnNavigating is never subscribed and nothing else runs as a page is left.
            DetachBackGestureContent();

            if (_backTracker == null || !SettingsService.Current.SwipeToGoBack)
            {
                return;
            }

            // Only a ScrollViewer for now. A ListViewBase host would need its ItemsPanelRoot, which
            // does not exist until the list realises - and the one that matters, the chat history,
            // is already covered by MessageSelector.
            if (content is not HostedPage hosted
                || hosted.FindName("ScrollingHost") is not ScrollViewer scrollingHost
                || scrollingHost.Content is not UIElement child)
            {
                return;
            }

            _backContentSource = VisualInteractionSource.Create(ElementComposition.GetElementVisual(child));
            _backContentSource.ManipulationRedirectionMode = VisualInteractionSourceRedirectionMode.CapableTouchpadOnly;
            _backContentSource.PositionXSourceMode = InteractionSourceMode.EnabledWithInertia;
            _backContentSource.PositionXChainingMode = InteractionChainingMode.Never;
            _backContentSource.IsPositionXRailsEnabled = true;

            _backTracker.InteractionSources.Add(_backContentSource);

            // ScrollMode.Auto turns the manipulation off entirely when there is nothing to scroll,
            // and then a touchpad pan is never classified as one - so no interaction source is
            // consulted and the gesture is simply absent. That is the whole difference between the
            // settings pages that worked and the two that did not: identical markup, and only
            // whether the content happened to overflow. Enabled keeps it on regardless of extent,
            // which costs nothing here since there is still no extent to scroll.
            _backScrollingHost = scrollingHost;
            _backScrollingMode = scrollingHost.VerticalScrollMode;

            scrollingHost.VerticalScrollMode = ScrollMode.Enabled;
        }

        private void DetachBackGestureContent()
        {
            if (_backScrollingHost != null)
            {
                _backScrollingHost.VerticalScrollMode = _backScrollingMode;
                _backScrollingHost = null;
            }

            if (_backContentSource != null)
            {
                _backTracker?.InteractionSources.Remove(_backContentSource);
                _backContentSource = null;
            }
        }

        /// <summary>
        /// Points the chip at whichever tracker owns the gesture: MessageSelector's over a message
        /// bubble, this control's everywhere else. An ExpressionAnimation may reference a tracker
        /// from anywhere in the compositor, so the chip runs without per-frame work on this thread.
        /// </summary>
        public void AttachBackGesture(InteractionTracker tracker)
        {
            if (tracker == null || _backDriver == tracker || DetailRoot == null)
            {
                return;
            }

            if (DetailFrame is not { CanGoBack: true } || !SettingsService.Current.SwipeToGoBack)
            {
                return;
            }

            EnsureBackIndicator();
            _backDriver = tracker;

            var compositor = _backIndicator.Compositor;
            var root = ElementComposition.GetElementVisual(DetailRoot);

            // The tint is baked into the effect graph, so a theme change means a new brush rather
            // than a colour set on the old one. Once per gesture, and only when it has moved.
            var color = (Color)BootStrapper.Current.Resources["MessageServiceBackgroundColor"];
            if (_backIndicatorCircle.Brush == null || _backIndicatorColor != color)
            {
                _backIndicatorColor = color;
                _backIndicatorCircle.Brush = SolidGaussianBrush.CreateCircleBrush(compositor, BackIndicatorSize / 2, color);
            }

            var progress = $"clamp(-tracker.Position.X / {BackGestureThreshold}, 0, 1)";

            // ChatListListView's indicator, on the same numbers, since this is the same gesture
            // language: in from the edge over 55px, growing 0.8 to 1, fading the whole way.
            var offset = compositor.CreateExpressionAnimation(
                $"vector3(-{BackIndicatorSize} + {progress} * 55, (root.Size.Y - {BackIndicatorSize}) / 2, 0)");
            offset.SetReferenceParameter("tracker", tracker);
            offset.SetReferenceParameter("root", root);

            // Negative on X, which mirrors the arrow: ArrowLeft.png is drawn for the right-hand
            // edge, and ChatListListView flips it the same way for the indicator it brings in from
            // the left.
            var scaled = $"(0.8 + {progress} * 0.2)";
            var scale = compositor.CreateExpressionAnimation($"vector3(-{scaled}, {scaled}, 1)");
            scale.SetReferenceParameter("tracker", tracker);

            var opacity = compositor.CreateExpressionAnimation(progress);
            opacity.SetReferenceParameter("tracker", tracker);

            _backIndicator.StartAnimation("Offset", offset);
            _backIndicator.StartAnimation("Scale", scale);
            _backIndicator.StartAnimation("Opacity", opacity);
        }

        /// <summary>
        /// Releases the chip. Passing null releases it whatever it is bound to.
        /// </summary>
        public void DetachBackGesture(InteractionTracker tracker)
        {
            if (_backDriver == null || (tracker != null && _backDriver != tracker))
            {
                return;
            }

            _backDriver = null;

            // Left bound, the expressions would hold a recycled container's tracker alive, and the
            // indicator would sit at whatever progress the gesture happened to end on.
            _backIndicator.StopAnimation("Offset");
            _backIndicator.StopAnimation("Scale");
            _backIndicator.StopAnimation("Opacity");
            _backIndicator.Opacity = 0;
        }

        public void CommitBackGesture()
        {
            if (DetailFrame is { CanGoBack: true })
            {
                // FromRight, and it does slide in from the left: the frame inverts the effect on a
                // back navigation, so this is the same value TLNavigationService passes going
                // forward. The default entrance transition reads as unrelated to the finger that
                // asked for it.
                //
                // Gated on the policy because FrameFacade only applies it to Navigate, not GoBack,
                // so nothing else would stop this one from animating.
                NavigationService?.GoBack(null, PowerSavingPolicy.AreSmoothTransitionsEnabled
                    ? new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight }
                    : null);
            }
        }

        private void EnsureBackIndicator()
        {
            if (_backIndicator != null)
            {
                return;
            }

            var compositor = ElementComposition.GetElementVisual(DetailRoot).Compositor;

            var sprite = compositor.CreateSpriteVisual();
            sprite.Size = new Vector2(BackIndicatorSize);
            sprite.CenterPoint = new Vector3(BackIndicatorSize / 2);

            var surface = LoadedImageSurface.StartLoadFromUri(new Uri("ms-appx:///Assets/Images/ArrowLeft.png"));
            void handler(LoadedImageSurface s, LoadedImageSourceLoadCompletedEventArgs args)
            {
                s.LoadCompleted -= handler;
                sprite.Brush = compositor.CreateSurfaceBrush(s);
            }

            surface.LoadCompleted += handler;

            _backIndicatorCircle = compositor.CreateSpriteVisual();
            _backIndicatorCircle.Size = new Vector2(BackIndicatorSize);

            _backIndicator = compositor.CreateContainerVisual();
            _backIndicator.Children.InsertAtBottom(_backIndicatorCircle);
            _backIndicator.Children.InsertAtTop(sprite);
            _backIndicator.Size = new Vector2(BackIndicatorSize);
            _backIndicator.CenterPoint = new Vector3(BackIndicatorSize / 2);
            _backIndicator.Opacity = 0;

            // Drawn over the detail content, and clipped to it by the inset clip already on
            // DetailPresenter's parent, so the chip appears to come in from the edge.
            ElementComposition.SetElementChildVisual(DetailRoot, _backIndicator);
        }

        private void OnBackInteractingStateEntered(InteractionTracker sender, InteractionTrackerInteractingStateEnteredArgs args)
        {
            AttachBackGesture(sender);
        }

        private void OnBackInertiaStateEntered(InteractionTracker sender, InteractionTrackerInertiaStateEnteredArgs args)
        {
            if (sender.Position.X <= -BackGestureThreshold)
            {
                CommitBackGesture();
            }
        }

        private void OnBackIdleStateEntered(InteractionTracker sender, InteractionTrackerIdleStateEnteredArgs args)
        {
            DetachBackGesture(sender);
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
        public event PropertyChangedEventHandler PropertyChanged;

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
                AdaptivePanel?.AllowCompact = value;
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
