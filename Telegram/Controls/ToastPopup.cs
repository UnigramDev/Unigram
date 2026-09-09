//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls.Messages;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Streams;
using Telegram.Td;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Shapes;

namespace Telegram.Controls
{
    public enum ToastPopupIcon
    {
        None,
        AntiSpam,
        Archived,
        AutoNightOff,
        AutoRemoveOff,
        AutoRemoveOn,
        Ban,
        Copied,
        Error,
        ExpiredStory,
        FolderIn,
        FolderOut,
        Forward,
        Gif,
        Gift,
        Info,
        JoinRequested,
        LinkCopied,
        Mute,
        MuteFor,
        Pin,
        Premium,
        SavedMessages,
        SoundDownload,
        SoundOff,
        SoundOn,
        SpeedLimit,
        StarsSent,
        StarsTopup,
        Success,
        Transcribe,
        Translate,
        Unmute,
        Unpin,
        VideoConversion
    }

    /// <summary>
    /// Where a toast sits: relative to its target, or to the window when it has none.
    /// </summary>
    public enum ToastPlacementMode
    {
        Top,
        Bottom,
        Left,
        Right,
        TopRight,
        TopLeft,
        BottomRight,
        BottomLeft,
        LeftTop,
        LeftBottom,
        RightTop,
        RightBottom,
        Center
    }

    /// <summary>
    /// A transient message, in a <see cref="Popup"/> of its own.
    /// </summary>
    /// <remarks>
    /// This used to derive from WinUI's TeachingTip and must not again. A managed class over a
    /// composable native type is two objects, and they die separately: TeachingTip arms a revoker
    /// on itself the first time it opens and never releases it before its destructor, so once the
    /// managed half is collected first, the native destructor resolves that weak reference and
    /// calls QueryInterface on freed memory. Reported by crash telemetry on 12.10.3, from a toast
    /// left over by a call window that had closed.
    ///
    /// Placement reproduces TeachingTip's, so that toasts land where they always have.
    /// </remarks>
    [TemplatePart(Name = "ContentRoot", Type = typeof(Border))]
    [TemplatePart(Name = "Tail", Type = typeof(Polygon))]
    public partial class ToastPopup : ContentControl
    {
        // The template holds the content in a band wide enough for the tail on all four sides, so
        // that moving the tail never changes the measured size. These must agree with the row and
        // column definitions in the style.
        private const double TailBand = 8;
        private const double TailInset = 10;
        private const double TailLength = 20;

        // Where the tail's point sits, measured in from the edge of the toast: the two outer bands
        // plus half the tail. The cornered placements line this up with the centre of the target.
        private const double EdgeToTailCenter = TailBand + TailInset + (TailLength / 2);

        // How far an untargeted toast stays clear of the window edge.
        private const double WindowEdgeMargin = 24;

        // Open toasts, so that a window can take them all down at once. Per thread, because every
        // secondary view runs on one of its own and this is touched without a lock.
        [ThreadStatic]
        private static List<ToastPopup> _opened;

        private Polygon Tail;
        private Border ContentRoot;

        private XamlRoot _xamlRoot;
        private Popup _popup;
        private Popup _lightDismiss;
        private DispatcherTimer _timer;
        private CompositionScopedBatch _batch;

        private bool _isOpen;
        private bool _entered;

        public ToastPopup()
        {
            DefaultStyleKey = typeof(ToastPopup);
        }

        /// <summary>
        /// The element the toast points at. Null places it against the window instead.
        /// </summary>
        public FrameworkElement Target { get; set; }

        public ToastPlacementMode PreferredPlacement { get; set; } = ToastPlacementMode.Center;

        /// <summary>
        /// Whether a click elsewhere takes the toast down. Needed by any toast that has no
        /// countdown behind it, or it would stay up for good.
        /// </summary>
        public bool IsLightDismissEnabled { get; set; }

        /// <summary>
        /// How long the toast stays up. Zero leaves it to the caller to close.
        /// </summary>
        public TimeSpan DismissAfter { get; set; }

        /// <summary>
        /// Raised once the toast is off the screen, animation included.
        /// </summary>
        public event TypedEventHandler<ToastPopup, object> Closed;

        public bool IsOpen
        {
            get => _isOpen;
            set
            {
                if (_isOpen != value)
                {
                    if (value)
                    {
                        Open();
                    }
                    else
                    {
                        Close(true);
                    }
                }
            }
        }

