#pragma once

#include "TextHost.g.h"

#include <atomic>

using PFN_GetSysColor = DWORD(WINAPI*)(int);

namespace winrt::Telegram::Native::implementation
{
    struct TextHost : TextHostT<TextHost>
    {
    public:
        static void OverrideWindowColor();

    private:
        static DWORD WINAPI GetSysColorHook(int index);
        static void EnsureDetour();

        static PFN_GetSysColor s_GetSysColor;

        static std::atomic<bool> s_initialized;
        static std::mutex s_mutex;
    };
} // namespace winrt::Telegram::Native::implementation

namespace winrt::Telegram::Native::factory_implementation
{
    struct TextHost : TextHostT<TextHost, implementation::TextHost>
    {
    };
} // namespace winrt::Telegram::Native::factory_implementation
