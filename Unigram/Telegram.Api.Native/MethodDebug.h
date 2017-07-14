#pragma once
#include <string>
#include "Helpers\DebugHelper.h"

#if _DEBUG
#define METHOD_DEBUG() const MethodDebug methodDebug(this, _STRINGIFY_W(__FUNCTION__))
#else
#define METHOD_DEBUG() 
#endif

class MethodDebug
{
public:
	MethodDebug(void const* object, LPCWSTR methodName) :
		m_startTime(GetTickCount64())
	{
		OutputDebugStringFormat(L"\n%sObject: 0x%p, Method: %s, Thread: %u\n", std::wstring(InterlockedIncrement(&s_indent) - 1, '\t').c_str(), object, methodName, GetCurrentThreadId());
	}

	~MethodDebug()
	{
		OutputDebugStringFormat(L"%sCompleted in %I64u ms\n\n", std::wstring(InterlockedDecrement(&s_indent), '\t').c_str(), GetTickCount64() - m_startTime);
	}

private:
	ULONGLONG m_startTime;

	static thread_local UINT32 s_indent;
};