        /// <summary>
        /// Takes down every toast on a window - or on all of them, when the root is null.
        /// </summary>
        /// <param name="animate">
        /// False during teardown: the animation would call back into a window that is going away.
        /// </param>
        public static void HideAll(XamlRoot xamlRoot, bool animate = true)
        {
            if (_opened == null)
            {
                return;
            }

            // Backwards, because closing removes from the list.
            for (int i = _opened.Count - 1; i >= 0; i--)
            {
                var toast = _opened[i];
                if (xamlRoot == null || toast._xamlRoot == xamlRoot)
                {
                    toast.Close(animate);
                }
            }
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            Tail = GetTemplateChild(nameof(Tail)) as Polygon;
            ContentRoot = GetTemplateChild(nameof(ContentRoot)) as Border;

            // TeachingTip cast this from EstablishShadows rather than from its template, so the
            // template carries no trace of it. Same elevation, and unguarded like TeachingTip's:
            // ThemeShadow arrived in 18362, which is TargetPlatformMinVersion. Deliberately not
            // ApiInfo.CanCreateThemeShadow - that withholds shadows from Windows 10 for reasons
            // that are about messages overlapping each other, and a toast is alone in its popup.
            // The tail stays out of it: TeachingTip's own tail shadow is behind a debug switch.
            if (ContentRoot != null)
            {
                ContentRoot.Shadow = new ThemeShadow();
                ContentRoot.Translation = new Vector3(0, 0, 32);
            }

            // Target and PreferredPlacement are both set before the toast is opened, and the
            // template is applied on the way in, so the tail is settled in one go here rather
            // than rebuilt every time the toast is placed again.
            UpdateTail();
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new ToastPopupAutomationPeer(this);
        }

