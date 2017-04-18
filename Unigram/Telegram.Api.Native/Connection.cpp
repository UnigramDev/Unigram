#include "pch.h"
#include <openssl/rand.h>
#include "Connection.h"
#include "ConnectionManager.h"

using namespace Telegram::Api::Native;

Connection::Connection(::ConnectionType connectionType) :
	m_connectionType(connectionType)
{
	m_reconnectTimer = ref new Timer([&] {
		m_reconnectTimer->Stop();
		Connect();
	});
}

Telegram::Api::Native::ConnectionType Connection::ConnectionType::get()
{
	return m_connectionType;
}

uint32 Connection::ConnectionToken::get()
{
	return m_connectionToken;
}

void Connection::Connect()
{
}