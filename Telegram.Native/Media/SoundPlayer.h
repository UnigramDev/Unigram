#pragma once

#include "Media/SoundPlayer.g.h"

namespace winrt::Telegram::Native::Media::implementation
{
    struct SoundPlayer : SoundPlayerT<SoundPlayer>
    {
        static void Play(hstring path, int32_t loopCount, SoundCategory category);

        static void StopAll();
    };
}

namespace winrt::Telegram::Native::Media::factory_implementation
{
    struct SoundPlayer : SoundPlayerT<SoundPlayer, implementation::SoundPlayer>
    {
    };
}