        private void Open()
        {
            // Captured, because the toast becomes the popup's child and reads its root from there
            // afterwards. Everything below needs the one it was shown on.
            _xamlRoot = XamlRoot;

            if (_xamlRoot == null)
            {
                return;
            }

            _isOpen = true;

            _opened ??= new List<ToastPopup>();
            _opened.Add(this);

            if (IsLightDismissEnabled)
            {
                // A light dismiss popup underneath, the way TeachingTip does it: it is what turns
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

            // Hidden until the first layout pass gives it a size to be placed by, or it would
            // show up in the corner for a frame.
            ElementComposition.GetElementVisual(this).Opacity = 0;

            SizeChanged += OnSizeChanged;
            _xamlRoot.Changed += OnXamlRootChanged;

            if (Target != null)
            {
                Target.Unloaded += OnTargetUnloaded;
            }

            _popup.IsOpen = true;

            if (DismissAfter > TimeSpan.Zero)
            {
                _timer = new DispatcherTimer
                {
                    Interval = DismissAfter
                };

                _timer.Tick += OnTick;
                _timer.Start();
            }
        }

        private void Close(bool animate)
        {
            if (!_isOpen)
            {
                return;
            }

            _isOpen = false;
            _opened.Remove(this);

            if (_timer != null)
            {
                _timer.Tick -= OnTick;
                _timer.Stop();
                _timer = null;
            }

            SizeChanged -= OnSizeChanged;

            if (_xamlRoot != null)
            {
                _xamlRoot.Changed -= OnXamlRootChanged;
            }

            if (Target != null)
            {
                Target.Unloaded -= OnTargetUnloaded;
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

            Closed?.Invoke(this, null);
        }

        private void OnTick(object sender, object e)
        {
            Logger.Info("closed");
            IsOpen = false;
        }

        private void OnLightDismissed(object sender, object e)
        {
            IsOpen = false;
        }

        private void OnTargetUnloaded(object sender, RoutedEventArgs e)
        {
            // Nothing left to point at, and asking a disconnected element where it is throws.
            IsOpen = false;
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
            }
        }

        #region Placement

        private void PositionPopup()
        {
            if (_popup == null || _xamlRoot == null)
            {
                return;
            }

            var placement = PreferredPlacement;

            if (Target != null && Target.XamlRoot != null)
            {
                PositionTargeted(placement, Target.TransformToVisual(null).TransformBounds(
                    new Rect(0, 0, Target.ActualWidth, Target.ActualHeight)));
            }
            else
            {
                PositionUntargeted(placement);
            }
        }

        private void PositionTargeted(ToastPlacementMode placement, Rect target)
        {
            var width = ActualWidth;
            var height = ActualHeight;

            // Centred on the target along the free axis, and pushed clear of it along the other.
            // The cornered modes line the tail up with the target instead of the toast's middle,
            // which is what lets a toast on a screen edge still point at its target.
            var centerX = ((target.X * 2) + target.Width - width) / 2;
            var centerY = ((target.Y * 2) + target.Height - height) / 2;
            var tailX = ((target.X * 2) + target.Width) / 2;
            var tailY = ((target.Y * 2) + target.Height) / 2;

            switch (placement)
            {
                case ToastPlacementMode.Top:
                    SetOffset(centerX, target.Y - height);
                    break;
                case ToastPlacementMode.Bottom:
                    SetOffset(centerX, target.Bottom);
                    break;
                case ToastPlacementMode.Left:
                    SetOffset(target.X - width, centerY);
                    break;
                case ToastPlacementMode.Right:
                    SetOffset(target.Right, centerY);
                    break;
                case ToastPlacementMode.TopRight:
                    SetOffset(tailX - EdgeToTailCenter, target.Y - height);
                    break;
                case ToastPlacementMode.TopLeft:
                    SetOffset(tailX - width + EdgeToTailCenter, target.Y - height);
                    break;
                case ToastPlacementMode.BottomRight:
                    SetOffset(tailX - EdgeToTailCenter, target.Bottom);
                    break;
                case ToastPlacementMode.BottomLeft:
                    SetOffset(tailX - width + EdgeToTailCenter, target.Bottom);
                    break;
                case ToastPlacementMode.LeftTop:
                    SetOffset(target.X - width, tailY - height + EdgeToTailCenter);
                    break;
                case ToastPlacementMode.LeftBottom:
                    SetOffset(target.X - width, tailY - EdgeToTailCenter);
                    break;
                case ToastPlacementMode.RightTop:
                    SetOffset(target.Right, tailY - height + EdgeToTailCenter);
                    break;
                case ToastPlacementMode.RightBottom:
                    SetOffset(target.Right, tailY - EdgeToTailCenter);
                    break;
                case ToastPlacementMode.Center:
                    SetOffset(centerX, target.Y + (target.Height / 2) - height);
                    break;
            }
        }

        private void PositionUntargeted(ToastPlacementMode placement)
        {
            var size = _xamlRoot.Size;

            var width = ActualWidth;
            var height = ActualHeight;

            var near = WindowEdgeMargin;
            var farX = size.Width - (width + WindowEdgeMargin);
            var farY = size.Height - (height + WindowEdgeMargin);
            var centerX = (size.Width - width) / 2;
            var centerY = (size.Height - height) / 2;

            switch (placement)
            {
                case ToastPlacementMode.Bottom:
                    SetOffset(centerX, farY);
                    break;
                case ToastPlacementMode.Top:
                    SetOffset(centerX, near);
                    break;
                case ToastPlacementMode.Left:
                    SetOffset(near, centerY);
                    break;
                case ToastPlacementMode.Right:
                    SetOffset(farX, centerY);
                    break;
                case ToastPlacementMode.TopRight:
                case ToastPlacementMode.RightTop:
                    SetOffset(farX, near);
                    break;
                case ToastPlacementMode.TopLeft:
                case ToastPlacementMode.LeftTop:
                    SetOffset(near, near);
                    break;
                case ToastPlacementMode.BottomRight:
                case ToastPlacementMode.RightBottom:
                    SetOffset(farX, farY);
                    break;
                case ToastPlacementMode.BottomLeft:
                case ToastPlacementMode.LeftBottom:
                    SetOffset(near, farY);
                    break;
                case ToastPlacementMode.Center:
                    SetOffset(centerX, centerY);
                    break;
            }
        }

        private void SetOffset(double horizontal, double vertical)
        {
            _popup.HorizontalOffset = horizontal;
            _popup.VerticalOffset = vertical;
        }

        /// <summary>
        /// Moves the tail to the edge that faces the target, and turns it to point at it.
        /// </summary>
        private void UpdateTail()
        {
            if (Tail == null)
            {
                return;
            }

            if (Target == null)
            {
                // Nothing to point at: an untargeted toast is a plain rectangle.
                Tail.Visibility = Visibility.Collapsed;
                return;
            }

            Tail.Visibility = Visibility.Visible;

            switch (PreferredPlacement)
            {
                // The toast is above the target, so the tail hangs off its bottom edge.
                case ToastPlacementMode.Top:
                    SetTail(4, 2, HorizontalAlignment.Center, VerticalAlignment.Bottom);
                    break;
                case ToastPlacementMode.TopRight:
                    SetTail(4, 2, HorizontalAlignment.Left, VerticalAlignment.Bottom);
                    break;
                case ToastPlacementMode.TopLeft:
                    SetTail(4, 2, HorizontalAlignment.Right, VerticalAlignment.Bottom);
                    break;
                case ToastPlacementMode.Center:
                    SetTail(4, 2, HorizontalAlignment.Center, VerticalAlignment.Bottom);
                    break;

                // Below the target: the tail sits on the top edge.
                case ToastPlacementMode.Bottom:
                    SetTail(0, 2, HorizontalAlignment.Center, VerticalAlignment.Top);
                    break;
                case ToastPlacementMode.BottomRight:
                    SetTail(0, 2, HorizontalAlignment.Left, VerticalAlignment.Top);
                    break;
                case ToastPlacementMode.BottomLeft:
                    SetTail(0, 2, HorizontalAlignment.Right, VerticalAlignment.Top);
                    break;

                // To the left of the target: the tail sits on the right edge.
                case ToastPlacementMode.Left:
                    SetTail(2, 4, HorizontalAlignment.Right, VerticalAlignment.Center);
                    break;
                case ToastPlacementMode.LeftTop:
                    SetTail(2, 4, HorizontalAlignment.Right, VerticalAlignment.Bottom);
                    break;
                case ToastPlacementMode.LeftBottom:
                    SetTail(2, 4, HorizontalAlignment.Right, VerticalAlignment.Top);
                    break;

                // To the right: the tail sits on the left edge.
                case ToastPlacementMode.Right:
                    SetTail(2, 0, HorizontalAlignment.Left, VerticalAlignment.Center);
                    break;
                case ToastPlacementMode.RightTop:
                    SetTail(2, 0, HorizontalAlignment.Left, VerticalAlignment.Bottom);
                    break;
                case ToastPlacementMode.RightBottom:
                    SetTail(2, 0, HorizontalAlignment.Left, VerticalAlignment.Top);
                    break;
            }
        }

        private void SetTail(int row, int column, HorizontalAlignment horizontal, VerticalAlignment vertical)
        {
            var points = new PointCollection();

            // Drawn pointing away from the edge it is docked against, and pulled a pixel into the
            // content so the two shapes read as one.
            if (row == 0)
            {
                points.Add(new Point(0, 10));
                points.Add(new Point(10, 0));
                points.Add(new Point(20, 10));
                Tail.Margin = new Thickness(0, 0, 0, -1);
            }
            else if (row == 4)
            {
                points.Add(new Point(0, 0));
                points.Add(new Point(10, 10));
                points.Add(new Point(20, 0));
                Tail.Margin = new Thickness(0, -1, 0, 0);
            }
            else if (column == 0)
            {
                points.Add(new Point(10, 0));
                points.Add(new Point(0, 10));
                points.Add(new Point(10, 20));
                Tail.Margin = new Thickness(0, 0, -1, 0);
            }
            else
            {
                points.Add(new Point(0, 0));
                points.Add(new Point(10, 10));
                points.Add(new Point(0, 20));
                Tail.Margin = new Thickness(-1, 0, 0, 0);
            }

            Tail.Points = points;
            Tail.HorizontalAlignment = horizontal;
            Tail.VerticalAlignment = vertical;

            Grid.SetRow(Tail, row);
            Grid.SetColumn(Tail, column);
        }

        #endregion

        #region Animation

        /// <summary>
        /// The point the toast grows out of and shrinks back into.
        /// </summary>
        /// <remarks>
        /// A targeted toast pivots on its tail, so it appears to come out of what it points at.
        /// The pivot is the corner of the content nearest the tail rather than the tail's own
        /// point - transcribed from TeachingTip, where it is the tail occlusion grid's first two
        /// rows and columns.
        /// </remarks>
        private Vector3 GetPivot()
        {
            var size = ActualSize;

            if (Target == null)
            {
                return new Vector3(size / 2, 0);
            }

            const float edge = (float)TailBand;
            const float inner = (float)(TailBand + TailInset) + 1;

            return PreferredPlacement switch
            {
                ToastPlacementMode.Top or ToastPlacementMode.Center => new Vector3(size.X / 2, size.Y - edge, 0),
                ToastPlacementMode.Bottom => new Vector3(size.X / 2, edge, 0),
                ToastPlacementMode.Left => new Vector3(size.X - edge, size.Y / 2, 0),
                ToastPlacementMode.Right => new Vector3(edge, size.Y / 2, 0),
                ToastPlacementMode.TopRight => new Vector3(inner, size.Y - edge, 0),
                ToastPlacementMode.TopLeft => new Vector3(size.X - inner, size.Y - edge, 0),
                ToastPlacementMode.BottomRight => new Vector3(inner, edge, 0),
                ToastPlacementMode.BottomLeft => new Vector3(size.X - inner, edge, 0),
                ToastPlacementMode.LeftTop => new Vector3(size.X - edge, size.Y - inner, 0),
                ToastPlacementMode.LeftBottom => new Vector3(size.X - edge, inner, 0),
                ToastPlacementMode.RightTop => new Vector3(edge, size.Y - inner, 0),
                ToastPlacementMode.RightBottom => new Vector3(edge, inner, 0),
                _ => new Vector3(size / 2, 0)
            };
        }

        private void PlayOpenAnimation()
        {
            var visual = ElementComposition.GetElementVisual(this);
            var compositor = visual.Compositor;

            visual.CenterPoint = GetPivot();

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

            // Re-read: the toast can have been resized after it opened, by an action button or a
            // countdown appended once it was already up.
            visual.CenterPoint = GetPivot();

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

        // Narrator reads the toast out from here: it is neither focusable nor navigable, so it is
        // never reached by moving around the window.
        private void Announce(string text, string action)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            // TODO: ignoring action for now, as it is not focusable
            //if (action != null)
            //{
            //    text += ", " + action;
            //}

            AutomationProperties.SetName(this, text);

            var peer = FrameworkElementAutomationPeer.CreatePeerForElement(this);
            peer?.RaiseNotificationEvent(AutomationNotificationKind.Other,
                AutomationNotificationProcessing.CurrentThenMostRecent, text, "ToastPopupOpened");
        }

        public static void ShowError(XamlRoot xamlRoot, Error error)
        {
            Show(xamlRoot, string.Format(Strings.UnknownErrorCode, error.Message), ToastPopupIcon.Error);
        }

        public static void ShowOptionPromo(INavigationService navigationService)
        {
            ShowPromo(navigationService, Strings.OptionPremiumRequiredMessage, Strings.OptionPremiumRequiredButton, null);
        }

        public static void ShowFeaturePromo(INavigationService navigationService, PremiumFeature feature)
        {
            var text = feature switch
            {
                PremiumFeatureAccentColor => Strings.UserColorApplyPremium,
                PremiumFeatureRealTimeChatTranslation => Strings.ShowTranslateChatButtonLocked,
                PremiumFeatureChecklists => Strings.TodoPremiumRequired,
                PremiumFeatureMessageEffects => Strings.AnimatedEffectPremium,
                PremiumFeatureUniqueReactions => Strings.UnlockPremiumEmojiReaction,
                PremiumFeatureCustomEmoji => Strings.UnlockPremiumEmojiHint,
                PremiumFeatureRichMessages => Strings.ArticleConversionText,
                _ => Strings.UnlockPremium
            };

            ShowFeaturePromo(navigationService, text, feature);
        }

        public static void ShowFeaturePromo(INavigationService navigationService, string text, PremiumFeature feature)
        {
            var label = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontFamily = BootStrapper.Current.Resources["EmojiThemeFontFamilyWithSymbols"] as FontFamily
            };

            var markdown = ClientEx.ParseMarkdown(text);
            if (markdown.Entities.Count == 1)
            {
                var e1 = markdown.Entities[0];
                if (e1.Offset > 0)
                {
                    label.Inlines.Add(markdown.Text.Substring(0, e1.Offset));
                }

                if (e1.Type is TextEntityTypeBold)
                {
                    var hyperlink = new Hyperlink();

                    void handler(object sender, object e)
                    {
                        var popups = VisualTreeHelper.GetOpenPopupsForXamlRoot(navigationService.XamlRoot);
                        foreach (var popup in popups)
                        {
                            if (popup.Child is ContentDialog dialog)
                            {
                                dialog.Hide();
                            }
                        }

                        hyperlink.Click -= handler;

                        if (feature != null)
                        {
                            navigationService.ShowPromo(feature);
                        }
                        else
                        {
                            navigationService.ShowPromo();
                        }
                    }

                    hyperlink.Click += handler;
                    hyperlink.FontWeight = FontWeights.SemiBold;
                    hyperlink.UnderlineStyle = UnderlineStyle.None;
                    hyperlink.Inlines.Add(markdown.Text.Substring(e1.Offset, e1.Length));

                    label.Inlines.Add(hyperlink);
                }

                if (e1.Offset + e1.Length < markdown.Text.Length)
                {
                    label.Inlines.Add(markdown.Text.Substring(e1.Offset + e1.Length));
                }
            }
            else
            {
                TextBlockHelper.SetFormattedText(label, markdown);
            }

            Show(navigationService.XamlRoot, label, ToastPopupIcon.Premium);
        }

