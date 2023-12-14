//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Numerics;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Composition;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Views.Host;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Automation.Provider;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace Telegram.Controls
{
    public class ContentPopup : ContentDialog
    {
        private ContentDialogResult _result;

        private Border AnimationElement;
        private Border BackgroundElement;
        private Border ContentElement;
        private Grid CommandSpace;

        public ContentPopup()
        {
            DefaultStyleKey = typeof(ContentPopup);
            DefaultButton = ContentDialogButton.Primary;

            UseLayoutRounding = SettingsService.Current.Diagnostics.UseLayoutRounding;

            if (Window.Current.Content is FrameworkElement element)
            {
                var app = BootStrapper.Current.RequestedTheme == ApplicationTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;
                var frame = element.RequestedTheme;

                if (app != frame)
                {
                    RequestedTheme = SettingsService.Current.Appearance.GetCalculatedElementTheme();
                }
            }

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            Opened += OnOpened;
            Closing += OnClosing;
            Closed += OnClosed;

            ElementCompositionPreview.SetIsTranslationEnabled(this, true);
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            BackgroundElement.Width = e.NewSize.Width;
            BackgroundElement.Height = e.NewSize.Height;

            AnimationElement.Width = e.NewSize.Width;
            AnimationElement.Height = e.NewSize.Height;

            if (e.PreviousSize.Height == 0 || e.NewSize.Height == 0 || e.PreviousSize.Height == e.NewSize.Height)
            {
                return;
            }

            var compositor = Window.Current.Compositor;
            var prev = e.PreviousSize.ToVector2();
            var next = e.NewSize.ToVector2();

            var transform = CommandSpace.TransformToVisual(ContentElement);
            var point = transform.TransformPoint(new Point()).ToVector2();

            var visual = ElementCompositionPreview.GetElementVisual(this);
            var content = ElementCompositionPreview.GetElementVisual(ContentElement);
            var background = ElementCompositionPreview.GetElementVisual(BackgroundElement);

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

        public virtual void OnNavigatedTo()
        {

        }

        public virtual void OnNavigatedFrom()
        {

        }

        private void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            if (Window.Current.Content is RootPage root)
            {
                root.PopupOpened();
            }
        }

        private void OnClosing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            var canvas = VisualTreeHelper.GetParent(this) as Canvas;
            if (canvas != null)
            {
                foreach (var child in canvas.Children)
                {
                    if (child is Rectangle rectangle)
                    {
                        rectangle.Visibility = Visibility.Collapsed;
                    }
                }
            }
        }

        private void OnClosed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            if (Window.Current.Content is RootPage root)
            {
                root.PopupClosed();
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            WindowContext.Current.InputListener.KeyDown += OnKeyDown;

            var canvas = VisualTreeHelper.GetParent(this) as Canvas;
            if (canvas != null)
            {
                foreach (var child in canvas.Children)
                {
                    if (child is Rectangle rectangle)
                    {
                        rectangle.Visibility = Visibility.Visible;
                        rectangle.Fill = new SolidColorBrush(ActualTheme == ElementTheme.Light
                            ? Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF)
                            : Color.FromArgb(0x99, 0x00, 0x00, 0x00));
                    }
                }
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            WindowContext.Current.InputListener.KeyDown -= OnKeyDown;
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

            CommandSpace = GetTemplateChild(nameof(CommandSpace)) as Grid;
            AnimationElement = GetTemplateChild(nameof(AnimationElement)) as Border;
            BackgroundElement = GetTemplateChild(nameof(BackgroundElement)) as Border;

            ContentElement = GetTemplateChild(nameof(ContentElement)) as Border;

            if (ContentElement != null)
            {
                ContentElement.SizeChanged += OnSizeChanged;
            }
        }

        private void PrimaryButton_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && FocusPrimaryButton)
            {
                button.Focus(FocusState.Keyboard);
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

        public async Task<ContentDialogResult> OpenAsync()
        {
            await this.ShowQueuedAsync();
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
    }
}
