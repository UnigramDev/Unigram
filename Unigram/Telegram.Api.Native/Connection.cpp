#include "pch.h"
#include "Connection.h"
#include "Datacenter.h"

using namespace Telegram::Api::Native;


Connection::Connection() :
	m_type(ConnectionType::Generic)
{
}

Connection::~Connection()
{
}

HRESULT Connection::RuntimeClassInitialize(Datacenter* datacenter, ConnectionType type)
{
	m_datacenter = datacenter;
	m_type = type;
	return S_OK;
}

HRESULT Connection::get_Datacenter(IDatacenter** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto lock = m_criticalSection.Lock();
	return m_datacenter.CopyTo(value);
}

HRESULT Connection::get_Type(ConnectionType* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_type;
	return S_OK;
}

HRESULT Connection::OnSocketOpened()
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement socket connection opened handling");

	return S_OK;
}

HRESULT Connection::OnDataReceived()
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement socket connection data received handling");

	return S_OK;
}

HRESULT Connection::OnSocketClosed()
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement socket connection closed handling");

	return S_OK;
}