#include "pch.h"
#include <zlib.h>
#include "ConnectionManager.h"
#include "Connection.h"
#include "Helpers\COMHelper.h"

using namespace Telegram::Api::Native;

ConnectionManager^ ConnectionManager::s_instance = nullptr;

ConnectionManager::ConnectionManager() :
	m_connectionState(Telegram::Api::Native::ConnectionState::NotInitialized)
{
	WSADATA wsaData;
	if (WSAStartup(MAKEWORD(2, 2), &wsaData) != 0)
	{
		ThrowWSALastError();
	}
}

ConnectionManager::~ConnectionManager()
{
	WSACleanup();
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
	//I want to die
}

void ConnectionManager::RemoveEvent(IEventObject^ eventObject)
{
	//I want to die
}