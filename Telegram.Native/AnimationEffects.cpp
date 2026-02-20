#include "pch.h"
#include "AnimationEffects.h"
#if __has_include("AnimationEffects.g.cpp")
#include "AnimationEffects.g.cpp"
#endif

namespace winrt::Telegram::Native::implementation
{
    PFN_SystemParametersInfoW AnimationEffects::s_SystemParametersInfoW = nullptr;
    PNF_add_AnimationsEnabledChanged AnimationEffects::s_add_AnimationsEnabledChanged = nullptr;
    PNF_remove_AnimationsEnabledChanged AnimationEffects::s_remove_AnimationsEnabledChanged = nullptr;

    UISettings AnimationEffects::s_settings;

    std::mutex AnimationEffects::s_mutex;

    std::atomic<bool> AnimationEffects::s_initialized{ false };
    std::atomic<AnimationEffectsState> AnimationEffects::s_state{ AnimationEffectsState::Auto };

    std::map<EventRegistration, TypedEventHandler<UISettings, UISettingsAnimationsEnabledChangedEventArgs>> AnimationEffects::s_events;

    bool AnimationEffects::Supported()
    {
        if (auto settings6 = s_settings.try_as<IUISettings6>())
        {
            return true;
        }

        return false;
    }

    bool AnimationEffects::Enabled()
    {
        if (s_SystemParametersInfoW)
        {
            BOOL enabled = FALSE;
            if (s_SystemParametersInfoW(SPI_GETCLIENTAREAANIMATION, 0, &enabled, 0))
            {
                return enabled;
            }

            return false;
        }

        return s_settings.AnimationsEnabled();
    }

    void AnimationEffects::Initialize()
    {
        if (s_initialized.load())
        {
            return;
        }

        std::lock_guard<std::mutex> lock(s_mutex);

        if (auto settings6 = s_settings.try_as<IUISettings6>())
        {
            auto vtbl = *reinterpret_cast<void***>(winrt::get_abi(settings6));
            s_add_AnimationsEnabledChanged = reinterpret_cast<PNF_add_AnimationsEnabledChanged>(vtbl[6]);
            s_remove_AnimationsEnabledChanged = reinterpret_cast<PNF_remove_AnimationsEnabledChanged>(vtbl[7]);

            HMODULE user32 = GetModuleHandle(L"User32.dll");
            if (!user32) user32 = LoadLibrary(L"User32.dll");

            s_SystemParametersInfoW = reinterpret_cast<PFN_SystemParametersInfoW>(GetProcAddress(user32, "SystemParametersInfoW"));

            DetourTransactionBegin();
            DetourUpdateThread(GetCurrentThread());

            DetourAttach(reinterpret_cast<PVOID*>(&s_SystemParametersInfoW), SystemParametersInfoWHook);
            DetourAttach(reinterpret_cast<PVOID*>(&s_add_AnimationsEnabledChanged), add_AnimationsEnabledChanged);
            DetourAttach(reinterpret_cast<PVOID*>(&s_remove_AnimationsEnabledChanged), remove_AnimationsEnabledChanged);

            DetourTransactionCommit();
        }

        s_initialized = true;
    }

    AnimationEffectsState AnimationEffects::State()
    {
        return s_state.load();
    }

    void AnimationEffects::State(AnimationEffectsState state)
    {
        s_state = state;

        std::lock_guard<std::mutex> lock(s_mutex);

        for (const auto& event : s_events)
        {
            UISettings settings{ nullptr };
            winrt::copy_from_abi(settings, event.first.event_source);
            event.second(settings, nullptr);
        }
    }

    DWORD WINAPI AnimationEffects::SystemParametersInfoWHook(_In_ UINT uiAction, _In_ UINT uiParam, _Pre_maybenull_ _Post_valid_ PVOID pvParam, _In_ UINT fWinIni)
    {
        if (uiAction == SPI_GETCLIENTAREAANIMATION && pvParam != nullptr)
        {
            // We want the following behavior:
            // If animation effects are disabled system-wide, we always disable them in the app
            // However, if they are enabled, we let the user choose.
            // If state is not Auto
            //   -> Query system value
            //      -> If system value FALSE => FALSE
            //      -> Otherwise, value provided by user
            //   -> Otherwise return system value

            auto state = s_state.load();
            if (state != AnimationEffectsState::Auto)
            {
                BOOL enabled = FALSE;
                if (s_SystemParametersInfoW(SPI_GETCLIENTAREAANIMATION, 0, &enabled, 0))
                {
                    if (!enabled)
                    {
                        *(BOOL*)pvParam = FALSE;
                        return TRUE;
                    }
                }

                *(BOOL*)pvParam = (BOOL)state;
                return TRUE;
            }
        }

        return s_SystemParametersInfoW(uiAction, uiParam, pvParam, fWinIni);
    }

    int32_t __stdcall AnimationEffects::add_AnimationsEnabledChanged(void* pThis, void* handler, winrt::event_token* token)
    {
        std::lock_guard<std::mutex> lock(s_mutex);

        TypedEventHandler<UISettings, UISettingsAnimationsEnabledChangedEventArgs> typedHandler{ nullptr };
        winrt::attach_abi(typedHandler, handler);

        auto result = s_add_AnimationsEnabledChanged(pThis, handler, token);
        EventRegistration registration{ pThis, *reinterpret_cast<winrt::event_token const*>(token) };
        s_events[registration] = typedHandler;

        return result;
    }

    int32_t __stdcall AnimationEffects::remove_AnimationsEnabledChanged(void* pThis, winrt::event_token token)
    {
        std::lock_guard<std::mutex> lock(s_mutex);

        auto result = s_remove_AnimationsEnabledChanged(pThis, token);
        EventRegistration registration{ pThis, token };
        s_events.erase(registration);

        return result;
    }
}
