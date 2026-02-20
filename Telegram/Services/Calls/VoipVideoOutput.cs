//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Numerics;
using Telegram.Common;
using Telegram.Native.Calls;
using Telegram.Navigation;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Services.Calls
{
    public partial class VoipVideoOutput
    {
        private readonly VoipVideoOutputSink _sink;
        private readonly object _lock = new();

        private VoipVideoState _state;
        private Vector2 _frame;

        private bool _initialize = true;
        private bool _active;

        public VoipVideoOutput(UIElement element, bool mirrored)
        {
            _sink = CreateSink(element, mirrored: mirrored);
            _sink.FrameReceived += OnFrameReceived;
        }

        public static VoipVideoOutputSink CreateSink(UIElement element, bool mirrored = false, bool uniformToFill = false)
        {
            var visual = BootStrapper.Current.Compositor.CreateSpriteVisual();
            visual.RelativeSizeAdjustment = Vector2.One;
            ElementCompositionPreview.SetElementChildVisual(element, visual);

            return new VoipVideoOutputSink(PlaceholderHelper.Foreground.Device, visual, mirrored, uniformToFill);
        }

        public void Stop()
        {
            _sink.FrameReceived -= OnFrameReceived;
            _sink.Stop();
        }

        public VoipVideoOutputSink Sink => _sink;

        public Vector2 Frame
        {
            get
            {
                lock (_lock)
                {
                    return _frame;
                }
            }
        }

        public bool IsActive
        {
            get
            {
                lock (_lock)
                {
                    return _active;
                }
            }
        }

        public bool SetState(VoipVideoState state, bool mirrored = false)
        {
            lock (_lock)
            {
                _state = state;

                MaybeRaiseStateChanged();

                if (_state != VoipVideoState.Inactive)
                {
                    _sink.IsMirrored = mirrored;
                }

                if (_initialize && _state != VoipVideoState.Inactive)
                {
                    _initialize = false;
                    return true;
                }
                else if (_state == VoipVideoState.Inactive)
                {
                    _frame.X = 0;
                    _frame.Y = 0;
                }

                return false;
            }
        }

        private void OnFrameReceived(VoipVideoOutputSink sender, FrameReceivedEventArgs args)
        {
            lock (_lock)
            {
                _frame.X = args.PixelWidth;
                _frame.Y = args.PixelHeight;

                MaybeRaiseStateChanged();
            }
        }

        public event TypedEventHandler<VoipVideoOutput, VoipVideoStateChangedEventArgs> StateChanged;

        private void MaybeRaiseStateChanged()
        {
            bool active;
            if (_state != VoipVideoState.Inactive)
            {
                active = _frame.X != 0 && _frame.Y != 0;
            }
            else
            {
                active = false;
            }

            if (_active != active)
            {
                _active = active;
                StateChanged?.Invoke(this, new VoipVideoStateChangedEventArgs(_active, _frame));
            }
        }
    }
}
