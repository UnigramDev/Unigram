#pragma once 
#include <string>
#include <stdio.h>
#include <Winsock2.h>
#include <wrl\client.h>
#include <wrl\wrappers\corewrappers.h>

using namespace Microsoft::WRL::Wrappers;

#if _DEBUG 
#include "DebugHelper.h"

#ifndef __STRINGIFY
#define __STRINGIFY(x) #x
#define _STRINGIFY(x) __STRINGIFY(x)
#endif 

#ifndef __STRINGIFY_W
#define __STRINGIFY_W(x) L##x
#define _STRINGIFY_W(x) __STRINGIFY_W(x)
#endif

#define ReturnIfFailed(result, method) \
	if(FAILED(result = method)) \
	{ \
		OutputDebugStringFormat(_STRINGIFY_W("HRESULT 0x%08X at " __FUNCTION__  ", line " _STRINGIFY(__LINE__) ", file " _STRINGIFY(__FILE__) "\n"), result); \
		return result; \
	}

#define BreakIfFailed(result, method) \
	if(FAILED(result = method)) \
	{ \
		OutputDebugStringFormat(_STRINGIFY_W("HRESULT 0x%08X at " __FUNCTION__  ", line " _STRINGIFY(__LINE__) ", file " _STRINGIFY(__FILE__) "\n"), result); \
		break; \
	}
#else

#define ReturnIfFailed(result, method) \
	if(FAILED(result = method)) \
	{ \
		return result; \
	}

#define BreakIfFailed(result, method) \
	if(FAILED(result = method)) \
	{ \
		break; \
	}
#endif


#define WIN32_FROM_HRESULT(result) ((result) & 0x0000FFFF)

inline HRESULT WindowsCreateString(std::wstring const& wstring, _Out_ HSTRING* hstring)
{
	return WindowsCreateString(wstring.c_str(), static_cast<UINT32>(wstring.length()), hstring);
}

inline HRESULT GetLastHRESULT()
{
	return HRESULT_FROM_WIN32(GetLastError());
}

inline HRESULT WSAGetLastHRESULT()
{
	return HRESULT_FROM_WIN32(WSAGetLastError());
}

#ifdef __cplusplus_winrt

using ::Platform::Exception;

inline void ThrowIfFailed(HRESULT hr)
{
	if (FAILED(hr))
		throw Exception::CreateException(hr);
}

inline void ThrowException(HRESULT hr)
{
	throw Exception::CreateException(hr);
}

#endif