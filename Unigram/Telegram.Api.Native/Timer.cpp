#include "pch.h"
#include "Timer.h"
#include "Helpers\COMHelper.h"

using namespace Telegram::Api::Native;


Timer::Timer() :
	m_started(false),
	m_repeatable(false),
	m_timeout(0)
{
}

Timer::~Timer()
{
}

HRESULT Timer::RuntimeClassInitialize(TimerCallback callback)
{
	/*m_waitableTimer.Attach(CreateWaitableTimer(nullptr, FALSE, nullptr));
	if (!m_waitableTimer.IsValid())
	{
		return GetLastHRESULT();
	}*/

	m_callback = callback;
	return S_OK;
}

HRESULT Timer::get_IsStarted(boolean* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto lock = m_criticalSection.Lock();

	/*auto threadpoolObjectHandle = GetThreadpoolObjectHandle();
	if (threadpoolObjectHandle == nullptr)
	{
		return E_NOT_VALID_STATE;
	}

	*value = IsThreadpoolTimerSet(threadpoolObjectHandle) == TRUE;*/

	*value = m_started;
	return S_OK;
}

HRESULT Timer::SetTimeout(UINT32 msTimeout, boolean repeat)
{
	auto lock = m_criticalSection.Lock();

	if (msTimeout != m_timeout || repeat != m_repeatable)
	{
		m_timeout = msTimeout;
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
	auto lock = m_criticalSection.Lock();

	if (m_timeout == 0)
	{
		return E_NOT_VALID_STATE;
	}

	return SetTimerTimeout();
}

HRESULT Timer::Stop()
{
	auto lock = m_criticalSection.Lock();

	if (m_started)
	{
		auto threadpoolObjectHandle = GetThreadpoolObjectHandle();
		if (threadpoolObjectHandle != nullptr)
		{
			WaitForThreadpoolTimerCallbacks(threadpoolObjectHandle, TRUE);
			SetThreadpoolTimer(threadpoolObjectHandle, nullptr, 0, 0);
		}
	}

	return S_OK;
}

HRESULT Timer::OnEvent(PTP_CALLBACK_INSTANCE callbackInstance)
{
	auto lock = m_criticalSection.Lock();

	HRESULT result = m_callback();

	m_started = m_repeatable;
	return result;
}

HRESULT Timer::SetTimerTimeout()
{
	auto threadpoolObjectHandle = GetThreadpoolObjectHandle();
	if (threadpoolObjectHandle == nullptr)
	{
		return E_NOT_VALID_STATE;
	}

	INT64 timeout = -10000LL * m_timeout;
	SetThreadpoolTimer(threadpoolObjectHandle, reinterpret_cast<PFILETIME>(&timeout), m_repeatable ? m_timeout : 0, 0);

	return S_OK;
}