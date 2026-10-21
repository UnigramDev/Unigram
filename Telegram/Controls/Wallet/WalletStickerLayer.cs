//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Numerics;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Wallet
{
    public sealed class WalletStickerContextRequestedEventArgs : EventArgs
    {
        public WalletStickerContextRequestedEventArgs(WalletStickerView sticker, ContextRequestedEventArgs args)
        {
            Sticker = sticker;
            Args = args;
        }

        public WalletStickerView Sticker { get; }

        public ContextRequestedEventArgs Args { get; }
    }

    /// <summary>
    /// The gifts stuck on the wallet card, under its print, and everything done to them there:
    /// landing, moving, turning, sizing, peeling off. The window also has one of its own, read-only,
    /// over everything, to carry a gift from the list to the card.
    /// </summary>
    /// <remarks>
    /// It sits in the card's face, so everything here is in face units, 360 by 220, whatever size
    /// the card is shown at. A sticker is placed where it is dropped and nowhere else, and keeps
    /// the size it is given.
    ///
    /// What leaves the card is the window's to deal with, through <see cref="HomeResolver"/> and
    /// the events: where a gift's cell is, the menu, saving.
    /// </remarks>
    public sealed partial class WalletStickerLayer : Canvas
    {
        public const float FaceWidth = 360;
        public const float FaceHeight = 220;

        // setTonWalletCardStickers takes up to 10.
        public const int MaximumStickers = 10;

        private const float MinimumScale = 0.5f;
        private const float MaximumScale = 1.6f;

        // A press becomes a drag past this many face units, about AutomaticDragHelper's distance.
        private const float PanThreshold = 8;

        // A turn within this many degrees of upright, or of a quarter turn, settles on it.
        private const float SnapDegrees = 4;

        private const float KeyboardStep = 2;
        private const float KeyboardTurn = 15;
        private const float KeyboardScale = 0.05f;

        // Where a gift added from the menu goes, in order, skipping any spot already taken: there
        // is no drop point to put it at.
        private static readonly Vector2[] Spots =
        {
            new(270, 50), new(90, 45), new(180, 40), new(300, 170), new(60, 175),
            new(180, 175), new(245, 115), new(120, 120), new(330, 110), new(30, 110)
        };

        private readonly List<WalletStickerView> _stickers = new();
        private readonly Dictionary<uint, Vector2> _pointers = new();

        private WalletStickerFrame _frame;
        private WalletStickerView _selected;
        private WalletStickerView _preview;
        private Vector2 _previewMotion;

        private enum Gesture
        {
            None,
            Press,
            Move,
            Handle,
            Pinch
        }

        private Gesture _gesture;
        private WalletStickerView _target;
        private Vector2 _pressPoint;
        private Vector2 _grab;
        private Vector2 _motion;

        private bool _handleLeft;
        private float _handleScale;
        private float _handleDistance;

        private int _z;
        private bool _rendering;
        private bool _active;
        private bool _held;
        private TimeSpan _lastTime;

        public WalletStickerLayer()
        {
            PointerPressed += OnPointerPressed;
            PointerMoved += OnPointerMoved;
            PointerReleased += OnPointerReleased;
            PointerCanceled += OnPointerCanceled;
            PointerCaptureLost += OnPointerCanceled;
            KeyDown += OnKeyDown;
            GotFocus += OnGotFocus;
            ContextRequested += OnContextRequested;
            Unloaded += OnUnloaded;
        }

        /// <summary>
        /// The ring round the selected sticker, which the card draws over its print.
        /// </summary>
        public WalletStickerFrame Frame
        {
            get => _frame;
            set
            {
                if (_frame != null)
                {
                    _frame.PointerPressed -= OnFramePointerPressed;
                }

                _frame = value;

                if (_frame != null)
                {
                    _frame.PointerPressed += OnFramePointerPressed;
                }
            }
        }

        /// <summary>
        /// Someone else's card: the stickers are shown, and touching them does nothing.
        /// </summary>
        public bool IsReadOnly { get; set; }

        /// <summary>
        /// The card's own layer: stickers on it are cut by its rounded edge, and the part of one
        /// hanging over it takes no input.
        /// </summary>
        public bool IsClipped { get; set; }

        /// <summary>
        /// The centre of a gift's cell, on the card, and the size of its art against a sticker's;
        /// null while the cell is not on screen.
        /// </summary>
        public Func<TonNft, (Vector2 Center, float Scale)?> HomeResolver { get; set; }

        /// <summary>
        /// What the card holds changed, and settled: worth saving.
        /// </summary>
        public event EventHandler Changed;

        /// <summary>
        /// The stickers took the places they are going to, by an edit or by <see cref="Show"/>.
        /// </summary>
        public event EventHandler Arranged;

        private void OnChanged()
        {
            Changed?.Invoke(this, EventArgs.Empty);
            Arranged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// A sticker that left the card has finished leaving.
        /// </summary>
        public event EventHandler<TonNft> StickerLeft;

        public event EventHandler<WalletStickerContextRequestedEventArgs> StickerContextRequested;

        /// <summary>
        /// A sticker in hand or moving, so possibly beyond the card's edge.
        /// </summary>
        public bool IsActive => _active;

        public event EventHandler ActiveChanged;

        private void UpdateActive()
        {
            var active = _target != null || _rendering;
            if (_active != active)
            {
                _active = active;
                ActiveChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// A sticker being moved, turned or sized by hand, past a press.
        /// </summary>
        public bool IsHeld => _held;

        public event EventHandler HeldChanged;

        private void UpdateHeld()
        {
            var held = _gesture is Gesture.Move or Gesture.Handle or Gesture.Pinch;
            if (_held != held)
            {
                _held = held;
                HeldChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public int Count
        {
            get
            {
                var count = 0;

                foreach (var sticker in _stickers)
                {
                    if (!sticker.IsLeaving && sticker != _preview)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public bool Contains(string address)
        {
            return Find(address) != null;
        }

        public WalletStickerView Find(string address)
        {
            foreach (var sticker in _stickers)
            {
                if (sticker.Address == address && !sticker.IsLeaving && sticker != _preview)
                {
                    return sticker;
                }
            }

            return null;
        }

        // The card's corners, as WalletCard rounds them.
        public const float CornerRadius = FaceWidth * (3.18f / 85.60f);

        public static bool IsOnCard(Vector2 point)
        {
            return point.X >= 0 && point.X <= FaceWidth && point.Y >= 0 && point.Y <= FaceHeight;
        }

        /// <summary>
        /// Whether a sticker on the card, where it is going, lies under any part of
        /// <paramref name="bounds"/> on the face. Taken as the round that takes the pointer, rather
        /// than its square.
        /// </summary>
        public bool IsUnder(Rect bounds)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return false;
            }

            foreach (var sticker in _stickers)
            {
                if (sticker.IsLeaving || sticker == _preview)
                {
                    continue;
                }

                var radius = WalletStickerView.HitShare / 2 * WalletStickerView.BaseSize * sticker.Scale.Target;
                var x = Math.Clamp(sticker.X.Target, (float)bounds.Left, (float)bounds.Right) - sticker.X.Target;
                var y = Math.Clamp(sticker.Y.Target, (float)bounds.Top, (float)bounds.Bottom) - sticker.Y.Target;

                if (x * x + y * y < radius * radius)
                {
                    return true;
                }
            }

            return false;
        }

        #region Card

        /// <summary>
        /// The card as saved, at once: whatever was on it goes.
        /// </summary>
        public void Show(IClientService clientService, TonWalletCard card)
        {
            Clear();

            if (card?.Stickers == null)
            {
                return;
            }

            foreach (var sticker in card.Stickers)
            {
                if (sticker.Nft?.Gift == null || sticker.Position == null || Contains(sticker.Nft.Address))
                {
                    continue;
                }

                var position = sticker.Position;
                var view = Create(clientService, sticker.Nft);

                view.Place(
                    new Vector2((float)(position.XPercentage / 100 * FaceWidth), (float)(position.YPercentage / 100 * FaceHeight)),
                    Math.Clamp((float)(position.WidthPercentage / 100 * FaceWidth / WalletStickerView.BaseSize), MinimumScale, MaximumScale),
                    (float)position.RotationAngle);
                view.Apply();
            }

            Arranged?.Invoke(this, EventArgs.Empty);
        }

        public void Clear()
        {
            Deselect();
            CancelGesture();

            foreach (var sticker in _stickers)
            {
                sticker.Release();
            }

            _stickers.Clear();
            _preview = null;

            Children.Clear();
            StopRendering();

            Arranged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// The stickers as setTonWalletCardStickers takes them, bottom first.
        /// </summary>
        public IReadOnlyList<InputTonWalletCardSticker> ToInput()
        {
            var stickers = new List<WalletStickerView>(_stickers.Count);

            foreach (var sticker in _stickers)
            {
                if (!sticker.IsLeaving && sticker != _preview)
                {
                    stickers.Add(sticker);
                }
            }

            stickers.Sort(static (x, y) => Canvas.GetZIndex(x).CompareTo(Canvas.GetZIndex(y)));

            var input = new List<InputTonWalletCardSticker>(stickers.Count);

            foreach (var sticker in stickers)
            {
                var size = WalletStickerView.BaseSize * sticker.Scale.Target;
                var rotation = sticker.Rotation.Target % 360;

                input.Add(new InputTonWalletCardSticker(new AreaPosition(
                    sticker.X.Target / FaceWidth * 100,
                    sticker.Y.Target / FaceHeight * 100,
                    size / FaceWidth * 100,
                    size / FaceHeight * 100,
                    rotation < 0 ? rotation + 360 : rotation,
                    0), sticker.Address));
            }

            return input;
        }

        private WalletStickerView Create(IClientService clientService, TonNft collectible)
        {
            var view = new WalletStickerView(clientService, collectible);
            view.IsClipped = IsClipped;

            Canvas.SetZIndex(view, ++_z);
            Children.Add(view);
            _stickers.Add(view);

            // Decoded ahead, so the first pick-up or landing has its curl.
            view.PrepareStill();

            return view;
        }

        #endregion

        #region Arriving and leaving

        /// <summary>
        /// Takes a gift up out of its cell, at <paramref name="from"/>, to be carried: it peels off
        /// the cell, grows to a sticker's size and comes up off the surface.
        /// </summary>
        public void PickUp(IClientService clientService, TonNft collectible, (Vector2 Center, float Scale)? from, Vector2 point)
        {
            HidePreview(false);

            _preview = Create(clientService, collectible);
            _preview.IsPreview = true;
            _previewMotion = Vector2.Zero;

            if (from is (Vector2 center, float scale))
            {
                _preview.Place(center, scale, 0);
                _preview.Scale.With(300, 0.6f).To(1);
            }
            else
            {
                _preview.Place(point, 1, 0);
                _preview.Presence.Set(0);
                _preview.Presence.To(1);
            }

            _preview.PickUp(Vector2.Zero);
            _preview.X.With(260, 0.85f).To(point.X);
            _preview.Y.With(260, 0.85f).To(point.Y);

            StartRendering();
        }

        /// <summary>
        /// Makes the sticker a dropped gift will become, out of sight, so that it has its art by the
        /// time it lands and takes over from the one carried without a blink.
        /// </summary>
        public void Prepare(IClientService clientService, TonNft collectible)
        {
            HidePreview(false);

            _preview = Create(clientService, collectible);
            _preview.IsPreview = true;
            _preview.Place(new Vector2(FaceWidth / 2, FaceHeight / 2), 1, 0);
            _preview.Presence.Set(0);
            _preview.Apply();
        }

        /// <summary>
        /// The gift being carried, under the pointer.
        /// </summary>
        public void MovePreview(Vector2 point)
        {
            if (_preview == null)
            {
                return;
            }

            _previewMotion = Vector2.Lerp(_previewMotion, point - _preview.Center, 0.5f);

            _preview.X.With(1100, 0.9f).To(point.X);
            _preview.Y.With(1100, 0.9f).To(point.Y);
            _preview.Presence.With(500, 1).To(1);

            StartRendering();
        }

        /// <summary>
        /// The way the carried gift was moving last, for the roll-on.
        /// </summary>
        public Vector2 PreviewMotion => _previewMotion;

        /// <summary>
        /// The pointer left: the carried gift fades until it comes back.
        /// </summary>
        public void FadePreview()
        {
            if (_preview != null)
            {
                _preview.Presence.With(500, 1).To(0);
                StartRendering();
            }
        }

        public void HidePreview(bool animated = true)
        {
            var view = _preview;
            if (view == null)
            {
                return;
            }

            _preview = null;

            if (animated)
            {
                view.IsLeaving = true;
                view.Presence.With(500, 1).To(0);
                StartRendering();
            }
            else
            {
                _stickers.Remove(view);
                Children.Remove(view);
                view.Release();
            }
        }

        /// <summary>
        /// The carried gift goes back into its cell, and <see cref="StickerLeft"/> says when it is
        /// there.
        /// </summary>
        public void ReturnPreview()
        {
            var view = _preview;
            if (view == null)
            {
                return;
            }

            _preview = null;
            view.IsPreview = false;

            // A drop the window does not take comes after a DragLeave, which has faded it.
            view.Presence.With(500, 1).To(1);

            var home = HomeResolver?.Invoke(view.Collectible);
            view.Leave(home?.Center, home?.Scale ?? 1);

            StartRendering();
        }

        /// <summary>
        /// Puts the gift down where it is, rolling on in the way it was moving.
        /// </summary>
        public bool Drop(IClientService clientService, TonNft collectible, Vector2 point, Vector2 motion)
        {
            if (!IsOnCard(point) || (Count >= MaximumStickers && !Contains(collectible.Address)))
            {
                HidePreview(false);
                return false;
            }

            var view = _preview?.Address == collectible.Address ? _preview : null;
            if (view == null)
            {
                HidePreview(false);

                view = Create(clientService, collectible);
                view.Presence.Set(0);
            }

            _preview = null;
            view.IsPreview = false;

            // Taking over from one carried elsewhere: here, as it was, held up off the card.
            view.Place(point, 1, 0);
            view.Lift.Set(1);
            view.RollOn(motion);

            Canvas.SetZIndex(view, ++_z);
            Select(view);
            StartRendering();

            OnChanged();
            return true;
        }

        /// <summary>
        /// Tosses a gift onto the card from <paramref name="from"/>, its cell, into the first free
        /// spot.
        /// </summary>
        public bool Add(IClientService clientService, TonNft collectible, (Vector2 Center, float Scale)? from)
        {
            if (Count >= MaximumStickers || Contains(collectible.Address))
            {
                return false;
            }

            var spot = FreeSpot();
            var view = Create(clientService, collectible);

            if (from is (Vector2 center, float scale))
            {
                view.Place(center, scale, 0);
                view.Lift.Set(1);
                view.IsArriving = true;

                view.X.With(150, 0.82f).To(spot.X);
                view.Y.With(150, 0.82f).To(spot.Y);
                view.Scale.With(300, 0.5f).To(1);
                view.Lift.With(110, 0.9f);
                view.RollOn(spot - center);
            }
            else
            {
                view.Place(spot, 1, 0);
                view.Lift.Set(1);
                view.Presence.Set(0);
                view.Presence.To(1);
                view.RollOn(new Vector2(0, 1));
            }

            StartRendering();

            OnChanged();
            return true;
        }

        private Vector2 FreeSpot()
        {
            foreach (var spot in Spots)
            {
                var free = true;

                foreach (var sticker in _stickers)
                {
                    if (!sticker.IsLeaving && sticker != _preview && Vector2.Distance(sticker.Center, spot) < WalletStickerView.BaseSize * 0.6f)
                    {
                        free = false;
                        break;
                    }
                }

                if (free)
                {
                    return spot;
                }
            }

            return new Vector2(FaceWidth / 2, FaceHeight / 2);
        }

        /// <summary>
        /// Peels the gift off the card and sends it back to its cell.
        /// </summary>
        public void Remove(string address)
        {
            var view = Find(address);
            if (view == null)
            {
                return;
            }

            if (view == _target)
            {
                CancelGesture();
            }

            Leave(view);
        }

        private void Leave(WalletStickerView view)
        {
            if (view == _selected)
            {
                Deselect();
            }

            view.PrepareStill();

            var home = HomeResolver?.Invoke(view.Collectible);
            view.Leave(home?.Center, home?.Scale ?? 1);

            StartRendering();
            OnChanged();
        }

        #endregion

        #region Selection

        public WalletStickerView Selected => _selected;

        public void Select(WalletStickerView view)
        {
            if (IsReadOnly || view?.IsLeaving == true)
            {
                return;
            }

            SetSelected(view);
        }

        public void Deselect()
        {
            SetSelected(null);
        }

        private void SetSelected(WalletStickerView view)
        {
            if (_selected != null)
            {
                _selected.IsSelected = false;
            }

            _selected = view;

            if (view != null)
            {
                view.IsSelected = true;
            }

            UpdateFrame();
        }

        private void UpdateFrame()
        {
            if (_frame == null)
            {
                return;
            }

            if (_selected == null || _selected.IsLeaving || _gesture == Gesture.Move)
            {
                _frame.Hide();
            }
            else
            {
                _frame.Show(_selected.Center, _selected.Size, _selected.Rotation.Value);
            }
        }

        private void OnGotFocus(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is WalletStickerView view)
            {
                Select(view);
            }
        }

        private static WalletStickerView FindView(object source)
        {
            for (var element = source as DependencyObject; element != null; element = VisualTreeHelper.GetParent(element))
            {
                if (element is WalletStickerView view)
                {
                    return view;
                }
                else if (element is WalletStickerLayer)
                {
                    break;
                }
            }

            return null;
        }

        #endregion

        #region Input

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(this);

            if (IsReadOnly || (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse && !point.Properties.IsLeftButtonPressed))
            {
                return;
            }

            var position = point.Position.ToVector2();

            // A second finger on the sticker being held turns the drag into a pinch.
            if (_pointers.Count > 0)
            {
                if (_pointers.Count == 1 && _target != null && _gesture is Gesture.Press or Gesture.Move && e.Pointer.PointerDeviceType == PointerDeviceType.Touch)
                {
                    // Held by two fingers it is worked on where it is, back on the card.
                    _target.Lift.With(150, 0.9f).To(0);
                    StartRendering();

                    _pointers[point.PointerId] = position;
                    _gesture = Gesture.Pinch;

                    CapturePointer(e.Pointer);
                    Select(_target);
                    UpdateHeld();
                }

                e.Handled = true;
                return;
            }

            var view = FindView(e.OriginalSource);
            if (view == null || view.IsLeaving || view == _preview || (IsClipped && !view.IsSelected && !IsOnCard(position)))
            {
                return;
            }

            Select(view);
            view.Focus(FocusState.Pointer);

            _target = view;
            _gesture = Gesture.Press;
            _pressPoint = position;
            _grab = view.Center - position;
            _motion = Vector2.Zero;
            _pointers[point.PointerId] = position;

            CapturePointer(e.Pointer);
            UpdateActive();

            // Taken: the card's tilt under it does not see this press.
            e.Handled = true;
        }

        private void OnFramePointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(this);

            if (IsReadOnly || _selected == null || _pointers.Count > 0 || !_frame.IsHandle(e.OriginalSource, out bool left)
                || (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse && !point.Properties.IsLeftButtonPressed))
            {
                return;
            }

            var position = point.Position.ToVector2();

            _target = _selected;
            _gesture = Gesture.Handle;
            _handleLeft = left;
            _handleScale = _selected.Scale.Value;
            _handleDistance = Math.Max(1, Vector2.Distance(position, _selected.Center));
            _pointers[point.PointerId] = position;

            CapturePointer(e.Pointer);
            UpdateActive();
            UpdateHeld();
            e.Handled = true;
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            var id = e.Pointer.PointerId;
            if (!_pointers.TryGetValue(id, out Vector2 previous) || _target == null)
            {
                return;
            }

            var position = e.GetCurrentPoint(this).Position.ToVector2();
            _pointers[id] = position;

            switch (_gesture)
            {
                case Gesture.Press:
                    if (Vector2.Distance(position, _pressPoint) < PanThreshold)
                    {
                        return;
                    }

                    _gesture = Gesture.Move;
                    Canvas.SetZIndex(_target, ++_z);

                    _target.PickUp(_pressPoint - _target.Center);
                    UpdateFrame();
                    UpdateHeld();
                    goto case Gesture.Move;

                case Gesture.Move:
                    // The grab offset melts away, so the sticker ends up centred under the pointer.
                    _grab *= 0.9f;
                    _motion = Vector2.Lerp(_motion, position - previous, 0.5f);

                    _target.X.With(1100, 0.9f).To(position.X + _grab.X);
                    _target.Y.With(1100, 0.9f).To(position.Y + _grab.Y);
                    StartRendering();
                    break;

                case Gesture.Handle:
                    MoveHandle(position);
                    break;

                case Gesture.Pinch:
                    MovePinch(id, previous, position);
                    break;
            }

            e.Handled = true;
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (!_pointers.Remove(e.Pointer.PointerId))
            {
                return;
            }

            ReleasePointerCapture(e.Pointer);

            if (_gesture == Gesture.Pinch)
            {
                // The finger left behind does nothing until it lifts too.
                if (_pointers.Count == 0)
                {
                    EndGesture(true);
                }

                return;
            }

            if (_gesture == Gesture.Press)
            {
                _target.Play();
                StartRendering();
                EndGesture(false);
            }
            else if (_gesture == Gesture.Move)
            {
                var target = _target;
                EndGesture(false);

                // Let go over the card it sticks where it is; anywhere else it goes back.
                if (IsOnCard(target.Center))
                {
                    Settle(target);
                    OnChanged();
                }
                else
                {
                    Leave(target);
                }
            }
            else
            {
                EndGesture(true);
            }

            e.Handled = true;
        }

        private void OnPointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            if (!_pointers.Remove(e.Pointer.PointerId) || _pointers.Count > 0)
            {
                return;
            }

            CancelGesture();
        }

        // Whatever was in hand stays on the card, where it is.
        private void CancelGesture()
        {
            var target = _target;
            var moved = _gesture is Gesture.Move or Gesture.Handle or Gesture.Pinch;

            if (_gesture == Gesture.Move && target != null)
            {
                Settle(target);
            }

            _pointers.Clear();
            EndGesture(moved);
        }

        private void EndGesture(bool changed)
        {
            _gesture = Gesture.None;
            _target = null;

            UpdateFrame();
            UpdateActive();
            UpdateHeld();

            if (changed)
            {
                OnChanged();
            }
        }

        // Set down by hand: it rolls on right where it is, kept on the card.
        private void Settle(WalletStickerView view)
        {
            var center = new Vector2(
                Math.Clamp(view.X.Target, 0, FaceWidth),
                Math.Clamp(view.Y.Target, 0, FaceHeight));

            view.X.With(500, 1).To(center.X);
            view.Y.With(500, 1).To(center.Y);
            view.RollOn(_motion);

            StartRendering();
        }

        // Sizes by how far the handle is from the centre and turns to face it.
        private void MoveHandle(Vector2 position)
        {
            var offset = position - _target.Center;
            var distance = offset.Length();

            if (distance < 1)
            {
                return;
            }

            var degrees = MathF.Atan2(offset.Y, offset.X) * 180 / MathF.PI - (_handleLeft ? 180 : 0);

            _target.Scale.Set(Math.Clamp(_handleScale * distance / _handleDistance, MinimumScale, MaximumScale));
            _target.Rotation.Set(Near(Snap(degrees), _target.Rotation.Value));
            _target.Apply();

            UpdateFrame();
        }

        private void MovePinch(uint id, Vector2 previous, Vector2 position)
        {
            Vector2? other = null;

            foreach (var pointer in _pointers)
            {
                if (pointer.Key != id)
                {
                    other = pointer.Value;
                    break;
                }
            }

            if (other is not Vector2 anchor)
            {
                return;
            }

            var before = previous - anchor;
            var after = position - anchor;

            if (before.Length() < 1 || after.Length() < 1)
            {
                return;
            }

            var turn = (MathF.Atan2(after.Y, after.X) - MathF.Atan2(before.Y, before.X)) * 180 / MathF.PI;
            var center = _target.Center + (position - previous) / 2;

            _target.Scale.Set(Math.Clamp(_target.Scale.Value * after.Length() / before.Length(), MinimumScale, MaximumScale));
            _target.Rotation.Set(_target.Rotation.Value + turn);
            _target.X.Set(Math.Clamp(center.X, 0, FaceWidth));
            _target.Y.Set(Math.Clamp(center.Y, 0, FaceHeight));
            _target.Apply();

            UpdateFrame();
        }

        private void OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            var view = _selected;
            if (IsReadOnly || view == null || _gesture != Gesture.None)
            {
                return;
            }

            var shift = WindowContext.IsKeyDown(VirtualKey.Shift);
            var step = shift ? KeyboardStep * 5 : KeyboardStep;

            switch (e.Key)
            {
                case VirtualKey.Left:
                    Move(view, new Vector2(-step, 0));
                    break;
                case VirtualKey.Right:
                    Move(view, new Vector2(step, 0));
                    break;
                case VirtualKey.Up:
                    Move(view, new Vector2(0, -step));
                    break;
                case VirtualKey.Down:
                    Move(view, new Vector2(0, step));
                    break;
                case VirtualKey.Add:
                case (VirtualKey)187: // OemPlus
                    Resize(view, KeyboardScale);
                    break;
                case VirtualKey.Subtract:
                case (VirtualKey)189: // OemMinus
                    Resize(view, -KeyboardScale);
                    break;
                case (VirtualKey)219: // OemOpenBrackets
                    Turn(view, -KeyboardTurn);
                    break;
                case (VirtualKey)221: // OemCloseBrackets
                    Turn(view, KeyboardTurn);
                    break;
                case VirtualKey.Delete:
                case VirtualKey.Back:
                    Leave(view);
                    e.Handled = true;
                    return;
                case VirtualKey.Escape:
                    Deselect();
                    e.Handled = true;
                    return;
                default:
                    return;
            }

            OnChanged();
            e.Handled = true;
        }

        private void Move(WalletStickerView view, Vector2 delta)
        {
            view.X.To(Math.Clamp(view.X.Target + delta.X, 0, FaceWidth));
            view.Y.To(Math.Clamp(view.Y.Target + delta.Y, 0, FaceHeight));
            StartRendering();
        }

        private void Resize(WalletStickerView view, float delta)
        {
            view.Scale.To(Math.Clamp(view.Scale.Target + delta, MinimumScale, MaximumScale));
            StartRendering();
        }

        private void Turn(WalletStickerView view, float degrees)
        {
            view.Rotation.To(view.Rotation.Target + degrees);
            StartRendering();
        }

        private static float Snap(float degrees)
        {
            var quarter = MathF.Round(degrees / 90) * 90;
            return MathF.Abs(degrees - quarter) < SnapDegrees ? quarter : degrees;
        }

        // The same angle, taken the whole turns nearest to the current one, so a handle crossing
        // the half turn does not spin the sticker the long way round.
        private static float Near(float degrees, float current)
        {
            return degrees + MathF.Round((current - degrees) / 360) * 360;
        }

        private void OnContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var view = FindView(args.OriginalSource) ?? _selected;
            if (IsReadOnly || view == null || view.IsLeaving || view == _preview || _gesture != Gesture.None)
            {
                return;
            }

            Select(view);
            StickerContextRequested?.Invoke(this, new WalletStickerContextRequestedEventArgs(view, args));
        }

        #endregion

        #region Frame loop

        private void StartRendering()
        {
            if (_rendering)
            {
                return;
            }

            _rendering = true;
            _lastTime = TimeSpan.Zero;

            CompositionTarget.Rendering += OnRendering;
            UpdateActive();
        }

        private void StopRendering()
        {
            if (_rendering)
            {
                _rendering = false;
                CompositionTarget.Rendering -= OnRendering;
                UpdateActive();
            }
        }

        private void OnRendering(object sender, object e)
        {
            var time = e is RenderingEventArgs args ? args.RenderingTime : TimeSpan.Zero;
            var dt = _lastTime == TimeSpan.Zero ? 1f / 60 : (float)(time - _lastTime).TotalSeconds;
            _lastTime = time;

            // A long stall is not a long step: springs fed a quarter second jump.
            dt = Math.Clamp(dt, 0, 1f / 20);

            var moving = false;

            for (int i = _stickers.Count - 1; i >= 0; i--)
            {
                var view = _stickers[i];
                var busy = view.Step(dt);

                view.Apply();

                if (busy)
                {
                    moving = true;
                }
                else if (view.IsLeaving)
                {
                    RemoveAt(i);
                }
            }

            UpdateFrame();

            if (!moving)
            {
                StopRendering();
            }
        }

        private void RemoveAt(int index)
        {
            var view = _stickers[index];

            _stickers.RemoveAt(index);
            Children.Remove(view);
            view.Release();

            // Carried over the card and away again, it was never on it.
            if (!view.IsPreview)
            {
                StickerLeft?.Invoke(this, view.Collectible);
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            CancelGesture();
            StopRendering();
        }

        #endregion
    }
}
