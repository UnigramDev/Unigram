//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Numerics;
using System.Text;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Native.Graphics;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.Views.Wallet;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Messages.Service
{
    public sealed partial class MessageTonWalletTransferContent : MessageService, IWalletTransferSite
    {
        public MessageTonWalletTransferContent()
        {
            InitializeComponent();

            var sheen = new WalletCardSheen();
            if (sheen.Update(new Windows.Foundation.Size(50, 50)))
            {
                Pattern.Background = new ImageBrush
                {
                    Stretch = Stretch.UniformToFill,
                    ImageSource = sheen.Source
                };
            }

            VisualUtilities.DropShadow(Address, radius: 0, opacity: 1.0f,
                target: AddressShadow, color: Colors.White, offset: new Vector3(1, 0, 0));
        }

        private readonly Color _ribbonFailedTop = Color.FromArgb(0xFF, 0xFF, 0x5B, 0x54);
        private readonly Color _ribbonFailedBottom = Color.FromArgb(0xFF, 0xED, 0x1D, 0x27);

        private readonly Color _ribbonSentTop = Color.FromArgb(0xFF, 0x0A, 0xCC, 0x0A);
        private readonly Color _ribbonSentBottom = Color.FromArgb(0xFF, 0x00, 0xBF, 0x00);

        private readonly Color _ribbonReceivedTop = Color.FromArgb(0xFF, 0x49, 0xBC, 0xFF);
        private readonly Color _ribbonReceivedBottom = Color.FromArgb(0xFF, 0x32, 0xAD, 0xFF);

        protected override void UpdateContent(MessageViewModel message)
        {
            if (message.Content is not MessageTonWalletTransfer transfer)
            {
                return;
            }

            UpdateSending(message);

            var user = message.ClientService.GetUser(message.Chat);
            var self = message.ClientService.GetUser(message.ClientService.Options.MyId);

            var sent = message.IsOutgoing;
            var amount = Formatter.TonBalance(Math.Abs(transfer.Amount));

            AmountInteger.Text = (sent ? "-" : "+") + amount.Integer;
            AmountFraction.Text = amount.Fraction;

            if (user == null || self == null)
            {
                return;
            }

            var builder = new StringBuilder();

            for (int i = 0; i < transfer.PeerAddress.Length; i += 4)
            {
                if (i > 0)
                {
                    builder.Append(i == 24 ? "\n" : " ");
                }

                builder.Append(transfer.PeerAddress.Substring(i, 4).ToUpperInvariant());
            }

            Address.Text = builder.ToString();
            Domain.Text = user.FullName().ToUpper();

            Ribbon.Text = message.SendingState switch
            {
                MessageSendingStatePending => Strings.WalletTransferStatusSending,
                MessageSendingStateFailed => Strings.WalletTransferStatusFailed,
                _ => message.IsOutgoing
                    ? Strings.WalletTransferStatusSent
                    : Strings.WalletTransferStatusReceived
            };

            (RibbonTop.Color, RibbonBottom.Color) = message.SendingState switch
            {
                MessageSendingStateFailed => (_ribbonFailedTop, _ribbonFailedBottom),
                _ => message.IsOutgoing
                    ? (_ribbonSentTop, _ribbonSentBottom)
                    : (_ribbonReceivedTop, _ribbonReceivedBottom)
            };

            if (transfer == null || (transfer.Comment.Length == 0 && !transfer.IsCommentEncrypted))
            {
                CommentRoot?.Visibility = Visibility.Collapsed;
                return;
            }

            if (CommentRoot == null)
            {
                FindName(nameof(CommentRoot));
                Comment.TextEntityClick += OnTextEntityClick;
            }

            CommentRoot.Visibility = Visibility.Visible;

            if (transfer.IsCommentEncrypted)
            {
                var entities = new TextEntity[]
                {
                    new TextEntity(0, CommentPlaceholder.Length, new TextEntityTypeSpoiler())
                };

                Comment.SetText(message.ClientService, new FormattedText(CommentPlaceholder, entities));
            }
            else
            {
                Comment.SetText(message.ClientService, transfer.Comment.AsFormattedText());
            }
        }

        private void OnTextEntityClick(object sender, TextEntityClickEventArgs e)
        {
            e.Handled = true;
        }

        private const string CommentPlaceholder = "encrypted comment";

        public override void Recycle()
        {
            base.Recycle();

            WalletTransferFlight.BindMessage(this, 0);
            Diamond.IsPaused = false;

            _awaited = null;
            _ribbonRequested = false;
            _landedAt = DateTime.MinValue;

            StopRibbonTimer();
            RemoveClock();

            if (_impact != null)
            {
                _impact.StopAnimation("Press");
                _impact.InsertScalar("Press", 0);
            }
        }

        #region Sending

        // The ribbon's own middle, where its text is drawn and where the clock stands in for it.
        private static readonly Vector2 RibbonCentre = new(37, 23);

        // The clock is the wallet row's, drawn for a 46 pixel avatar: a little larger here, where
        // it covers a ribbon rather than the corner of a photo.
        private const float ClockScale = 1.1f;

        private static readonly TimeSpan ClockToRibbonDuration = WalletTransferFlight.Slow(TimeSpan.FromMilliseconds(260));

        // How long the clock is shown at least once the stone has landed, as the wallet row stays
        // raised: a transfer refused at once would otherwise lose its clock as the stone arrives.
        private static readonly TimeSpan MinimumClock = WalletTransferFlight.Slow(TimeSpan.FromSeconds(1));

        private ShapeVisual _clock;
        private CompositionScopedBatch _clockBatch;

        // The message the send screen's stone is flying to, until it lands or goes elsewhere.
        private MessageViewModel _awaited;

        private bool _ribbonRequested;
        private DispatcherTimer _ribbonTimer;
        private DateTime _landedAt = DateTime.MinValue;

        // The card's press, made the first time a stone is on its way to it.
        private CompositionPropertySet _impact;

        /// <summary>
        /// Called on every bind and every update, which is how the sending state arrives: TDLib
        /// replaces the message in place when it is sent, or fails to be, and the container is
        /// updated with it.
        /// </summary>
        private void UpdateSending(MessageViewModel message)
        {
            var pending = message.SendingState as MessageSendingStatePending;

            if (_awaited != null && ReferenceEquals(message, _awaited))
            {
                // Still the message the stone is flying to - TDLib replaces its state in place -
                // so it keeps it, whatever it says now: a transfer refused at once is failed while
                // the stone is still in the air. The clock waits for it to land.
                if (pending == null)
                {
                    RequestRibbon();
                }

                return;
            }

            // Blank while the send screen's stone is on its way: it is moved into this panel when it
            // lands, and a panel already showing one would show two.
            var incoming = WalletTransferFlight.BindMessage(this, pending?.SendingId ?? 0);

            _awaited = incoming ? message : null;
            Diamond.IsPaused = incoming;

            if (pending != null)
            {
                ShowClock();
            }
            else if (_clock != null)
            {
                RequestRibbon();
            }
        }

        /// <summary>
        /// The transfer is over: the clock turns into the ribbon, but not before the stone has
        /// landed and the clock has been seen for a moment.
        /// </summary>
        private void RequestRibbon()
        {
            _ribbonRequested = true;

            if (_awaited != null || _ribbonTimer != null)
            {
                return;
            }

            var delay = _landedAt + MinimumClock - DateTime.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                _ribbonTimer = new DispatcherTimer { Interval = delay };
                _ribbonTimer.Tick += RibbonTimer_Tick;
                _ribbonTimer.Start();
                return;
            }

            _ribbonRequested = false;

            if (_clock != null)
            {
                ClockToRibbon();
            }
        }

        private void RibbonTimer_Tick(object sender, object e)
        {
            StopRibbonTimer();
            RequestRibbon();
        }

        private void StopRibbonTimer()
        {
            if (_ribbonTimer != null)
            {
                _ribbonTimer.Tick -= RibbonTimer_Tick;
                _ribbonTimer.Stop();
                _ribbonTimer = null;
            }
        }

        private void ShowClock()
        {
            if (_clock != null)
            {
                return;
            }

            var compositor = ElementCompositionPreview.GetElementVisual(ClockHost).Compositor;

            // The row's clock, in the white the rest of the message is drawn in and with nothing
            // behind it: it stands over the card, not a photo it has to be told apart from.
            _clock = WalletTransferVisuals.CreateClock(compositor, ClockScale, Colors.White, null, null, out var centre);
            _clock.Offset = new Vector3(RibbonCentre - centre, 0);

            ElementCompositionPreview.SetElementChildVisual(ClockHost, _clock);

            // The ribbon waits under it, folded into the clock's middle, for the clock to turn
            // into it.
            var ribbon = ElementCompositionPreview.GetElementVisual(Ribbon);
            ribbon.StopAnimation("Opacity");
            ribbon.StopAnimation("Scale");
            ribbon.CenterPoint = new Vector3(RibbonCentre, 0);
            ribbon.Opacity = 0;
            ribbon.Scale = new Vector3(0.3f, 0.3f, 1);
        }

        /// <summary>
        /// The transfer has been sent, or has failed: the clock shrinks away into the ribbon's
        /// middle as the ribbon, already saying which, opens out of it.
        /// </summary>
        private void ClockToRibbon()
        {
            var ribbon = ElementCompositionPreview.GetElementVisual(Ribbon);
            var compositor = ribbon.Compositor;
            var easing = WalletTransferVisuals.Standard(compositor);

            var appear = compositor.CreateScalarKeyFrameAnimation();
            appear.InsertKeyFrame(1, 1, easing);
            appear.Duration = ClockToRibbonDuration;

            var grow = compositor.CreateVector3KeyFrameAnimation();
            grow.InsertKeyFrame(1, Vector3.One, easing);
            grow.Duration = ClockToRibbonDuration;

            var disappear = compositor.CreateScalarKeyFrameAnimation();
            disappear.InsertKeyFrame(1, 0, easing);
            disappear.Duration = ClockToRibbonDuration;

            var shrink = compositor.CreateVector3KeyFrameAnimation();
            shrink.InsertKeyFrame(1, new Vector3(0, 0, 1), easing);
            shrink.Duration = ClockToRibbonDuration;

            var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            ribbon.StartAnimation("Opacity", appear);
            ribbon.StartAnimation("Scale", grow);
            _clock.StartAnimation("Opacity", disappear);
            _clock.StartAnimation("Scale", shrink);
            batch.End();

            _clockBatch = batch;
            batch.Completed += ClockToRibbon_Completed;
        }

        private void ClockToRibbon_Completed(object sender, CompositionBatchCompletedEventArgs args)
        {
            if (sender is CompositionScopedBatch batch)
            {
                batch.Completed -= ClockToRibbon_Completed;
            }

            // Not a clock this message has since shown again, or a recycled one.
            if (sender == _clockBatch)
            {
                RemoveClock();
            }
        }

        // At once, back to the ribbon alone.
        private void RemoveClock()
        {
            _clockBatch = null;

            if (_clock == null)
            {
                return;
            }

            ElementCompositionPreview.SetElementChildVisual(ClockHost, null);

            _clock.Dispose();
            _clock = null;

            var ribbon = ElementCompositionPreview.GetElementVisual(Ribbon);
            ribbon.StopAnimation("Opacity");
            ribbon.StopAnimation("Scale");
            ribbon.Opacity = 1;
            ribbon.Scale = Vector3.One;
        }

        #endregion

        #region IWalletTransferSite

        public FrameworkElement Anchor => Diamond;

        // The stone takes the place of this panel's own, at its size.
        public Rect LandingBounds(Size stone)
        {
            return Diamond.TransformToVisual(null).TransformBounds(new Rect(0, 0, Diamond.ActualWidth, Diamond.ActualHeight));
        }

        // Asked for by the flight as it sets off, so made then: the card squashes and is pushed down
        // a little as the stone lands, like a wallet row.
        public CompositionPropertySet Impact
        {
            get
            {
                if (_impact == null)
                {
                    var visual = ElementCompositionPreview.GetElementVisual(Card);
                    var compositor = visual.Compositor;

                    _impact = compositor.CreatePropertySet();
                    _impact.InsertScalar("Press", 0);

                    ElementCompositionPreview.SetIsTranslationEnabled(Card, true);

                    var centre = compositor.CreateExpressionAnimation("Vector3(this.Target.Size.X * 0.5, this.Target.Size.Y * 0.5, 0)");

                    var scale = compositor.CreateExpressionAnimation("Vector3(1 + 0.012 * r.Press, 1 - 0.04 * r.Press, 1)");
                    scale.SetReferenceParameter("r", _impact);

                    var translation = compositor.CreateExpressionAnimation("Vector3(0, 4 * r.Press, 0)");
                    translation.SetReferenceParameter("r", _impact);

                    visual.StartAnimation("CenterPoint", centre);
                    visual.StartAnimation("Scale", scale);
                    visual.StartAnimation("Translation", translation);
                }

                return _impact;
            }
        }

        public void ScheduleImpact(TimeSpan delay)
        {
            var impact = Impact;
            impact.StartAnimation("Press", WalletTransferVisuals.CreatePress(impact.Compositor, delay));
        }

        public WalletTransferLanding Land(WalletTransferFlight flight, Scene3DPanel stone)
        {
            if (!stone.TransferTo(Diamond))
            {
                return WalletTransferLanding.Refused;
            }

            // It arrives turning at the flight's speed and this panel's own idle turn takes over as
            // that runs down, rather than stopping dead on contact.
            Diamond.Kick(WalletTransferFlight.FlightSpinSpeed);

            _awaited = null;
            _landedAt = DateTime.UtcNow;

            if (_ribbonRequested)
            {
                RequestRibbon();
            }

            return WalletTransferLanding.Taken;
        }

        public void ForgetStone()
        {
            _awaited = null;

            if (_ribbonRequested)
            {
                RequestRibbon();
            }

            // Its own stone after all, and no stone to push it down.
            Diamond.IsPaused = false;

            if (_impact != null)
            {
                _impact.StopAnimation("Press");
                _impact.InsertScalar("Press", 0);
            }
        }

        #endregion

        private void Service_Click(object sender, RoutedEventArgs e)
        {
            if (Message?.Delegate != null)
            {
                Message.Delegate.ExecuteServiceMessage(Message);
            }
        }
    }
}
