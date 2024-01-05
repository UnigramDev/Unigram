//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Windows.UI.Xaml.Media;

namespace Telegram.Composition
{
    public class CompositionVSync
    {
        private TimeSpan _interval;
        private TimeSpan _elapsed;

        public CompositionVSync(double framerate)
        {
            _interval = TimeSpan.FromMilliseconds(1000 / framerate);
        }

        private event EventHandler _rendering;
        public event EventHandler Rendering
        {
            add
            {
                if (_rendering == null)
                {
                    CompositionTarget.Rendering += OnRendering;
                }

                _rendering += value;
            }
            remove
            {
                _rendering -= value;

                if (_rendering == null)
                {
                    CompositionTarget.Rendering -= OnRendering;
                }
            }
        }

        private void OnRendering(object sender, object e)
        {
            var args = e as RenderingEventArgs;
            var diff = args.RenderingTime - _elapsed;

            if (diff < _interval)
            {
                return;
            }

            _elapsed = args.RenderingTime;
            _rendering?.Invoke(sender, EventArgs.Empty);
        }
    }
}
