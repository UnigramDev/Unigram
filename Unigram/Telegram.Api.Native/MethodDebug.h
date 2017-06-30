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

class DebugCriticalSectionLock
{
	friend class DebugCriticalSection;

public:
	DebugCriticalSectionLock(_Inout_ DebugCriticalSectionLock&& other) throw() :
		m_criticalSection(other.m_criticalSection)
	{
		other.m_criticalSection = nullptr;
	}

	DebugCriticalSectionLock(const DebugCriticalSectionLock&) = delete;
	DebugCriticalSectionLock& operator=(const DebugCriticalSectionLock&) = delete;

	_Releases_lock_(*m_criticalSection) ~DebugCriticalSectionLock() throw()
	{
		InternalUnlock();
	}

	_Releases_lock_(*m_criticalSection) void Unlock() throw()
	{
		InternalUnlock();
	}

	bool IsLocked() const throw()
	{
		return m_criticalSection != nullptr;
	}

private:
	explicit DebugCriticalSectionLock(CRITICAL_SECTION* criticalSection = nullptr) throw() :
		m_criticalSection(criticalSection)
	{
	}

	_Releases_lock_(*m_criticalSection) void InternalUnlock() throw()
	{
		if (IsLocked())
		{
			LeaveCriticalSection(m_criticalSection);

			OutputDebugStringFormat(L"LeaveCriticalSection -> Pointer: 0x%p\n", m_criticalSection);

			m_criticalSection = nullptr;
		}
	}

	CRITICAL_SECTION* m_criticalSection;
};

class DebugCriticalSection
{
public:
	typedef DebugCriticalSectionLock SyncLock;

	explicit DebugCriticalSection(ULONG spincount = 0) throw()
	{
		::InitializeCriticalSectionEx(&m_criticalSection, spincount, 0);
	}

	DebugCriticalSection(const DebugCriticalSection&) = delete;
	DebugCriticalSection& operator=(const DebugCriticalSection&) = delete;

	~DebugCriticalSection()
	{
		DeleteCriticalSection(&m_criticalSection);
	}

	_Acquires_lock_(*return.m_criticalSection) _Post_same_lock_(*return.m_criticalSection, m_criticalSection) SyncLock Lock(LPCWSTR methodName) throw()
	{
		EnterCriticalSection(&m_criticalSection);

		OutputDebugStringFormat(L"EnterCriticalSection -> Pointer: 0x%p, LockCount: %d, RecursionCount: %d, Thread: %u, Method: %s\n", &m_criticalSection,
			m_criticalSection.LockCount, m_criticalSection.RecursionCount, m_criticalSection.OwningThread, methodName);

		return SyncLock(&m_criticalSection);
	}

	_Acquires_lock_(*return.m_criticalSection) _Post_same_lock_(*return.m_criticalSection, m_criticalSection) SyncLock Lock() throw()
	{
		EnterCriticalSection(&m_criticalSection);
		return SyncLock(&m_criticalSection);
	}

private:
	CRITICAL_SECTION m_criticalSection;
};