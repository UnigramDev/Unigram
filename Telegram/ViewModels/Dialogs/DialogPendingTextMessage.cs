//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.UI.Xaml;

namespace Telegram.ViewModels
{
    public class DialogPendingTextMessage
    {
        private readonly MessageViewModel _message;
        private readonly DispatcherTimer _timer;
        private DispatcherTimer _typing;

        private readonly Random _random = new();

        private FormattedText _text;
        private FormattedText _pending;

        private Message _completed;

        public DialogPendingTextMessage(UpdatePendingTextMessage update, MessageViewModel message)
        {
            _text = string.Empty.AsFormattedText();
            _pending = update.Text;
            _message = message;

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(message.ClientService.Options.PendingTextMessagePeriod)
            };

            _timer.Tick += OnTick;
            _timer.Start();

            DraftId = update.DraftId;
            LastUpdate = Logger.TickCount;

            Typing_Tick(null, null);
        }

        private int GetRandomChunkSize(int remainingLength)
        {
            if (remainingLength <= 10)
            {
                return remainingLength;
            }

            float speedMultiplier = GetSpeedMultiplier(_pending.Text.Length);

            var rand = _random.NextDouble();
            int baseSize;

            if (rand < 0.6)
                baseSize = 2 + (int)Math.Floor(_random.NextDouble() * 4);
            else if (rand < 0.9)
                baseSize = 6 + (int)Math.Floor(_random.NextDouble() * 3);
            else
                baseSize = 9 + (int)Math.Floor(_random.NextDouble() * 2);

            int adjustedSize = (int)Math.Ceiling(baseSize * speedMultiplier);

            return Math.Min(Math.Min(adjustedSize, 20), remainingLength);
        }

        private TimeSpan GetRandomDelay(char lastChar)
        {
            float speedMultiplier = GetSpeedMultiplier(_pending.Text.Length);

            double baseDelay;
            if (lastChar is '.' or '!' or '?')
            {
                baseDelay = 50 + _random.NextDouble() * 30;
            }
            else if (lastChar == ',')
            {
                baseDelay = 30 + _random.NextDouble() * 20;
            }
            else
            {
                baseDelay = 15 + _random.NextDouble() * 20;
            }

            double adjustedDelay = baseDelay / speedMultiplier;
            adjustedDelay = Math.Max(adjustedDelay, 8);

            return TimeSpan.FromMilliseconds(adjustedDelay);
        }

        private float GetSpeedMultiplier(int remainingLength)
        {
            if (remainingLength < 200) return 1.0f;
            if (remainingLength < 500) return 1.3f;
            if (remainingLength < 1000) return 1.6f;
            if (remainingLength < 2000) return 2.0f;
            return 2.5f;
        }

        public long DraftId { get; }

        private void OnTick(object sender, object e)
        {
            _timer.Stop();
            Completed?.Invoke(this, null);
        }

        public ulong LastUpdate { get; private set; }

        public void Update(UpdatePendingTextMessage update)
        {
            _timer.Stop();
            _timer.Start();

            LastUpdate = Logger.TickCount;
            Update(update.Text);
        }

        public void Update(Message message)
        {
            _timer.Stop();
            _completed = message;

            LastUpdate = Logger.TickCount;
            Update(message.GetCaption());
        }

        private void Update(FormattedText text)
        {
            if (text == null)
            {
                _timer.Stop();
                _typing.Stop();
                Completed?.Invoke(this, _completed);

                return;
            }

            if (text.Text.StartsWith(_text.Text))
            {
                _pending = text;
            }
            else if (text.Text.Length > _text.Text.Length)
            {
                _text = text.Substring(0, _text.Text.Length);
                _pending = text;
            }
            else
            {
                _text = text;
                _pending = text;
            }

            if (_typing.IsEnabled)
            {
                return;
            }

            RaiseUpdate();
        }

        private void Typing_Tick(object sender, object e)
        {
            if (_typing == null)
            {
                _typing = new DispatcherTimer();
                _typing.Tick += Typing_Tick;
            }
            else
            {
                _typing.Stop();
            }

            var length = GetRandomChunkSize(_pending.Text.Length - _text.Text.Length);

            _text = _pending.Substring(0, _text.Text.Length + length);

            RaiseUpdate();
        }

        private void RaiseUpdate()
        {
            if (_completed != null && _text.Text.Length == _pending.Text.Length)
            {
                _timer.Stop();
                Completed?.Invoke(this, _completed);
            }
            else
            {
                _message.Content = new MessageText(_text, null, null);
                Updated?.Invoke(this, _message);
            }

            if (_text.Text.Length < _pending.Text.Length)
            {
                _typing.Interval = GetRandomDelay(_text.Text.Length > 0 ? _text.Text[^1] : 'a');
                _typing.Start();
            }
        }

        public void Stop()
        {
            _timer.Stop();
            _typing.Stop();
        }

        public event TypedEventHandler<DialogPendingTextMessage, MessageViewModel> Updated;

        public event TypedEventHandler<DialogPendingTextMessage, Message> Completed;
    }
}