        public static async void ShowPromo(INavigationService navigationService, string text, string action, PremiumSource source, PremiumFeature feature = null)
        {
            var markdown = ClientEx.ParseMarkdown(text);

            var confirm = await ShowActionAsync(navigationService.XamlRoot, markdown, action, ToastPopupIcon.Premium);
            if (confirm == ContentDialogResult.Primary)
            {
                var popups = VisualTreeHelper.GetOpenPopupsForXamlRoot(navigationService.XamlRoot);
                foreach (var popup in popups)
                {
                    if (popup.Child is MessageEffectMenuFlyout)
                    {
                        popup.IsOpen = false;
                    }
                }

                if (feature != null)
                {
                    navigationService.ShowPromo(feature);
                }
                else
                {
                    navigationService.ShowPromo(source);
                }
            }
        }

        public static ToastPopup Show(XamlRoot xamlRoot, string text, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            return Show(xamlRoot, ClientEx.ParseMarkdown(text), null, requestedTheme, dismissAfter);
        }

        public static ToastPopup Show(XamlRoot xamlRoot, string text, ToastPopupIcon icon, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            return Show(xamlRoot, ClientEx.ParseMarkdown(text), icon, requestedTheme, dismissAfter);
        }

        public static ToastPopup Show(XamlRoot xamlRoot, string text, AnimatedImageSource icon, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            return Show(xamlRoot, ClientEx.ParseMarkdown(text), icon, requestedTheme, dismissAfter);
        }

