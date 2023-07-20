#include "pch.h"
#include "RemoteMediaStateUpdatedEventArgs.h"

namespace winrt::Telegram::Native::Calls::implementation
{
    RemoteMediaStateUpdatedEventArgs::RemoteMediaStateUpdatedEventArgs(VoipAudioState audio, VoipVideoState video)
        : m_audio(audio),
        m_video(video)
    {
    }

    VoipAudioState RemoteMediaStateUpdatedEventArgs::Audio()
    {
        return m_audio;
    }

    VoipVideoState RemoteMediaStateUpdatedEventArgs::Video()
    {
        return m_video;
    }
} // namespace winrt::Telegram::Native::Calls::implementation
