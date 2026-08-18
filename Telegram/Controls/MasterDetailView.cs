//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Numerics;
using Telegram.Common;
using Telegram.Composition;
using Telegram.Controls.Chats;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Views;
using Windows.Graphics.Display;
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
            DetachBackGestureHost();

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

            // Queued, never inline: see the remarks on ConfigureBackGestureHost.
            VisualUtilities.QueueCallbackForCompositionRendered(ConfigureBackGestureHost);

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
        // Chrome's numbers, from OverscrollConfig and OverscrollController::DispatchEventCompletesAction:
        // a touchpad pan commits at 30% of the display's larger edge, and the first 60 DIPs only serve
        // to recognise the gesture - nothing is drawn over them. It is measured against the display and
        // not the window, which is why the distance to go back feels the same maximized and in a small
        // window. Shared with MessageSelector, which clamps its own tracker to it whenever the back
        // direction is the one in play.
        private const float BackGestureCompletePercent = 0.3f;
        private const float BackGestureStartThreshold = 60;

        /// <summary>
        /// The larger edge of the display the window is on, in DIPs: Chrome's max_size.
        /// </summary>
        private static float BackGestureDisplayEdge
        {
            get
            {
                var display = DisplayInformation.GetForCurrentView();
                var scale = display.RawPixelsPerViewPixel;

                var width = display.ScreenWidthInRawPixels / scale;
                var height = display.ScreenHeightInRawPixels / scale;

                return (float)Math.Max(width, height);
            }
        }

        /// <summary>
        /// The pan that commits the navigation.
        /// </summary>
        public static float BackGestureThreshold => BackGestureDisplayEdge * BackGestureCompletePercent;

        /// <summary>
        /// How far the pan is allowed to travel at all: Chrome caps the overscroll delta at the
        /// display edge, and the pan between the threshold and here is what the chip's last 72px of
        /// rubber band are spread over.
        /// </summary>
        public static float BackGestureMaxPosition => BackGestureDisplayEdge;

        // How far the chip travels by the time the gesture commits, and how much further the rubber
        // band carries it: kAffordanceActivationOffset and kAffordanceExtraOffset.
        private const float BackIndicatorTravel = 146;
        private const float BackIndicatorExtraTravel = 72;

        // The affordance, to Chrome's measurements: a radius-20 circle carrying a 20px arrow over a
        // ripple that grows from the circle's radius to 40, and bursts to 48 on commit. The layer is
        // sized for the burst, so 96 square.
        private const float BackIndicatorBackgroundRadius = 20;
        private const float BackIndicatorRippleRadius = 40;
        private const float BackIndicatorBurstRadius = 48;
        // kArrowSize is 20, but that is the box of a vector icon drawn to its edges. ArrowLeft.png
        // carries padding - a 13x10 glyph in a 30 bitmap - so the sprite is its native size, which
        // draws the glyph at the 13x10 that Material's arrow_back comes to at 20.
        private const float BackIndicatorArrowSize = 30;

        // At rest the centre sits one background radius outside the edge, so nothing of it shows
        // until the pan carries it in: Chrome's GetPaintedLayerOrigin.
        private const float BackIndicatorOrigin = -(BackIndicatorBurstRadius + BackIndicatorBackgroundRadius);

        // kBgShadowOffsetY, kBgShadowBlurRadius and the alpha of kBgShadowColor. Chrome draws it with
        // the circle rather than under the ripple, so it falls between the two.
        private const float BackIndicatorShadowOffset = 2;
        private const float BackIndicatorShadowBlur = 8;
        private const float BackIndicatorShadowOpacity = 0x4D / 255f;

        // kRippleColor's alpha.
        private const byte BackIndicatorRippleOpacity = 0x4C;

        // FAST_OUT_SLOW_IN, which every one of these animations uses.
        private static readonly Vector2 BackIndicatorEaseFrom = new(0.4f, 0);
        private static readonly Vector2 BackIndicatorEaseTo = new(0.2f, 1);

        // kRippleBurstAnimationDuration, and kAbortAnimationDuration - the abort is scaled by how
        // far the chip had come, so a short pull snaps back and a long one is walked back.
        private static readonly TimeSpan BackIndicatorBurstDuration = TimeSpan.FromMilliseconds(200);
        private static readonly TimeSpan BackIndicatorAbortDuration = TimeSpan.FromMilliseconds(300);

        private VisualInteractionSource _backSource;
        private ScrollViewer _backScrollingHost;
        private ScrollMode _backScrollingMode;
        private InteractionTracker _backTracker;
        private WeakInteractionTrackerOwner _backTrackerOwner;

        private ContainerVisual _backIndicator;
        private ShapeVisual _backIndicatorRipple;
        private ShapeVisual _backIndicatorCircle;
        private SpriteVisual _backIndicatorArrow;

        // The activated pair sits on top of the plain one and is stepped in, rather than either being
        // recoloured: an expression can drive an opacity off the tracker, but not a brush.
        private ShapeVisual _backIndicatorCircleActivated;
        private SpriteVisual _backIndicatorArrowActivated;
        private SpriteVisual _backIndicatorShadow;
        private LoadedImageSurface _backIndicatorArrowSurface;

        private CompositionColorBrush _backIndicatorRippleBrush;
        private CompositionColorBrush _backIndicatorCircleActivatedBrush;

        private Color _backIndicatorAccentColor;
        private bool _backCompleting;
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

                var neutralX = InteractionTrackerInertiaRestingValue.Create(compositor);
                neutralX.Condition = compositor.CreateExpressionAnimation("true");
                neutralX.RestingValue = compositor.CreateExpressionAnimation("0");

                // A List, not an array: InteractionTrackerInertiaModifier is a WinRT runtimeclass,
                // and an array of one boxes through IReferenceArray, which NativeAOT cannot synthesise.
                _backTracker.ConfigurePositionXInertiaModifiers(new List<InteractionTrackerInertiaModifier> { neutralX });
            }

            // Re-read on every navigation, so switching the setting off takes effect at once, and so
            // the threshold follows the window onto a display of another size.
            _backTracker.MinPosition = new Vector3(-BackGestureMaxPosition, 0, 0);
            _backSource.PositionXSourceMode = enabled
                ? InteractionSourceMode.EnabledWithInertia
                : InteractionSourceMode.Disabled;
        }

        /// <summary>
        /// Holds the page's scrolling host manipulable, so the source on DetailRoot can be reached
        /// over it.
        /// </summary>
        /// <remarks>
        /// ScrollMode.Auto turns the manipulation off entirely when there is nothing to scroll, and
        /// a touchpad pan over an element that cannot be manipulated is never classified as one -
        /// so no interaction source is consulted anywhere up the tree and the gesture is absent
        /// rather than misrouted. Every settings page declares Auto, which made this work or not
        /// depending on the window size rather than on anything in the markup. Enabled costs
        /// nothing: there is still no extent, so nothing becomes scrollable that was not.
        ///
        /// Must not run inline from the navigation. Writing the scroll mode of a scroller that is
        /// already laid out reconfigures its manipulation, and that runs a layout pass over the
        /// whole tree - by which point the incoming page is the frame's content but NavigateToAsync
        /// has not activated it yet. Arranging a page that has no view model throws, and the
        /// exception unwinds the navigation, so it never gets activated at all.
        /// </remarks>
        private void ConfigureBackGestureHost()
        {
            // Restores the previous page first: this is the only place it can happen, since
            // OnNavigating is never subscribed and nothing else runs as a page is left.
            DetachBackGestureHost();

            if (_backTracker == null || !SettingsService.Current.SwipeToGoBack)
            {
                return;
            }

            // Read here rather than passed in: several navigations can land before this runs, and
            // only the page that ends up current should be holding its scroller open.
            // A ListViewBase host is left alone: those scroll in practice, so their manipulation is
            // live already, and the chat history - the one that matters - is served by
            // MessageSelector's own source rather than by anything here.
            if (DetailFrame?.Content is not HostedPage hosted
                || hosted.FindName("ScrollingHost") is not ScrollViewer scrollingHost)
            {
                return;
            }

            _backScrollingHost = scrollingHost;
            _backScrollingMode = scrollingHost.VerticalScrollMode;

            scrollingHost.VerticalScrollMode = ScrollMode.Enabled;
        }

        private void DetachBackGestureHost()
        {
            if (_backScrollingHost != null)
            {
                _backScrollingHost.VerticalScrollMode = _backScrollingMode;
                _backScrollingHost = null;
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

            // Once per gesture, and only when the theme has moved the accent under us.
            if (_backIndicatorAccentColor != BackIndicatorAccentColor)
            {
                UpdateBackIndicatorBrushes(compositor);
            }

            // Chrome's drag progress: the first 60 DIPs only recognise the gesture, so the chip does
            // not stir over them, and 1 is the commit. Invariant because a computed float would be
            // written with a comma on most of the world's machines, and the expression would not parse.
            var start = BackGestureStartThreshold.ToString(CultureInfo.InvariantCulture);
            var threshold = BackGestureThreshold.ToString(CultureInfo.InvariantCulture);
            var span = (BackGestureThreshold - BackGestureStartThreshold).ToString(CultureInfo.InvariantCulture);
            var overspan = (BackGestureMaxPosition - BackGestureThreshold).ToString(CultureInfo.InvariantCulture);

            var progress = $"((-tracker.Position.X - {start}) / {span})";

            // Past the commit the chip keeps 72px of rubber band, spread over the whole remaining
            // pan. Chrome eases it with FAST_OUT_SLOW_IN, which the expression language has no bezier
            // for; smoothstep is the same shape to within a pixel of the 72.
            var extra = $"clamp((-tracker.Position.X - {threshold}) / {overspan}, 0, 1)";
            var eased = $"({extra} * {extra} * (3 - 2 * {extra}))";

            var travel = $"(clamp({progress}, 0, 1) * {BackIndicatorTravel} + {eased} * {BackIndicatorExtraTravel})";

            var offset = compositor.CreateExpressionAnimation(
                $"vector3({BackIndicatorOrigin} + {travel}, (root.Size.Y - {BackIndicatorBurstRadius * 2}) / 2, 0)");
            offset.SetReferenceParameter("tracker", tracker);
            offset.SetReferenceParameter("root", root);

            // The ripple grows from the circle's own radius out to 40, which is the sprite, so the
            // scale runs from 20/40 to 1.
            var scaled = $"(({BackIndicatorBackgroundRadius} + clamp({progress}, 0, 1) * {BackIndicatorRippleRadius - BackIndicatorBackgroundRadius}) / {BackIndicatorRippleRadius})";
            var ripple = compositor.CreateExpressionAnimation($"vector3({scaled}, {scaled}, 1)");
            ripple.SetReferenceParameter("tracker", tracker);

            _backIndicator.Opacity = 1;
            _backIndicator.StartAnimation("Offset", offset);
            _backIndicatorRipple.StartAnimation("Scale", ripple);

            // Chrome inverts the affordance the instant it activates, with no transition, so this is
            // a step and not a fade. The plain pair steps out on the same frame rather than being
            // left underneath: two stacked circles blend along their antialiased edge, and the white
            // one reads as a rim around the accent.
            var swap = compositor.CreateExpressionAnimation($"{progress} >= 1 ? 1 : 0");
            swap.SetReferenceParameter("tracker", tracker);

            var unswap = compositor.CreateExpressionAnimation($"{progress} >= 1 ? 0 : 1");
            unswap.SetReferenceParameter("tracker", tracker);

            _backIndicatorCircle.StartAnimation("Opacity", unswap);
            _backIndicatorArrow.StartAnimation("Opacity", unswap);

            _backIndicatorCircleActivated.StartAnimation("Opacity", swap);
            _backIndicatorArrowActivated.StartAnimation("Opacity", swap);
        }

        /// <summary>
        /// Chrome's palette with our accent standing in for Google Blue: a white circle carrying an
        /// accent arrow, the two swapped the instant it activates, over an accent ripple.
        /// </summary>
        private Color BackIndicatorAccentColor => ActualTheme == ElementTheme.Light
            ? Theme.AccentLight.Default
            : Theme.AccentDark.Default;

        private void UpdateBackIndicatorBrushes(Compositor compositor)
        {
            var accent = BackIndicatorAccentColor;
            _backIndicatorAccentColor = accent;

            _backIndicatorRippleBrush.Color = Color.FromArgb(BackIndicatorRippleOpacity, accent.R, accent.G, accent.B);
            _backIndicatorCircleActivatedBrush.Color = accent;

            // The surface only arrives asynchronously, and a theme change can beat it here.
            if (_backIndicatorArrowSurface != null)
            {
                _backIndicatorArrow.Brush = CreateArrowBrush(compositor, accent);
                _backIndicatorArrowActivated.Brush = CreateArrowBrush(compositor, Colors.White);
            }
        }

        /// <summary>
        /// A circle painted into a surface, which is what a DropShadow needs to take its shape from.
        /// Only ever a mask, never anything drawn: a surface is a raster and does not survive being
        /// scaled up, whether by the display or by the burst.
        /// </summary>
        private CompositionSurfaceBrush CreateCircleSurfaceBrush(Compositor compositor, float radius)
        {
            var ellipse = compositor.CreateEllipseGeometry();
            ellipse.Radius = new Vector2(radius);

            var shape = compositor.CreateSpriteShape(ellipse);
            shape.FillBrush = compositor.CreateColorBrush(Colors.White);
            shape.Offset = new Vector2(radius);

            var visual = compositor.CreateShapeVisual();
            visual.Shapes.Add(shape);
            visual.Size = new Vector2(radius * 2);

            var surface = compositor.CreateVisualSurface();
            surface.SourceVisual = visual;
            surface.SourceSize = new Vector2(radius * 2);

            return compositor.CreateSurfaceBrush(surface);
        }

        /// <summary>
        /// The arrow is a bitmap where Chrome's is a vector icon it can hand a colour to, so it is
        /// tinted by using its own alpha as a mask over a flat colour.
        /// </summary>
        private CompositionMaskBrush CreateArrowBrush(Compositor compositor, Color color)
        {
            var brush = compositor.CreateMaskBrush();
            brush.Mask = compositor.CreateSurfaceBrush(_backIndicatorArrowSurface);
            brush.Source = compositor.CreateColorBrush(color);

            return brush;
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

            // A commit has already taken the chip off the tracker, and its burst outlives both the
            // gesture and the navigation that follows.
            if (_backCompleting)
            {
                return;
            }

            // Left bound, the expressions would hold a recycled container's tracker alive, and the
            // indicator would sit at whatever progress the gesture happened to end on.
            _backIndicator.StopAnimation("Offset");
            _backIndicatorRipple.StopAnimation("Scale");
            _backIndicatorCircle.StopAnimation("Opacity");
            _backIndicatorArrow.StopAnimation("Opacity");
            _backIndicatorCircleActivated.StopAnimation("Opacity");
            _backIndicatorArrowActivated.StopAnimation("Opacity");

            var progress = (_backIndicator.Offset.X - BackIndicatorOrigin) / BackIndicatorTravel;
            if (progress <= 0)
            {
                _backIndicator.Opacity = 0;
                return;
            }

            // Chrome's abort: the chip retreats the way it came rather than blinking out.
            var compositor = _backIndicator.Compositor;

            var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += OnBackIndicatorRetreated;

            var offset = compositor.CreateVector3KeyFrameAnimation();
            offset.InsertKeyFrame(1, new Vector3(BackIndicatorOrigin, _backIndicator.Offset.Y, 0),
                compositor.CreateCubicBezierEasingFunction(BackIndicatorEaseFrom, BackIndicatorEaseTo));
            offset.Duration = TimeSpan.FromMilliseconds(BackIndicatorAbortDuration.TotalMilliseconds * progress);

            _backIndicator.StartAnimation("Offset", offset);

            batch.End();
        }

        private void OnBackIndicatorRetreated(object sender, CompositionBatchCompletedEventArgs args)
        {
            if (_backDriver == null && !_backCompleting)
            {
                _backIndicator.Opacity = 0;
            }
        }

        private void OnBackIndicatorBurst(object sender, CompositionBatchCompletedEventArgs args)
        {
            _backCompleting = false;

            if (_backDriver == null)
            {
                _backIndicator.Opacity = 0;
                _backIndicatorRipple.Scale = Vector3.One;
                _backIndicatorCircle.Opacity = 1;
                _backIndicatorArrow.Opacity = 1;
                _backIndicatorCircleActivated.Opacity = 0;
                _backIndicatorArrowActivated.Opacity = 0;
            }
        }

        public void CommitBackGesture()
        {
            // The chip finishes on its own: the navigation below pulls the page out from under it,
            // and Chrome lets the burst play over whatever replaces it.
            if (_backIndicator != null && !_backCompleting)
            {
                _backCompleting = true;

                var compositor = _backIndicator.Compositor;
                var easing = compositor.CreateCubicBezierEasingFunction(BackIndicatorEaseFrom, BackIndicatorEaseTo);

                _backIndicator.StopAnimation("Offset");
                _backIndicatorRipple.StopAnimation("Scale");
                _backIndicatorCircle.StopAnimation("Opacity");
                _backIndicatorArrow.StopAnimation("Opacity");
                _backIndicatorCircleActivated.StopAnimation("Opacity");
                _backIndicatorArrowActivated.StopAnimation("Opacity");

                var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
                batch.Completed += OnBackIndicatorBurst;

                var ripple = compositor.CreateVector3KeyFrameAnimation();
                ripple.InsertKeyFrame(1, new Vector3(BackIndicatorBurstRadius / BackIndicatorRippleRadius, BackIndicatorBurstRadius / BackIndicatorRippleRadius, 1), easing);
                ripple.Duration = BackIndicatorBurstDuration;

                var opacity = compositor.CreateScalarKeyFrameAnimation();
                opacity.InsertKeyFrame(1, 0, easing);
                opacity.Duration = BackIndicatorBurstDuration;

                _backIndicatorRipple.StartAnimation("Scale", ripple);
                _backIndicator.StartAnimation("Opacity", opacity);

                batch.End();
            }

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

            SpriteVisual Centered(float size)
            {
                var sprite = compositor.CreateSpriteVisual();
                sprite.Size = new Vector2(size);
                sprite.Offset = new Vector3(BackIndicatorBurstRadius - size / 2);
                sprite.CenterPoint = new Vector3(size / 2);
                return sprite;
            }

            // Every circle is drawn as a shape rather than painted into a surface: a VisualSurface
            // rasterises at its logical size once and is then sampled, which on a scaled display is
            // an upscale. Shapes are re-rasterised at whatever transform they end up under, so they
            // survive both the DPI and the burst's scale.
            ShapeVisual Circle(float radius, out CompositionColorBrush fill)
            {
                var ellipse = compositor.CreateEllipseGeometry();
                ellipse.Radius = new Vector2(radius);

                fill = compositor.CreateColorBrush();

                var shape = compositor.CreateSpriteShape(ellipse);
                shape.FillBrush = fill;
                shape.Offset = new Vector2(radius);

                var visual = compositor.CreateShapeVisual();
                visual.Shapes.Add(shape);
                visual.Size = new Vector2(radius * 2);
                visual.Offset = new Vector3(BackIndicatorBurstRadius - radius);
                visual.CenterPoint = new Vector3(radius);
                return visual;
            }

            // Sized for the ripple at rest, and scaled from there: 40 covers the drag, and the burst
            // takes it to 48, which is exactly the layer.
            _backIndicatorRipple = Circle(BackIndicatorRippleRadius, out _backIndicatorRippleBrush);

            // The plain layer stays opaque and carries the shadow, so only the activated one has to
            // be stepped in and the shadow is drawn once either way.
            _backIndicatorCircle = Circle(BackIndicatorBackgroundRadius, out var circle);
            _backIndicatorCircleActivated = Circle(BackIndicatorBackgroundRadius, out _backIndicatorCircleActivatedBrush);
            _backIndicatorCircleActivated.Opacity = 0;

            circle.Color = Colors.White;

            // A ShapeVisual cannot cast one, so the shadow is a sprite of its own. It paints nothing:
            // a surface is a raster, and on a scaled display the soft edge of the upscale crept out
            // from under the vector circle as a pale rim. The shape it casts comes from the mask.
            _backIndicatorShadow = Centered(BackIndicatorBackgroundRadius * 2);
            _backIndicatorShadow.Brush = compositor.CreateColorBrush(Colors.Transparent);

            var shadow = compositor.CreateDropShadow();
            shadow.BlurRadius = BackIndicatorShadowBlur;
            shadow.Offset = new Vector3(0, BackIndicatorShadowOffset, 0);
            shadow.Opacity = BackIndicatorShadowOpacity;
            shadow.Color = Colors.Black;

            // Without a mask the shadow is the sprite's bounds, which is a square.
            shadow.Mask = CreateCircleSurfaceBrush(compositor, BackIndicatorBackgroundRadius);

            _backIndicatorShadow.Shadow = shadow;

            // Not mirrored: the asset points left, which is the way back. ChatListListView flips it
            // because the indicator it brings in from the left is the one that goes forward.
            _backIndicatorArrow = Centered(BackIndicatorArrowSize);
            _backIndicatorArrowActivated = Centered(BackIndicatorArrowSize);
            _backIndicatorArrowActivated.Opacity = 0;

            _backIndicatorArrowSurface = LoadedImageSurface.StartLoadFromUri(new Uri("ms-appx:///Assets/Images/ArrowLeft.png"));
            void handler(LoadedImageSurface s, LoadedImageSourceLoadCompletedEventArgs args)
            {
                s.LoadCompleted -= handler;
                UpdateBackIndicatorBrushes(compositor);
            }

            _backIndicatorArrowSurface.LoadCompleted += handler;

            _backIndicator = compositor.CreateContainerVisual();
            _backIndicator.Size = new Vector2(BackIndicatorBurstRadius * 2);
            _backIndicator.Children.InsertAtBottom(_backIndicatorRipple);
            _backIndicator.Children.InsertAtTop(_backIndicatorShadow);
            _backIndicator.Children.InsertAtTop(_backIndicatorCircle);
            _backIndicator.Children.InsertAtTop(_backIndicatorCircleActivated);
            _backIndicator.Children.InsertAtTop(_backIndicatorArrow);
            _backIndicator.Children.InsertAtTop(_backIndicatorArrowActivated);
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
