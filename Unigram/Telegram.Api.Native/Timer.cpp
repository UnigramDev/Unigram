#include "pch.h"
#include "Timer.h"
#include "ConnectionManager.h"

using namespace Telegram::Api::Native;

Timer::Timer(std::function<void()> callback) :
	m_callback(callback),
	m_started(false),
	m_repeatable(false),
	m_timeout(0)
{
}

Timer::~Timer()
{
	Stop();
}

bool Timer::Repeatable::get()
{
	auto lock = m_criticalSection.Lock();

	return m_repeatable;
}

bool Timer::Started::get()
{
	auto lock = m_criticalSection.Lock();

	return m_started;
}

uint32 Timer::Timeout::get()
{
	auto lock = m_criticalSection.Lock();

	return m_timeout;
}

void Timer::Start()
{
	auto lock = m_criticalSection.Lock();

	if (!m_started && m_timeout > 0)
	{
		ConnectionManager::Instance->ScheduleEvent(this, m_timeout);

		m_started = true;
	}
}

void Timer::Stop()
{
	auto lock = m_criticalSection.Lock();

	if (m_started)
	{
		ConnectionManager::Instance->RemoveEvent(this);

		m_started = false;
	}
}

void Timer::SetTimeout(uint32 timeout, bool repeat)
{
	auto lock = m_criticalSection.Lock();

	if (m_timeout != timeout)
	{
		m_repeatable = repeat;
		m_timeout = timeout;

		if (m_started)
		{
			ConnectionManager::Instance->RemoveEvent(this);
			ConnectionManager::Instance->ScheduleEvent(this, timeout);
		}
	}
}

void Timer::OnEvent(uint32 events)
{
	auto lock = m_criticalSection.Lock();

	m_callback();

	if (m_started && m_repeatable && m_timeout > 0)
		ConnectionManager::Instance->ScheduleEvent(this, m_timeout);
}