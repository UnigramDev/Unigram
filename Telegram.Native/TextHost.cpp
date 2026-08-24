#include "pch.h"
#include "TextHost.h"
#if __has_include("TextHost.g.cpp")
#include "TextHost.g.cpp"
#endif

#include <detours.h>

namespace winrt::Telegram::Native::implementation
{
    namespace
    {
        // What RichEdit is contrasting against instead of the white it would otherwise
        // assume. Mid grey is 127 from both black and white, over twice the radius the
        // test rejects within, so the one answer serves a light theme and a dark one and
        // nothing has to follow a theme change.
        constexpr COLORREF c_windowColor = RGB(127, 127, 127);

        // Per thread, since a view draws on its own.
        thread_local bool t_overridden = false;
    }

    PFN_GetSysColor TextHost::s_GetSysColor = nullptr;

    std::atomic<bool> TextHost::s_initialized{ false };
    std::mutex TextHost::s_mutex;

    void TextHost::OverrideWindowColor()
    {
        EnsureDetour();

        t_overridden = true;
    }

    void TextHost::EnsureDetour()
    {
        if (s_initialized.load())
        {
            return;
        }

        std::lock_guard<std::mutex> lock(s_mutex);

        if (s_initialized.load())
        {
            return;
        }

        HMODULE user32 = GetModuleHandle(L"User32.dll");
        if (!user32) user32 = LoadLibrary(L"User32.dll");

        s_GetSysColor = reinterpret_cast<PFN_GetSysColor>(GetProcAddress(user32, "GetSysColor"));

        if (s_GetSysColor)
        {
            DetourTransactionBegin();
            DetourUpdateThread(GetCurrentThread());
            DetourAttach(reinterpret_cast<PVOID*>(&s_GetSysColor), GetSysColorHook);
            DetourTransactionCommit();
        }

        s_initialized = true;
    }

    DWORD WINAPI TextHost::GetSysColorHook(int index)
    {
        if (index == COLOR_WINDOW && t_overridden)
        {
            return c_windowColor;
        }

        return s_GetSysColor(index);
    }
} // namespace winrt::Telegram::Native::implementation