        public static ToastPopup Show(XamlRoot xamlRoot, FormattedText text, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            return Show(xamlRoot, text, null, requestedTheme, dismissAfter);
        }

        public static ToastPopup Show(XamlRoot xamlRoot, FrameworkElement label, ToastPopupIcon icon, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            AnimatedImage animated = null;
            if (icon != ToastPopupIcon.None)
            {
                animated = new AnimatedImage
                {
                    Source = new LocalFileSource($"ms-appx:///Assets/Toasts/{icon}.tgs"),
                    Width = 32,
                    Height = 32,
                    AutoPlay = true,
                    LoopCount = 1,
                    IsCachingEnabled = false,
                    FrameSize = new Size(32, 32),
                    DecodeFrameType = DecodePixelType.Logical,
                    Margin = new Thickness(-4, -12, 8, -12)
                };
            }

            return ShowImpl(xamlRoot, label, animated, ToastPlacementMode.Center, requestedTheme, dismissAfter);
        }

        public static ToastPopup Show(XamlRoot xamlRoot, FormattedText text, ToastPopupIcon icon, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            AnimatedImage animated = null;
            if (icon != ToastPopupIcon.None)
            {
                animated = new AnimatedImage
                {
                    Source = new LocalFileSource($"ms-appx:///Assets/Toasts/{icon}.tgs"),
                    Width = 32,
                    Height = 32,
                    AutoPlay = true,
                    LoopCount = 1,
                    IsCachingEnabled = false,
                    FrameSize = new Size(32, 32),
                    DecodeFrameType = DecodePixelType.Logical,
                    Margin = new Thickness(-4, -12, 8, -12)
                };
            }

            return ShowImpl(xamlRoot, text, animated, ToastPlacementMode.Center, requestedTheme, dismissAfter);
        }

