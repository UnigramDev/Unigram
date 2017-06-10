#include "pch.h"
#include <algorithm>
#include <openssl/rand.h>
#include "ConnectionSession.h"
#include "Helpers\COMHelper.h"

using namespace Telegram::Api::Native;


ConnectionSession::ConnectionSession() :
	m_nextSequenceNumber(0),
	m_minProcessedMessageId(0),
	m_id(GenereateNewId())
{
}

ConnectionSession::~ConnectionSession()
{
}

void ConnectionSession::Recreate()
{
	m_processedMessageIds.clear();
	m_messagesIdsForConfirmation.clear();
	m_processedSessionChanges.clear();
	m_nextSequenceNumber = 0;

	m_id = GenereateNewId();
}

UINT32 ConnectionSession::GenerateMessageSequenceNumber(boolean increment)
{
	UINT32 value = m_nextSequenceNumber;
	if (increment)
	{
		m_nextSequenceNumber++;
	}

	return value * 2 + (increment ? 1 : 0);
}

bool ConnectionSession::IsMessageIdProcessed(INT64 messageId)
{
	return !(messageId & 1) || m_minProcessedMessageId != 0 && messageId < m_minProcessedMessageId ||
		std::find(m_processedMessageIds.begin(), m_processedMessageIds.end(), messageId) != m_processedMessageIds.end();
}

void ConnectionSession::AddProcessedMessageId(INT64 messageId)
{
	if (m_processedMessageIds.size() > 300)
	{
		std::sort(m_processedMessageIds.begin(), m_processedMessageIds.end());
		m_processedMessageIds.erase(m_processedMessageIds.begin(), m_processedMessageIds.begin() + 100);
		m_minProcessedMessageId = *(m_processedMessageIds.begin());
	}

	m_processedMessageIds.push_back(messageId);
}

void ConnectionSession::AddMessageToConfirm(INT64 messageId)
{
	if (std::find(m_processedMessageIds.begin(), m_processedMessageIds.end(), messageId) == m_processedMessageIds.end())
	{
		m_messagesIdsForConfirmation.push_back(messageId);
	}
}

bool ConnectionSession::IsSessionProcessed(INT64 sessionId)
{
	return std::find(m_processedSessionChanges.begin(), m_processedSessionChanges.end(), sessionId) != m_processedSessionChanges.end();
}

void ConnectionSession::AddProcessedSession(INT64 sessionId)
{
	m_processedSessionChanges.push_back(sessionId);
}

INT64 ConnectionSession::GenereateNewId()
{
	INT64 newSessionId;
	RAND_bytes(reinterpret_cast<UINT8*>(&newSessionId), 8);

#if _DEBUG
	return 0xabcd000000000000L | (newSessionId & 0x0000ffffffffffffL);
#else
	return newSessionId;
#endif
}