#pragma once
#include <vector>
#include <rpc.h>
#include <rpcndr.h>

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			class ConnectionSession abstract
			{
			public:
				ConnectionSession();
				~ConnectionSession();

			protected:
				inline INT64 GetSessionId() const
				{
					return m_id;
				}

				inline boolean GetHasMessagesToConfirm() const
				{
					return !m_messagesIdsForConfirmation.empty();
				}

				void RecreateSession();
				UINT32 GenerateMessageSequenceNumber(boolean increment);
				bool IsMessageIdProcessed(INT64 messageId);
				void AddProcessedMessageId(INT64 messageId);
				void AddMessageToConfirm(INT64 messageId);
				bool IsSessionProcessed(INT64 sessionId);
				void AddProcessedSession(INT64 sessionId);

				static INT64 GenereateNewSessionId();

			private:
				INT64 m_id;
				UINT32 m_nextSequenceNumber;
				INT64 m_minProcessedMessageId;
				std::vector<INT64> m_processedMessageIds;
				std::vector<INT64> m_messagesIdsForConfirmation;
				std::vector<INT64> m_processedSessionChanges;
			};

		}
	}
}