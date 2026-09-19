//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Native.Media;
using Telegram.Td.Api;
using Windows.ApplicationModel;

namespace Telegram.Common
{
    public static class SoundEffects
    {
        private static string _assets;

        private static bool _suspended;

        public static void Suspend()
        {
            _suspended = true;
            Stop();
        }

        public static void Resume()
        {
            _suspended = false;
        }

        public static void Stop()
        {
            SoundPlayer.StopAll();
        }

        public static void Play(SoundEffect effect)
        {
            if (_suspended)
            {
                return;
            }

            switch (effect)
            {
                case SoundEffect.Sent:
                    PlayAsset("sent.mp3", 0, SoundCategory.Notification);
                    break;
                case SoundEffect.Received:
                    PlayAsset("received.mp3", 0, SoundCategory.Notification);
                    break;
                case SoundEffect.VoipIncoming:
                    PlayAsset("voip_incoming.mp3", -1, SoundCategory.Call);
                    break;
                case SoundEffect.VoipRingback:
                    PlayAsset("voip_ringback.mp3", -1, SoundCategory.Call);
                    break;
                case SoundEffect.VoipConnecting:
                    PlayAsset("voip_connecting.mp3", 0, SoundCategory.Call);
                    break;
                case SoundEffect.VoipBusy:
                    PlayAsset("voip_busy.mp3", 4, SoundCategory.Call);
                    break;
                case SoundEffect.VoipEnd:
                    PlayAsset("voip_end.mp3", 0, SoundCategory.Call);
                    break;
                case SoundEffect.VoipFailed:
                    PlayAsset("voip_failed.mp3", 0, SoundCategory.Call);
                    break;
                case SoundEffect.VideoChatJoin:
                    PlayAsset("voicechat_join.mp3", 0, SoundCategory.VideoChat);
                    break;
                case SoundEffect.VideoChatLeave:
                    PlayAsset("voicechat_leave.mp3", 0, SoundCategory.VideoChat);
                    break;
            }
        }

        public static void Play(File file)
        {
            if (file.Local.IsDownloadingCompleted && !_suspended)
            {
                SoundPlayer.Play(file.Local.Path, 0, SoundCategory.Custom);
            }
        }

        private static void PlayAsset(string name, int loopCount, SoundCategory category)
        {
            _assets ??= System.IO.Path.Combine(Package.Current.InstalledLocation.Path, "Assets", "Audio");
            SoundPlayer.Play(System.IO.Path.Combine(_assets, name), loopCount, category);
        }
    }

    public enum SoundEffect
    {
        Sent,
        Received,
        VoipIncoming,
        VoipRingback,
        VoipBusy,
        VoipFailed,
        VoipEnd,
        VoipConnecting,
        VideoChatJoin,
        VideoChatLeave
    }
}
