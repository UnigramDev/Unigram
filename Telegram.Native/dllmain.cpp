#include "pch.h"
#include "NativeUtils.h"
#include "DebugUtils.h"

using namespace winrt::Telegram::Native::implementation;

LONG WINAPI Filter(EXCEPTION_POINTERS* exceptionInfo)
{
    if (NativeUtils::Callback)
    {
        auto record = exceptionInfo->ExceptionRecord;

        // The backtrace is this thread's as it stands here, so it opens with the dispatcher that
        // reached the filter and the fault is somewhere below it. ExceptionAddress is the only
        // thing on the way in that names where the crash actually was.
        NativeUtils::Callback(NativeUtils::GetBackTrace(winrt::hstring(), winrt::hstring(GetExceptionMessage(record)), record->ExceptionAddress));
    }

    // This code would allow the app to continue running,
    // but there are great chances to make a big mess.
    //if (exceptionInfo->ExceptionRecord->ExceptionFlags & EXCEPTION_NONCONTINUABLE)
    //{
    return EXCEPTION_EXECUTE_HANDLER;
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