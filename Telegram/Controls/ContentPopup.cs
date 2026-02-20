//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Composition;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Views.Host;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Automation.Provider;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace Telegram.Controls
{
    public enum ContentPopupButtonsLayout
    {
        Horizontal,
        Vertical
    }

    public partial class ContentPopup : ContentDialogEx
    {
        private ContentDialogResult _result;

        private Grid LayoutRoot;
        private Border AnimationElement;
        private Border BackgroundElement;
        private Border BorderElement;
        private Border ContentElement;
        private Grid CommandSpace;
        private Grid PrimaryRoot;
        private Button PrimaryButton;
        private Microsoft.UI.Xaml.Controls.ProgressRing PrimaryButtonPending;

        private Button DismissButton;

        private Rectangle Smoke;

        private long _primaryTextToken;
        private long _secondaryTextToken;
        private long _closeTextToken;

        public ContentPopup()
        {
            DefaultStyleKey = typeof(ContentPopup);
            DefaultButton = ContentDialogButton.Primary;

            if (WindowContext.Current.Content is FrameworkElement element)
            {
                var app = BootStrapper.Current.RequestedTheme == ApplicationTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;
                var frame = element.RequestedTheme;

                if (app != frame)
                {
                    RequestedTheme = SettingsService.Current.Appearance.GetCalculatedElementTheme();
                }
            }

            Connected += OnLoaded;
            Disconnected += OnUnloaded;

            CloseButtonClick += OnCloseButtonClick;

            this.RegisterPropertyChangedCallback(PrimaryButtonTextProperty, OnButtonTextChanged, ref _primaryTextToken);
            this.RegisterPropertyChangedCallback(SecondaryButtonTextProperty, OnButtonTextChanged, ref _secondaryTextToken);
            this.RegisterPropertyChangedCallback(CloseButtonTextProperty, OnButtonTextChanged, ref _closeTextToken);
        }

        private void OnCloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // For some weird reason, there's no ContentDialogResult.Close, so we hack it around.

            var result = CloseButtonResult;
            if (result == ContentDialogResult.None)
            {
                return;
            }

            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => Hide(result));
            args.Cancel = true;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            BackgroundElement.Width = e.NewSize.Width;
            BackgroundElement.Height = e.NewSize.Height;

            BorderElement.Width = e.NewSize.Width;
            BorderElement.Height = e.NewSize.Height;

            AnimationElement.Width = e.NewSize.Width;
            AnimationElement.Height = e.NewSize.Height;

            if (e.PreviousSize.Height == 0 || e.NewSize.Height == 0 || e.PreviousSize.Height == e.NewSize.Height || VerticalContentAlignment == VerticalAlignment.Stretch)
            {
                return;
            }

            var compositor = BootStrapper.Current.Compositor;
            var prev = e.PreviousSize.ToVector2();
            var next = e.NewSize.ToVector2();

            var transform = CommandSpace.TransformToVisual(ContentElement);
            var point = transform.TransformVector2();

            var visual = ElementComposition.GetElementVisual(LayoutRoot);
            var content = ElementComposition.GetElementVisual(ContentElement);
            var background = ElementComposition.GetElementVisual(BackgroundElement);
            var border = ElementComposition.GetElementVisual(BorderElement);

            var clip = compositor.CreateInsetClip();
            content.Clip = clip;

            var redirect = compositor.CreateRedirectVisual(CommandSpace, Vector2.Zero, new Vector2(CommandSpace.ActualSize.X, CommandSpace.ActualSize.Y * 2));
            redirect.Offset = new Vector3(point.X, 0, 0);

            var translate = compositor.CreateScalarKeyFrameAnimation();
            translate.InsertKeyFrame(0, (next.Y - prev.Y) / 2);
            translate.InsertKeyFrame(1, 0);

            var scale = compositor.CreateScalarKeyFrameAnimation();
            scale.InsertKeyFrame(0, prev.Y / next.Y);
            scale.InsertKeyFrame(1, 1);

            var offset = compositor.CreateScalarKeyFrameAnimation();
            offset.InsertKeyFrame(0, prev.Y - CommandSpace.ActualSize.Y - point.X);
            offset.InsertKeyFrame(1, next.Y - CommandSpace.ActualSize.Y - point.X);

            var inset = compositor.CreateScalarKeyFrameAnimation();
            inset.InsertKeyFrame(0, next.Y - prev.Y);
            inset.InsertKeyFrame(1, 0);

            var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += Batch_Completed;

            visual.StartAnimation("Offset.Y", translate);
            background.StartAnimation("Scale.Y", scale);
            border.StartAnimation("Scale.Y", scale);
            redirect.StartAnimation("Offset.Y", offset);
            clip.StartAnimation("BottomInset", inset);

            batch.End();

            CommandSpace.Opacity = 0;
            ElementCompositionPreview.SetElementChildVisual(AnimationElement, redirect);
        }

        private void Batch_Completed(object sender, CompositionBatchCompletedEventArgs args)
        {
            CommandSpace.Opacity = 1;
            ElementCompositionPreview.SetElementChildVisual(AnimationElement, null);
        }

        public virtual void OnCreate()
        {

        }

        public virtual void OnNavigatedTo(object parameter)
        {

        }

        public virtual void OnNavigatedFrom()
        {

        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            this.RegisterPropertyChangedCallback(PrimaryButtonTextProperty, OnButtonTextChanged, ref _primaryTextToken);
            this.RegisterPropertyChangedCallback(SecondaryButtonTextProperty, OnButtonTextChanged, ref _secondaryTextToken);
            this.RegisterPropertyChangedCallback(CloseButtonTextProperty, OnButtonTextChanged, ref _closeTextToken);

            try
            {
                if (XamlRoot.Content is IPopupHost host)
                {
                    host.PopupOpened();
                }
            }
            catch
            {
                // XamlRoot.Content seems to throw a NullReferenceException
                // whenever corresponding window has been already closed.
            }

            var canvas = VisualTreeHelper.GetParent(this) as Canvas;
            if (canvas != null)
            {
                foreach (var child in canvas.Children)
                {
                    if (child is Rectangle rectangle)
                    {
                        // TODO: I don't remember why it is needed to show-hide it.
                        Smoke = rectangle;
                        Smoke.Fill = new SolidColorBrush(ActualTheme == ElementTheme.Light
                            ? Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF)
                            : Color.FromArgb(0x99, 0x00, 0x00, 0x00));

                        Smoke.Visibility = IsSmokeEnabled
                            ? Visibility.Visible
                            : Visibility.Collapsed;
                    }
                }
            }

            // This won't look great because the smoke will disappear instantly, but at least it won't flash
            if (Smoke != null && Parent is Popup popup)
            {
                popup.Opened += OnOpened;
                popup.Closed += OnClosed;
            }
        }

        private void OnOpened(object sender, object e)
        {
            Smoke.Visibility = IsSmokeEnabled
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void OnClosed(object sender, object e)
        {
            Smoke.Visibility = Visibility.Collapsed;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            this.UnregisterPropertyChangedCallback(PrimaryButtonTextProperty, ref _primaryTextToken);
            this.UnregisterPropertyChangedCallback(SecondaryButtonTextProperty, ref _secondaryTextToken);
            this.UnregisterPropertyChangedCallback(CloseButtonTextProperty, ref _closeTextToken);

            try
            {
                if (XamlRoot.Content is IPopupHost host)
                {
                    host.PopupClosed();
                }
            }
            catch
            {
                // XamlRoot.Content seems to throw a NullReferenceException
                // whenever corresponding window has been already closed.
            }
        }

        private void OnProcessKeyboardAccelerators(UIElement sender, ProcessKeyboardAcceleratorEventArgs args)
        {
            if (args.Key == VirtualKey.Enter && args.Modifiers == VirtualKeyModifiers.None && DefaultButton != ContentDialogButton.Primary)
            {
                // TODO: should the if be simplified to focused is null or not Control?

                var focused = FocusManagerEx.TryGetFocusedElement();
                if (focused is null or (not TextBox and not RichEditBox and not Button and not MenuFlyoutItem))
                {
                    Hide(ContentDialogResult.Primary);
                    args.Handled = true;
                }
            }
        }

        public bool IsFullWindow { get; set; } = false;

        public bool FocusPrimaryButton { get; set; } = true;
        public bool IsLightDismissEnabled { get; set; } = true;

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            VisualStateManager.GoToState(this, IsPrimaryButtonSplit ? "PrimaryAsSplitButton" : "NoSplitButton", false);

            // TODO: Name
            PrimaryRoot = GetTemplateChild(nameof(PrimaryRoot)) as Grid;
            PrimaryButton = GetTemplateChild(nameof(PrimaryButton)) as Button;
            PrimaryButtonPending = GetTemplateChild(nameof(PrimaryButtonPending)) as Microsoft.UI.Xaml.Controls.ProgressRing;

            PrimaryRoot?.CreateInsetClip();

            if (PrimaryButton != null && FocusPrimaryButton)
            {
                PrimaryButton.Loaded += PrimaryButton_Loaded;
            }

            // TODO: Name
            var rectangle = GetTemplateChild("LightDismiss") as Rectangle;
            if (rectangle != null)
            {
                rectangle.PointerReleased += Rectangle_PointerReleased;
            }

            if (IsDismissButtonVisible)
            {
                DismissButton = GetTemplateChild(nameof(DismissButton)) as Button;
                DismissButton.RequestedTheme = DismissButtonRequestedTheme;
                DismissButton.Click += DismissButton_Click;
            }

            CommandSpace = GetTemplateChild(nameof(CommandSpace)) as Grid;
            AnimationElement = GetTemplateChild(nameof(AnimationElement)) as Border;
            BackgroundElement = GetTemplateChild(nameof(BackgroundElement)) as Border;
            BorderElement = GetTemplateChild(nameof(BorderElement)) as Border;
            ContentElement = GetTemplateChild(nameof(ContentElement)) as Border;
            LayoutRoot = GetTemplateChild(nameof(LayoutRoot)) as Grid;

            if (ContentElement != null)
            {
                ContentElement.SizeChanged += OnSizeChanged;
            }

            if (LayoutRoot != null)
            {
                LayoutRoot.ProcessKeyboardAccelerators += OnProcessKeyboardAccelerators;
                ElementCompositionPreview.SetIsTranslationEnabled(LayoutRoot, true);
            }

            this.RegisterPropertyChangedCallback(PrimaryButtonTextProperty, OnButtonTextChanged, ref _primaryTextToken);
            this.RegisterPropertyChangedCallback(SecondaryButtonTextProperty, OnButtonTextChanged, ref _secondaryTextToken);
            this.RegisterPropertyChangedCallback(CloseButtonTextProperty, OnButtonTextChanged, ref _closeTextToken);

            CalculateButtonsVisualState();
        }

        private void OnButtonTextChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (dp == PrimaryButtonTextProperty)
            {
                PrimaryButtonContent = PrimaryButtonText;
            }

            CalculateButtonsVisualState();
        }

        private void DismissButton_Click(object sender, RoutedEventArgs e)
        {
            OnDismissButtonClick();
        }

        protected virtual void OnDismissButtonClick()
        {
            Hide();
        }

        private void PrimaryButton_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && FocusPrimaryButton)
            {
                if (button.Focus(FocusState.Keyboard))
                {
                    return;
                }

                this.Focus(FocusState.Programmatic);
            }
        }

        private void Rectangle_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            var pointer = e.GetCurrentPoint(this);
            if (pointer.Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonReleased && IsLightDismissEnabled)
            {
                Hide();
            }
        }

        public async Task<ContentDialogResult> OpenAsync(XamlRoot xamlRoot)
        {
            await this.ShowQueuedAsync(xamlRoot);
            return _result;
        }

        protected void SetResult(ContentDialogResult result)
        {
            _result = result;
        }

        public void Close()
        {
            Hide();
        }

        public void Hide(ContentDialogResult result)
        {
            _result = result;

            if (result == ContentDialogResult.Primary)
            {
                // TODO: Name
                var button = GetTemplateChild("PrimaryButton") as Button;
                if (button != null)
                {
                    if (button.IsEnabled)
                    {
                        var invoke = new ButtonAutomationPeer(button) as IInvokeProvider;
                        invoke?.Invoke();
                    }

                    return;
                }
            }
            else if (result == ContentDialogResult.Secondary)
            {
                // TODO: Name
                var button = GetTemplateChild("SecondaryButton") as Button;
                if (button != null)
                {
                    if (button.IsEnabled)
                    {
                        var invoke = new ButtonAutomationPeer(button) as IInvokeProvider;
                        invoke?.Invoke();
                    }

                    return;
                }
            }

            Hide();
        }

        public bool IsSmokeEnabled { get; set; } = true;

        #region IsPrimaryButtonSplit

        public bool IsPrimaryButtonSplit
        {
            get => (bool)GetValue(IsPrimaryButtonSplitProperty);
            set => SetValue(IsPrimaryButtonSplitProperty, value);
        }

        public static readonly DependencyProperty IsPrimaryButtonSplitProperty =
            DependencyProperty.Register("IsPrimaryButtonSplit", typeof(bool), typeof(ContentPopup), new PropertyMetadata(false));

        #endregion

        #region IsDismissButtonVisible

        public bool IsDismissButtonVisible
        {
            get { return (bool)GetValue(IsDismissButtonVisibleProperty); }
            set { SetValue(IsDismissButtonVisibleProperty, value); }
        }

        public static readonly DependencyProperty IsDismissButtonVisibleProperty =
            DependencyProperty.Register("IsDismissButtonVisible", typeof(bool), typeof(ContentPopup), new PropertyMetadata(false, OnDismissButtonVisibleChanged));

        private static void OnDismissButtonVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as ContentPopup;
            if (sender?.DismissButton == null)
            {
                sender.DismissButton = sender.GetTemplateChild(nameof(sender.DismissButton)) as Button;
                sender.DismissButton?.Click += sender.DismissButton_Click;
            }

            sender.DismissButton?.Visibility = (bool)e.NewValue
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        #endregion

        #region DismissButtonRequestedTheme

        public ElementTheme DismissButtonRequestedTheme
        {
            get { return (ElementTheme)GetValue(DismissButtonRequestedThemeProperty); }
            set { SetValue(DismissButtonRequestedThemeProperty, value); }
        }

        public static readonly DependencyProperty DismissButtonRequestedThemeProperty =
            DependencyProperty.Register("DismissButtonRequestedTheme", typeof(ElementTheme), typeof(ContentPopup), new PropertyMetadata(ElementTheme.Default, OnDismissButtonRequestedThemeChanged));

        private static void OnDismissButtonRequestedThemeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as ContentPopup;
            if (sender?.DismissButton == null)
            {
                sender.DismissButton = sender.GetTemplateChild(nameof(sender.DismissButton)) as Button;
                sender.DismissButton?.Click += sender.DismissButton_Click;
            }

            sender?.DismissButton?.RequestedTheme = (ElementTheme)e.NewValue;
        }

        #endregion

        #region PrimaryButtonContent

        public object PrimaryButtonContent
        {
            get { return (object)GetValue(PrimaryButtonContentProperty); }
            set { SetValue(PrimaryButtonContentProperty, value); }
        }

        public static readonly DependencyProperty PrimaryButtonContentProperty =
            DependencyProperty.Register(nameof(PrimaryButtonContent), typeof(object), typeof(ContentPopup), new PropertyMetadata(null));

        #endregion

        #region IsPrimaryButtonPending


        public bool IsPrimaryButtonPending
        {
            get { return (bool)GetValue(IsPrimaryButtonPendingProperty); }
            set { SetValue(IsPrimaryButtonPendingProperty, value); }
        }

        public static readonly DependencyProperty IsPrimaryButtonPendingProperty =
            DependencyProperty.Register(nameof(IsPrimaryButtonPending), typeof(bool), typeof(ContentPopup), new PropertyMetadata(false, OnIsPrimaryButtonPendingChanged));

        private static void OnIsPrimaryButtonPendingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ContentPopup)d).OnIsPrimaryButtonPendingChanged((bool)e.NewValue);
        }

        private bool _primaryButtonPendingCollapsed = true;

        private void OnIsPrimaryButtonPendingChanged(bool show)
        {
            var contentTemplateRoot = PrimaryButton?.ContentTemplateRoot;
            if (contentTemplateRoot == null || PrimaryButtonPending == null)
            {
                return;
            }

            if (_primaryButtonPendingCollapsed != show)
            {
                return;
            }

            _primaryButtonPendingCollapsed = !show;
            PrimaryButtonPending.Visibility = Visibility.Visible;

            var visual1 = ElementComposition.GetElementVisual(contentTemplateRoot);
            var visual2 = ElementComposition.GetElementVisual(PrimaryButtonPending);

            ElementCompositionPreview.SetIsTranslationEnabled(contentTemplateRoot, true);
            ElementCompositionPreview.SetIsTranslationEnabled(PrimaryButtonPending, true);

            var translate1 = visual1.Compositor.CreateScalarKeyFrameAnimation();
            translate1.InsertKeyFrame(0, show ? 0 : 32);
            translate1.InsertKeyFrame(1, show ? -32 : 0);

            var translate2 = visual1.Compositor.CreateScalarKeyFrameAnimation();
            translate2.InsertKeyFrame(0, show ? 32 : 0);
            translate2.InsertKeyFrame(1, show ? 0 : -32);

            visual1.StartAnimation("Translation.Y", translate1);
            visual2.StartAnimation("Translation.Y", translate2);
        }

        #endregion

        #region CloseButtonResult

        public ContentDialogResult CloseButtonResult
        {
            get { return (ContentDialogResult)GetValue(CloseButtonResultProperty); }
            set { SetValue(CloseButtonResultProperty, value); }
        }

        public static readonly DependencyProperty CloseButtonResultProperty =
            DependencyProperty.Register("CloseButtonResult", typeof(ContentDialogResult), typeof(ContentPopup), new PropertyMetadata(ContentDialogResult.None));

        #endregion

        #region SecondaryBackground

        public Brush SecondaryBackground
        {
            get { return (Brush)GetValue(SecondaryBackgroundProperty); }
            set { SetValue(SecondaryBackgroundProperty, value); }
        }

        public static readonly DependencyProperty SecondaryBackgroundProperty =
            DependencyProperty.Register("SecondaryBackground", typeof(Brush), typeof(ContentPopup), new PropertyMetadata(null));

        #endregion

        #region ContentMaxWidth

        public double ContentMaxWidth
        {
            get { return (double)GetValue(ContentMaxWidthProperty); }
            set { SetValue(ContentMaxWidthProperty, value); }
        }

        public static readonly DependencyProperty ContentMaxWidthProperty =
            DependencyProperty.Register("ContentMaxWidth", typeof(double), typeof(ContentPopup), new PropertyMetadata(320d));

        #endregion

        #region ContentMaxHeight

        public double ContentMaxHeight
        {
            get { return (double)GetValue(ContentMaxHeightProperty); }
            set { SetValue(ContentMaxHeightProperty, value); }
        }

        public static readonly DependencyProperty ContentMaxHeightProperty =
            DependencyProperty.Register("ContentMaxHeight", typeof(double), typeof(ContentPopup), new PropertyMetadata(568d));

        #endregion

        #region ContentMinWidth

        public double ContentMinWidth
        {
            get { return (double)GetValue(ContentMinWidthProperty); }
            set { SetValue(ContentMinWidthProperty, value); }
        }

        public static readonly DependencyProperty ContentMinWidthProperty =
            DependencyProperty.Register("ContentMinWidth", typeof(double), typeof(ContentPopup), new PropertyMetadata(320d));

        #endregion

        #region ContentMinHeight

        public double ContentMinHeight
        {
            get { return (double)GetValue(ContentMinHeightProperty); }
            set { SetValue(ContentMinHeightProperty, value); }
        }

        public static readonly DependencyProperty ContentMinHeightProperty =
            DependencyProperty.Register("ContentMinHeight", typeof(double), typeof(ContentPopup), new PropertyMetadata(184d));

        #endregion

        #region ButtonsLayout

        public ContentPopupButtonsLayout ButtonsLayout
        {
            get { return (ContentPopupButtonsLayout)GetValue(ButtonsLayoutProperty); }
            set { SetValue(ButtonsLayoutProperty, value); }
        }

        public static readonly DependencyProperty ButtonsLayoutProperty =
            DependencyProperty.Register("ButtonsLayout", typeof(ContentPopupButtonsLayout), typeof(ContentPopup), new PropertyMetadata(ContentPopupButtonsLayout.Horizontal, OnButtonsLayoutChanged));

        private static void OnButtonsLayoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ContentPopup)d).CalculateButtonsVisualState();
        }

        #endregion

        private void CalculateButtonsVisualState()
        {
            var primary = !string.IsNullOrEmpty(PrimaryButtonText);
            var secondary = !string.IsNullOrEmpty(SecondaryButtonText);
            var close = !string.IsNullOrEmpty(CloseButtonText);

            var builder = new StringBuilder();

            if (primary && secondary && close)
            {
                builder.Append("ButtonsAllVisible");
            }
            else
            {
                if (primary)
                {
                    builder.Append("Primary");
                }

                if (secondary)
                {
                    if (builder.Length > 0)
                    {
                        builder.Append("And");
                    }

                    builder.Append("Secondary");
                }

                if (close)
                {
                    if (builder.Length > 0)
                    {
                        builder.Append("And");
                    }

                    builder.Append("Close");
                }

                if (builder.Length > 0)
                {
                    builder.Append(ButtonsLayout == ContentPopupButtonsLayout.Vertical ? "Vertical" : "Horizontal");
                }
                else
                {
                    builder.Append("ButtonsNoneVisible");
                }
            }

            VisualStateManager.GoToState(this, builder.ToString(), false);
        }

        // TODO: terrible naming, this is used to prevent NavigatedFrom logic on temporary hide
        public bool IsFinalized { get; set; } = true;





        public static bool IsAnyPopupOpen(XamlRoot xamlRoot)
        {
            // If XamlRoot is null we aren't in a popup
            // TODO: Problem persists, because then popup fails to open because of no XamlRoot.
            if (xamlRoot == null)
            {
                return false;
            }

            foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(xamlRoot))
            {
                if (popup.Child is ContentDialog)
                {
                    return true;
                }
            }

            return false;
        }

        public static ContentPopup Block(XamlRoot xamlRoot)
        {
            var content = new Grid();
            content.Children.Add(new Microsoft.UI.Xaml.Controls.ProgressRing
            {
                Width = 48,
                Height = 48,
                Margin = new Thickness(12)
            });

            var toast = new ContentPopup
            {
                Content = content,
                IsLightDismissEnabled = true,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                ContentMinWidth = 0,
                ContentMinHeight = 0,
                Padding = new Thickness(0),
                RequestedTheme = ElementTheme.Dark,
                IsEnabled = false,
                IsHitTestVisible = false,
                Tag = new object()
            };

            toast.Closing += OnBlockedClosing;

            _ = toast.ShowQueuedAsync(xamlRoot);
            return toast;
        }

        private static void OnBlockedClosing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            args.Cancel = sender.Tag != null;
        }

        [ThreadStatic]
        private static TaskCompletionSource<ContentDialog> _currentDialogShowRequest;

        public async Task<ContentDialogResult> ShowQueuedAsync(XamlRoot xamlRoot)
        {
            while (_currentDialogShowRequest != null)
            {
                await _currentDialogShowRequest.Task;
            }

            var dialog = this;
            Logger.Info(dialog.GetType().Name);

            if (dialog is ContentPopup popup)
            {
                popup.OnCreate();
            }

            dialog.XamlRoot = xamlRoot;

            var request = _currentDialogShowRequest = new TaskCompletionSource<ContentDialog>();
            var result = await dialog.ShowAsync();
            _currentDialogShowRequest = null;
            request.SetResult(dialog);

            Logger.Info(dialog.GetType().Name + ", closed");
            return result;
        }
    }
}
