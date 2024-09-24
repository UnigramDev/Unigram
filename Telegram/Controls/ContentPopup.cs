//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Composition;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System.Numerics;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Composition;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Views.Host;
using Windows.UI;

namespace Telegram.Controls
{
    public partial class ContentPopup : ContentDialogEx
    {
        private ContentDialogResult _result;

        private Border AnimationElement;
        private Border BackgroundElement;
        private Border BorderElement;
        private Border ContentElement;
        private Grid CommandSpace;

        private Button DismissButton;

        private Rectangle Smoke;

        public ContentPopup()
        {
            DefaultStyleKey = typeof(ContentPopup);
            DefaultButton = ContentDialogButton.Primary;

            // TODO: missing
            if (Window.Current?.Content is FrameworkElement element)
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

            ElementCompositionPreview.SetIsTranslationEnabled(this, true);
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            BackgroundElement.Width = e.NewSize.Width;
            BackgroundElement.Height = e.NewSize.Height;

            BorderElement.Width = e.NewSize.Width;
            BorderElement.Height = e.NewSize.Height;

            AnimationElement.Width = e.NewSize.Width;
            AnimationElement.Height = e.NewSize.Height;

            if (e.PreviousSize.Height == 0 || e.NewSize.Height == 0 || e.PreviousSize.Height == e.NewSize.Height)
            {
                return;
            }

            var compositor = BootStrapper.Current.Compositor;
            var prev = e.PreviousSize.ToVector2();
            var next = e.NewSize.ToVector2();

            var transform = CommandSpace.TransformToVisual(ContentElement);
            var point = transform.TransformVector2();

            var visual = ElementComposition.GetElementVisual(this);
            var content = ElementComposition.GetElementVisual(ContentElement);
            var background = ElementComposition.GetElementVisual(BackgroundElement);
            var border = ElementComposition.GetElementVisual(BorderElement);

            var clip = compositor.CreateInsetClip();
            content.Clip = clip;

            var redirect = compositor.CreateRedirectVisual(CommandSpace, Vector2.Zero, CommandSpace.ActualSize);
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

            visual.StartAnimation("Translation.Y", translate);
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
            if (XamlRoot.Content is RootPage root)
            {
                root.PopupOpened();
            }

            var context = WindowContext.ForXamlRoot(this);
            if (context != null)
            {
                context.InputListener.KeyDown += OnKeyDown;
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
                        Smoke.Visibility = Visibility.Visible;
                        Smoke.Fill = new SolidColorBrush(ActualTheme == ElementTheme.Light
                            ? Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF)
                            : Color.FromArgb(0x99, 0x00, 0x00, 0x00));
                    }
                }
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (XamlRoot.Content is RootPage root)
                {
                    root.PopupClosed();
                }
            }
            catch
            {
                // XamlRoot.Content seems to throw a NullReferenceException
                // whenever corresponding window has been already closed.
            }

            var context = WindowContext.ForXamlRoot(this);
            if (context != null)
            {
                context.InputListener.KeyDown -= OnKeyDown;
            }

            if (Smoke != null)
            {
                Smoke.Visibility = Visibility.Collapsed;
            }
        }

        private void OnKeyDown(Window sender, Services.Keyboard.InputKeyDownEventArgs args)
        {
            if (args.VirtualKey == Windows.System.VirtualKey.Enter && args.OnlyKey && DefaultButton != ContentDialogButton.Primary)
            {
                // TODO: should the if be simplified to focused is null or not Control?

                var focused = FocusManager.GetFocusedElement();
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

            var button = GetTemplateChild("PrimaryButton") as Button;
            if (button != null && FocusPrimaryButton)
            {
                button.Loaded += PrimaryButton_Loaded;
            }

            var rectangle = GetTemplateChild("LightDismiss") as Rectangle;
            if (rectangle != null)
            {
                rectangle.PointerReleased += Rectangle_PointerReleased;
            }

            if (IsDismissButtonVisible)
            {
                DismissButton = GetTemplateChild(nameof(DismissButton)) as Button;
                DismissButton.Click += DismissButton_Click;
            }

            CommandSpace = GetTemplateChild(nameof(CommandSpace)) as Grid;
            AnimationElement = GetTemplateChild(nameof(AnimationElement)) as Border;
            BackgroundElement = GetTemplateChild(nameof(BackgroundElement)) as Border;
            BorderElement = GetTemplateChild(nameof(BorderElement)) as Border;

            ContentElement = GetTemplateChild(nameof(ContentElement)) as Border;

            if (ContentElement != null)
            {
                ContentElement.SizeChanged += OnSizeChanged;
            }
        }

        private void DismissButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
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
            if (sender?.DismissButton != null || (bool)e.NewValue)
            {
                if (sender.DismissButton == null)
                {
                    sender.DismissButton = sender.GetTemplateChild(nameof(sender.DismissButton)) as Button;

                    if (sender.DismissButton != null)
                    {
                        sender.DismissButton.Click += sender.DismissButton_Click;
                    }
                }

                if (sender.DismissButton != null)
                {
                    sender.DismissButton.Visibility = (bool)e.NewValue
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                }
            }
        }

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
    }
}
