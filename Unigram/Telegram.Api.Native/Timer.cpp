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
	m_waitableTimer.Attach(CreateWaitableTimer(nullptr, FALSE, nullptr));
	if (!m_waitableTimer.IsValid())
	{
		return GetLastHRESULT();
	}

	m_callback = callback;
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

	if (m_started || m_timeout == 0)
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
		if (!CancelWaitableTimer(m_waitableTimer.Get()))
		{
			return GetLastHRESULT();
		}

		m_started = false;
	}

	return S_OK;
}

HRESULT Timer::OnEvent(EventObjectEventContext const* context)
{
	auto lock = m_criticalSection.Lock();

	HRESULT result = m_callback();

	m_started = m_repeatable;

	return result;
}

HRESULT Timer::SetTimerTimeout()
{
	LARGE_INTEGER dueTime;
	dueTime.QuadPart = -10000LL * m_timeout;

	if (!SetWaitableTimer(m_waitableTimer.Get(), &dueTime, m_repeatable ? m_timeout : 0, nullptr, nullptr, FALSE))
	{
		return GetLastHRESULT();
	}

	m_started = true;
	return S_OK;
}