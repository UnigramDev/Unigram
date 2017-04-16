#include "pch.h"
#include "ConnectionManager.h"

using namespace Telegram::Api::Native;

ConnectionManager^ ConnectionManager::s_instance = nullptr;

ConnectionManager::ConnectionManager()
{
}

ConnectionManager^ ConnectionManager::Instance::get()
{
	if (s_instance == nullptr)
		s_instance = ref new ConnectionManager();

	return s_instance;
}