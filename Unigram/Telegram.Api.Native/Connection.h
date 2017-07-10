#pragma once
#include <map>
#include <wrl.h>
#include <windows.foundation.h>
#include "Telegram.Api.Native.h"
#include "EventObject.h"
#include "Timer.h"
#include "ConnectionSession.h"
#include "ConnectionSocket.h"
#include "ConnectionCryptography.h"

using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;
using ABI::Telegram::Api::Native::IConnection;
using ABI::Windows::Foundation::IClosable;
using ABI::Telegram::Api::Native::IDatacenter;
using ABI::Telegram::Api::Native::ConnectionNeworkType;
using ABI::Telegram::Api::Native::ConnectionType;
using ABI::Telegram::Api::Native::TL::ITLObject;

namespace ABI
{
	namespace Telegram
	{
		namespace Api
		{
			namespace Native
			{

				struct IMessageRequest;

			}
		}
	}
}

using ABI::Telegram::Api::Native::IMessageRequest;


namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			enum class ConnectionFlag
			{
				None = 0,
				ConnectionState = 0xF,
				CurrentNeworkType = 0x70,
				IPv6 = 0x80,
				CryptographyInitialized = 0x100,
				TryingNextEndpoint = 0x200,
				Closed = 0x400
			};

		}
	}
}

DEFINE_ENUM_FLAG_OPERATORS(Telegram::Api::Native::ConnectionFlag);


namespace Telegram
{
	namespace Api
	{
		namespace Native
		{
			namespace TL
			{

				class TLMemoryBinaryReader;
				class TLMemoryBinaryWriter;
				class TLObject;
				class TLMessage;
				class TLNewSessionCreated;
				class TLMsgDetailedInfo;
				class TLMsgNewDetailedInfo;

			}


			class Datacenter;
			class NativeBuffer;
			struct MessageContext;

			class Connection WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, IConnection, CloakedIid<IClosable>>,
				public virtual EventObjectT<EventTraits::WaitTraits>, public ConnectionSession, public ConnectionSocket, public ConnectionCryptography
			{
				friend class Datacenter;
				friend class ConnectionManager;
				friend class TL::TLObject;
				friend class TL::TLMessage;
				friend class TL::TLNewSessionCreated;
				friend class TL::TLMsgDetailedInfo;
				friend class TL::TLMsgNewDetailedInfo;

				InspectableClass(RuntimeClass_Telegram_Api_Native_Connection, BaseTrust);

			public:
				Connection(_In_ Datacenter* datacenter, ConnectionType type);
				~Connection();

				//COM exported methods
				IFACEMETHODIMP get_Datacenter(_Out_ IDatacenter** value);
				IFACEMETHODIMP get_Type(_Out_ ConnectionType* value);
				IFACEMETHODIMP get_CurrentNetworkType(_Out_ ConnectionNeworkType* value);
				IFACEMETHODIMP get_SessionId(_Out_ INT64* value);
				IFACEMETHODIMP get_IsConnected(_Out_ boolean* value);

				inline ComPtr<Datacenter> const& GetDatacenter() const
				{
					return m_datacenter;
				}

				inline ConnectionType GetType() const
				{
					return m_type;
				}

				inline bool IsConnected()
				{
					auto lock = LockCriticalSection();
					return static_cast<ConnectionState>(m_flags & ConnectionFlag::ConnectionState) > ConnectionState::Disconnected;
				}

			private:
				enum class ConnectionState
				{
					Disconnected = 0x0,
					Connecting = 0x1,
					Reconnecting = 0x3,
					Connected = 0x7,
					DataReceived = 0xF
				};

				IFACEMETHODIMP Close();
				HRESULT Connect(bool ipv6);
				HRESULT Reconnect();
				HRESULT CreateMessagePacket(UINT32 messageLength, bool reportAck, _Out_ ComPtr<TL::TLMemoryBinaryWriter>& writer, _Out_ BYTE** messageBuffer);
				HRESULT SendEncryptedMessage(_In_ MessageContext const* messageContext, _In_ ITLObject* messageBody, _Outptr_opt_ INT32* quickAckId);
				HRESULT SendEncryptedMessageWithConfirmation(_In_ MessageContext const* messageContext, _In_ ITLObject* messageBody, _Outptr_opt_ INT32* quickAckId);
				HRESULT SendUnencryptedMessage(_In_ ITLObject* messageBody, bool reportAck);
				HRESULT HandleMessageResponse(_In_ MessageContext const* messageContext, _In_ ITLObject* messageBody);
				HRESULT OnNewSessionCreatedResponse(_In_ TL::TLNewSessionCreated* response);
				HRESULT OnMsgDetailedInfoResponse(_In_ TL::TLMsgDetailedInfo* response);
				HRESULT OnMsgNewDetailedInfoResponse(_In_ TL::TLMsgNewDetailedInfo* response);
				HRESULT OnMessageReceived(_In_ TL::TLMemoryBinaryReader* messageReader, UINT32 messageLength);
				virtual HRESULT OnSocketConnected() override;
				virtual HRESULT OnDataReceived(_In_reads_(length) BYTE* buffer, UINT32 length) override;
				virtual HRESULT OnSocketDisconnected(int wsaError) override;

				ConnectionType m_type;
				ConnectionFlag m_flags;
				ComPtr<Datacenter> m_datacenter;
				ComPtr<NativeBuffer> m_partialPacketBuffer;
				UINT32 m_failedConnectionCount;
			};

		}
	}
}