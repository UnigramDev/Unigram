#include "pch.h"
#include "NativeUtils.h"
#include "OrphanTerminator.h"
#include "DebugUtils.h"

#define DMANIP_DELEGATE_THREAD L"DManip Delegate Thread"

using namespace winrt::Telegram::Native::implementation;

LONG WINAPI Filter(EXCEPTION_POINTERS* exceptionInfo)
{
    if (NativeUtils::Callback)
    {
        NativeUtils::Callback(GetBacktrace(exceptionInfo->ExceptionRecord->ExceptionCode));
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
    else if (reason == DLL_THREAD_DETACH)
    {
        HANDLE hThread = GetCurrentThread();
        PWSTR pDescription;

        HRESULT hr = GetThreadDescription(hThread, &pDescription);

        if (SUCCEEDED(hr))
        {
            int compare = wcscmp(pDescription, DMANIP_DELEGATE_THREAD);
            if (compare == 0)
            {
                OrphanTerminator::DetachingThread();
            }
        }
    }

    return TRUE;
}