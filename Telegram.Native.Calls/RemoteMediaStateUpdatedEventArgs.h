#pragma once

#include "RemoteMediaStateUpdatedEventArgs.g.h"

using namespace winrt::Windows::Foundation::Collections;

namespace winrt::Telegram::Native::Calls::implementation
{
    struct RemoteMediaStateUpdatedEventArgs : RemoteMediaStateUpdatedEventArgsT<RemoteMediaStateUpdatedEventArgs>
    {
        RemoteMediaStateUpdatedEventArgs() = default;
        RemoteMediaStateUpdatedEventArgs(VoipAudioState audio, VoipVideoState video);

        VoipAudioState Audio();
        VoipVideoState Video();

    private:
        VoipAudioState m_audio;
        VoipVideoState m_video;
    };
} // namespace winrt::Telegram::Native::Calls::implementation
