#include "pch.h"
#include "NativeUtils.h"
#include "DebugUtils.h"

using namespace winrt::Telegram::Native::implementation;

namespace
{
    // Something that retries a dead handle in a loop would go through exception dispatch on
    // every turn of it.
    constexpr int c_maxContinues = 16;
    thread_local int t_continues = 0;
}

LONG WINAPI Filter(EXCEPTION_POINTERS* exceptionInfo)
{
    auto record = exceptionInfo->ExceptionRecord;

    // UWP enables the strict handle check for good, so a handle that is no longer valid raises
    // where a desktop process would just see the call fail. Most of these handles are not ours -
    // drivers, the D3D runtime, input methods - so the caller gets that failure back, as it would
    // in any other app on the machine.
    if (record->ExceptionCode == EXCEPTION_INVALID_HANDLE
        && (record->ExceptionFlags & EXCEPTION_NONCONTINUABLE) == 0
        && t_continues < c_maxContinues)
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