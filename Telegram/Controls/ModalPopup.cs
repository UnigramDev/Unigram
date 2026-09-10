//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
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
using Windows.UI.Xaml.Automation;
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

    /// <summary>
    /// The deferral that <see cref="ContentDialog"/>'s event args carry, on args of our own: the
    /// framework types are sealed and cannot be raised from outside it.
    /// </summary>
    public abstract partial class ModalPopupDeferrableEventArgs
    {
        private int _pending;
        private TaskCompletionSource<object> _completed;

        public Deferral GetDeferral()
        {
            Interlocked.Increment(ref _pending);
            return new Deferral(OnDeferralCompleted);
        }

        private void OnDeferralCompleted()
        {
            if (Interlocked.Decrement(ref _pending) == 0)
            {
                _completed?.TrySetResult(null);
            }
        }

        /// <summary>
        /// Completes once every deferral a handler took has been completed.
        /// </summary>
        internal Task WaitAsync()
        {
            if (Volatile.Read(ref _pending) == 0)
            {
                return Task.CompletedTask;
            }

            _completed = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            // A deferral completed between the two reads found no source to complete.
            if (Volatile.Read(ref _pending) == 0)
            {
                _completed.TrySetResult(null);
            }

            return _completed.Task;
        }
    }

    /// <summary>
    /// What a button did to the popup: nothing, unless the handler cancels the close.
    /// </summary>
    public partial class ModalPopupButtonClickEventArgs : ModalPopupDeferrableEventArgs
    {
        /// <summary>
        /// Leaves the popup open, for a handler that has something to say about the input first.
        /// </summary>
        public bool Cancel { get; set; }
    }

    public partial class ModalPopupOpenedEventArgs
    {
    }

    public partial class ModalPopupClosingEventArgs : ModalPopupDeferrableEventArgs
    {
        public ModalPopupClosingEventArgs(ContentDialogResult result)
        {
            Result = result;
        }

        public ContentDialogResult Result { get; }

        public bool Cancel { get; set; }
    }

    public partial class ModalPopupClosedEventArgs
    {
        public ModalPopupClosedEventArgs(ContentDialogResult result)
        {
            Result = result;
        }

        public ContentDialogResult Result { get; }
    }

    /// <summary>
    /// The one queue <see cref="ContentPopup.ShowQueuedAsync"/> and
    /// <see cref="ModalPopup.ShowQueuedAsync"/> share, so that the two kinds wait for each other
    /// while callers are moved from one to the other.
    /// </summary>
    internal static class PopupQueue
    {
        private static readonly ConditionalWeakTable<XamlRoot, TaskCompletionSource<ContentDialogResult>> _requests = new();

        public static async Task WaitAsync(XamlRoot xamlRoot)
        {
            while (_requests.TryGetValue(xamlRoot, out var tsc))
            {
                await tsc.Task;
            }
        }

        public static void Enqueue(XamlRoot xamlRoot, TaskCompletionSource<ContentDialogResult> tsc)
        {
            _requests.AddOrUpdate(xamlRoot, tsc);
        }

        public static void Dequeue(XamlRoot xamlRoot)
        {
            _requests.Remove(xamlRoot);
        }
    }

    /// <summary>
    /// A title, a body and up to three buttons, in a <see cref="Popup"/> of its own, centred on
    /// the window and dismissable by clicking away from it.
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
    /// Shaped after <see cref="ContentPopup"/> - the three buttons, the deferrals, the sizing
    /// properties, the navigation contract, a <see cref="ContentDialogResult"/>, ShowQueuedAsync
    /// and Hide - so that it can take that over as well, and a caller moving between the two has
    /// nothing to relearn.
    ///
    /// The popup fills the window rather than being centred by offsets: the smoke, the pinned
    /// button row and keeping focus inside all want a root that owns the whole surface.
    /// </remarks>
    [TemplatePart(Name = "LayoutRoot", Type = typeof(Grid))]
    [TemplatePart(Name = "SmokeElement", Type = typeof(Border))]
    [TemplatePart(Name = "LightDismiss", Type = typeof(Rectangle))]
    [TemplatePart(Name = "CardRoot", Type = typeof(Grid))]
    [TemplatePart(Name = "ShadowCaster", Type = typeof(Rectangle))]
    [TemplatePart(Name = "ClipRoot", Type = typeof(Grid))]
    [TemplatePart(Name = "BackgroundElement", Type = typeof(Border))]
    [TemplatePart(Name = "ContentRoot", Type = typeof(Border))]
    [TemplatePart(Name = "TitlePresenter", Type = typeof(ContentPresenter))]
    [TemplatePart(Name = "SubtitleTextBlock", Type = typeof(TextBlock))]
    [TemplatePart(Name = "ContentPresenter", Type = typeof(ContentPresenter))]
    [TemplatePart(Name = "CommandSpace", Type = typeof(Grid))]
    [TemplatePart(Name = "PrimaryRoot", Type = typeof(Grid))]
    [TemplatePart(Name = "PrimaryButton", Type = typeof(Button))]
    [TemplatePart(Name = "PrimaryButtonPending", Type = typeof(Microsoft.UI.Xaml.Controls.ProgressRing))]
    [TemplatePart(Name = "PrimarySplitButton", Type = typeof(Button))]
    [TemplatePart(Name = "SecondaryButton", Type = typeof(Button))]
    [TemplatePart(Name = "CloseButton", Type = typeof(Button))]
    [TemplatePart(Name = "DismissButton", Type = typeof(Button))]
    public partial class ModalPopup : ContentControl
    {
        private Grid LayoutRoot;
        private Border SmokeElement;
        private Rectangle LightDismiss;
        private Grid CardRoot;
        private Rectangle ShadowCaster;
        private Grid ClipRoot;
        private Border BackgroundElement;
        private Border ContentRoot;
        private ContentPresenter TitlePresenter;
        private TextBlock SubtitleTextBlock;
        private ContentPresenter ContentPresenter;
        private Grid CommandSpace;
        private Grid PrimaryRoot;
        private Button PrimaryButton;
        private Microsoft.UI.Xaml.Controls.ProgressRing PrimaryButtonPending;
        private Button PrimarySplitButton;
        private Button SecondaryButton;
        private Button CloseButton;
        private Button DismissButton;

        private XamlRoot _xamlRoot;
        private Popup _popup;
        private WeakReference _focusedBefore;
        private CompositionScopedBatch _batch;
        private CompositionScopedBatch _resizeBatch;

        // The card is drawn rather than laid out: a shape whose geometry is animated keeps the
        // corner radius and the stroke width exact at every size, where a scaled Border squashes
        // both. _clipGeometry is the outer rounded rect, and rounds the content off at rest as
        // well as hiding what a growing card has not reached yet.
        private CompositionRoundedRectangleGeometry _clipGeometry;
        private CompositionRoundedRectangleGeometry _cardGeometry;
        private ShapeVisual _cardVisual;
        private CompositionColorSource _fillBrush;
        private CompositionColorSource _strokeBrush;

        private long _fillToken;
        private long _strokeToken;

        private TaskCompletionSource<ContentDialogResult> _tsc;

        // Two results, as ContentPopup has them: _closingResult is whatever closed the popup and
        // is what ShowAsync completes with, _result is what a subclass named through SetResult
        // and is what OpenAsync returns.
        private ContentDialogResult _closingResult = ContentDialogResult.None;
        private ContentDialogResult _result = ContentDialogResult.None;

        private bool _isOpen;
        private bool _isClosing;
        private bool _themeApplied;
        private bool _entered;

        // Set while a button handler runs, so that a handler hiding with its own result closes
        // rather than asking for the same button again.
        private bool _raising;

        public ModalPopup()
        {
            DefaultStyleKey = typeof(ModalPopup);
        }

        /// <summary>
        /// Whether a click away from the popup takes it down.
        /// </summary>
        public bool IsLightDismissEnabled { get; set; } = true;

        /// <summary>
        /// Whether the window behind the popup is dimmed.
        /// </summary>
        public bool IsSmokeEnabled
        {
            get => _isSmokeEnabled;
            set
            {
                _isSmokeEnabled = value;
                UpdateSmoke();
            }
        }

        private bool _isSmokeEnabled = true;

        /// <summary>
        /// Whether the primary button takes focus once the popup is up, rather than whatever the
        /// content offers first.
        /// </summary>
        public bool FocusPrimaryButton { get; set; } = true;

        /// <summary>
        /// Cleared by a popup that hides itself only to come back, so that the navigation service
        /// does not tear its view model down. Terrible name, inherited from ContentPopup.
        /// </summary>
        public bool IsFinalized { get; set; } = true;

        public bool IsOpen => _isOpen;

        public event TypedEventHandler<ModalPopup, ModalPopupOpenedEventArgs> Opened;

        /// <summary>
        /// Raised before the popup starts closing, and able to stop it.
        /// </summary>
        public event TypedEventHandler<ModalPopup, ModalPopupClosingEventArgs> Closing;

        /// <summary>
        /// Raised once the popup is off the screen, animation included.
        /// </summary>
        public event TypedEventHandler<ModalPopup, ModalPopupClosedEventArgs> Closed;

        public event TypedEventHandler<ModalPopup, ModalPopupButtonClickEventArgs> PrimaryButtonClick;
        public event TypedEventHandler<ModalPopup, ModalPopupButtonClickEventArgs> SecondaryButtonClick;
        public event TypedEventHandler<ModalPopup, ModalPopupButtonClickEventArgs> CloseButtonClick;

        #region Show and hide

        public virtual void OnCreate()
        {

        }

        public virtual void OnNavigatedTo(object parameter)
        {

        }

        public virtual void OnNavigatedFrom()
        {

        }

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
            ApplyTheme(xamlRoot);

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
        /// Opens the popup once whatever is already queued on the same window has closed, the way
        /// <see cref="ContentPopup.ShowQueuedAsync"/> does, and against the same queue.
        /// </summary>
        public async Task<ContentDialogResult> ShowQueuedAsync(XamlRoot xamlRoot)
        {
            if (xamlRoot == null)
            {
                return ContentDialogResult.None;
            }

            await PopupQueue.WaitAsync(xamlRoot);

            Logger.Info(GetType().Name);

            // Before OnCreate, as ContentPopup does it, so a subclass building its content already
            // has the theme it will be shown in. ShowAsync would otherwise do it a moment later.
            ApplyTheme(xamlRoot);

            OnCreate();

            var queued = new TaskCompletionSource<ContentDialogResult>();
            PopupQueue.Enqueue(xamlRoot, queued);

            var result = await ShowAsync(xamlRoot);

            // Removed before the waiters are woken: they loop on the table, and a completed task
            // still in it spins.
            PopupQueue.Dequeue(xamlRoot);
            queued.TrySetResult(result);

            return result;
        }

        /// <summary>
        /// Opens the popup and completes with the result <see cref="SetResult"/> was given,
        /// rather than the one that closed it.
        /// </summary>
        public async Task<ContentDialogResult> OpenAsync(XamlRoot xamlRoot)
        {
            await ShowQueuedAsync(xamlRoot);
            return _result;
        }

        /// <summary>
        /// Resolves the popup's theme against the window it opens on, and forwards that window's
        /// chat brushes into it. Popups hang off the PopupRoot, a sibling of the window's content,
        /// so neither reaches them by inheritance.
        /// </summary>
        /// <remarks>
        /// Once only, whichever way the popup was shown: ApplyChatTheme adds a merged dictionary
        /// rather than setting one.
        /// </remarks>
        private void ApplyTheme(XamlRoot xamlRoot)
        {
            if (_themeApplied)
            {
                return;
            }

            _themeApplied = true;

            if (RequestedTheme == ElementTheme.Default && xamlRoot.TryGetContent(out FrameworkElement element))
            {
                var app = BootStrapper.Current.RequestedTheme == ApplicationTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;
                if (app != element.RequestedTheme)
                {
                    RequestedTheme = NightModeService.Current.GetCalculatedElementTheme();
                }
            }

            this.ApplyChatTheme(xamlRoot);
        }

        protected void SetResult(ContentDialogResult result)
        {
            _result = result;
        }

        public void Close()
        {
            Hide();
        }

        /// <summary>
        /// Closes the popup, and completes <see cref="ShowAsync"/> with
        /// <see cref="ContentDialogResult.None"/>.
        /// </summary>
        public void Hide()
        {
            HideCore(ContentDialogResult.None);
        }

        /// <summary>
        /// Closes the popup with <paramref name="result"/>, running the matching button's handler
        /// on the way out - so that hiding with Primary gets the validation a click would have
        /// got. A button that is present but disabled refuses, as it does in ContentPopup.
        /// </summary>
        public void Hide(ContentDialogResult result)
        {
            _result = result;

            if (!_raising)
            {
                if (result == ContentDialogResult.Primary && HasContent(PrimaryButtonContent))
                {
                    InvokeButton(ContentDialogButton.Primary);
                    return;
                }
                else if (result == ContentDialogResult.Secondary && HasContent(SecondaryButtonContent))
                {
                    InvokeButton(ContentDialogButton.Secondary);
                    return;
                }
            }

            HideCore(result);
        }

        // async void rather than a discarded Task: a handler that throws must still reach the
        // UI thread's unhandled hook, the way it did when the Click event ran it.
        private async void HideCore(ContentDialogResult result)
        {
            await HideCoreAsync(result);
        }

        private async Task HideCoreAsync(ContentDialogResult result)
        {
            if (!_isOpen || _isClosing)
            {
                return;
            }

            if (Closing != null)
            {
                var args = new ModalPopupClosingEventArgs(result);

                _isClosing = true;
                Closing(this, args);
                await args.WaitAsync();
                _isClosing = false;

                if (args.Cancel || !_isOpen)
                {
                    return;
                }
            }

            _closingResult = result;
            Close(true);
        }

        #endregion

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (PrimaryButton != null)
            {
                PrimaryButton.Click -= OnPrimaryButtonClick;
            }

            if (SecondaryButton != null)
            {
                SecondaryButton.Click -= OnSecondaryButtonClick;
            }

            if (CloseButton != null)
            {
                CloseButton.Click -= OnCloseButtonClick;
            }

            if (DismissButton != null)
            {
                DismissButton.Click -= OnDismissButtonClick;
                DismissButton = null;
            }

            if (LightDismiss != null)
            {
                LightDismiss.PointerReleased -= OnLightDismissed;
            }

            if (LayoutRoot != null)
            {
                LayoutRoot.ProcessKeyboardAccelerators -= OnProcessKeyboardAccelerators;
            }

            if (ContentRoot != null)
            {
                ContentRoot.SizeChanged -= OnContentRootSizeChanged;
            }

            LayoutRoot = GetTemplateChild(nameof(LayoutRoot)) as Grid;
            SmokeElement = GetTemplateChild(nameof(SmokeElement)) as Border;
            LightDismiss = GetTemplateChild(nameof(LightDismiss)) as Rectangle;
            CardRoot = GetTemplateChild(nameof(CardRoot)) as Grid;
            ShadowCaster = GetTemplateChild(nameof(ShadowCaster)) as Rectangle;
            ClipRoot = GetTemplateChild(nameof(ClipRoot)) as Grid;

            // All of it belongs to the parts that were just replaced.
            _clipGeometry = null;
            _cardGeometry = null;
            _cardVisual = null;
            BackgroundElement = GetTemplateChild(nameof(BackgroundElement)) as Border;
            ContentRoot = GetTemplateChild(nameof(ContentRoot)) as Border;
            TitlePresenter = GetTemplateChild(nameof(TitlePresenter)) as ContentPresenter;
            SubtitleTextBlock = GetTemplateChild(nameof(SubtitleTextBlock)) as TextBlock;
            ContentPresenter = GetTemplateChild(nameof(ContentPresenter)) as ContentPresenter;
            CommandSpace = GetTemplateChild(nameof(CommandSpace)) as Grid;
            PrimaryRoot = GetTemplateChild(nameof(PrimaryRoot)) as Grid;
            PrimaryButton = GetTemplateChild(nameof(PrimaryButton)) as Button;
            PrimaryButtonPending = GetTemplateChild(nameof(PrimaryButtonPending)) as Microsoft.UI.Xaml.Controls.ProgressRing;
            PrimarySplitButton = GetTemplateChild(nameof(PrimarySplitButton)) as Button;
            SecondaryButton = GetTemplateChild(nameof(SecondaryButton)) as Button;
            CloseButton = GetTemplateChild(nameof(CloseButton)) as Button;

            if (PrimaryButton != null)
            {
                PrimaryButton.Click += OnPrimaryButtonClick;
            }

            if (SecondaryButton != null)
            {
                SecondaryButton.Click += OnSecondaryButtonClick;
            }

            if (CloseButton != null)
            {
                CloseButton.Click += OnCloseButtonClick;
            }

            if (LightDismiss != null)
            {
                LightDismiss.PointerReleased += OnLightDismissed;
            }

            // The split button's own click is the caller's business - it grabs the part by name -
            // so only its visibility is ours.
            PrimaryRoot?.CreateInsetClip();

            if (IsDismissButtonVisible || DismissButtonRequestedTheme != ElementTheme.Default)
            {
                UpdateDismissButton();
            }

            if (LayoutRoot != null)
            {
                LayoutRoot.ProcessKeyboardAccelerators += OnProcessKeyboardAccelerators;
            }

            if (ContentRoot != null)
            {
                ContentRoot.SizeChanged += OnContentRootSizeChanged;
            }

            // Same elevation and the same lack of a guard as ToastPopup's: ThemeShadow arrived in
            // 18362, which is TargetPlatformMinVersion, and this is alone in its own popup. It
            // goes on the caster and never on ContentRoot - a ThemeShadow set on an element
            // breaks the hit test of every child it has.
            if (ShadowCaster != null)
            {
                ShadowCaster.RadiusX = ShadowCaster.RadiusY = CornerRadius.TopLeft;
                ShadowCaster.Shadow = new ThemeShadow();
                ShadowCaster.Translation = new Vector3(0, 0, 32);
            }

            // The card is painted from these, so a theme that swaps either brush has to reach
            // the composition side too.
            this.RegisterPropertyChangedCallback(BackgroundProperty, OnCardBrushChanged, ref _fillToken);
            this.RegisterPropertyChangedCallback(BorderBrushProperty, OnCardBrushChanged, ref _strokeToken);

            // The template can be applied after the properties were set - a XAML subclass sets
            // them on itself, and the style is resolved later - so the parts catch up here.
            UpdateSmoke();
            UpdateTitle();
            UpdateSubtitle();
            UpdateButtons();
            UpdateSplitButton();
        }

        #region Hosting

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

            // Saved before the popup takes focus and given back on the way out, as
            // ContentDialog::SetInitialFocusElement does: a dialog that swallows the caret is the
            // loudest accessibility failure there is. Weak, because the element can be gone by
            // the time the popup closes - a page swapped, a container recycled.
            _focusedBefore = FocusManagerEx.TryGetFocusedElement(_xamlRoot) is DependencyObject focused
                ? new WeakReference(focused)
                : null;

            // The first layout pass is what reveals it, so a popup shown a second time has to
            // wait for one again.
            _entered = false;

            _popup = new Popup
            {
                XamlRoot = _xamlRoot,
                Child = this
            };

            // What makes Narrator treat this as a dialog: it announces the popup by name on
            // entry, reads the contents, and stops walking what is behind. On the popup rather
            // than on the control, which is where ContentDialog puts it too - see
            // EnsurePopupAndSmokeLayer in ContentDialog_Partial.cpp.
            AutomationProperties.SetIsDialog(_popup, true);
            UpdateAutomationName();

            // Hidden until the first layout pass has placed the card, or it would show up
            // unsized for a frame.
            ElementComposition.GetElementVisual(this).Opacity = 0;

            StretchToWindow();

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

            // The theme brushes outlive the popup, and these hold callbacks on them.
            _fillBrush?.Unregister();
            _strokeBrush?.Unregister();

            // Resolved again rather than held from Open: ContentPopup does the same, and the
            // content of a window can have been swapped while the popup was up.
            if (_xamlRoot != null && _xamlRoot.TryGetContent(out IPopupHost host))
            {
                host.PopupClosed();
            }

            if (_focusedBefore?.Target is Control control)
            {
                control.Focus(FocusState.Programmatic);
            }

            _focusedBefore = null;

            Closed?.Invoke(this, new ModalPopupClosedEventArgs(_closingResult));

            _tsc?.TrySetResult(_closingResult);
            _tsc = null;
        }

        /// <summary>
        /// The popup child is the window: the card is centred by layout inside it, which is what
        /// gives the smoke somewhere to live and keeps the pointer off whatever is behind.
        /// </summary>
        private void StretchToWindow()
        {
            if (_xamlRoot == null)
            {
                return;
            }

            var size = _xamlRoot.Size;

            Width = size.Width;
            Height = size.Height;
        }

        private void OnXamlRootChanged(XamlRoot sender, XamlRootChangedEventArgs args)
        {
            StretchToWindow();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_entered)
            {
                return;
            }

            _entered = true;
            PlayOpenAnimation();

            Opened?.Invoke(this, new ModalPopupOpenedEventArgs());

            // Only now: the content is in the tree and measured, so there is something to move
            // focus to.
            MoveFocus();
        }

        private void MoveFocus()
        {
            // A content control that focused itself from its own Loaded keeps it: several of
            // these open on a text field, and Loaded and the first layout pass race.
            if (ContainsFocus())
            {
                return;
            }

            if (FocusPrimaryButton && PrimaryButton != null && PrimaryButton.Visibility == Visibility.Visible)
            {
                if (PrimaryButton.Focus(FocusState.Keyboard))
                {
                    return;
                }
            }

            if (FocusManager.FindFirstFocusableElement(this) is Control focusable && focusable.Focus(FocusState.Programmatic))
            {
                return;
            }

            Focus(FocusState.Programmatic);
        }

        private bool ContainsFocus()
        {
            var focused = FocusManagerEx.TryGetFocusedElement(_xamlRoot) as DependencyObject;

            while (focused != null)
            {
                if (focused == this)
                {
                    return true;
                }

                focused = VisualTreeHelper.GetParent(focused);
            }

            return false;
        }

        private void OnLightDismissed(object sender, PointerRoutedEventArgs e)
        {
            var pointer = e.GetCurrentPoint(this);
            if (pointer.Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonReleased && IsLightDismissEnabled)
            {
                Hide();
            }
        }

        private void OnProcessKeyboardAccelerators(UIElement sender, ProcessKeyboardAcceleratorEventArgs args)
        {
            if (args.Modifiers != VirtualKeyModifiers.None)
            {
                return;
            }

            // Enter confirms, as it does in a ContentPopup: ContentDialog invokes the default
            // button itself, and ContentPopup adds the same for when that button is not the
            // primary one, so between them Enter always presses primary. DefaultButton is not
            // consulted - callers set it for the accent colour, not for this.
            if (args.Key == VirtualKey.Enter)
            {
                // A control that makes its own use of Enter keeps it.
                var focused = FocusManagerEx.TryGetFocusedElement(XamlRoot);
                if (DefaultButton == ContentDialogButton.Primary
                    || focused is null or (not TextBox and not RichEditBox and not Button and not MenuFlyoutItem))
                {
                    InvokeButton(ContentDialogButton.Primary);
                    args.Handled = true;
                }
            }
            else if (args.Key == VirtualKey.Escape)
            {
                args.Handled = CancelRequested();
            }
        }

        /// <summary>
        /// What Escape and the back gesture do. Called by <see cref="WindowContext"/> too, for the
        /// windows that take the key before an accelerator scope can see it.
        /// </summary>
        /// <remarks>
        /// ContentDialog::ExecuteCloseAction: the close button if it has something to say and is
        /// enabled, a plain None close otherwise - and it always closes, and always reports the
        /// key handled. <see cref="IsLightDismissEnabled"/> has no say here; it governs a click
        /// outside the card and nothing else, exactly as it does on a ContentPopup. A popup that
        /// must not go away this way cancels <see cref="Closing"/>, the way Block does.
        /// </remarks>
        public bool CancelRequested()
        {
            if (!_isOpen)
            {
                return false;
            }

            if (HasContent(CloseButtonContent) && CloseButton?.IsEnabled != false)
            {
                InvokeButton(ContentDialogButton.Close);
            }
            else
            {
                Hide();
            }

            return true;
        }

        /// <summary>
        /// A spinner that owns the window until the caller clears <see cref="FrameworkElement.Tag"/>
        /// and hides it. ContentPopup's version leaves hit testing off and relies on the dialog's
        /// own smoke to block; here the smoke is inside the popup, so it stays hit-testable.
        /// </summary>
        public static ModalPopup Block(XamlRoot xamlRoot)
        {
            var content = new Grid();
            content.Children.Add(new Microsoft.UI.Xaml.Controls.ProgressRing
            {
                Width = 48,
                Height = 48,
                Margin = new Thickness(12)
            });

            var popup = new ModalPopup
            {
                Content = content,
                IsLightDismissEnabled = false,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                ContentMinWidth = 0,
                ContentMinHeight = 0,
                Padding = new Thickness(0),
                RequestedTheme = ElementTheme.Dark,
                IsEnabled = false,
                Tag = new object()
            };

            popup.Closing += OnBlockedClosing;

            _ = popup.ShowQueuedAsync(xamlRoot);
            return popup;
        }

        private static void OnBlockedClosing(ModalPopup sender, ModalPopupClosingEventArgs args)
        {
            args.Cancel = sender.Tag != null;
        }

        public static bool IsAnyPopupOpen(XamlRoot xamlRoot)
        {
            if (xamlRoot == null)
            {
                return false;
            }

            foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(xamlRoot))
            {
                if (popup.Child is ContentDialog or ModalPopup)
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Animation

        // ContentDialogOpenCloseThemeTransition, which is what actually animates a ContentDialog -
        // the storyboards in its template's DialogShowingStates never run. It is built in code, in
        // ContentDialogOpenCloseThemeTransition::CreateStoryboardImpl (LayoutTransition_partial.cpp),
        // and these are its numbers: the dialog settles INWARD from 1.05 rather than growing into
        // place, on a cubic-bezier(0, 0, 0, 1), while opacity crosses linearly in a third of the time.
        private static readonly TimeSpan OpenScaleDuration = TimeSpan.FromMilliseconds(250);   // s_OpenScaleDuration
        private static readonly TimeSpan CloseScaleDuration = TimeSpan.FromMilliseconds(167);  // s_CloseScaleDuration
        private static readonly TimeSpan OpacityChangeDuration = TimeSpan.FromMilliseconds(83); // s_OpacityChangeDuration

        private const float ScaledFactor = 1.05f;

        private void PlayOpenAnimation()
        {
            // The smoke fades with the root, the card scales on its own: scaling the root would
            // scale the dimming with it. ContentDialog animates the two separately for the same
            // reason, and gives both the same linear fade.
            FrameworkElement element = CardRoot ?? (FrameworkElement)this;

            var visual = ElementComposition.GetElementVisual(this);
            var card = ElementComposition.GetElementVisual(element);
            var compositor = visual.Compositor;

            card.CenterPoint = new Vector3(element.ActualSize / 2, 0);

            var opacity = compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(0, 0);
            opacity.InsertKeyFrame(1, 1, compositor.CreateLinearEasingFunction());
            opacity.Duration = OpacityChangeDuration;

            var scale = compositor.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(0, new Vector3(ScaledFactor, ScaledFactor, 1));
            scale.InsertKeyFrame(1, Vector3.One, CreateSettleEasing(compositor));
            scale.Duration = OpenScaleDuration;

            visual.StartAnimation("Opacity", opacity);
            card.StartAnimation("Scale", scale);
        }

        private void PlayCloseAnimation()
        {
            FrameworkElement element = CardRoot ?? (FrameworkElement)this;

            var visual = ElementComposition.GetElementVisual(this);
            var card = ElementComposition.GetElementVisual(element);
            var compositor = visual.Compositor;

            _batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            _batch.Completed += OnCloseAnimationCompleted;

            // Re-read: the content can have grown since it opened.
            card.CenterPoint = new Vector3(element.ActualSize / 2, 0);

            var opacity = compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(0, 1);
            opacity.InsertKeyFrame(1, 0, compositor.CreateLinearEasingFunction());
            opacity.Duration = OpacityChangeDuration;

            var scale = compositor.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(0, Vector3.One);
            scale.InsertKeyFrame(1, new Vector3(ScaledFactor, ScaledFactor, 1), CreateSettleEasing(compositor));
            scale.Duration = CloseScaleDuration;

            visual.StartAnimation("Opacity", opacity);
            card.StartAnimation("Scale", scale);

            _batch.End();
        }

        // cubic-bezier(0, 0, 0, 1): the easing CreateStoryboardImpl builds by taking a linear
        // TimingFunctionDescription and moving cp3.X to zero.
        private static CompositionEasingFunction CreateSettleEasing(Compositor compositor)
        {
            return compositor.CreateCubicBezierEasingFunction(new Vector2(0, 0), new Vector2(0, 1));
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

        private void OnContentRootSizeChanged(object sender, SizeChangedEventArgs e)
        {
            var previous = e.PreviousSize.ToVector2();
            var next = e.NewSize.ToVector2();

            // The resting shape, every time. PlayResizeAnimation takes it from here when there is
            // something to animate, and an assignment stops whatever animation is on it.
            EnsureCardGeometry();
            UpdateCardGeometry(next, next.Y);

            // Nothing to grow from on the way in, nothing to grow to on the way out, and a card
            // told to fill the window never changes height by itself. ContentPopup guards the
            // same three.
            if (previous.Y == 0 || next.Y == 0 || previous.Y == next.Y
                || VerticalContentAlignment == VerticalAlignment.Stretch)
            {
                return;
            }

            PlayResizeAnimation(previous, next);
        }

        /// <summary>
        /// Builds the card: one rounded rectangle painted by a shape, and a second one used as a
        /// clip. Both outlive the animation, because they are also what the card looks like at rest.
        /// </summary>
        private void EnsureCardGeometry()
        {
            if (_cardVisual != null || ClipRoot == null || BackgroundElement == null)
            {
                return;
            }

            var visual = ElementComposition.GetElementVisual(ClipRoot);
            var compositor = visual.Compositor;

            _clipGeometry = compositor.CreateRoundedRectangleGeometry();
            visual.Clip = compositor.CreateGeometricClip(_clipGeometry);

            _cardGeometry = compositor.CreateRoundedRectangleGeometry();

            _fillBrush = new CompositionColorSource(Background, true);
            _strokeBrush = new CompositionColorSource(BorderBrush, true);

            var thickness = (float)BorderThickness.Top;

            // A composition stroke straddles its path, so it is drawn at twice the width and the
            // fill goes over the inner half of it - which leaves exactly the outer half showing,
            // one thickness wide, sitting on the card's edge like a Border's would.
            var stroke = compositor.CreateSpriteShape(_cardGeometry);
            stroke.StrokeBrush = _strokeBrush;
            stroke.StrokeThickness = thickness * 2;

            var fill = compositor.CreateSpriteShape(_cardGeometry);
            fill.FillBrush = _fillBrush;
            fill.StrokeBrush = compositor.CreateColorBrush(Colors.Transparent);
            fill.StrokeThickness = thickness * 2;

            _cardVisual = compositor.CreateShapeVisual();
            _cardVisual.Shapes.Add(stroke);
            _cardVisual.Shapes.Add(fill);

            ElementCompositionPreview.SetElementChildVisual(BackgroundElement, _cardVisual);
        }

        private void OnCardBrushChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (dp == BackgroundProperty)
            {
                _fillBrush?.PropertyChanged(Background as SolidColorBrush, true);
            }
            else
            {
                _strokeBrush?.PropertyChanged(BorderBrush as SolidColorBrush, true);
            }
        }

        /// <summary>
        /// Sizes the painted card and the clip to <paramref name="size"/>. The card's path is
        /// inset by a whole stroke and drawn twice that wide, so that half of it lands back on
        /// the card's edge - see EnsureCardGeometry for why the stroke is built that way.
        /// </summary>
        private void UpdateCardGeometry(Vector2 size, float extent)
        {
            if (_cardVisual == null)
            {
                return;
            }

            var thickness = (float)BorderThickness.Top;
            var radius = (float)CornerRadius.TopLeft;

            _clipGeometry.CornerRadius = new Vector2(radius, radius);
            _clipGeometry.Size = size;

            _cardGeometry.Offset = new Vector2(thickness, thickness);
            _cardGeometry.CornerRadius = new Vector2(radius, radius);
            _cardGeometry.Size = new Vector2(size.X - thickness * 2, size.Y - thickness * 2);

            // A ShapeVisual clips to its own Size, and the card is drawn beyond the element's
            // bounds while a shrink is running.
            _cardVisual.Size = new Vector2(size.X, extent);
        }

        /// <summary>
        /// Carries the card from one height to the next when its content changes size.
        /// </summary>
        /// <remarks>
        /// The card is DRAWN, not laid out and not scaled: a shape whose rounded-rectangle
        /// geometry is animated is exact at every size, where scaling a Border turns its corners
        /// into ellipses and its stroke into an uneven one. That also lets it paint outside the
        /// element's own bounds, which is what a shrink needs - a clip cannot do it, because a
        /// clip only ever hides what is already laid out. ContentPopup scales instead, and pays
        /// for it in squashed corners plus an offscreen surface for the command row it redirects.
        ///
        /// Two rectangles animate together: the painted one, whose path is inset by a stroke's
        /// width, and the clip on ClipRoot, which hides the content a growing card has not reached
        /// yet and rounds it off at rest.
        ///
        /// The shadow is scaled rather than clipped - a clip does not reach a cast shadow, which
        /// is why it kept the new size when this was clip-only. The command row is a real element
        /// and only needs translating, so it stays interactive throughout.
        /// </remarks>
        private void PlayResizeAnimation(Vector2 previous, Vector2 next)
        {
            if (CardRoot == null || BackgroundElement == null || _cardVisual == null)
            {
                return;
            }

            // A second resize landing mid-animation must not let the first batch complete and
            // undo what the second one just set up.
            if (_resizeBatch != null)
            {
                _resizeBatch.Completed -= OnResizeAnimationCompleted;
                _resizeBatch = null;
            }

            var compositor = ElementComposition.GetElementVisual(ClipRoot).Compositor;

            var delta = next.Y - previous.Y;
            var thickness = (float)BorderThickness.Top;

            // Both rectangles have to be able to draw beyond the element's own bounds, which is
            // what a shrink needs and what no clip can give.
            UpdateCardGeometry(next, Math.Max(previous.Y, next.Y));

            var clip = compositor.CreateVector2KeyFrameAnimation();
            clip.InsertKeyFrame(0, new Vector2(next.X, previous.Y));
            clip.InsertKeyFrame(1, next);
            clip.Duration = Constants.FastAnimation;

            var card = compositor.CreateVector2KeyFrameAnimation();
            card.InsertKeyFrame(0, new Vector2(next.X - thickness * 2, previous.Y - thickness * 2));
            card.InsertKeyFrame(1, new Vector2(next.X - thickness * 2, next.Y - thickness * 2));
            card.Duration = Constants.FastAnimation;

            // The shadow is scaled and not clipped: a clip does not reach a cast shadow.
            var scale = compositor.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(0, new Vector3(1, previous.Y / next.Y, 1));
            scale.InsertKeyFrame(1, Vector3.One);
            scale.Duration = Constants.FastAnimation;

            // The card is centred, so growing by delta moves its top up by half of that. This
            // puts it back where it was and lets it drift into place.
            var translate = compositor.CreateScalarKeyFrameAnimation();
            translate.InsertKeyFrame(0, delta / 2);
            translate.InsertKeyFrame(1, 0);
            translate.Duration = Constants.FastAnimation;

            _resizeBatch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            _resizeBatch.Completed += OnResizeAnimationCompleted;

            _clipGeometry.StartAnimation("Size", clip);
            _cardGeometry.StartAnimation("Size", card);

            // From the top edge, which is the one that stays put once CardRoot is translated.
            if (ShadowCaster != null)
            {
                var shadow = ElementComposition.GetElementVisual(ShadowCaster);
                shadow.CenterPoint = Vector3.Zero;
                shadow.StartAnimation("Scale", scale);
            }

            ElementCompositionPreview.SetIsTranslationEnabled(CardRoot, true);
            ElementComposition.GetElementVisual(CardRoot).StartAnimation("Translation.Y", translate);

            // The buttons ride the bottom edge rather than waiting behind the clip for it.
            if (CommandSpace != null && CommandSpace.Visibility == Visibility.Visible)
            {
                var command = compositor.CreateScalarKeyFrameAnimation();
                command.InsertKeyFrame(0, -delta);
                command.InsertKeyFrame(1, 0);
                command.Duration = Constants.FastAnimation;

                ElementCompositionPreview.SetIsTranslationEnabled(CommandSpace, true);
                ElementComposition.GetElementVisual(CommandSpace).StartAnimation("Translation.Y", command);
            }

            _resizeBatch.End();
        }

        private void OnResizeAnimationCompleted(object sender, CompositionBatchCompletedEventArgs args)
        {
            if (_resizeBatch != null)
            {
                _resizeBatch.Completed -= OnResizeAnimationCompleted;
                _resizeBatch = null;
            }

            // The card and its clip stay - they are what it looks like at rest - but they have to
            // settle on the size it actually ended at.
            if (ContentRoot != null)
            {
                UpdateCardGeometry(ContentRoot.ActualSize, ContentRoot.ActualSize.Y);
            }
        }

        #endregion

        #region Smoke

        private void UpdateSmoke()
        {
            if (SmokeElement != null)
            {
                SmokeElement.Visibility = _isSmokeEnabled
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        #endregion

        #region Title

        public object Title
        {
            get => GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register("Title", typeof(object), typeof(ModalPopup), new PropertyMetadata(null, OnTitleChanged));

        public DataTemplate TitleTemplate
        {
            get => (DataTemplate)GetValue(TitleTemplateProperty);
            set => SetValue(TitleTemplateProperty, value);
        }

        public static readonly DependencyProperty TitleTemplateProperty =
            DependencyProperty.Register("TitleTemplate", typeof(DataTemplate), typeof(ModalPopup), new PropertyMetadata(null, OnTitleChanged));

        private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ModalPopup)d).UpdateTitle();
        }

        private void UpdateTitle()
        {
            var title = Title;

            UpdateAutomationName();

            if (TitlePresenter != null)
            {
                TitlePresenter.Visibility = HasContent(title) || TitleTemplate != null
                    ? Visibility.Visible
                    : Visibility.Collapsed;
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

            // Markdown, as the TeachingTip template read it: the strings come from Android and
            // carry **bold**.
            TextBlockHelper.SetMarkdown(SubtitleTextBlock, subtitle);
            SubtitleTextBlock.Visibility = string.IsNullOrEmpty(subtitle)
                ? Visibility.Collapsed
                : Visibility.Visible;

            UpdateAutomationName();
        }

        /// <summary>
        /// What Narrator reads the dialog out by. The title if there is one, and otherwise the
        /// subtitle cut short, which is ContentDialog's fallback to its content's plain text -
        /// see ContentDialog::GetPlainText, which truncates the same way.
        /// </summary>
        private void UpdateAutomationName()
        {
            var name = Title as string;

            if (string.IsNullOrEmpty(name))
            {
                name = TruncateAutomationName(Subtitle);
            }

            name ??= string.Empty;

            AutomationProperties.SetName(this, name);

            // The popup is the element Narrator announces, so it needs the name too; the control
            // inside it carries the same one for anything that walks the tree instead.
            if (_popup != null)
            {
                AutomationProperties.SetName(_popup, name);
            }
        }

        // Popup::TruncateAutomationName, Popup_Partial.cpp: cut at the first newline, or after
        // twenty words.
        private static string TruncateAutomationName(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            var words = 0;
            var space = true;

            for (int i = 0; i < text.Length; i++)
            {
                var c = text[i];

                if (c is '\r' or '\n')
                {
                    return text.Substring(0, i);
                }

                if (char.IsWhiteSpace(c))
                {
                    space = true;
                }
                else if (space)
                {
                    space = false;

                    if (++words > 20)
                    {
                        return text.Substring(0, i);
                    }
                }
            }

            return text;
        }

        #endregion

        #region Content sizing

        public double ContentMaxWidth
        {
            get => (double)GetValue(ContentMaxWidthProperty);
            set => SetValue(ContentMaxWidthProperty, value);
        }

        public static readonly DependencyProperty ContentMaxWidthProperty =
            DependencyProperty.Register("ContentMaxWidth", typeof(double), typeof(ModalPopup), new PropertyMetadata(320d));

        public double ContentMaxHeight
        {
            get => (double)GetValue(ContentMaxHeightProperty);
            set => SetValue(ContentMaxHeightProperty, value);
        }

        public static readonly DependencyProperty ContentMaxHeightProperty =
            DependencyProperty.Register("ContentMaxHeight", typeof(double), typeof(ModalPopup), new PropertyMetadata(568d));

        public double ContentMinWidth
        {
            get => (double)GetValue(ContentMinWidthProperty);
            set => SetValue(ContentMinWidthProperty, value);
        }

        public static readonly DependencyProperty ContentMinWidthProperty =
            DependencyProperty.Register("ContentMinWidth", typeof(double), typeof(ModalPopup), new PropertyMetadata(320d));

        public double ContentMinHeight
        {
            get => (double)GetValue(ContentMinHeightProperty);
            set => SetValue(ContentMinHeightProperty, value);
        }

        public static readonly DependencyProperty ContentMinHeightProperty =
            DependencyProperty.Register("ContentMinHeight", typeof(double), typeof(ModalPopup), new PropertyMetadata(184d));

        #endregion

        #region Dismiss button

        public bool IsDismissButtonVisible
        {
            get => (bool)GetValue(IsDismissButtonVisibleProperty);
            set => SetValue(IsDismissButtonVisibleProperty, value);
        }

        public static readonly DependencyProperty IsDismissButtonVisibleProperty =
            DependencyProperty.Register("IsDismissButtonVisible", typeof(bool), typeof(ModalPopup), new PropertyMetadata(false, OnDismissButtonVisibleChanged));

        private static void OnDismissButtonVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ModalPopup)d).UpdateDismissButton();
        }

        public ElementTheme DismissButtonRequestedTheme
        {
            get => (ElementTheme)GetValue(DismissButtonRequestedThemeProperty);
            set => SetValue(DismissButtonRequestedThemeProperty, value);
        }

        public static readonly DependencyProperty DismissButtonRequestedThemeProperty =
            DependencyProperty.Register("DismissButtonRequestedTheme", typeof(ElementTheme), typeof(ModalPopup), new PropertyMetadata(ElementTheme.Default, OnDismissButtonRequestedThemeChanged));

        private static void OnDismissButtonRequestedThemeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ModalPopup)d).UpdateDismissButton();
        }

        // x:Load'ed, so the part is only realized once something asked for it - and asking can
        // happen before the template is applied, from a XAML subclass setting the property on
        // itself, so both ends call in here.
        private void UpdateDismissButton()
        {
            DismissButton ??= GetTemplateChild(nameof(DismissButton)) as Button;

            if (DismissButton == null)
            {
                return;
            }

            DismissButton.Click -= OnDismissButtonClick;
            DismissButton.Click += OnDismissButtonClick;

            DismissButton.RequestedTheme = DismissButtonRequestedTheme;
            DismissButton.Visibility = IsDismissButtonVisible
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void OnDismissButtonClick(object sender, RoutedEventArgs e)
        {
            OnDismissButtonClick();
        }

        protected virtual void OnDismissButtonClick()
        {
            Hide();
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

        public object CloseButtonContent
        {
            get => GetValue(CloseButtonContentProperty);
            set => SetValue(CloseButtonContentProperty, value);
        }

        public static readonly DependencyProperty CloseButtonContentProperty =
            DependencyProperty.Register("CloseButtonContent", typeof(object), typeof(ModalPopup), new PropertyMetadata(null, OnButtonContentChanged));

        private static void OnButtonContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ModalPopup)d).UpdateButtons();
        }

        // The three text properties are ContentDialog's spelling of the same thing, kept so that a
        // caller moving off ContentPopup does not have to be rewritten - dependency properties
        // rather than plain ones because four popups bind to them. Each writes through to the
        // content the template binds, which stays the one source of truth.
        public string PrimaryButtonText
        {
            get => (string)GetValue(PrimaryButtonTextProperty);
            set => SetValue(PrimaryButtonTextProperty, value);
        }

        public static readonly DependencyProperty PrimaryButtonTextProperty =
            DependencyProperty.Register("PrimaryButtonText", typeof(string), typeof(ModalPopup), new PropertyMetadata(null, OnPrimaryButtonTextChanged));

        private static void OnPrimaryButtonTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ModalPopup)d).PrimaryButtonContent = e.NewValue;
        }

        public string SecondaryButtonText
        {
            get => (string)GetValue(SecondaryButtonTextProperty);
            set => SetValue(SecondaryButtonTextProperty, value);
        }

        public static readonly DependencyProperty SecondaryButtonTextProperty =
            DependencyProperty.Register("SecondaryButtonText", typeof(string), typeof(ModalPopup), new PropertyMetadata(null, OnSecondaryButtonTextChanged));

        private static void OnSecondaryButtonTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ModalPopup)d).SecondaryButtonContent = e.NewValue;
        }

        public string CloseButtonText
        {
            get => (string)GetValue(CloseButtonTextProperty);
            set => SetValue(CloseButtonTextProperty, value);
        }

        public static readonly DependencyProperty CloseButtonTextProperty =
            DependencyProperty.Register("CloseButtonText", typeof(string), typeof(ModalPopup), new PropertyMetadata(null, OnCloseButtonTextChanged));

        private static void OnCloseButtonTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ModalPopup)d).CloseButtonContent = e.NewValue;
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

        public Style CloseButtonStyle
        {
            get => (Style)GetValue(CloseButtonStyleProperty);
            set => SetValue(CloseButtonStyleProperty, value);
        }

        public static readonly DependencyProperty CloseButtonStyleProperty =
            DependencyProperty.Register("CloseButtonStyle", typeof(Style), typeof(ModalPopup), new PropertyMetadata(null));

        public bool IsPrimaryButtonEnabled
        {
            get => (bool)GetValue(IsPrimaryButtonEnabledProperty);
            set => SetValue(IsPrimaryButtonEnabledProperty, value);
        }

        public static readonly DependencyProperty IsPrimaryButtonEnabledProperty =
            DependencyProperty.Register("IsPrimaryButtonEnabled", typeof(bool), typeof(ModalPopup), new PropertyMetadata(true));

        public bool IsSecondaryButtonEnabled
        {
            get => (bool)GetValue(IsSecondaryButtonEnabledProperty);
            set => SetValue(IsSecondaryButtonEnabledProperty, value);
        }

        public static readonly DependencyProperty IsSecondaryButtonEnabledProperty =
            DependencyProperty.Register("IsSecondaryButtonEnabled", typeof(bool), typeof(ModalPopup), new PropertyMetadata(true));

        /// <summary>
        /// Carried for the callers that set it, which do so to give the primary button the accent
        /// colour rather than to say anything about Enter.
        /// </summary>
        public ContentDialogButton DefaultButton
        {
            get => (ContentDialogButton)GetValue(DefaultButtonProperty);
            set => SetValue(DefaultButtonProperty, value);
        }

        public static readonly DependencyProperty DefaultButtonProperty =
            DependencyProperty.Register("DefaultButton", typeof(ContentDialogButton), typeof(ModalPopup), new PropertyMetadata(ContentDialogButton.Primary));

        /// <summary>
        /// What the close button resolves to. ContentDialogResult has no Close member, so a
        /// caller that needs to tell it apart from a dismissal names one here.
        /// </summary>
        public ContentDialogResult CloseButtonResult
        {
            get => (ContentDialogResult)GetValue(CloseButtonResultProperty);
            set => SetValue(CloseButtonResultProperty, value);
        }

        public static readonly DependencyProperty CloseButtonResultProperty =
            DependencyProperty.Register("CloseButtonResult", typeof(ContentDialogResult), typeof(ModalPopup), new PropertyMetadata(ContentDialogResult.None));

        public ContentPopupButtonsLayout ButtonsLayout
        {
            get => (ContentPopupButtonsLayout)GetValue(ButtonsLayoutProperty);
            set => SetValue(ButtonsLayoutProperty, value);
        }

        public static readonly DependencyProperty ButtonsLayoutProperty =
            DependencyProperty.Register("ButtonsLayout", typeof(ContentPopupButtonsLayout), typeof(ModalPopup), new PropertyMetadata(ContentPopupButtonsLayout.Horizontal, OnButtonContentChanged));

        public bool IsPrimaryButtonSplit
        {
            get => (bool)GetValue(IsPrimaryButtonSplitProperty);
            set => SetValue(IsPrimaryButtonSplitProperty, value);
        }

        public static readonly DependencyProperty IsPrimaryButtonSplitProperty =
            DependencyProperty.Register("IsPrimaryButtonSplit", typeof(bool), typeof(ModalPopup), new PropertyMetadata(false, OnIsPrimaryButtonSplitChanged));

        private static void OnIsPrimaryButtonSplitChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ModalPopup)d).UpdateSplitButton();
        }

        private void UpdateSplitButton()
        {
            if (PrimarySplitButton != null)
            {
                PrimarySplitButton.Visibility = IsPrimaryButtonSplit
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }

            if (PrimaryButton == null)
            {
                return;
            }

            if (IsPrimaryButtonSplit)
            {
                PrimaryButton.CornerRadius = new CornerRadius(2, 0, 0, 2);
            }
            else
            {
                PrimaryButton.ClearValue(CornerRadiusProperty);
            }
        }

        private void OnPrimaryButtonClick(object sender, RoutedEventArgs e)
        {
            InvokeButton(ContentDialogButton.Primary);
        }

        private void OnSecondaryButtonClick(object sender, RoutedEventArgs e)
        {
            InvokeButton(ContentDialogButton.Secondary);
        }

        private void OnCloseButtonClick(object sender, RoutedEventArgs e)
        {
            InvokeButton(ContentDialogButton.Close);
        }

        private async void InvokeButton(ContentDialogButton button)
        {
            await InvokeButtonAsync(button);
        }

        private async Task InvokeButtonAsync(ContentDialogButton button)
        {
            TypedEventHandler<ModalPopup, ModalPopupButtonClickEventArgs> handler;
            ContentDialogResult result;

            switch (button)
            {
                case ContentDialogButton.Primary:
                    if (!IsPrimaryButtonEnabled)
                    {
                        return;
                    }

                    handler = PrimaryButtonClick;
                    result = ContentDialogResult.Primary;
                    break;
                case ContentDialogButton.Secondary:
                    if (!IsSecondaryButtonEnabled)
                    {
                        return;
                    }

                    handler = SecondaryButtonClick;
                    result = ContentDialogResult.Secondary;
                    break;
                case ContentDialogButton.Close:
                    handler = CloseButtonClick;
                    result = CloseButtonResult;
                    break;
                default:
                    return;
            }

            if (handler != null)
            {
                var args = new ModalPopupButtonClickEventArgs();

                _raising = true;
                handler(this, args);
                _raising = false;

                await args.WaitAsync();

                if (args.Cancel)
                {
                    return;
                }
            }

            await HideCoreAsync(result);
        }

        private static bool HasContent(object content)
        {
            return content is string text ? text.Length > 0 : content != null;
        }

        // The gaps ContentPopup carries in its visual states. Applied here rather than in the
        // template because which margin a button wants depends on which of its neighbours are
        // there at all. The close button's -12 pulls it back into CommandSpace's own padding.
        private static readonly Thickness NoMargin = new Thickness(0, 0, 0, 0);
        private static readonly Thickness LeftButtonMargin = new Thickness(0, 0, 4, 0);
        private static readonly Thickness RightButtonMargin = new Thickness(4, 0, 0, 0);
        private static readonly Thickness CloseButtonMargin = new Thickness(0, 8, 0, -12);

        /// <summary>
        /// Places the three buttons in the four-column, two-row command grid, the way
        /// ContentPopup's fourteen visual states do: a button with nothing to say is not in the
        /// layout at all, and the survivors spread into the room it leaves.
        /// </summary>
        private void UpdateButtons()
        {
            var primary = HasContent(PrimaryButtonContent);
            var secondary = HasContent(SecondaryButtonContent);
            var close = HasContent(CloseButtonContent);

            if (CommandSpace != null)
            {
                CommandSpace.Visibility = primary || secondary || close
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }

            // A lone button sits in the right half when the row is horizontal and spans it when
            // it is vertical. Two of them always share it.
            var lonely = ButtonsLayout == ContentPopupButtonsLayout.Vertical || close
                ? (column: 0, span: 4, margin: NoMargin)
                : (column: 2, span: 2, margin: RightButtonMargin);

            if (PrimaryRoot != null)
            {
                PrimaryRoot.Visibility = primary ? Visibility.Visible : Visibility.Collapsed;

                var placement = secondary
                    ? (column: 0, span: 2, margin: LeftButtonMargin)
                    : lonely;

                Grid.SetColumn(PrimaryRoot, placement.column);
                Grid.SetColumnSpan(PrimaryRoot, placement.span);
                PrimaryRoot.Margin = placement.margin;
            }

            if (SecondaryButton != null)
            {
                SecondaryButton.Visibility = secondary ? Visibility.Visible : Visibility.Collapsed;

                var placement = primary
                    ? (column: 2, span: 2, margin: RightButtonMargin)
                    : lonely;

                Grid.SetColumn(SecondaryButton, placement.column);
                Grid.SetColumnSpan(SecondaryButton, placement.span);
                SecondaryButton.Margin = placement.margin;
            }

            if (CloseButton != null)
            {
                CloseButton.Visibility = close ? Visibility.Visible : Visibility.Collapsed;
                CloseButton.Margin = primary || secondary ? CloseButtonMargin : NoMargin;
            }
        }

        #endregion

        #region IsPrimaryButtonPending

        public bool IsPrimaryButtonPending
        {
            get => (bool)GetValue(IsPrimaryButtonPendingProperty);
            set => SetValue(IsPrimaryButtonPendingProperty, value);
        }

        public static readonly DependencyProperty IsPrimaryButtonPendingProperty =
            DependencyProperty.Register("IsPrimaryButtonPending", typeof(bool), typeof(ModalPopup), new PropertyMetadata(false, OnIsPrimaryButtonPendingChanged));

        private static void OnIsPrimaryButtonPendingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ModalPopup)d).OnIsPrimaryButtonPendingChanged((bool)e.NewValue);
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
    }
}