        public static ToastPopup Show(XamlRoot xamlRoot, FormattedText text, AnimatedImageSource icon, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            AnimatedImage animated = null;
            if (icon != null)
            {
                animated = new AnimatedImage
                {
                    Source = icon,
                    Width = 32,
                    Height = 32,
                    AutoPlay = true,
                    LoopCount = 1,
                    IsCachingEnabled = false,
                    FrameSize = new Size(32, 32),
                    DecodeFrameType = DecodePixelType.Logical,
                    Margin = new Thickness(-4, -12, 8, -12)
                };
            }

            return ShowImpl(xamlRoot, text, animated, ToastPlacementMode.Center, requestedTheme, dismissAfter);
        }

        public static ToastPopup Show(FrameworkElement target, string text, ToastPlacementMode placement, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            return Show(target, text, ToastPopupIcon.None, placement, requestedTheme, dismissAfter);
        }

        public static ToastPopup Show(FrameworkElement target, string text, ToastPopupIcon icon, ToastPlacementMode placement, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            return Show(target, ClientEx.ParseMarkdown(text), icon, placement, requestedTheme, dismissAfter);
        }

        public static ToastPopup Show(FrameworkElement target, FormattedText text, ToastPlacementMode placement, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            return Show(target, text, ToastPopupIcon.None, placement, requestedTheme, dismissAfter);
        }

        public static ToastPopup Show(FrameworkElement target, FormattedText text, ToastPopupIcon icon, ToastPlacementMode placement, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            AnimatedImage animated = null;
            if (icon != ToastPopupIcon.None)
            {
                animated = new AnimatedImage
                {
                    Source = new LocalFileSource($"ms-appx:///Assets/Toasts/{icon}.tgs"),
                    Width = 32,
                    Height = 32,
                    AutoPlay = true,
                    LoopCount = 1,
                    IsCachingEnabled = false,
                    FrameSize = new Size(32, 32),
                    DecodeFrameType = DecodePixelType.Logical,
                    Margin = new Thickness(-4, -12, 8, -12)
                };
            }

            return ShowImpl(target.XamlRoot, text, animated, placement, requestedTheme, dismissAfter, target);
        }

        public static ToastPopup ShowImpl(XamlRoot xamlRoot, FormattedText text, FrameworkElement icon, ToastPlacementMode placement, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null, FrameworkElement target = null, string action = null)
        {
            var label = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontFamily = BootStrapper.Current.Resources["EmojiThemeFontFamilyWithSymbols"] as FontFamily
            };

            TextBlockHelper.SetFormattedText(label, text);
            return ShowImpl(xamlRoot, label, icon, placement, requestedTheme, dismissAfter, target, action);
        }

