#pragma once
#include <map>
#include <wrl.h>
#include <windows.foundation.h>
#include "Telegram.Api.Native.h"
#include "ThreadpoolObject.h"
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
				Ipv6 = 0x80,
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

				class TLBinaryReader;
				class TLBinaryWriter;
				class TLObject;
				class TLMessage;
				class TLNewSessionCreated;
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

				InspectableClass(RuntimeClass_Telegram_Api_Native_Connection, BaseTrust);

			public:
				Connection(_In_ Datacenter* datacenter, ConnectionType type);
				~Connection();

				//COM exported methods
				IFACEMETHODIMP get_Datacenter(_Out_ IDatacenter** value);
				IFACEMETHODIMP get_Type(_Out_ ConnectionType* value);
				IFACEMETHODIMP get_CurrentNetworkType(_Out_ ConnectionNeworkType* value);
				IFACEMETHODIMP get_SessionId(_Out_ INT64* value);

				inline ComPtr<Datacenter> const& GetDatacenter() const
				{
					return m_datacenter;
				}

				inline ConnectionType GetType() const
				{
					return m_type;
				}

				inline boolean IsConnected()
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
				HRESULT Connect();
				HRESULT Connect(_In_ ComPtr<ConnectionManager> const& connectionManager, boolean ipv6);
				HRESULT Reconnect();
				HRESULT CreateMessagePacket(UINT32 messageLength, boolean reportAck, _Out_ ComPtr<TL::TLBinaryWriter>& writer, _Out_ BYTE** messageBuffer);
				HRESULT SendEncryptedMessage(_In_ MessageContext const* messageContext, _In_ ITLObject* messageBody, _Outptr_opt_ INT32* quickAckId);
				HRESULT SendUnencryptedMessage(_In_ ITLObject* messageBody, boolean reportAck);
				HRESULT HandleMessageResponse(_In_ MessageContext const* messageContext, _In_ ITLObject* messageBody, _In_ ConnectionManager* connectionManager);
				HRESULT OnNewSessionCreatedResponse(_In_ ConnectionManager* connectionManager, _In_ TL::TLNewSessionCreated* response);
				virtual HRESULT OnSocketConnected() override;
				virtual HRESULT OnDataReceived(_In_reads_(length) BYTE* buffer, UINT32 length) override;
				virtual HRESULT OnSocketDisconnected(int wsaError) override;
				HRESULT OnMessageReceived(_In_ ComPtr<ConnectionManager> const& connectionManager, _In_ TL::TLBinaryReader* messageReader, UINT32 messageLength);

				ConnectionType m_type;
				ConnectionFlag m_flags;
				ComPtr<Datacenter> m_datacenter;
				ComPtr<Timer> m_reconnectionTimer;
				ComPtr<NativeBuffer> m_partialPacketBuffer;
				UINT32 m_failedConnectionCount;
			};

		}
	}
}