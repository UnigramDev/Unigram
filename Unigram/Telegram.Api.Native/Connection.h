#pragma once
#include <memory>
#include <vector>
#include <openssl/aes.h>
#include "Timer.h"
#include "Datacenter.h"

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			public enum class ConnectionType
			{
				Generic = 1,
				Download = 2,
				Upload = 4,
				Push = 8
			};

			ref class Connection sealed : IEventObject
			{
				friend ref class ConnectionManager;

			public:
				//Connection

				property ConnectionType Type
				{
					ConnectionType get();
				}

				property Telegram::Api::Native::Datacenter^ Datacenter
				{
					Telegram::Api::Native::Datacenter^ get();
				}

				property uint32 Token
				{
					uint32 get();
				}

				void Connect();
				void Suspend();
				//void SendData(_In_ NativeByteBuffer^ buffer, bool reportAck);


				//ConnectionSession

				property int64 SessionId
				{
					int64 get();
					void set(int64 value);
				}

				property bool HasMessagesToConfirm
				{
					bool get();
				}

				void RecreateSession();
				void GenereateNewSessionId();
				uint32 GenerateMessageSeqNo(bool increment);
				bool IsMessageIdProcessed(int64 messageId);
				void AddProcessedMessageId(int64 messageId);
				void AddMessageToConfirm(int64 messageId);
				//NetworkMessage^ GenerateConfirmationRequest();
				bool IsSessionProcessed(int64 sessionId);
				void AddProcessedSession(int64 sessionId);

			internal:
				Connection(_In_ Telegram::Api::Native::Datacenter^ datacenter, ConnectionType connectionType);

			private:
				virtual void OnEvent(uint32 events) sealed = IEventObject::OnEvent;

				CriticalSection m_criticalSection;


				//Connection

				void Reconnect();

				ConnectionType m_type;
				uint32 m_token;
				Timer^ m_reconnectTimer;
				Telegram::Api::Native::Datacenter^ m_datacenter;

				AES_KEY m_encryptKey;
				uint8_t m_encryptIv[16];
				uint32_t m_encryptNum;
				uint8_t m_encryptCount[16];

				AES_KEY m_decryptKey;
				uint8_t m_decryptIv[16];
				uint32_t m_decryptNum;
				uint8_t m_decryptCount[16];


				//ConnectionSession

				int64 m_sessionId;
				uint32 m_nextSeqNo  ;
				int64 m_minProcessedMessageId  ;
				std::vector<int64> m_processedMessageIds;
				std::vector<int64> m_messagesIdsForConfirmation;
				std::vector<int64> m_processedSessionChanges;
			};

		}
	}
}