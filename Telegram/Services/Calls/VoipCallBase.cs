//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Threading;
using Windows.System;

namespace Telegram.Services.Calls
{
    public abstract partial class VoipCallBase : ServiceBase
    {
        protected VoipCallBase(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
        }

        public abstract string VideoInputId { get; set; }

        public abstract string AudioInputId { get; set; }

        public abstract string AudioOutputId { get; set; }

        public abstract void Show();

        public abstract void Discard();

        // A crash report carries one memory figure, taken at the fault, so a call that grew
        // steadily and one that spiked at the end look identical. Sampling here puts the shape
        // of the growth in the log tail instead.
        //
        // Only real movement is logged. The tail holds 200 lines for the whole app, and a
        // heartbeat every interval would fill it several times over on a long call, pushing out
        // everything that says what the call was doing.
        private const int MemorySampleInterval = 30 * 1000;
        private const long MemoryLogThreshold = 32 * 1024 * 1024;

        private Timer _memoryTimer;
        private long _memoryLogged;
        private long _memoryLoggedAt;
        private long _memoryPeak;

        protected void StartMemorySampling()
        {
            if (_memoryTimer != null)
            {
                return;
            }

            _memoryLogged = (long)MemoryManager.AppMemoryUsage;
            _memoryLoggedAt = Environment.TickCount64;
            _memoryPeak = _memoryLogged;

            _memoryTimer = new Timer(OnMemorySample, null, MemorySampleInterval, MemorySampleInterval);

            Logger.Info($"{_memoryLogged / 1024 / 1024} MB");
        }

        protected void StopMemorySampling()
        {
            var timer = Interlocked.Exchange(ref _memoryTimer, null);
            if (timer == null)
            {
                return;
            }

            timer.Dispose();

            Logger.Info($"{(long)MemoryManager.AppMemoryUsage / 1024 / 1024} MB, peak {_memoryPeak / 1024 / 1024} MB");
        }

        private void OnMemorySample(object state)
        {
            var usage = (long)MemoryManager.AppMemoryUsage;
            var now = Environment.TickCount64;

            if (usage > _memoryPeak)
            {
                _memoryPeak = usage;
            }

            var delta = usage - _memoryLogged;
            if (Math.Abs(delta) < MemoryLogThreshold)
            {
                return;
            }

            // Against the last line actually written, not the last sample taken, so the rate
            // is readable straight off two consecutive entries.
            var elapsed = (now - _memoryLoggedAt) / 1000;

            _memoryLogged = usage;
            _memoryLoggedAt = now;

            Logger.Info($"{usage / 1024 / 1024} MB, {delta / 1024 / 1024:+#;-#;0} MB in {elapsed}s");
        }
    }
}
