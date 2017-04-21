#include "pch.h"
#include <openssl/rand.h>
#include "Connection.h"
#include "ConnectionManager.h"
#include "Datacenter.h"

using namespace Telegram::Api::Native;

Connection::Connection(Telegram::Api::Native::Datacenter^ datacenter, ConnectionType type) :
	m_type(type),
	m_datacenter(datacenter),
	m_nextSeqNo(0),
	m_minProcessedMessageId(0),
	m_socket(INVALID_SOCKET)
{
	GenereateNewSessionId();

	m_reconnectTimer = ref new Timer([&] {
		m_reconnectTimer->Stop();
		Connect();
	});
}

ConnectionType Connection::Type::get()
{
	return m_type;
}

uint32 Connection::Token::get()
{
	auto lock = m_criticalSection.Lock();

	return m_token;
}

void Connection::Connect()
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO
}

void Connection::Suspend()
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO
}

void Connection::Reconnect()
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO
}

int64 Connection::SessionId::get()
{
	auto lock = m_criticalSection.Lock();

	return m_sessionId;
}

void Connection::SessionId::set(int64 value)
{
	auto lock = m_criticalSection.Lock();

	m_sessionId = value;
}

bool Connection::HasMessagesToConfirm::get()
{
	auto lock = m_criticalSection.Lock();

	return !m_messagesIdsForConfirmation.empty();
}

void Connection::RecreateSession()
{
	auto lock = m_criticalSection.Lock();

	m_processedMessageIds.clear();
	m_messagesIdsForConfirmation.clear();
	m_processedSessionChanges.clear();
	m_nextSeqNo = 0;

	GenereateNewSessionId();
}

void Connection::GenereateNewSessionId()
{
	auto lock = m_criticalSection.Lock();

	int64 newSessionId;
	RAND_bytes(reinterpret_cast<uint8*>(&newSessionId), 8);

#if _DEBUG
	m_sessionId = (0xabcd000000000000L | (newSessionId & 0x0000ffffffffffffL));
#else
	m_sessionId = newSessionId;
#endif
}

uint32 Connection::GenerateMessageSeqNo(bool increment)
{
	auto lock = m_criticalSection.Lock();

	uint32 value = m_nextSeqNo;
	if (increment)
	{
		m_nextSeqNo++;
	}

	return value * 2 + (increment ? 1 : 0);
}

bool Connection::IsMessageIdProcessed(int64 messageId)
{
	auto lock = m_criticalSection.Lock();

	return !(messageId & 1) || m_minProcessedMessageId != 0 && messageId < m_minProcessedMessageId ||
		std::find(m_processedMessageIds.begin(), m_processedMessageIds.end(), messageId) != m_processedMessageIds.end();
}

void Connection::AddProcessedMessageId(int64 messageId)
{
	auto lock = m_criticalSection.Lock();

	if (m_processedMessageIds.size() > 300)
	{
		std::sort(m_processedMessageIds.begin(), m_processedMessageIds.end());
		m_processedMessageIds.erase(m_processedMessageIds.begin(), m_processedMessageIds.begin() + 100);
		m_minProcessedMessageId = *(m_processedMessageIds.begin());
	}

	m_processedMessageIds.push_back(messageId);
}

void Connection::AddMessageToConfirm(int64 messageId)
{
	auto lock = m_criticalSection.Lock();

	if (std::find(m_processedMessageIds.begin(), m_processedMessageIds.end(), messageId) == m_processedMessageIds.end())
	{
		m_messagesIdsForConfirmation.push_back(messageId);
	}
}

//NetworkMessage^ Connection::GenerateConfirmationRequest()
//{
//	auto lock = m_criticalSection.Lock();
//
//	if (!m_messagesIdsForConfirmation.empty())
//	{
//		return nullptr;
//	}
//
//	TL_msgs_ack *msgAck = new TL_msgs_ack();
//	msgAck->msg_ids.insert(msgAck->msg_ids.begin(), m_messagesIdsForConfirmation.begin(), m_messagesIdsForConfirmation.end());
//	NativeByteBuffer *os = new NativeByteBuffer(true);
//	msgAck->serializeToStream(os);
//
//	auto networkMessage = new NetworkMessage();
//	networkMessage->message = std::unique_ptr<TL_message>(new TL_message);
//	networkMessage->message->msg_id = ConnectionsManager::Instance->GenerateMessageId();
//	networkMessage->message->seqno = GenerateMessageSeqNo(false);
//	networkMessage->message->bytes = os->capacity();
//	networkMessage->message->body = std::unique_ptr<TLObject>(msgAck);
//	m_messagesIdsForConfirmation.clear();
//
//	return networkMessage;
//}

bool Connection::IsSessionProcessed(int64 sessionId)
{
	auto lock = m_criticalSection.Lock();

	return std::find(m_processedSessionChanges.begin(), m_processedSessionChanges.end(), sessionId) != m_processedSessionChanges.end();
}

void Connection::AddProcessedSession(int64 sessionId)
{
	auto lock = m_criticalSection.Lock();

	m_processedSessionChanges.push_back(sessionId);
}

void Connection::OnEvent(uint32 events)
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO
}