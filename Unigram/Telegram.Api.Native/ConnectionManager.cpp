#include "pch.h"
#include <zlib.h>
#include "ConnectionManager.h"
#include "Connection.h"

using namespace Telegram::Api::Native;

ConnectionManager^ ConnectionManager::s_instance = nullptr;

ConnectionManager::ConnectionManager() :
	m_connectionState(Telegram::Api::Native::ConnectionState::NotInitialized)
{
}

ConnectionManager^ ConnectionManager::Instance::get()
{
	if (s_instance == nullptr)
		s_instance = ref new ConnectionManager();

	return s_instance;
}

Telegram::Api::Native::ConnectionState ConnectionManager::ConnectionState::get()
{
	auto lock = m_criticalSection.Lock();

	return m_connectionState;
}

bool ConnectionManager::IsNetworkAvailable::get()
{
	return false;
}

void ConnectionManager::ScheduleEvent(IEventObject^ eventObject, uint32 timeout)
{
}

void ConnectionManager::RemoveEvent(IEventObject^ eventObject)
{

}