        // action is only used to announce the button ShowActionAsync appends once this returns:
        // by then the toast is already open, and with it the only chance to read it out.
        public static ToastPopup ShowImpl(XamlRoot xamlRoot, FrameworkElement label, FrameworkElement icon, ToastPlacementMode placement, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null, FrameworkElement target = null, string action = null)
        {
            Logger.Info();
            Grid.SetColumn(label, 1);

            var content = new Grid();
            content.ColumnDefinitions.Add(1, GridUnitType.Auto);
            content.ColumnDefinitions.Add(new ColumnDefinition());
            content.ColumnDefinitions.Add(1, GridUnitType.Auto);
            content.Children.Add(label);

            if (icon != null)
            {
                content.Children.Add(icon);
            }

            var toast = new ToastPopup
            {
                Target = target,
                PreferredPlacement = placement,
                IsLightDismissEnabled = target != null && (dismissAfter == null || dismissAfter == TimeSpan.Zero),
                Content = content,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                MinWidth = 0,
                XamlRoot = xamlRoot
            };

            if (requestedTheme != ElementTheme.Default)
            {
                toast.RequestedTheme = requestedTheme;
            }

            // A targeted toast with no countdown is dismissed by clicking away from it instead.
            if ((target == null || dismissAfter.HasValue) && (dismissAfter == null || dismissAfter.Value.TotalSeconds > 0))
            {
                toast.DismissAfter = dismissAfter ?? TimeSpan.FromSeconds(3);
            }

            try
            {
                toast.IsOpen = true;
            }
            catch
            {
                // A window on its way out still hands out a XamlRoot, and touching it throws.
                // Unwind, or a toast that never opened is left on the list of open ones.
                Logger.Info("XamlRoot thrown");
                toast.Close(false);
                return null;
            }

            // Answers false when the root was already gone, which callers read as never shown.
            if (!toast.IsOpen)
            {
                return null;
            }

            toast.Announce(label is TextBlock text ? text.Text : null, action);

            return toast;
        }


        public static Task<ContentDialogResult> ShowActionAsync(XamlRoot xamlRoot, string text, string action, ToastPopupIcon icon, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            return ShowActionAsync(xamlRoot, ClientEx.ParseMarkdown(text), action, icon, ToastPlacementMode.Center, requestedTheme, dismissAfter);
        }

        public static Task<ContentDialogResult> ShowActionAsync(XamlRoot xamlRoot, FormattedText text, string action, ToastPopupIcon icon, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            return ShowActionAsync(xamlRoot, text, action, icon, ToastPlacementMode.Center, requestedTheme, dismissAfter);
        }

        public static Task<ContentDialogResult> ShowActionAsync(XamlRoot xamlRoot, FormattedText text, string action, ToastPopupIcon? icon, ToastPlacementMode placement, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            AnimatedImage animated = null;
            if (icon != null)
            {
                animated = new AnimatedImage
                {
                    Source = new LocalFileSource($"ms-appx:///Assets/Toasts/{icon}.tgs"),
                    Width = 32,
                    Height = 32,
                    AutoPlay = true,
                    LoopCount = 1,
                    IsCachingEnabled = false,
                    FrameSize = new Size(32, 32),
                    DecodeFrameType = DecodePixelType.Logical,
                    Margin = new Thickness(-4, -12, 8, -12)
                };
            }

            return ShowActionAsync(xamlRoot, text, action, animated, placement, requestedTheme, dismissAfter);
        }

        public static Task<ContentDialogResult> ShowActionAsync(XamlRoot xamlRoot, string text, string action, AnimatedImageSource icon, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            return ShowActionAsync(xamlRoot, ClientEx.ParseMarkdown(text), action, icon, requestedTheme, dismissAfter);
        }

        public static Task<ContentDialogResult> ShowActionAsync(XamlRoot xamlRoot, FormattedText text, string action, AnimatedImageSource icon, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            AnimatedImage animated = null;
            if (icon != null)
            {
                animated = new AnimatedImage
                {
                    Source = icon,
                    Width = 32,
                    Height = 32,
                    AutoPlay = true,
                    LoopCount = 1,
                    IsCachingEnabled = false,
                    FrameSize = new Size(32, 32),
                    DecodeFrameType = DecodePixelType.Logical,
                    Margin = new Thickness(-4, -12, 8, -12)
                };
            }

            return ShowActionAsync(xamlRoot, text, action, animated, ToastPlacementMode.Center, requestedTheme, dismissAfter);
        }
        public static Task<ContentDialogResult> ShowActionAsync(XamlRoot xamlRoot, string text, string action, FrameworkElement icon, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            return ShowActionAsync(xamlRoot, ClientEx.ParseMarkdown(text), action, icon, ToastPlacementMode.Center, requestedTheme, dismissAfter);
        }

        public static Task<ContentDialogResult> ShowActionAsync(XamlRoot xamlRoot, FormattedText text, string action, FrameworkElement icon, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            return ShowActionAsync(xamlRoot, text, action, icon, ToastPlacementMode.Center, requestedTheme, dismissAfter);
        }

