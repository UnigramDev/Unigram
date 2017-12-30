#pragma once
#include <vector>
#include <wrl.h>
#include <rpcndr.h>
#include "Telegram.Api.Native.h"
#include "MultiThreadObject.h"

using namespace Microsoft::WRL;
using ABI::Telegram::Api::Native::TL::ITLObject;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{
			namespace TL
			{

				class TLMessage;

			}


			class ConnectionManager;

			class ConnectionSession abstract : public virtual MultiThreadObject
			{
			public:
				ConnectionSession();
				~ConnectionSession();

			protected:
				HRESULT AddConfirmationMessage(_In_ ConnectionManager* connectionManager, _In_ std::vector<ComPtr<TL::TLMessage>>& messages);
				HRESULT CreateConfirmationMessage(_In_ ConnectionManager* connectionManager, _Out_ TL::TLMessage** messages);
				void RecreateSession();
				UINT32 GenerateMessageSequenceNumber(bool increment);
				bool IsMessageIdProcessed(INT64 messageId);
				void AddProcessedMessageId(INT64 messageId);
				void AddMessageToConfirm(INT64 messageId);
				bool IsSessionProcessed(INT64 sessionId);
				void AddProcessedSession(INT64 sessionId);

				inline INT64 GetSessionId()
				{
					return m_sessionId;
				}

				inline void SetSessionId(INT64 sessionId)
				{
					m_sessionId = sessionId;
				}

				inline bool HasMessagesToConfirm()
				{
					auto lock = LockCriticalSection();
					return !m_messagesIdsToConfirm.empty();
				}

				static INT64 GenereateNewSessionId();

			private:
				INT64 m_sessionId;
				UINT32 m_nextMessageSequenceNumber;
				INT64 m_minProcessedMessageId;
				std::vector<INT64> m_processedMessageIds;
				std::vector<INT64> m_messagesIdsToConfirm;
				std::vector<INT64> m_processedSessionChanges;
			};

		}
	}
}