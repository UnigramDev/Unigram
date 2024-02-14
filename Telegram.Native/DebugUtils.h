#pragma once

#include <vector>
#include <sstream>
#include <windows.h>

#include "StringUtils.h"

#ifndef DEBUG_UTILS_H
#define DEBUG_UTILS_H

typedef struct _STOWED_EXCEPTION_INFORMATION_HEADER
{
    ULONG Size;
    ULONG Signature;
} STOWED_EXCEPTION_INFORMATION_HEADER, * PSTOWED_EXCEPTION_INFORMATION_HEADER;

typedef struct _STOWED_EXCEPTION_INFORMATION_V2
{
    STOWED_EXCEPTION_INFORMATION_HEADER Header;
    HRESULT                             ResultCode;
    struct
    {
        DWORD ExceptionForm : 2;
        DWORD ThreadId : 30;
    };
    union
    {
        struct
        {
            PVOID ExceptionAddress;
            ULONG StackTraceWordSize;
            ULONG StackTraceWords;
            PVOID StackTrace;
        };
        struct
        {
            PWSTR ErrorText;
        };
    };
    ULONG                               NestedExceptionType;
    PVOID                               NestedException;
} STOWED_EXCEPTION_INFORMATION_V2, * PSTOWED_EXCEPTION_INFORMATION_V2;

MIDL_INTERFACE("22bf789e-94f7-460f-a389-d1fa18649749")
IRestrictedErrorInfoContext : public ::IUnknown
{
public:
    virtual HRESULT STDMETHODCALLTYPE CaptureContext(
        USHORT framesToSkip) = 0;

    virtual HRESULT STDMETHODCALLTYPE GetContext(
        /* [out] */ __RPC__deref_out_opt STOWED_EXCEPTION_INFORMATION_V2** exception) = 0;
};

inline const wchar_t* GetExceptionMessage(DWORD code)
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

#endif