        public static Task<ContentDialogResult> ShowActionAsync(XamlRoot xamlRoot, FormattedText text, string action, FrameworkElement icon, ToastPlacementMode placement, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            var toast = ShowImpl(xamlRoot, text, icon, placement, requestedTheme, dismissAfter, action: action);
            if (toast?.Content is Grid content)
            {
                var tsc = new TaskCompletionSource<ContentDialogResult>();
                var undo = new Button()
                {
                    Content = action,
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                    Style = BootStrapper.Current.Resources["AccentTextButtonStyle"] as Style,
                    Margin = new Thickness(8, -4, -4, -4),
                    Padding = new Thickness(4, 5, 4, 6)
                };

                void handler(object sender, RoutedEventArgs e)
                {
                    Logger.Info("closed");

                    tsc.TrySetResult(ContentDialogResult.Primary);
                    undo.Click -= handler;

                    toast.IsOpen = false;
                }

                void closed(ToastPopup sender, object e)
                {
                    tsc.TrySetResult(ContentDialogResult.None);
                    sender.Closed -= closed;
                }

                undo.Click += handler;
                toast.Closed += closed;

                Grid.SetColumn(undo, 2);
                content.Children.Add(undo);

                return tsc.Task;
            }

            return Task.FromResult(ContentDialogResult.None);
        }

        public static Task<ContentDialogResult> ShowActionAsync(XamlRoot xamlRoot, FrameworkElement text, string action, FrameworkElement icon, ToastPlacementMode placement, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null, CancellationToken cancellationToken = default)
        {
            var toast = ShowImpl(xamlRoot, text, icon, placement, requestedTheme, dismissAfter, action: action);
            if (toast?.Content is Grid content)
            {
                toast.MaxWidth = 500;

                if (cancellationToken != default)
                {
                    cancellationToken.Register(() =>
                    {
                        toast.IsOpen = false;
                    });
                }

                var tsc = new TaskCompletionSource<ContentDialogResult>();
                var undo = new Button()
                {
                    Content = action,
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                    Style = BootStrapper.Current.Resources["AccentTextButtonStyle"] as Style,
                    Margin = new Thickness(8, -4, -4, -4),
                    Padding = new Thickness(4, 5, 4, 6)
                };

                void handler(object sender, RoutedEventArgs e)
                {
                    Logger.Info("closed");

                    tsc.TrySetResult(ContentDialogResult.Primary);
                    undo.Click -= handler;

                    toast.IsOpen = false;
                }

                void closed(ToastPopup sender, object e)
                {
                    tsc.TrySetResult(ContentDialogResult.None);
                    sender.Closed -= closed;
                }

                undo.Click += handler;
                toast.Closed += closed;

                Grid.SetColumn(undo, 2);
                content.Children.Add(undo);

                return tsc.Task;
            }

            return Task.FromResult(ContentDialogResult.None);
        }

        public static Task<ContentDialogResult> ShowCountdownAsync(XamlRoot xamlRoot, string text, string action, TimeSpan dismissAfter, ElementTheme requestedTheme = ElementTheme.Dark)
        {
            return ShowCountdownAsync(xamlRoot, ClientEx.ParseMarkdown(text), action, dismissAfter, ToastPlacementMode.Center, requestedTheme);
        }

        public static Task<ContentDialogResult> ShowCountdownAsync(XamlRoot xamlRoot, FormattedText text, string action, TimeSpan dismissAfter, ElementTheme requestedTheme = ElementTheme.Dark)
        {
            return ShowCountdownAsync(xamlRoot, text, action, dismissAfter, ToastPlacementMode.Center, requestedTheme);
        }

        public static Task<ContentDialogResult> ShowCountdownAsync(XamlRoot xamlRoot, FormattedText text, string action, TimeSpan dismissAfter, ToastPlacementMode placement, ElementTheme requestedTheme = ElementTheme.Dark)
        {
            var animated = new Grid
            {
                Width = 32,
                Height = 32,
                Margin = new Thickness(-4, -12, 8, -12)
            };

            var slice = new SelfDestructTimer
            {
                Background = new SolidColorBrush(Colors.Transparent),
                Foreground = new SolidColorBrush(Colors.White),
                Center = 16,
                Radius = 14.5
            };

            var total = (int)dismissAfter.TotalSeconds;

            slice.Maximum = total;
            slice.Value = DateTime.Now.Add(dismissAfter);

            var value = new AnimatedTextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 1, 0, 3),
                Text = total.ToString()
            };

            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1),
            };

            void handler(object sender, object e)
            {
                total--;

                if (total == 0)
                {
                    timer.Tick -= handler;
                    timer.Stop();
                }
                else
                {
                    value.Text = total.ToString();
                }
            }

            timer.Tick += handler;
            timer.Start();

            animated.Children.Add(slice);
            animated.Children.Add(value);

            return ShowActionAsync(xamlRoot, text, action, animated, placement, requestedTheme, dismissAfter);
        }

        public event EventHandler<TextUrlClickEventArgs> Click;

        // Used by TextBlockHelper
        public void OnClick(string url)
        {
            Click?.Invoke(this, new TextUrlClickEventArgs(url));
        }
    }

    // A toast is neither focusable nor navigable, so it reports as a pane rather than as
    // something to be reached, and ToastPopup reads it out on open instead.
    public partial class ToastPopupAutomationPeer : FrameworkElementAutomationPeer
    {
        public ToastPopupAutomationPeer(ToastPopup owner)
            : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore()
        {
            return AutomationControlType.Pane;
        }
    }
}
