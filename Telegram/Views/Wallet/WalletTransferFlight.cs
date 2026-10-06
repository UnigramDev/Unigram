//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Numerics;
using Telegram.Common;
using Telegram.Controls.Cells;
using Telegram.Native.Graphics;
using Telegram.Td.Api;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Views.Wallet
{
    /// <summary>
    /// Carries the send screen's diamond to the history row the transfer becomes.
    /// </summary>
    /// <remarks>
    /// Neither end knows the other, and their order is not fixed: the state change that adds the
    /// row reaches the list through a dispatch that may run inline, so the row can be bound before
    /// the popup's await on SendAsync resumes or after it. The flight is therefore registered
    /// before the transfer is sent, keyed by what the popup knows up front and the pending row
    /// repeats verbatim - the address it was given and the amount - and whichever of the launch
    /// and the landing site comes second starts it.
    ///
    /// One stone for the whole way, taken off the send screen with TransferTo rather than
    /// recreated: a new panel starts from rest, faded out and held still, and could not be made to
    /// match the one being typed at. The row has no stone of its own: the flight lands on its
    /// glyph, which is hidden while the stone sits there spinning, and gives way to it again when
    /// the transfer is over.
    /// </remarks>
    public sealed class WalletTransferFlight
    {
#if DEBUG
        // How many times slower the send transition runs, to check it frame by frame. Applies to
        // every duration, timer and spin speed of the flight and of the raised row, and to nothing
        // else - the list's own add transition keeps its speed.
        private const double DebugSlowMotion = 1;
#else
        private const double DebugSlowMotion = 1;
#endif

        internal static TimeSpan Slow(TimeSpan duration)
        {
            return DebugSlowMotion == 1 ? duration : TimeSpan.FromTicks((long)(duration.Ticks * DebugSlowMotion));
        }

        // The prototype's: a linear clock on a quadratic curve is a thrown body, steady sideways
        // and falling ever faster, and the rise is how far above the higher end it is thrown.
        private static readonly TimeSpan FlightDuration = Slow(TimeSpan.FromMilliseconds(600));
        private const float Rise = 150;

        // Degrees per second while thrown, against an idle turn of about 34.
        private const double FlightSpinSpeed = 540 / DebugSlowMotion;

        // How long a launched stone waits for somewhere to land. A send from a chat has no row on
        // screen to go to, and the stone goes out where it is instead.
        private static readonly TimeSpan SiteTimeout = Slow(TimeSpan.FromSeconds(1));

        private static readonly TimeSpan FadeDuration = Slow(TimeSpan.FromMilliseconds(200));

        // A quick swell as it goes, for a stone that lands somewhere with nothing to hold it.
        private const float PopScale = 1.6f;
        private static readonly TimeSpan PopDuration = Slow(TimeSpan.FromMilliseconds(150));

        // Degrees per second while held on the row, waiting for the transfer.
        private const double RowSpinSpeed = 300 / DebugSlowMotion;

        // When the transfer is over it shrinks away as the glyph comes back, on the row's own
        // settle: the prototype's gem goes to 0.6 and out while its flat diamond comes in.
        private const float SettleScale = 0.6f;

        // Degrees per second added to the spin as it goes.
        private const double ReleaseKick = 1200 / DebugSlowMotion;
        private static readonly TimeSpan SettleDuration = Slow(TimeSpan.FromMilliseconds(260));

        // What the send screen's panel stands in for: the GRAM glyph beside the amount, at 40, with
        // the panel drawn out from its box by its margin. Both are WalletSendPopup's markup.
        private const double SourceGlyphSize = 40;
        private static readonly Vector2 SourceOffset = new(-6, -14);

        // What those proportions miss on the row's glyph, set by eye: the stone came out small and
        // high. Grown about the glyph's centre, then moved down, in the window's pixels.
        private const float LandingSize = 1.2f;
        private static readonly Vector2 LandingNudge = new(0, 6);

        // One UI thread per window, hence the lock.
        private static readonly object _flightsLock = new();
        private static readonly List<WalletTransferFlight> _flights = new();

        private readonly XamlRoot _xamlRoot;
        private readonly string _address;
        private readonly long _nanograms;

        private Popup _overlay;
        private Scene3DPanel _stone;
        private Vector3 _from;

        // Where the stone is at rest - its start, then the end of the flight once it has one - and
        // the scale it is drawn at there.
        private Vector3 _at;
        private float _scale = 1;

        private WalletTransactionCell _site;

        // The list key of the row it found, which it follows from then on.
        private string _rowKey;
        private DispatcherTimer _timeout;

        private bool _siteReady;

        private bool _launched;
        private bool _flying;
        private bool _landed;

        private WalletTransferFlight(XamlRoot xamlRoot, string address, long nanograms)
        {
            _xamlRoot = xamlRoot;
            _address = address;
            _nanograms = nanograms;
        }

        /// <summary>
        /// Says a transfer is about to be sent from this window, before it is, so that the row it
        /// becomes waits for the stone rather than growing one of its own.
        /// </summary>
        /// <remarks>
        /// Every flight is either launched or cancelled: one left registered would hold the first
        /// matching row empty.
        /// </remarks>
        public static WalletTransferFlight Expect(XamlRoot xamlRoot, string address, long nanograms)
        {
            var flight = new WalletTransferFlight(xamlRoot, address, nanograms);

            lock (_flightsLock)
            {
                _flights.Add(flight);
            }

            return flight;
        }

        /// <summary>
        /// Called for every history row bound or recycled: makes it the destination of the flight
        /// carrying its transfer, if one is on its way, and stops it being any other's.
        /// </summary>
        /// <param name="transaction">What the row now shows, or null when it is recycled.</param>
        /// <param name="transaction">What the row now shows, or null when it is recycled.</param>
        /// <param name="key">The row's key in the list, which a transaction that replaced a pending
        /// row shares with it.</param>
        /// <returns>Whether a stone is on its way to this row.</returns>
        public static bool Bind(WalletTransactionCell site, TonWalletTransaction transaction, string key)
        {
            WalletTransferFlight target = null;
            List<WalletTransferFlight> left = null;

            var xamlRoot = site.XamlRoot;
            var pending = transaction?.State is TonWalletTransactionStatePending && transaction.Type is TonWalletTransactionTypeTransfer;
            var nanograms = transaction?.Type is TonWalletTransactionTypeTransfer transfer ? -transfer.Amount : 0;

            lock (_flightsLock)
            {
                foreach (var flight in _flights)
                {
                    if (target == null && transaction != null && ReferenceEquals(flight._xamlRoot, xamlRoot) && flight.Matches(site, transaction, key, pending, nanograms))
                    {
                        target = flight;
                    }
                    else if (flight._site == site)
                    {
                        left ??= new List<WalletTransferFlight>();
                        left.Add(flight);
                    }
                }
            }

            if (left != null)
            {
                foreach (var flight in left)
                {
                    flight.Leave(site);
                }
            }

            target?.Arrive(site, key);
            return target != null;
        }

        private bool Matches(WalletTransactionCell site, TonWalletTransaction transaction, string key, bool pending, long nanograms)
        {
            // Popping, it has nowhere left to go, and a row told otherwise would wait for it.
            if (_landed)
            {
                return false;
            }

            // Once it has found its row it follows it by key, whatever the row says by then: a
            // transfer refused at once is already failed while the stone is in the air, and the
            // list may have moved it to another container on the way.
            if (_rowKey != null)
            {
                return string.Equals(_rowKey, key, StringComparison.Ordinal);
            }

            return pending
                && _nanograms == nanograms
                && (_site == null || _site == site)
                && string.Equals(_address, transaction.PeerAddress, StringComparison.Ordinal);
        }

        public void Cancel()
        {
            Leave(_site);
            Unregister();
        }

        /// <summary>
        /// Takes the stone off the send screen. Called while the popup is still up, so that its
        /// exit animation does not carry the stone down with it.
        /// </summary>
        public void Launch(Scene3DPanel source)
        {
            if (_launched)
            {
                return;
            }

            _launched = true;

            // Where the source panel sits, which is where its stone is drawn: the overlay covers the
            // window from its origin, so a position in the window is a translation in the overlay.
            _from = new Vector3(source.TransformToVector2(null), 0);
            _at = _from;

            _stone = new Scene3DPanel
            {
                Model = Scene3DModel.Diamond,
                IsSpinning = true,
                IsInteractive = false,
                SpinSpeed = FlightSpinSpeed,
                Width = source.ActualWidth,
                Height = source.ActualHeight,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };

            ElementCompositionPreview.SetIsTranslationEnabled(_stone, true);

            var visual = ElementCompositionPreview.GetElementVisual(_stone);
            visual.Properties.InsertVector3("Translation", _from);

            // Not hit testable: the window under it has to keep working while the stone is in the
            // air, and the popup closing is part of that.
            _overlay = new Popup
            {
                XamlRoot = _xamlRoot,
                IsHitTestVisible = false,
                Child = _stone
            };

            _overlay.IsOpen = true;

            // Before the overlay panel is loaded, which TransferTo allows: the stone is held there
            // stopped until the panel is shown, and the source goes blank at the same moment.
            source.TransferTo(_stone);

            // Even with a row already here: one that leaves before it is laid out would otherwise
            // leave the stone hanging.
            _timeout = new DispatcherTimer { Interval = SiteTimeout };
            _timeout.Tick += Timeout_Tick;
            _timeout.Start();

            TryFly();
        }

        private void Arrive(WalletTransactionCell site, string key)
        {
            _rowKey = key;

            if (_site == site)
            {
                return;
            }

            Leave(_site);

            // Not measured yet. Rows are bound from ContainerContentChanging, which is raised
            // during measure: the text has not been arranged, so the glyph has no position, and
            // the container has not been placed, so the row's own is stale or the container's.
            // The end of the next layout pass is the first moment both are true.
            _site = site;
            _siteReady = false;
            _site.Anchor.LayoutUpdated += Anchor_LayoutUpdated;
        }

        private void Leave(WalletTransactionCell site, bool landed = false)
        {
            if (site == null || _site != site)
            {
                return;
            }

            _site.Anchor.LayoutUpdated -= Anchor_LayoutUpdated;

            if (!landed)
            {
                _site.PendingRow?.ForgetStone();
            }

            _site = null;
            _siteReady = false;

            // Already in the air, it carries on and pops where it was aimed: a stone that turned
            // around would be stranger than one that arrives at a row that has just moved.
        }

        private void Anchor_LayoutUpdated(object sender, object e)
        {
            if (_site == null)
            {
                return;
            }

            // Once is all that is needed, and the event is raised for every layout pass in the
            // window, not only for passes that touch this element.
            _site.Anchor.LayoutUpdated -= Anchor_LayoutUpdated;
            _siteReady = true;

            TryFly();
        }

        private void Timeout_Tick(object sender, object e)
        {
            StopTimeout();

            if (!_flying)
            {
                Leave(_site);
                FadeOut();
            }
        }

        private void StopTimeout()
        {
            if (_timeout != null)
            {
                _timeout.Tick -= Timeout_Tick;
                _timeout.Stop();
                _timeout = null;
            }
        }

        private void TryFly()
        {
            if (!_launched || _flying || _site == null || !_siteReady)
            {
                return;
            }

            _flying = true;
            StopTimeout();

            // The send screen's panel stands in for a glyph of SourceGlyphSize, offset from it by
            // its own margin, so the same proportions put the stone over the row's glyph: scaled
            // to its font size, and centred on its line.
            var glyph = _site.GlyphBounds(out var fontSize);
            var scale = (float)(fontSize / SourceGlyphSize) * LandingSize;

            var target = new Vector3(
                (float)(glyph.X + (glyph.Width - SourceGlyphSize * scale) / 2 + SourceOffset.X * scale) + LandingNudge.X,
                (float)(glyph.Y + (glyph.Height - SourceGlyphSize * scale) / 2 + SourceOffset.Y * scale) + LandingNudge.Y,
                0);

            // The control point: a little way across, and well above whichever end is higher.
            var control = new Vector3(_from.X + (target.X - _from.X) * 0.35f, Math.Min(_from.Y, target.Y) - Rise, 0);

            var visual = ElementCompositionPreview.GetElementVisual(_stone);
            var compositor = visual.Compositor;

            // Scaled about the corner the translation moves, so that both ends of the curve are
            // exact: the panel's top left is what is measured at each.
            visual.CenterPoint = Vector3.Zero;

            var props = compositor.CreatePropertySet();
            props.InsertScalar("Progress", 0);
            props.InsertVector3("A", _from);
            props.InsertVector3("B", target);
            props.InsertVector3("C", control);
            props.InsertScalar("S", scale);

            // The row's press, which the impact starts on this animation's last frame: the stone
            // squashes about its foot as the row is pushed down, with the same arithmetic Hold
            // takes over with. A scale about the foot is the corner's plus a translation, folded in
            // here because the curve is measured at the corner.
            var row = _site.PendingRow?.Properties;
            if (row == null)
            {
                row = compositor.CreatePropertySet();
                row.InsertScalar("Press", 0);
            }

            const string Size = "(1 + (p.S - 1) * p.Progress)";
            const string SquashX = "(1 + 0.12 * r.Press)";
            const string SquashY = "(1 - 0.18 * r.Press)";

            var translation = compositor.CreateExpressionAnimation(
                "(1 - p.Progress) * (1 - p.Progress) * p.A + 2 * (1 - p.Progress) * p.Progress * p.C + p.Progress * p.Progress * p.B"
                + $" + Vector3({Size} * f.X * (1 - {SquashX}), {Size} * f.Y * (1 - {SquashY}) + 4 * r.Press, 0)");
            translation.SetReferenceParameter("p", props);
            translation.SetReferenceParameter("r", row);
            translation.SetVector2Parameter("f", Foot);

            var size = compositor.CreateExpressionAnimation($"Vector3({Size} * {SquashX}, {Size} * {SquashY}, 1)");
            size.SetReferenceParameter("p", props);
            size.SetReferenceParameter("r", row);

            visual.StartAnimation("Translation", translation);
            visual.StartAnimation("Scale", size);

            var progress = compositor.CreateScalarKeyFrameAnimation();
            progress.InsertKeyFrame(1, 1, compositor.CreateLinearEasingFunction());
            progress.Duration = FlightDuration;

            var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            props.StartAnimation("Progress", progress);
            batch.End();

            batch.Completed += Flight_Completed;

            // On the compositor's clock, so the row is pushed down on the frame the stone arrives
            // rather than when the UI thread hears that it has.
            _site.PendingRow?.ScheduleImpact(FlightDuration);

            // The property set has to outlive the expressions reading it, and nothing else holds it.
            _progress = props;

            _at = target;
            _scale = scale;
        }

        private CompositionPropertySet _progress;

        // What the stone squashes about when the row is pushed down: near its foot, as a thing
        // landing is. In the panel's own pixels, the same in the air and once held.
        private Vector2 Foot => new((float)_stone.Width * 0.5f, (float)_stone.Height * 0.8f);

        // The list it is held over, and how far it was scrolled when the stone landed.
        private ScrollViewer _scroller;
        private double _scrolled;

        private void Flight_Completed(object sender, CompositionBatchCompletedEventArgs args)
        {
            if (sender is CompositionScopedBatch batch)
            {
                batch.Completed -= Flight_Completed;
            }

            _landed = true;

            // Nothing is matched against it any more: the row it landed on holds it from here.
            Unregister();

            var site = _site;
            Leave(site, landed: true);

            if (site?.PendingRow != null && site.PendingRow.Land(this))
            {
                Hold(site);
            }
            else
            {
                Disappear(PopScale, PopDuration);
            }
        }

        /// <summary>
        /// Stays on the row's glyph, spinning faster, for as long as the transfer is under way -
        /// squashed by the row's press as it lands, and carried along when the list scrolls.
        /// </summary>
        /// <remarks>
        /// In the overlay rather than in the row, so it stays the one stone it has been since the
        /// send screen. Which is why it has to be told about scrolling: the overlay does not move
        /// with the list.
        /// </remarks>
        private void Hold(WalletTransactionCell site)
        {
            _stone.SpinSpeed = RowSpinSpeed;

            var visual = ElementCompositionPreview.GetElementVisual(_stone);
            var compositor = visual.Compositor;

            visual.StopAnimation("Translation");
            visual.StopAnimation("Scale");

            // Squashed about a point near its foot, as a thing landing is. A scale about the corner
            // and the same scale about this point differ by a translation, which is folded in so the
            // stone does not move at rest.
            var centre = new Vector3(Foot, 0);
            visual.CenterPoint = centre;

            var press = site.PendingRow.Properties;

            var scale = compositor.CreateExpressionAnimation("Vector3(s * (1 + 0.12 * r.Press), s * (1 - 0.18 * r.Press), 1)");
            scale.SetReferenceParameter("r", press);
            scale.SetScalarParameter("s", _scale);

            // The row is pushed down by its press as well, and the list moves under the overlay.
            _scroller = site.GetParent<ScrollViewer>();
            _scrolled = _scroller?.VerticalOffset ?? 0;

            ExpressionAnimation translation;

            if (_scroller != null)
            {
                translation = compositor.CreateExpressionAnimation("t + Vector3(0, 4 * r.Press + m.Translation.Y + y, 0)");
                translation.SetReferenceParameter("m", ElementCompositionPreview.GetScrollViewerManipulationPropertySet(_scroller));
                translation.SetScalarParameter("y", (float)_scrolled);
            }
            else
            {
                translation = compositor.CreateExpressionAnimation("t + Vector3(0, 4 * r.Press, 0)");
            }

            translation.SetReferenceParameter("r", press);
            translation.SetVector3Parameter("t", _at - centre * (1 - _scale));

            visual.StartAnimation("Scale", scale);
            visual.StartAnimation("Translation", translation);
        }

        /// <summary>
        /// The transfer is over: the stone gives way to the row's glyph, which comes back as it
        /// goes.
        /// </summary>
        public void Release()
        {
            if (_stone == null)
            {
                return;
            }

            if (_scroller != null)
            {
                // Where the scroll left it, which the expression carried it to.
                _at.Y += (float)(_scrolled - _scroller.VerticalOffset);
                _scroller = null;
            }

            // Whirls as it goes. Not the prototype's, which only shrinks it away; the kick runs
            // down on its own, over about as long as the stone takes to vanish.
            _stone.Kick(ReleaseKick);

            Disappear(SettleScale, SettleDuration);
        }

        // Unclaimed, it shrinks away where it is.
        private void FadeOut()
        {
            Disappear(0.5f, FadeDuration);
        }

        // To a multiple of the scale it has now, about its own centre.
        private void Disappear(float factor, TimeSpan duration)
        {
            if (_stone == null)
            {
                Close();
                return;
            }

            var visual = ElementCompositionPreview.GetElementVisual(_stone);
            var compositor = visual.Compositor;

            // The flight's animations stop where they are, and the centre point moves to the middle
            // without the stone jumping: a scale about the corner and the same scale about the
            // centre differ by a translation, which is folded in here.
            // From what the flight was told rather than read back: an animated property reads as the
            // last value set on it, not as where the animation left it.
            visual.StopAnimation("Translation");
            visual.StopAnimation("Scale");

            var size = _scale;
            var centre = new Vector3((float)_stone.ActualWidth / 2, (float)_stone.ActualHeight / 2, 0);

            visual.Properties.InsertVector3("Translation", _at - centre * (1 - size));
            visual.Scale = new Vector3(size, size, 1);
            visual.CenterPoint = centre;

            var opacity = compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(1, 0);
            opacity.Duration = duration;

            var scale = compositor.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(1, new Vector3(size * factor, size * factor, 1));
            scale.Duration = duration;

            var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            visual.StartAnimation("Opacity", opacity);
            visual.StartAnimation("Scale", scale);
            batch.End();

            batch.Completed += Fade_Completed;
        }

        private void Fade_Completed(object sender, CompositionBatchCompletedEventArgs args)
        {
            if (sender is CompositionScopedBatch batch)
            {
                batch.Completed -= Fade_Completed;
            }

            Close();
        }

        private void Close()
        {
            StopTimeout();
            Leave(_site);
            Unregister();

            if (_overlay != null)
            {
                // Out of the tree is what gives a panel's device back, if it still has one.
                _overlay.IsOpen = false;
                _overlay.Child = null;
                _overlay = null;
            }

            _stone = null;
            _progress = null;
        }

        private void Unregister()
        {
            lock (_flightsLock)
            {
                _flights.Remove(this);
            }
        }
    }
}
