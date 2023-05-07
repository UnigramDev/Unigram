#include "pch.h"
#include "NativeUtils.h"
#include "StringUtils.h"

using namespace winrt::Telegram::Native::implementation;

const wchar_t* GetExceptionMessage(DWORD code)
{
    switch (code) {
    case EXCEPTION_ACCESS_VIOLATION: return L"ACCESS_VIOLATION";
    case EXCEPTION_ARRAY_BOUNDS_EXCEEDED: return L"ARRAY_BOUNDS_EXCEEDED";
    case EXCEPTION_BREAKPOINT: return L"EXCEPTION_BREAKPOINT";
    case EXCEPTION_DATATYPE_MISALIGNMENT: return L"DATATYPE_MISALIGNMENT";
    case EXCEPTION_FLT_DENORMAL_OPERAND: return L"FLT_DENORMAL_OPERAND";
    case EXCEPTION_FLT_DIVIDE_BY_ZERO: return L"FLT_DIVIDE_BY_ZERO";
    case EXCEPTION_FLT_INEXACT_RESULT: return L"FLT_INEXACT_RESULT";
    case EXCEPTION_FLT_INVALID_OPERATION: return L"FLT_INVALID_OPERATION";
    case EXCEPTION_FLT_OVERFLOW: return L"FLT_OVERFLOW";
    case EXCEPTION_FLT_STACK_CHECK: return L"FLT_STACK_CHECK";
    case EXCEPTION_FLT_UNDERFLOW: return L"FLT_UNDERFLOW";
    case EXCEPTION_ILLEGAL_INSTRUCTION: return L"ILLEGAL_INSTRUCTION";
    case EXCEPTION_IN_PAGE_ERROR: return L"IN_PAGE_ERROR";
    case EXCEPTION_INT_DIVIDE_BY_ZERO: return L"INT_DIVIDE_BY_ZERO";
    case EXCEPTION_INT_OVERFLOW: return L"INT_OVERFLOW";
    case EXCEPTION_INVALID_DISPOSITION: return L"INVALID_DISPOSITION";
    case EXCEPTION_NONCONTINUABLE_EXCEPTION: return L"NONCONTINUABLE_EXCEPTION";
    case EXCEPTION_PRIV_INSTRUCTION: return L"PRIV_INSTRUCTION";
    case EXCEPTION_SINGLE_STEP: return L"SINGLE_STEP";
    case EXCEPTION_STACK_OVERFLOW: return L"STACK_OVERFLOW";
    default: return L"UNKNOWN";
    };
}

// From http://davidpritchard.org/archives/907
std::wstring GetStackTrace(DWORD code)
{
    constexpr uint32_t TRACE_MAX_STACK_FRAMES = 99;
    void* stack[TRACE_MAX_STACK_FRAMES];

    ULONG hash;
    const int numFrames = CaptureStackBackTrace(1, TRACE_MAX_STACK_FRAMES, stack, &hash);

    const wchar_t* message = GetExceptionMessage(code);
    std::wstring result = wstrprintf(L"Unhandled exception: %s\n", message);

    for (int i = 0; i < numFrames; ++i)
    {
        void* moduleBaseVoid = nullptr;
        RtlPcToFileHeader(stack[i], &moduleBaseVoid);

        auto moduleBase = (const unsigned char*)moduleBaseVoid;
        constexpr auto MODULE_BUF_SIZE = 4096U;
        wchar_t modulePath[MODULE_BUF_SIZE];
        const wchar_t* moduleFilename = modulePath;

        if (moduleBase != nullptr)
        {
            GetModuleFileName((HMODULE)moduleBase, modulePath, MODULE_BUF_SIZE);

            int moduleFilenamePos = std::wstring(modulePath).find_last_of(L"\\");
            if (moduleFilenamePos >= 0)
            {
                moduleFilename += moduleFilenamePos + 1;
            }

            result += wstrprintf(L"   at %s+0x%08lx\n", moduleFilename, (uint32_t)((unsigned char*)stack[i] - moduleBase));
        }
        else
        {
            result += wstrprintf(L"   at %s+0x%016llx\n", moduleFilename, (uint64_t)stack[i]);
        }
    }

    return result;
}

static long Filter(_In_ struct _EXCEPTION_POINTERS* exceptionInfo)
{
    if (NativeUtils::Callback)
    {
        NativeUtils::Callback(GetStackTrace(exceptionInfo->ExceptionRecord->ExceptionCode));
    }

    // This code would allow the app to continue running,
    // but there are great chances to make a big mess.
    //if (exceptionInfo->ExceptionRecord->ExceptionFlags & EXCEPTION_NONCONTINUABLE)
    //{
    //    return EXCEPTION_EXECUTE_HANDLER;
    //}

    return EXCEPTION_CONTINUE_EXECUTION;
}

STDAPI_(BOOL) DllMain(_In_opt_ HINSTANCE hinst, DWORD reason, _In_opt_ void* reserved)
{
    if (reason == DLL_THREAD_ATTACH)
    {
        SetUnhandledExceptionFilter(Filter);
    }

    return TRUE;
}