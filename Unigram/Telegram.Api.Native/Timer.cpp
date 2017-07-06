#include "pch.h"
#include "Timer.h"
#include "Helpers\COMHelper.h"

using namespace Telegram::Api::Native;


Timer::Timer(TimerCallback callback) :
	m_callback(callback),
	m_started(false),
	m_repeatable(false),
	m_timeout(0)
{
}

Timer::~Timer()
{
}

HRESULT Timer::get_IsStarted(boolean* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto lock = LockCriticalSection();

	/*auto threadpoolObjectHandle = GetThreadpoolObjectHandle();
	if (threadpoolObjectHandle == nullptr)
	{
		return E_NOT_VALID_STATE;
	}

	*value = IsThreadpoolTimerSet(threadpoolObjectHandle) == TRUE;*/

	*value = m_started;
	return S_OK;
}

HRESULT Timer::SetTimeout(UINT32 timeoutMs, boolean repeat)
{
	auto lock = LockCriticalSection();

	if (timeoutMs != m_timeout || static_cast<bool>(repeat) != m_repeatable)
	{
		m_timeout = timeoutMs;
		m_repeatable = repeat;

		if (m_started)
		{
			return SetTimerTimeout();
		}
	}

	return S_OK;
}

HRESULT Timer::Start()
{
	auto lock = LockCriticalSection();

	if (m_timeout == 0)
	{
		return E_NOT_VALID_STATE;
	}

	m_started = true;
	return SetTimerTimeout();
}

HRESULT Timer::Stop()
{
	bool started;

	{
		auto lock = LockCriticalSection();

		started = m_started;
		m_started = false;
	}

	if (started)
	{
		return ResetThreadpoolObject(false);
	}
	else
	{
		return S_FALSE;
	}
}

HRESULT Timer::OnEvent(PTP_CALLBACK_INSTANCE callbackInstance, ULONG_PTR param)
{
	auto lock = LockCriticalSection();

	if (m_started)
	{
		m_started = m_repeatable;

		m_callback();
	}

	return S_OK;
}

HRESULT Timer::SetTimerTimeout()
{
	auto threadpoolObjectHandle = EventObjectT::GetHandle();
	if (threadpoolObjectHandle == nullptr)
	{
		return E_NOT_VALID_STATE;
	}

	FILETIME timeout;
	TimeoutToFileTime(m_timeout, timeout);

	SetThreadpoolTimer(threadpoolObjectHandle, &timeout, m_repeatable ? m_timeout : 0, 0);
	return S_OK;
}