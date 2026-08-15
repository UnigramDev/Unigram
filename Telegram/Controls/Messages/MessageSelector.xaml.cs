//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using System.Numerics;
using Telegram.Assets.Icons;
using Telegram.Common;
using Telegram.Composition;
using Telegram.Controls.Chats;
using Telegram.Controls.Messages.Content;
using Telegram.Controls.Messages.Service;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Composition.Interactions;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Automation.Provider;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Messages
{
    // TEMP: revert to ToggleButtonEx
    public sealed partial class MessageSelector : ToggleButtonEx2
    {
        private Border Header;
        private Border Footer;
        private Border Icon;
        private ContentPresenter Presenter;

        private bool _templateApplied;

        private bool _selected;

        private MessageViewModel _message;
        private ChatHistoryView _owner;

        public MessageSelector()
        {
            DefaultStyleKey = typeof(MessageSelector);

            Instrumentation.Register(this);
        }

        public MessageSelector(MessageViewModel message, UIElement child)
            : this()
        {
            _message = message;
            Content = child;
        }

#if INSTRUMENTATION
        internal System.Collections.Generic.IEnumerable<object> DebugChildren()
        {
            if (Content != null)
            {
                yield return Content;
            }
        }
#endif

        private TextSelectionManager _textSelectionManager;

        public bool HasSelection => _textSelectionManager?.HasSelection ?? false;

        public void CopySelectionToClipboard() => _textSelectionManager?.CopySelectionToClipboard();

        public FormattedText GetSelectedText() => _textSelectionManager?.GetSelectedText();

        public FormattedText GetSelectedSourceText(out int position)
        {
            if (_textSelectionManager == null)
            {
                position = 0;
                return null;
            }

            return _textSelectionManager.GetSelectedSourceText(out position);
        }

        // The manager needs the content element, which isn't guaranteed to be there yet when
        // Loaded fires: a container can be connected before it has been given any content.
        // Building the manager then threw ArgumentNullException(root) out of the Loaded handler
        // and took the app down, so attach whenever a content element is actually present -
        // on load, and again whenever the content itself changes.
        //
        // IsConnected is required: only OnUnloaded and OnContentChanged detach the manager, so
        // attaching while disconnected would leave the pointer handlers on the content forever,
        // and through them root this control for the rest of the session.
        private void EnsureTextSelectionManager()
        {
            if (IsConnected && Content is UIElement root)
            {
                // Built once and re-pointed afterwards. A container is handed new content
                // every time it comes back round the history, and the manager is the same
                // shape each time: only the element it watches differs.
                if (_textSelectionManager == null)
                {
                    _textSelectionManager = new TextSelectionManager(this, root);
                }
                else
                {
                    _textSelectionManager.Attach(root);
                }
            }
        }

        protected override void OnContentChanged(object oldContent, object newContent)
        {
            base.OnContentChanged(oldContent, newContent);

            // The manager's pointer handlers live on the previous content element, so they have
            // to come off before attaching to the new one - otherwise they outlive the element
            // they were added to and the manager keeps reporting selection for content that is
            // no longer displayed.
            _textSelectionManager?.Detach();

            EnsureTextSelectionManager();
        }

        public bool IsTrackerEnabled { get; set; } = true;

        protected override void OnLoaded()
        {
            EnsureTextSelectionManager();

            if (_trackerOwner == null && RootGrid != null && IsTrackerEnabled && (SettingsService.Current.SwipeToReply || SettingsService.Current.SwipeToShare || SettingsService.Current.SwipeToGoBack))
            {
                _compositor = BootStrapper.Current.Compositor;
                _container ??= _compositor.CreateContainerVisual();

                if (_requiresArrange)
                {
                    _container.Size = ActualSize;
                }
                else
                {
                    _container.RelativeSizeAdjustment = Vector2.One;
                }

                ElementCompositionPreview.SetElementChildVisual(this, _container);
                ConfigureInteractionTracker();
            }

            if (_trackerOwner != null)
            {
                _trackerOwner.ValuesChanged += OnValuesChanged;
                _trackerOwner.InertiaStateEntered += OnInertiaStateEntered;
                _trackerOwner.InteractingStateEntered += OnInteractingStateEntered;
                _trackerOwner.IdleStateEntered += OnIdleStateEntered;
            }
        }

        protected override void OnUnloaded()
        {
            // Detached, not dropped: the container goes back to the pool and comes out again
            // with new content, and Detach leaves the manager clean for it.
            _textSelectionManager?.Detach();

            // Recycled mid-gesture the tracker never reaches idle, and the chip would stay bound to
            // a container already on its way back to the pool. Gated on _interacting: this runs for
            // every container recycled while scrolling, and the walk to the root is not free.
            if (_back && _interacting)
            {
                this.GetParent<MasterDetailView>()?.DetachBackGesture(_tracker);
            }

            if (_trackerOwner != null)
            {
                _trackerOwner.ValuesChanged -= OnValuesChanged;
                _trackerOwner.InertiaStateEntered -= OnInertiaStateEntered;
                _trackerOwner.InteractingStateEntered -= OnInteractingStateEntered;
                _trackerOwner.IdleStateEntered -= OnIdleStateEntered;
            }
        }

        public MessageViewModel Message => _message;

        public void Recycle()
        {
            if (Content is MessageBubble bubble)
            {
                bubble.Recycle();
            }
            else if (Content is IContent content)
            {
                content.Recycle();
            }

            _message = null;
            _owner = null;
        }

        private void CreateIcon()
        {
            if (Icon != null || !_selectionEnabled)
            {
                return;
            }

            var visual = GetVisual(BootStrapper.Current.Compositor, out var source, out _props);

            _source = source;
            _previous = visual;

            Icon = GetTemplateChild(nameof(Icon)) as Border;
            ElementCompositionPreview.SetIsTranslationEnabled(Icon, true);
            ElementCompositionPreview.SetElementChildVisual(Icon, visual?.RootVisual);

            RegisterPropertyChangedCallback(BackgroundProperty, OnBackgroundChanged);
            OnBackgroundChanged(this, BackgroundProperty);

            if (IsAlbumChild)
            {
                if (_message.Content is MessagePhoto or MessageVideo)
                {
                    Icon.VerticalAlignment = VerticalAlignment.Top;
                    Icon.HorizontalAlignment = HorizontalAlignment.Right;
                    Icon.Margin = new Thickness(0, 4, 6, 0);
                }
                else
                {
                    Icon.VerticalAlignment = VerticalAlignment.Bottom;
                    Icon.HorizontalAlignment = HorizontalAlignment.Left;
                    Icon.Margin = new Thickness(28, 0, 0, 0);
                }

                Grid.SetColumn(Icon, 1);
            }
        }

        private void OnBackgroundChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (_source != null && Background is SolidColorBrush background)
            {
                _source.SetColorProperty("Color_FF0000", background.Color);
            }
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            RootGrid = GetTemplateChild(nameof(RootGrid)) as Grid;
            ElementCompositionPreview.SetIsTranslationEnabled(RootGrid, true);

            Presenter = GetTemplateChild(nameof(Presenter)) as ContentPresenter;
            ElementCompositionPreview.SetIsTranslationEnabled(Presenter, true);

            _hitTest = ElementComposition.GetElementVisual(this);
            _visual = ElementComposition.GetElementVisual(Presenter);
            _templateApplied = true;

            if (_message?.Delegate != null)
            {
                UpdateMessage(_message, _owner, _message.Delegate.IsSelectionEnabled);
            }
        }

        protected override void OnToggle()
        {
            if (_selectionEnabled && _message is MessageViewModel message)
            {
                base.OnToggle();

                CreateIcon();
                UpdateIcon(IsChecked is true, true);

                if (IsChecked is true)
                {
                    message.Delegate.Select(message);
                }
                else
                {
                    message.Delegate.Unselect(message, true);
                }
            }
        }

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                try
                {
                    _interactionSource.TryRedirectForManipulation(e.GetCurrentPoint(this));
                }
                catch (Exception)
                {
                    // Ignoring the failed redirect to prevent app crashing
                }
            }

            if (e.OriginalSource is Grid { Name: "RootGrid" } or TextBlock { Name: "Label" })
            {
                _owner?.OnPointerPressed(this, e);
            }

            try
            {
                base.OnPointerPressed(e);
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }
        }

        protected override void OnPointerEntered(PointerRoutedEventArgs e)
        {
            _owner?.OnPointerEntered(this, e);

            try
            {
                base.OnPointerEntered(e);
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }
        }

        protected override void OnPointerMoved(PointerRoutedEventArgs e)
        {
            _owner?.OnPointerMoved(this, e);

            try
            {
                base.OnPointerMoved(e);
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }
        }

        protected override void OnPointerReleased(PointerRoutedEventArgs e)
        {
            _owner?.OnPointerReleased(this, e);

            try
            {
                base.OnPointerReleased(e);
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }
        }

        protected override void OnDoubleTapped(DoubleTappedRoutedEventArgs e)
        {
            if (e.OriginalSource is Grid { Name: "RootGrid" } or TextBlock { Name: "Label" })
            {
                _owner?.OnDoubleTapped(_message, e);
            }

            base.OnDoubleTapped(e);
        }

        public void UpdateMessage(MessageViewModel message, ChatHistoryView owner, bool selectionEnabled)
        {
            _message = message;
            _owner = owner;

            if (message == null || !_templateApplied)
            {
                return;
            }

            UpdateSelectionEnabled(selectionEnabled, false);
            UpdateMessageSuggestedPostInfo(message);
            UpdateMessageStakeDice(message);
        }

        private bool _hasSuggestedPostInfo;

        public void UpdateMessageSuggestedPostInfo(MessageViewModel message)
        {
            if (message == null || !_templateApplied)
            {
                return;
            }

            if (message.SuggestedPostInfo != null)
            {
                _hasSuggestedPostInfo = true;
                Header ??= GetTemplateChild(nameof(Header)) as Border;
                Header.Child = new SuggestedPostInfoCell(message);
            }
            else if (_hasSuggestedPostInfo)
            {
                _hasSuggestedPostInfo = false;
                Header.Child = null;
            }
        }

        private bool _hasStakeDice;

        public void UpdateMessageStakeDice(MessageViewModel message)
        {
            if (message == null || !_templateApplied)
            {
                return;
            }

            if (message.Content is MessageStakeDice { Value: not 0 } && !message.GeneratedContentUnread)
            {
                _hasStakeDice = true;
                Footer ??= GetTemplateChild(nameof(Footer)) as Border;
                Footer.Child = new StakeDiceInfoCell(message);
            }
            else if (_hasStakeDice)
            {
                _hasStakeDice = false;
                Footer.Child = null;
            }
        }

        private bool _selectionEnabled;

        public void UpdateSelectionEnabled(bool value, bool animate)
        {
            if (_message is MessageViewModel message && _templateApplied)
            {
                var selected = value && message.Delegate.SelectedItems.ContainsKey(message.Id);
                if (selected == _selected && value == _selectionEnabled)
                {
                    return;
                }

                _selectionEnabled = value;

                _interactionSource?.PositionXSourceMode = value
                    ? InteractionSourceMode.Disabled
                    : InteractionSourceMode.EnabledWithInertia;

                IsChecked = _selected = selected;
                IsDoubleTapEnabled = !value;
                Presenter.IsHitTestVisible = !value || IsAlbum;

                CreateIcon();

                var presenter = ElementComposition.GetElementVisual(Presenter);
                var outgoing = (message.IsOutgoing && !message.IsChannelPost) || (message.IsSaved && message.ForwardInfo?.Source is { IsOutgoing: true });

                if (animate)
                {
                    var offset = BootStrapper.Current.Compositor.CreateScalarKeyFrameAnimation();
                    offset.InsertKeyFrame(0, value ? -36 : 0);
                    offset.InsertKeyFrame(1, value ? 0 : -36);

                    if (Icon != null)
                    {
                        UpdateIcon(IsChecked is true, true);

                        var scale = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();
                        scale.InsertKeyFrame(0, value ? Vector3.Zero : Vector3.One);
                        scale.InsertKeyFrame(1, value ? Vector3.One : Vector3.Zero);

                        var icon = ElementComposition.GetElementVisual(Icon);
                        icon.CenterPoint = new Vector3(12, 12, 0);
                        icon.StartAnimation("Scale", scale);

                        if (IsAlbumChild)
                        {
                            icon.Properties.InsertVector3("Translation", new Vector3());
                        }
                        else
                        {
                            icon.StartAnimation("Translation.X", offset);
                        }
                    }

                    if (!outgoing && !IsAlbumChild)
                    {
                        offset.InsertKeyFrame(0, value ? 0 : 36);
                        offset.InsertKeyFrame(1, value ? 36 : 0);

                        presenter.StartAnimation("Offset.X", offset);
                    }
                    else
                    {
                        presenter.Offset = Vector3.Zero;
                    }
                }
                else
                {
                    if (Icon != null)
                    {
                        UpdateIcon(IsChecked is true, false);

                        var icon = ElementComposition.GetElementVisual(Icon);
                        icon.Scale = value ? Vector3.One : Vector3.Zero;

                        if (IsAlbumChild)
                        {
                            icon.Properties.InsertVector3("Translation", new Vector3());
                        }
                        else
                        {
                            icon.Properties.InsertVector3("Translation", new Vector3(value ? 0 : -36, 0, 0));
                        }
                    }

                    if (!outgoing && !IsAlbumChild)
                    {
                        presenter.Offset = new Vector3(value ? 36 : 0, 0, 0);
                    }
                    else
                    {
                        presenter.Offset = Vector3.Zero;
                    }
                }
            }

            if (Content is MessageBubble bubble && bubble.MediaTemplateRoot is AlbumContent album)
            {
                album.UpdateSelectionEnabled(value, animate);
            }
        }

        public void UpdateSelection()
        {
            var message = _message;
            if (message != null && _templateApplied)
            {
                bool selected;
                if (_selectionEnabled)
                {
                    if (message.Content is MessageAlbum album)
                    {
                        selected = album.Messages.All(x => message.Delegate.SelectedItems.ContainsKey(x.Id));
                    }
                    else
                    {
                        selected = message.Delegate.SelectedItems.ContainsKey(message.Id);
                    }
                }
                else
                {
                    selected = false;
                }

                if (selected != _selected)
                {
                    IsChecked = _selected = selected;
                    Presenter.IsHitTestVisible = !_selectionEnabled || IsAlbum;

                    CreateIcon();
                    UpdateIcon(IsChecked is true, true);

                    var peer = FrameworkElementAutomationPeer.CreatePeerForElement(this);
                    peer?.RaiseAutomationEvent(AutomationEvents.SelectionItemPatternOnElementSelected);
                }
            }
        }

        public void UpdateSelection(long messageId)
        {
            // Album container: any member changing can flip the album-level "all selected" state,
            // so recompute it unconditionally (don't gate on messageId — the album group's Id
            // aliases one child's Id). Then refresh the specific child sub-message.
            if (Content is MessageBubble bubble && bubble.MediaTemplateRoot is AlbumContent album)
            {
                UpdateSelection();
                album.UpdateSelection(messageId);
            }
            else if (_message?.Id == messageId)
            {
                UpdateSelection();
            }
        }


        // This should be held in memory, or animation will stop
        private CompositionPropertySet _props;

        private IAnimatedVisual _previous;
        private IAnimatedVisualSource2 _source;

        private IAnimatedVisual GetVisual(Compositor compositor, out IAnimatedVisualSource2 source, out CompositionPropertySet properties)
        {
            source = new Select();

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
            properties.InsertScalar("Progress", 0.0F);

            var progressAnimation = compositor.CreateExpressionAnimation("_.Progress");
            progressAnimation.SetReferenceParameter("_", properties);
            visual.RootVisual.Properties.InsertScalar("Progress", 0.0F);
            visual.RootVisual.Properties.StartAnimation("Progress", progressAnimation);

            return visual;
        }

        private void UpdateIcon(bool selected, bool animate)
        {
            if (_props != null && _previous != null)
            {
                if (animate)
                {
                    var linearEasing = _props.Compositor.CreateLinearEasingFunction();
                    var animation = _props.Compositor.CreateScalarKeyFrameAnimation();
                    animation.Duration = _previous.Duration;
                    animation.InsertKeyFrame(1, selected ? 1 : 0, linearEasing);

                    _props.StartAnimation("Progress", animation);
                }
                else
                {
                    _props.InsertScalar("Progress", selected ? 1.0F : 0.0F);
                }
            }
        }

        private bool IsAlbum => _message?.Content is MessageAlbum;

        private bool IsAlbumChild => _message != null && _message.Content is not MessageAlbum && _message.MediaAlbumId != 0;

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new MessageSelectorAutomationPeer(this);
        }

        #region Moved from ChatHistoryViewItem

        public Visual ContentVisual => _visual;

        private Visual _hitTest;
        private Visual _visual;
        private Compositor _compositor;
        private ContainerVisual _container;
        private ContainerVisual _indicator;

        private WeakInteractionTrackerOwner _trackerOwner;
        private InteractionTracker _tracker;
        private VisualInteractionSource _interactionSource;
        private bool _interacting;

        private static readonly bool _requiresArrange = !ApiInfo.IsWindows11;

        private bool _share;
        private bool _reply;
        private bool _back;

        private Grid RootGrid;

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (_container != null && _requiresArrange)
            {
                _container.Size = finalSize.ToVector2();
            }

            return base.ArrangeOverride(finalSize);
        }

        private void ConfigureInteractionTracker()
        {
            if (IsAlbumChild)
            {
                return;
            }

            _interactionSource = VisualInteractionSource.Create(_hitTest);

            //Configure for x-direction panning
            _interactionSource.ManipulationRedirectionMode = VisualInteractionSourceRedirectionMode.CapableTouchpadOnly;
            _interactionSource.PositionXSourceMode = _selectionEnabled
                ? InteractionSourceMode.Disabled
                : InteractionSourceMode.EnabledWithInertia;
            _interactionSource.PositionXChainingMode = InteractionChainingMode.Never;
            _interactionSource.IsPositionXRailsEnabled = true;

            _trackerOwner = new WeakInteractionTrackerOwner();

            //Create tracker and associate interaction source
            _tracker = InteractionTracker.CreateWithOwner(_compositor, _trackerOwner);
            _tracker.InteractionSources.Add(_interactionSource);

            _tracker.MaxPosition = new Vector3(_reply ? 72 : 0);
            _tracker.MinPosition = new Vector3(_share || _back ? -72 : 0);

            _tracker.Properties.InsertBoolean("CanReply", _reply);
            _tracker.Properties.InsertBoolean("CanShare", _share);

            //ConfigureAnimations(_visual, null);
            ConfigureRestingPoints();

            if (_interacting)
            {
                _interacting = false;
                _visual.Properties.InsertVector3("Translation", Vector3.Zero);
            }
        }

        private void ConfigureRestingPoints()
        {
            var neutralX = InteractionTrackerInertiaRestingValue.Create(_compositor);
            neutralX.Condition = _compositor.CreateExpressionAnimation("true");
            neutralX.RestingValue = _compositor.CreateExpressionAnimation("0");

            _tracker.ConfigurePositionXInertiaModifiers(new InteractionTrackerInertiaModifier[] { neutralX });
        }

        private void ConfigureAnimations(Visual visual)
        {
            // Create an animation that changes the offset of the photoVisual and shadowVisual based on the manipulation progress
            var offsetExp = _compositor.CreateExpressionAnimation("(tracker.Position.X > 0 && !tracker.CanReply) || (tracker.Position.X <= 0 && !tracker.CanShare) ? 0 : -tracker.Position.X");
            //var photoOffsetExp = _visual.Compositor.CreateExpressionAnimation("tracker.Position.X > 0 && !tracker.CanReply || tracker.Position.X <= 0 && !tracker.CanShare ? 0 : Max(-72, Min(72, -tracker.Position.X))");
            //var photoOffsetExp = _visual.Compositor.CreateExpressionAnimation("-tracker.Position.X");
            offsetExp.SetReferenceParameter("tracker", _tracker);
            visual.StartAnimation("Translation.X", offsetExp);
        }

        // td_api packs the type of a message in the low bits of its identifier, with the
        // server id shifted up by MessageId::SERVER_ID_SHIFT. Nothing below the shift is
        // set once the server has confirmed the message.
        private const long MessageTypeMask = (1L << 20) - 1;
        private const long MessageTypeYetUnsent = 1;
        private const long MessageTypeLocal = 2;
        private const long MessageShortTypeMask = 3;

        // MessageId(ServerMessageId(1)), the message a channel is created with.
        private const long FirstServerMessageId = 1L << 20;

        private static bool IsServerMessage(long messageId)
        {
            return messageId > 0 && (messageId & MessageTypeMask) == 0;
        }

        /// <summary>
        /// Whether the message can be forwarded, after MessagesManager::can_forward_message.
        /// </summary>
        private static bool CanBeForwarded(MessageViewModel message)
        {
            // Self destructing, scheduled, or not yet acknowledged by the server.
            if (message.SelfDestructType != null || message.SchedulingState != null || !IsServerMessage(message.Id))
            {
                return false;
            }

            if (message.Chat?.Type is ChatTypeSecret)
            {
                return false;
            }

            // The same protected content test, already carried on the message: it is off
            // when the message is marked no-forwards, when the chat protects its content,
            // and when the content is secret.
            if (!message.CanBeSaved)
            {
                return false;
            }

            return message.Content switch
            {
                // Nothing to carry over without text or a link preview.
                MessageText text => text.Text?.Text.Length > 0 || text.LinkPreview != null,

                MessageUnsupported => false,

                // TDLib also turns down a poll that is still local. It cannot be one
                // here: a message that has not reached the server has no server id, and
                // the caller has already let only settled messages through.

                // Service and expired messages are left behind. IsService covers both,
                // which is why the expired contents are not named here.
                _ => !message.Content.IsService()
            };
        }

        /// <summary>
        /// Whether the message can be replied to, here or in another chat, after
        /// MessagesManager::can_reply_to_message and can_reply_to_message_in_another_dialog.
        /// </summary>
        private static bool CanBeReplied(MessageViewModel message)
        {
            var chat = message.Chat;
            if (chat == null || (message.Id & MessageShortTypeMask) == MessageTypeYetUnsent)
            {
                return false;
            }

            // A message a channel was created with is not a reply target, in either sense.
            var channel = chat.Type is ChatTypeSupergroup { IsChannel: true };
            if (message.Id == FirstServerMessageId && channel)
            {
                return false;
            }

            // Replying in this chat.
            var local = (message.Id & MessageShortTypeMask) == MessageTypeLocal;
            if ((!local || chat.Type is ChatTypeSecret) && chat.CanSendBasicMessages(message.ClientService))
            {
                return true;
            }

            // Or quoting it into another one, which needs it forwardable and on the
            // server, and is not offered by the chats that only carry direct messages.
            return CanBeForwarded(message)
                && IsServerMessage(message.Id)
                && !(message.ClientService.TryGetSupergroup(chat, out var supergroup) && supergroup.IsDirectMessagesGroup);
        }

        public void PrepareForItemOverride(MessageViewModel message, bool canReply)
        {
            bool share = false;
            bool reply = false;

            if (message.SendingState == null)
            {
                // Derived here rather than asked of TDLib. GetMessageProperties was a
                // request per message scrolled into view, answered on the one thread that
                // also carries every update, and it came back after the container had
                // been bound to something else, so the gesture was configured for a
                // message that had already left it.
                share = SettingsService.Current.SwipeToShare && CanBeForwarded(message);

                // canReply is deliberately not consulted: it is not reliable yet, and it
                // is wrong in the costlier direction. Offering the gesture on the few
                // messages that turn out not to accept a reply is a smaller failure than
                // withholding it wherever the flag is wrong, which reads as the feature
                // being broken.
                reply = SettingsService.Current.SwipeToReply && CanBeReplied(message);
            }

            // Back takes the direction Share is not using. The precedence is deliberate: a user who
            // wants to go back from anywhere turns Share off, and there is no state where a setting
            // is on but silently does nothing. CanShare stays false either way, which is what keeps
            // the bubble still under a back swipe - the offset expression already reads it.
            var back = !share && SettingsService.Current.SwipeToGoBack;

            if (_tracker != null)
            {
                if (_share != share)
                {
                    _tracker.Properties.InsertBoolean("CanShare", share);
                }

                if (_share != share || _back != back)
                {
                    _tracker.MinPosition = new Vector3(share || back ? -72 : 0);
                }

                if (_reply != reply)
                {
                    _tracker.Properties.InsertBoolean("CanReply", reply);
                    _tracker.MaxPosition = new Vector3(reply ? 72 : 0);
                }

                if (_tracker.Position.X != 0)
                {
                    _tracker.TryUpdatePosition(new Vector3());
                }

                try
                {
                    if (_visual != null && _visual.Offset.X != 0)
                    {
                        _visual.Offset = new Vector3();
                    }
                }
                catch
                {
                    // ???
                }
            }

            _share = share;
            _reply = reply;
            _back = back;
        }

        private void OnValuesChanged(InteractionTracker sender, InteractionTrackerValuesChangedArgs args)
        {
            // Only for a direction that has something to show. A back swipe travels the same way a
            // forward one would, and without the test it would build this indicator - and start a
            // surface load for it - on every bubble it crosses, to then hold it at zero opacity.
            if (_indicator == null && ((sender.Position.X > 0.0001f && _reply) || (sender.Position.X < -0.0001f && _share)))
            {
                var sprite = _compositor.CreateSpriteVisual();
                sprite.Size = new Vector2(30, 30);
                sprite.CenterPoint = new Vector3(15);

                var surface = LoadedImageSurface.StartLoadFromUri(new Uri("ms-appx:///Assets/Images/Reply.png"));
                void handler(LoadedImageSurface s, LoadedImageSourceLoadCompletedEventArgs args)
                {
                    s.LoadCompleted -= handler;
                    sprite.Brush = _compositor.CreateSurfaceBrush(s);
                }

                surface.LoadCompleted += handler;

                var ellipse = _compositor.CreateEllipseGeometry();
                ellipse.Radius = new Vector2(15);

                var ellipseShape = _compositor.CreateSpriteShape(ellipse);
                ellipseShape.FillBrush = _compositor.CreateColorBrush((Windows.UI.Color)Navigation.BootStrapper.Current.Resources["MessageServiceBackgroundColor"]);
                ellipseShape.Offset = new Vector2(15);

                var shape = _compositor.CreateShapeVisual();
                shape.Shapes.Add(ellipseShape);
                shape.Size = new Vector2(30, 30);

                _indicator = _compositor.CreateContainerVisual();
                _indicator.Children.InsertAtBottom(shape);
                _indicator.Children.InsertAtTop(sprite);
                _indicator.Size = new Vector2(30, 30);
                _indicator.CenterPoint = new Vector3(15);
                _indicator.Scale = new Vector3();

                _container.Children.InsertAtTop(_indicator);

                //ElementCompositionPreview.SetElementChildVisual(this, _indicator);
                //ElementCompositionPreview.SetElementChildVisual(this, _container);
            }

            var offset = (sender.Position.X > 0 && !_reply) || (sender.Position.X <= 0 && !_share) ? 0 : Math.Max(0, Math.Min(72, Math.Abs(sender.Position.X)));

            var abs = Math.Abs(offset);
            var percent = abs / 72f;

            var width = (float)ActualWidth;
            var height = (float)ActualHeight;

            if (_indicator != null)
            {
                _indicator.Offset = new Vector3(sender.Position.X > 0 ? width - percent * 60 : -30 + percent * 55, (height - 30) / 2, 0);
                _indicator.Scale = new Vector3(sender.Position.X > 0 ? 0.8f + percent * 0.2f : -(0.8f + percent * 0.2f), 0.8f + percent * 0.2f, 1);
                _indicator.Opacity = percent;
            }
        }

        private void OnInertiaStateEntered(InteractionTracker sender, InteractionTrackerInertiaStateEnteredArgs args)
        {
            if (Message != null)
            {
                if (sender.Position.X >= 72 && _reply)
                {
                    _owner.ViewModel.ReplyToMessage(Message);
                }
                else if (sender.Position.X <= -72 && _share)
                {
                    _owner.ViewModel.ForwardMessage(Message);
                }
                else if (sender.Position.X <= -72 && _back)
                {
                    this.GetParent<MasterDetailView>()?.CommitBackGesture();
                }
            }
        }

        private void OnIdleStateEntered(InteractionTracker sender, InteractionTrackerIdleStateEnteredArgs args)
        {
            _interacting = false;

            // Before the disconnected branch: OnUnloaded tears the container down, and from there
            // the tree walk to the MasterDetailView no longer finds it.
            if (_back)
            {
                this.GetParent<MasterDetailView>()?.DetachBackGesture(sender);
            }

            if (IsDisconnected)
            {
                OnUnloaded();
            }
            else
            {
                ConfigureAnimations(_visual);
            }
        }

        private void OnInteractingStateEntered(InteractionTracker sender, InteractionTrackerInteractingStateEnteredArgs args)
        {
            _interacting = true;
            ConfigureAnimations(_visual);

            // The chip belongs to the MasterDetailView, which cannot win this manipulation for
            // itself: our source claims the contact first, and chaining is off. So we hand it the
            // tracker instead and it binds the chip to ours for the length of the gesture. Once per
            // gesture, so the tree walk is not worth caching - and a cached root would outlive this
            // container, which is pooled.
            if (_back)
            {
                this.GetParent<MasterDetailView>()?.AttachBackGesture(sender);
            }
        }

        #endregion

        public partial class MessageSelectorAutomationPeer : ToggleButtonAutomationPeer, ISelectionItemProvider
        {
            private readonly MessageSelector _owner;

            public MessageSelectorAutomationPeer(MessageSelector owner)
                : base(owner)
            {
                _owner = owner;
            }

            protected override string GetNameCore()
            {
                if (_owner.Content is MessageBubble bubble)
                {
                    return bubble.GetAutomationName() ?? base.GetNameCore();
                }
                else if (_owner.ContentTemplateRoot is MessageBubble child)
                {
                    return child.GetAutomationName() ?? base.GetNameCore();
                }
                else if (_owner.Message != null)
                {
                    return Automation.GetSummary(_owner.Message, true);
                }

                return base.GetNameCore();
            }

            protected override object GetPatternCore(PatternInterface patternInterface)
            {
                if (patternInterface == PatternInterface.SelectionItem)
                {
                    return this;
                }
                else if (patternInterface == PatternInterface.Toggle)
                {
                    return null;
                }

                return base.GetPatternCore(patternInterface);
            }

            protected override AutomationControlType GetAutomationControlTypeCore()
            {
                return AutomationControlType.ListItem;
            }

            protected override int GetPositionInSetCore()
            {
                if (_owner._owner != null)
                {
                    return 1 + _owner._owner.Items.IndexOf(_owner.Message);
                }

                return base.GetPositionInSetCore();
            }

            protected override int GetSizeOfSetCore()
            {
                if (_owner._owner != null)
                {
                    return _owner._owner.Items.Count;
                }

                return base.GetSizeOfSetCore();
            }

            public void AddToSelection()
            {
                _owner._owner.SelectedItems.Add(_owner.Message);
            }

            public void RemoveFromSelection()
            {
                _owner._owner.SelectedItems.Remove(_owner.Message);
            }

            public void Select()
            {
                _owner._owner.SelectedItems.Add(_owner.Message);
            }

            public bool IsSelected => _owner._selected;

            public IRawElementProviderSimple SelectionContainer
            {
                get
                {
                    if (_owner._owner != null)
                    {
                        var peer = FrameworkElementAutomationPeer.CreatePeerForElement(_owner._owner);
                        return ProviderFromPeer(peer);
                    }

                    return null;
                }
            }
        }
    }
}
