#include "pch.h"
#include "NativeUtils.h"
#include "DebugUtils.h"

using namespace winrt::Telegram::Native::implementation;

namespace
{
    // User-mode GPU drivers, by file name prefix: Intel, NVIDIA, AMD.
    constexpr const wchar_t* c_graphicsDrivers[] = { L"igd", L"nvwgf2um", L"nvd3dum", L"amdxx", L"atidxx", L"aticfx" };

    // What a handle passes through on its way to the kernel. None of them chose it: a driver
    // waits through kernelbase, and submits and presents through the D3D runtime's callbacks,
    // which call the D3DKMT stubs in gdi32/win32u.
    constexpr const wchar_t* c_systemModules[] = { L"ntdll.dll", L"kernelbase.dll", L"kernel32.dll",
        L"win32u.dll", L"gdi32.dll", L"d3d11.dll", L"d3d12core.dll", L"dxgi.dll" };

    // A driver that keeps failing would go through exception dispatch on every frame it draws.
    constexpr int c_maxContinues = 16;
    thread_local int t_continues = 0;

    const wchar_t* ModuleFileName(ULONG_PTR address, wchar_t (&path)[MAX_PATH])
    {
        void* base = nullptr;
        if (RtlPcToFileHeader(reinterpret_cast<PVOID>(address), &base) == nullptr
            || GetModuleFileNameW(static_cast<HMODULE>(base), path, MAX_PATH) == 0)
        {
            return nullptr;
        }

        const wchar_t* slash = wcsrchr(path, L'\\');
        return slash != nullptr ? slash + 1 : path;
    }

    // Unwinds from where the strict handle check raised, past the system DLLs the call went
    // through, to the code that passed the handle.
    bool IsRaisedForGraphicsDriver(const CONTEXT* raised)
    {
        CONTEXT context = *raised;
        wchar_t path[MAX_PATH];

        for (int depth = 0; depth < 8; depth++)
        {
#if defined(_M_ARM64)
            const ULONG_PTR pc = context.Pc;
#else
            const ULONG_PTR pc = context.Rip;
#endif

            const wchar_t* name = ModuleFileName(pc, path);
            if (name == nullptr)
            {
                return false;
            }

            bool system = false;
            for (auto module : c_systemModules)
            {
                system |= _wcsicmp(name, module) == 0;
            }

            if (!system)
            {
                for (auto driver : c_graphicsDrivers)
                {
                    if (_wcsnicmp(name, driver, wcslen(driver)) == 0)
                    {
                        return true;
                    }
                }

                return false;
            }

            ULONG_PTR imageBase = 0;
            PRUNTIME_FUNCTION function = RtlLookupFunctionEntry(pc, &imageBase, nullptr);
            if (function == nullptr)
            {
                return false;
            }

            PVOID handlerData = nullptr;
            ULONG_PTR establisherFrame = 0;
            RtlVirtualUnwind(UNW_FLAG_NHANDLER, imageBase, pc, function, &context, &handlerData, &establisherFrame, nullptr);
        }

        return false;
    }
}

LONG WINAPI Filter(EXCEPTION_POINTERS* exceptionInfo)
{
    auto record = exceptionInfo->ExceptionRecord;

    // UWP enables the strict handle check for good, so a handle that is no longer valid raises
    // where a desktop process would just see the call fail. A GPU driver we cannot fix gets that
    // failed call back, as it does in every other app on the machine. Anyone else still crashes:
    // there an invalid handle is usually freed memory, and carrying on would be worse.
    if (record->ExceptionCode == EXCEPTION_INVALID_HANDLE
        && (record->ExceptionFlags & EXCEPTION_NONCONTINUABLE) == 0
        && t_continues < c_maxContinues
        && IsRaisedForGraphicsDriver(exceptionInfo->ContextRecord))
    {
        // A process that goes on to suspend cleanly turns this into a survivable report.
        if (t_continues++ == 0 && NativeUtils::Callback)
        {
            NativeUtils::Callback(NativeUtils::GetBackTrace(winrt::hstring(), winrt::hstring(GetExceptionMessage(record) + L" (continued)"), record->ExceptionAddress));
        }

        return EXCEPTION_CONTINUE_EXECUTION;
    }

    if (NativeUtils::Callback)
    {
        // The backtrace is this thread's as it stands here, so it opens with the dispatcher that
        // reached the filter and the fault is somewhere below it. ExceptionAddress is the only
        // thing on the way in that names where the crash actually was.
        NativeUtils::Callback(NativeUtils::GetBackTrace(winrt::hstring(), winrt::hstring(GetExceptionMessage(record)), record->ExceptionAddress));
    }

    // This code would allow the app to continue running,
    // but there are great chances to make a big mess.
    //if (exceptionInfo->ExceptionRecord->ExceptionFlags & EXCEPTION_NONCONTINUABLE)
    //{
    return EXCEPTION_CONTINUE_SEARCH;
    //}

    //return EXCEPTION_CONTINUE_EXECUTION;
}

STDAPI_(BOOL) DllMain(_In_opt_ HINSTANCE hinst, DWORD reason, _In_opt_ void* reserved)
{
    if (reason == DLL_THREAD_ATTACH)
    {
        SetUnhandledExceptionFilter(Filter);
    }

    return TRUE;
}