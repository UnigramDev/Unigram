#include "pch.h"
#include "NativeUtils.h"
#include "DebugUtils.h"

using namespace winrt::Telegram::Native::implementation;

LONG WINAPI Filter(EXCEPTION_POINTERS* exceptionInfo)
{
    if (NativeUtils::Callback)
    {
        NativeUtils::Callback(NativeUtils::GetBackTrace(exceptionInfo->ExceptionRecord->ExceptionCode));
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