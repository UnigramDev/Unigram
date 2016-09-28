#pragma once
using Platform::Exception;

inline void ThrowIfFailed(HRESULT hr)
{
	if (FAILED(hr))
		throw Exception::CreateException(hr);
}

inline void ThrowException(HRESULT hr)
{
	throw Exception::CreateException(hr);
}

inline void OutputDebugStringFormat(LPCWSTR pwhFormat, ...)
{
	va_list args;
	va_start(args, pwhFormat);
	WCHAR szBuffer[1024];
	int32 nBuf = vswprintf_s(szBuffer, 1024, pwhFormat, args);
	OutputDebugStringW(szBuffer);
	va_end(args);
}