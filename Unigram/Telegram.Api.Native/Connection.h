#pragma once
#include <memory>
#include <vector>
#include <Winsock2.h>
#include <openssl/aes.h>
#include "Timer.h"

#define DOWNLOAD_CONNECTIONS_COUNT 2
#define UPLOAD_CONNECTIONS_COUNT 2

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			ref class Datacenter;


			public enum class ConnectionType
			{
				Generic = 1,
				Download = 2,
				Upload = 4,
				Push = 8
			};

			public enum class ConnectionNeworkType
			{
				Mobile = 0,
				WiFi = 1,
				Roaming = 2
			};


			interface class IConnectionSession
			{
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

			};


			ref class Connection sealed : IEventObject, public IConnectionSession
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

				virtual property int64 SessionId
				{
					int64 get();
					void set(int64 value);
				}

				virtual property bool HasMessagesToConfirm
				{
					bool get();
				}

				virtual void RecreateSession();
				virtual void GenereateNewSessionId();
				virtual uint32 GenerateMessageSeqNo(bool increment);
				virtual bool IsMessageIdProcessed(int64 messageId);
				virtual void AddProcessedMessageId(int64 messageId);
				virtual void AddMessageToConfirm(int64 messageId);
				//virtual NetworkMessage^ GenerateConfirmationRequest();
				virtual bool IsSessionProcessed(int64 sessionId);
				virtual void AddProcessedSession(int64 sessionId);

				//ConnectionSocket

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
				uint8 m_encryptIv[16];
				uint32 m_encryptNum;
				uint8 m_encryptCount[16];

				AES_KEY m_decryptKey;
				uint8 m_decryptIv[16];
				uint32 m_decryptNum;
				uint8 m_decryptCount[16];


				//ConnectionSession

				int64 m_sessionId;
				uint32 m_nextSeqNo;
				int64 m_minProcessedMessageId;
				std::vector<int64> m_processedMessageIds;
				std::vector<int64> m_messagesIdsForConfirmation;
				std::vector<int64> m_processedSessionChanges;


				//ConnectionSocket

				SOCKET m_socket;
			};

		}
	}
}