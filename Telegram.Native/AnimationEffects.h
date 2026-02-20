#pragma once

#include "AnimationEffects.g.h"

#include <detours.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.UI.Core.h>
#include <winrt/Windows.UI.ViewManagement.h>
#include <mutex>

using namespace winrt::Windows::Foundation;
using namespace winrt::Windows::UI::Core;
using namespace winrt::Windows::UI::ViewManagement;

using PFN_SystemParametersInfoW = BOOL(WINAPI*)(
    _In_ UINT uiAction,
    _In_ UINT uiParam,
    _Pre_maybenull_ _Post_valid_ PVOID pvParam,
    _In_ UINT fWinIni);

using PNF_add_AnimationsEnabledChanged = int32_t(__stdcall*)(void* pThis, void*, winrt::event_token*);
using PNF_remove_AnimationsEnabledChanged = int32_t(__stdcall*)(void* pThis, winrt::event_token);

namespace winrt::Telegram::Native::implementation
{
    struct EventRegistration
    {
        void* event_source;
        winrt::event_token token;

        bool operator<(const EventRegistration& other) const
        {
            if (event_source != other.event_source)
            {
                return event_source < other.event_source;
            }
            if (token == other.token)
            {
                return false;
            }

            return token.value < other.token.value;
        }
    };

    struct AnimationEffects : AnimationEffectsT<AnimationEffects>
    {
        static bool Supported();

        static bool Enabled();

        static void Initialize();

        static AnimationEffectsState State();

        static void State(AnimationEffectsState state);

    private:
        static PFN_SystemParametersInfoW s_SystemParametersInfoW;
        static PNF_add_AnimationsEnabledChanged s_add_AnimationsEnabledChanged;
        static PNF_remove_AnimationsEnabledChanged s_remove_AnimationsEnabledChanged;

        static UISettings s_settings;

        static std::mutex s_mutex;

        static std::atomic<bool> s_initialized;
        static std::atomic<AnimationEffectsState> s_state;

        static std::map<EventRegistration, TypedEventHandler<UISettings, UISettingsAnimationsEnabledChangedEventArgs>> s_events;

        static DWORD WINAPI SystemParametersInfoWHook(_In_ UINT uiAction, _In_ UINT uiParam, _Pre_maybenull_ _Post_valid_ PVOID pvParam, _In_ UINT fWinIni);
        static int32_t __stdcall add_AnimationsEnabledChanged(void* pThis, void* handler, winrt::event_token* token);
        static int32_t __stdcall remove_AnimationsEnabledChanged(void* pThis, winrt::event_token token);


    };
}

namespace winrt::Telegram::Native::factory_implementation
{
    struct AnimationEffects : AnimationEffectsT<AnimationEffects, implementation::AnimationEffects>
    {
    };
}
