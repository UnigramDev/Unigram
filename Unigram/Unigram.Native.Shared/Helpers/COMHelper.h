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



class HResultException
{
	HRESULT m_Hr;

protected:
	explicit HResultException(HRESULT hr)
		: m_Hr(hr)
	{}

public:
	HRESULT GetHr() const
	{
		return m_Hr;
	}

	__declspec(noreturn)
		friend void ThrowHR(HRESULT);
};

//
// Throws an exception for the given HRESULT.
//
__declspec(noreturn) __declspec(noinline)
inline void ThrowHR(HRESULT hr)
{
	//if (DeviceLostException::IsDeviceLostHResult(hr))
	//	throw DeviceLostException(hr);
	//else
		throw HResultException(hr);
}

//
// Converts exceptions in the callable code into HRESULTs.
//
__declspec(noinline)
inline HRESULT ThrownExceptionToHResult()
{
	try
	{
		throw;
	}
	catch (HResultException const& e)
	{
		return e.GetHr();
	}
	catch (std::bad_alloc const&)
	{
		return E_OUTOFMEMORY;
	}
	catch (...)
	{
		return E_UNEXPECTED;
	}
}

template<typename CALLABLE>
HRESULT ExceptionBoundary(CALLABLE&& fn)
{
	try
	{
		fn();
		return S_OK;
	}
	catch (...)
	{
		return ThrownExceptionToHResult();
	}
}

//
// WRL's Make<>() function returns an empty ComPtr on failure rather than
// throwing an exception.  This checks the result and throws bad_alloc.
//
// Note: generally we use exceptions inside constructors to report errors.
// Therefore the only way that Make() will return an error is if an allocation
// fails.
//
__declspec(noreturn) __declspec(noinline)
inline void ThrowBadAlloc()
{
	throw std::bad_alloc();
}

inline void CheckMakeResult(bool result)
{
	if (!result)
		ThrowBadAlloc();
}

#endif