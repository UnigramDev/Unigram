//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Numerics;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Views.Host;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls
{
    /// <summary>
    /// What a button did to the popup: nothing, unless the handler cancels the close.
    /// </summary>
    public partial class ModalPopupButtonClickEventArgs
    {
        /// <summary>
        /// Leaves the popup open, for a handler that has something to say about the input first.
        /// </summary>
        public bool Cancel { get; set; }
    }

    /// <summary>
    /// A title, a body and up to two buttons, in a <see cref="Popup"/> of its own, centred on the
    /// window and dismissable by clicking away from it.
    /// </summary>
    /// <remarks>
    /// This replaced TeachingTipEx, which derived from WinUI's TeachingTip - see ToastPopup for
    /// why nothing here may derive from a muxc control: the managed half and the native half die
    /// separately, and TeachingTip touches itself from a destructor and from revokers it never
    /// releases. Reported by crash telemetry on 12.10.2 and 12.10.3.
    ///
    /// Nothing that survived the conversion pointed at anything: of the twenty tips, four set a
    /// Target and all four are as good centred, so the tail, its band in the template and the
    /// placement arithmetic are all gone with it.
    ///
    /// Shaped after <see cref="ContentPopup"/> - PrimaryButton/SecondaryButton, a
    /// <see cref="ContentDialogResult"/>, ShowAsync and Hide - so that it can take that over as
    /// well, and a caller moving between the two has nothing to relearn.
    /// </remarks>
    [TemplatePart(Name = "ContentRoot", Type = typeof(Border))]
    [TemplatePart(Name = "TitleTextBlock", Type = typeof(TextBlock))]
    [TemplatePart(Name = "SubtitleTextBlock", Type = typeof(TextBlock))]
    [TemplatePart(Name = "ContentPresenter", Type = typeof(ContentPresenter))]
    [TemplatePart(Name = "PrimaryButton", Type = typeof(Button))]
    [TemplatePart(Name = "SecondaryButton", Type = typeof(Button))]
    public partial class ModalPopup : ContentControl
    {
        private Border ContentRoot;
        private TextBlock TitleTextBlock;
        private TextBlock SubtitleTextBlock;
        private ContentPresenter ContentPresenter;
        private Button PrimaryButton;
        private Button SecondaryButton;

        private XamlRoot _xamlRoot;
        private Popup _popup;
        private Popup _lightDismiss;
        private CompositionScopedBatch _batch;

        private TaskCompletionSource<ContentDialogResult> _tsc;
        private ContentDialogResult _result = ContentDialogResult.None;

        private bool _isOpen;
        private bool _entered;

        public ModalPopup()
        {
            DefaultStyleKey = typeof(ModalPopup);
        }

        /// <summary>
        /// Whether a click away from the popup takes it down.
        /// </summary>
        public bool IsLightDismissEnabled { get; set; } = true;

        /// <summary>
        /// Raised once the popup is off the screen, animation included.
        /// </summary>
        public event TypedEventHandler<ModalPopup, object> Closed;

        public event TypedEventHandler<ModalPopup, ModalPopupButtonClickEventArgs> PrimaryButtonClick;
        public event TypedEventHandler<ModalPopup, ModalPopupButtonClickEventArgs> SecondaryButtonClick;

        public bool IsOpen => _isOpen;

        /// <summary>
        /// Opens the popup and completes when it is closed, with whatever closed it.
        /// </summary>
        public Task<ContentDialogResult> ShowAsync(XamlRoot xamlRoot)
        {
            if (xamlRoot == null || _isOpen)
            {
                return Task.FromResult(ContentDialogResult.None);
            }

            XamlRoot = xamlRoot;

            _tsc = new TaskCompletionSource<ContentDialogResult>();
            Open();

            // Open bails when there is no root to open on, and a caller awaiting a popup that
            // never appeared would wait for good.
            if (!_isOpen)
            {
                return Task.FromResult(ContentDialogResult.None);
            }

            return _tsc.Task;
        }

        /// <summary>
        /// Closes the popup, and completes <see cref="ShowAsync"/> with <paramref name="result"/>.
        /// </summary>
        public void Hide(ContentDialogResult result = ContentDialogResult.None)
        {
            _result = result;
            Close(true);
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            ContentRoot = GetTemplateChild(nameof(ContentRoot)) as Border;
            TitleTextBlock = GetTemplateChild(nameof(TitleTextBlock)) as TextBlock;
            SubtitleTextBlock = GetTemplateChild(nameof(SubtitleTextBlock)) as TextBlock;
            ContentPresenter = GetTemplateChild(nameof(ContentPresenter)) as ContentPresenter;

            if (PrimaryButton != null)
            {
                PrimaryButton.Click -= OnPrimaryButtonClick;
            }

            if (SecondaryButton != null)
            {
                SecondaryButton.Click -= OnSecondaryButtonClick;
            }

            PrimaryButton = GetTemplateChild(nameof(PrimaryButton)) as Button;
            SecondaryButton = GetTemplateChild(nameof(SecondaryButton)) as Button;

            if (PrimaryButton != null)
            {
                PrimaryButton.Click += OnPrimaryButtonClick;
            }

            if (SecondaryButton != null)
            {
                SecondaryButton.Click += OnSecondaryButtonClick;
            }

            // Same elevation and the same lack of a guard as ToastPopup's: ThemeShadow arrived in
            // 18362, which is TargetPlatformMinVersion, and this is alone in its own popup.
            if (ContentRoot != null)
            {
                ContentRoot.Shadow = new ThemeShadow();
                ContentRoot.Translation = new Vector3(0, 0, 32);
            }

            // The template can be applied after the properties were set - a XAML subclass sets
            // them on itself, and the style is resolved later - so the parts catch up here.
            UpdateTitle();
            UpdateSubtitle();
            UpdateContent();
            UpdateButtons();
        }

        protected override void OnContentChanged(object oldContent, object newContent)
        {
            base.OnContentChanged(oldContent, newContent);
            UpdateContent();
        }

        private void Open()
        {
            // Captured: this becomes the popup's child and reads its root from there afterwards,
            // and everything below needs the one it was shown on.
            _xamlRoot = XamlRoot;

            if (_xamlRoot == null)
            {
                return;
            }

            _isOpen = true;

            // The first layout pass is what places and reveals it, so a popup shown a second
            // time has to wait for one again.
            _entered = false;

            if (IsLightDismissEnabled)
            {
                // A light dismiss popup underneath, the way TeachingTip did it: it is what turns
                // a click anywhere else into a dismissal.
                _lightDismiss = new Popup
                {
                    XamlRoot = _xamlRoot,
                    IsLightDismissEnabled = true,
                    // A popup needs a child to open at all.
                    Child = new Grid()
                };

                _lightDismiss.Closed += OnLightDismissed;
                _lightDismiss.IsOpen = true;
            }

            _popup = new Popup
            {
                XamlRoot = _xamlRoot,
                Child = this
            };

            // Hidden until the first layout pass gives it a size to be centred by, or it would
            // show up in the corner for a frame.
            ElementComposition.GetElementVisual(this).Opacity = 0;

            SizeChanged += OnSizeChanged;
            _xamlRoot.Changed += OnXamlRootChanged;

            _popup.IsOpen = true;

            // What tells the window a popup is up, the way ContentPopup does from
            // ShowQueuedAsync: it pauses the animations behind it, and the page under it gets to
            // put itself away. A TeachingTip never said anything, so none of that used to happen
            // for one of these. Dismiss is the other half, and the two are paired - Close returns
            // early unless Open got this far.
            if (_xamlRoot.TryGetContent(out IPopupHost host))
            {
                host.PopupOpened();
            }
        }

        private void Close(bool animate)
        {
            if (!_isOpen)
            {
                return;
            }

            _isOpen = false;

            SizeChanged -= OnSizeChanged;

            if (_xamlRoot != null)
            {
                _xamlRoot.Changed -= OnXamlRootChanged;
            }

            if (_lightDismiss != null)
            {
                _lightDismiss.Closed -= OnLightDismissed;
                _lightDismiss.IsOpen = false;
                _lightDismiss = null;
            }

            if (animate && _entered)
            {
                PlayCloseAnimation();
            }
            else
            {
                Dismiss();
            }
        }

        private void Dismiss()
        {
            if (_popup != null)
            {
                _popup.IsOpen = false;
                _popup.Child = null;
                _popup = null;
            }

            // Resolved again rather than held from Open: ContentPopup does the same, and the
            // content of a window can have been swapped while the popup was up.
            if (_xamlRoot != null && _xamlRoot.TryGetContent(out IPopupHost host))
            {
                host.PopupClosed();
            }

            Closed?.Invoke(this, null);

            _tsc?.TrySetResult(_result);
            _tsc = null;
        }

        private void OnLightDismissed(object sender, object e)
        {
            Hide();
        }

        private void OnXamlRootChanged(XamlRoot sender, XamlRootChangedEventArgs args)
        {
            PositionPopup();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            PositionPopup();

            if (!_entered)
            {
                _entered = true;
                PlayOpenAnimation();

                // Only now: the content is in the tree and measured, so there is something to
                // move focus to. ModalPopup did this from its container's Loaded.
                var focusable = FocusManager.FindFirstFocusableElement(this) as Control;
                focusable?.Focus(FocusState.Programmatic);
            }
        }

        private void PositionPopup()
        {
            if (_popup == null || _xamlRoot == null)
            {
                return;
            }

            var size = _xamlRoot.Size;

            _popup.HorizontalOffset = (size.Width - ActualWidth) / 2;
            _popup.VerticalOffset = (size.Height - ActualHeight) / 2;
        }

        private void OnPrimaryButtonClick(object sender, RoutedEventArgs e)
        {
            InvokeButton(PrimaryButtonClick, ContentDialogResult.Primary);
        }

        private void OnSecondaryButtonClick(object sender, RoutedEventArgs e)
        {
            InvokeButton(SecondaryButtonClick, ContentDialogResult.Secondary);
        }

        private void InvokeButton(TypedEventHandler<ModalPopup, ModalPopupButtonClickEventArgs> handler, ContentDialogResult result)
        {
            if (handler != null)
            {
                var args = new ModalPopupButtonClickEventArgs();
                handler(this, args);

                if (args.Cancel)
                {
                    return;
                }
            }

            Hide(result);
        }

        #region Animation

        private void PlayOpenAnimation()
        {
            var visual = ElementComposition.GetElementVisual(this);
            var compositor = visual.Compositor;

            visual.CenterPoint = new Vector3(ActualSize / 2, 0);

            var opacity = compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(0, 0);
            opacity.InsertKeyFrame(1, 1);
            opacity.Duration = Constants.FastAnimation;

            var scale = compositor.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(0, new Vector3(0.85f, 0.85f, 1));
            scale.InsertKeyFrame(1, Vector3.One);
            scale.Duration = Constants.FastAnimation;

            visual.StartAnimation("Opacity", opacity);
            visual.StartAnimation("Scale", scale);
        }

        private void PlayCloseAnimation()
        {
            var visual = ElementComposition.GetElementVisual(this);
            var compositor = visual.Compositor;

            _batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            _batch.Completed += OnCloseAnimationCompleted;

            // Re-read: the content can have grown since it opened.
            visual.CenterPoint = new Vector3(ActualSize / 2, 0);

            var opacity = compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(0, 1);
            opacity.InsertKeyFrame(1, 0);
            opacity.Duration = Constants.FastAnimation;

            var scale = compositor.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(0, Vector3.One);
            scale.InsertKeyFrame(1, new Vector3(0.85f, 0.85f, 1));
            scale.Duration = Constants.FastAnimation;

            visual.StartAnimation("Opacity", opacity);
            visual.StartAnimation("Scale", scale);

            _batch.End();
        }

        private void OnCloseAnimationCompleted(object sender, CompositionBatchCompletedEventArgs args)
        {
            if (_batch != null)
            {
                _batch.Completed -= OnCloseAnimationCompleted;
                _batch = null;
            }

            Dismiss();
        }

        #endregion

        #region Title

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register("Title", typeof(string), typeof(ModalPopup), new PropertyMetadata(null, OnTitleChanged));

        private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ModalPopup)d).UpdateTitle();
        }

        private void UpdateTitle()
        {
            var title = Title;

            // Narrator has nothing else to read a popup out by: it is not focusable, and the
            // title is the only thing every one of them has.
            AutomationProperties.SetName(this, title ?? string.Empty);

            if (TitleTextBlock != null)
            {
                TitleTextBlock.Text = title ?? string.Empty;
                TitleTextBlock.Visibility = string.IsNullOrEmpty(title)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
        }

        #endregion

        #region Subtitle

        public string Subtitle
        {
            get => (string)GetValue(SubtitleProperty);
            set => SetValue(SubtitleProperty, value);
        }

        public static readonly DependencyProperty SubtitleProperty =
            DependencyProperty.Register("Subtitle", typeof(string), typeof(ModalPopup), new PropertyMetadata(null, OnSubtitleChanged));

        private static void OnSubtitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ModalPopup)d).UpdateSubtitle();
        }

        private void UpdateSubtitle()
        {
            if (SubtitleTextBlock == null)
            {
                return;
            }

            var subtitle = Subtitle;

            // Markdown, as the ModalPopup template read it: the strings come from Android and
            // carry **bold**.
            TextBlockHelper.SetMarkdown(SubtitleTextBlock, subtitle);
            SubtitleTextBlock.Visibility = string.IsNullOrEmpty(subtitle)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        #endregion

        #region Buttons

        public object PrimaryButtonContent
        {
            get => GetValue(PrimaryButtonContentProperty);
            set => SetValue(PrimaryButtonContentProperty, value);
        }

        public static readonly DependencyProperty PrimaryButtonContentProperty =
            DependencyProperty.Register("PrimaryButtonContent", typeof(object), typeof(ModalPopup), new PropertyMetadata(null, OnButtonContentChanged));

        public object SecondaryButtonContent
        {
            get => GetValue(SecondaryButtonContentProperty);
            set => SetValue(SecondaryButtonContentProperty, value);
        }

        public static readonly DependencyProperty SecondaryButtonContentProperty =
            DependencyProperty.Register("SecondaryButtonContent", typeof(object), typeof(ModalPopup), new PropertyMetadata(null, OnButtonContentChanged));

        private static void OnButtonContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ModalPopup)d).UpdateButtons();
        }

        public Style PrimaryButtonStyle
        {
            get => (Style)GetValue(PrimaryButtonStyleProperty);
            set => SetValue(PrimaryButtonStyleProperty, value);
        }

        public static readonly DependencyProperty PrimaryButtonStyleProperty =
            DependencyProperty.Register("PrimaryButtonStyle", typeof(Style), typeof(ModalPopup), new PropertyMetadata(null));

        public Style SecondaryButtonStyle
        {
            get => (Style)GetValue(SecondaryButtonStyleProperty);
            set => SetValue(SecondaryButtonStyleProperty, value);
        }

        public static readonly DependencyProperty SecondaryButtonStyleProperty =
            DependencyProperty.Register("SecondaryButtonStyle", typeof(Style), typeof(ModalPopup), new PropertyMetadata(null));

        public bool IsPrimaryButtonEnabled
        {
            get => (bool)GetValue(IsPrimaryButtonEnabledProperty);
            set => SetValue(IsPrimaryButtonEnabledProperty, value);
        }

        public static readonly DependencyProperty IsPrimaryButtonEnabledProperty =
            DependencyProperty.Register("IsPrimaryButtonEnabled", typeof(bool), typeof(ModalPopup), new PropertyMetadata(true));

        // The gaps TeachingTip carried in its visual states, transcribed from
        // microsoft-ui-xaml, dev/TeachingTip/TeachingTip_rs1_themeresources.xaml. That is the
        // file the app resolves: XamlControlsResources defaults to ControlsResourcesVersion2,
        // and UpdateSource reads `themeresources.xaml` for Version2 - the `_v1` sibling is the
        // OLDER Version1 dictionary, and its 6 would put 12 between the buttons rather than 8.
        //
        // Applied here rather than in the template because which margin a button wants depends
        // on whether the other button is there at all.
        private static readonly Thickness ButtonPanelMargin = new Thickness(0, 12, 0, 0);  // TeachingTipButtonPanelMargin
        private static readonly Thickness LeftButtonMargin = new Thickness(0, 12, 4, 0);   // TeachingTipLeftButtonMargin
        private static readonly Thickness RightButtonMargin = new Thickness(4, 12, 0, 0);  // TeachingTipRightButtonMargin
        private static readonly Thickness ContentMargin = new Thickness(0, 12, 0, 0);      // TeachingTipMainContentPresentMargin

        private void UpdateContent()
        {
            if (ContentPresenter != null)
            {
                // TeachingTipMainContentAbsentMargin is zero: nothing there, nothing to space.
                ContentPresenter.Margin = Content != null ? ContentMargin : default;
            }
        }

        // A button with nothing to say is not in the layout at all, so one button spans the row
        // rather than sitting in half of it.
        private void UpdateButtons()
        {
            var primary = PrimaryButtonContent is string text ? text.Length > 0 : PrimaryButtonContent != null;
            var secondary = SecondaryButtonContent is string other ? other.Length > 0 : SecondaryButtonContent != null;

            if (PrimaryButton != null)
            {
                PrimaryButton.Visibility = primary ? Visibility.Visible : Visibility.Collapsed;
                Grid.SetColumnSpan(PrimaryButton, secondary ? 1 : 2);
                PrimaryButton.Margin = secondary ? LeftButtonMargin : ButtonPanelMargin;
            }

            if (SecondaryButton != null)
            {
                SecondaryButton.Visibility = secondary ? Visibility.Visible : Visibility.Collapsed;
                Grid.SetColumn(SecondaryButton, primary ? 1 : 0);
                Grid.SetColumnSpan(SecondaryButton, primary ? 1 : 2);
                SecondaryButton.Margin = primary ? RightButtonMargin : ButtonPanelMargin;
            }
        }

        #endregion
    }
}
