#include "pch.h"
#include <algorithm>
#include <openssl/rand.h>
#include "ConnectionSession.h"
#include "ConnectionManager.h"
#include "TLTypes.h"
#include "Helpers\COMHelper.h"

using namespace Telegram::Api::Native;
using namespace Telegram::Api::Native::TL;


ConnectionSession::ConnectionSession() :
	m_nextMessageSequenceNumber(0),
	m_minProcessedMessageId(0),
	m_sessionId(GenereateNewSessionId())
{
}

ConnectionSession::~ConnectionSession()
{
}

void ConnectionSession::RecreateSession()
{
	auto lock = LockCriticalSection();

	m_processedMessageIds.clear();
	m_messagesIdsToConfirm.clear();
	m_processedSessionChanges.clear();
	m_nextMessageSequenceNumber = 0;

	m_sessionId = GenereateNewSessionId();
}

HRESULT ConnectionSession::AddConfirmationMessage(ConnectionManager* connectionManager, std::vector<ComPtr<TLMessage>>& messages)
{
	auto lock = LockCriticalSection();

	if (m_messagesIdsToConfirm.empty())
	{
		return S_FALSE;
	}

	HRESULT result;
	ComPtr<TLMessage> message;
	ReturnIfFailed(result, CreateConfirmationMessage(connectionManager, &message));

	messages.push_back(message);
	return S_OK;
}

HRESULT ConnectionSession::CreateConfirmationMessage(ConnectionManager* connectionManager, TL::TLMessage** messages)
{
	auto msgAck = Make<TLMsgsAck>();
	auto& messagesIds = msgAck->GetMessagesIds();
	messagesIds.insert(messagesIds.begin(), m_messagesIdsToConfirm.begin(), m_messagesIdsToConfirm.end());

	HRESULT result;
	ReturnIfFailed(result, MakeAndInitialize<TLMessage>(messages, connectionManager->GenerateMessageId(), GenerateMessageSequenceNumber(false), msgAck.Get()));

	m_messagesIdsToConfirm.clear();
	return S_OK;
}

UINT32 ConnectionSession::GenerateMessageSequenceNumber(bool increment)
{
	auto lock = LockCriticalSection();

	auto value = m_nextMessageSequenceNumber;
	if (increment)
	{
		m_nextMessageSequenceNumber++;
	}

	return value * 2 + (increment ? 1 : 0);
}

bool ConnectionSession::IsMessageIdProcessed(INT64 messageId)
{
	return !(messageId & 1) || (m_minProcessedMessageId != 0 && messageId < m_minProcessedMessageId) ||
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
		m_messagesIdsToConfirm.push_back(messageId);
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

INT64 ConnectionSession::GenereateNewSessionId()
{
	INT64 newSessionId;
	RAND_bytes(reinterpret_cast<BYTE*>(&newSessionId), sizeof(INT64));

#if _DEBUG
	return 0xabcd000000000000L | (newSessionId & 0x0000ffffffffffffL);
#else
	return newSessionId;
#endif
}