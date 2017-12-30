#pragma once
#include <map>
#include <atomic>
#include <wrl.h>
#include <windows.foundation.h>
#include "Telegram.Api.Native.h"
#include "ConnectionSession.h"
#include "ConnectionSocket.h"
#include "ConnectionCryptography.h"

using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;
using ABI::Telegram::Api::Native::IConnection;
using ABI::Windows::Foundation::IClosable;
using ABI::Telegram::Api::Native::IDatacenter;
using ABI::Telegram::Api::Native::IProxySettings;
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
				ProxyHandshakeState = 0xF0,
				CurrentNeworkType = 0x300,
				IPv6 = 0x400,
				CryptographyInitialized = 0x800,
				TryingNextEndpoint = 0x1000,
				Closed = 0x2000
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
				class TLMsgsStateInfo;

			}


			class Datacenter;
			class NativeBuffer;
			struct MessageContext;
			struct ServerEndpoint;

			class Connection WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, IConnection, CloakedIid<IClosable>>,
				public virtual MultiThreadObject, protected ConnectionSession, protected ConnectionSocket, protected ConnectionCryptography
			{
				friend class Datacenter;
				friend class ConnectionManager;
				friend class TL::TLObject;
				friend class TL::TLMessage;
				friend class TL::TLNewSessionCreated;
				friend class TL::TLMsgDetailedInfo;
				friend class TL::TLMsgNewDetailedInfo;
				friend class TL::TLMsgsStateInfo;

				InspectableClass(RuntimeClass_Telegram_Api_Native_Connection, BaseTrust);

			public:
				Connection();
				~Connection();

				//COM exported methods
				IFACEMETHODIMP get_Datacenter(_Out_ IDatacenter** value);
				IFACEMETHODIMP get_Type(_Out_ ConnectionType* value);
				IFACEMETHODIMP get_CurrentNetworkType(_Out_ ConnectionNeworkType* value);
				IFACEMETHODIMP get_SessionId(_Out_ INT64* value);
				IFACEMETHODIMP get_IsConnected(_Out_ boolean* value);

				//Internal methods
				STDMETHODIMP RuntimeClassInitialize(_In_ Datacenter* datacenter, ConnectionType type);

				inline ComPtr<Datacenter> const& GetDatacenter() const
				{
					//auto lock = LockCriticalSection();
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

				inline bool IsHandshaking()
				{
					auto lock = LockCriticalSection();
					return static_cast<ProxyHandshakeState>(m_flags & ConnectionFlag::ProxyHandshakeState) != ProxyHandshakeState::None;
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

				enum class ProxyHandshakeState
				{
					None = 0x0,
					Initializing = 0x1 << 4,
					SendingGreeting = 0x3 << 4,
					Authenticating = 0x7 << 4,
					RequestingConnection = 0xF << 4,
				};

				IFACEMETHODIMP Close();
				HRESULT EnsureConnected();
				HRESULT Connect(bool ipv6);
				HRESULT Reconnect();
				HRESULT CreateMessagePacket(UINT32 messageLength, bool reportAck, _Out_ ComPtr<TL::TLMemoryBinaryWriter>& writer, _Out_ BYTE** messageBuffer);
				HRESULT SendEncryptedMessage(_In_ MessageContext const* messageContext, _In_ ITLObject* messageBody, _Outptr_opt_ INT32* quickAckId);
				HRESULT SendEncryptedMessageWithConfirmation(_In_ MessageContext const* messageContext, _In_ ITLObject* messageBody, _Outptr_opt_ INT32* quickAckId);
				HRESULT SendUnencryptedMessage(_In_ ITLObject* messageBody, bool reportAck);
				HRESULT HandleMessageResponse(_In_ MessageContext const* messageContext, _In_ ITLObject* messageBody);
				void ConfirmAndResetRequest(INT64 messageId);
				HRESULT OnNewSessionCreatedResponse(_In_ TL::TLNewSessionCreated* response);
				HRESULT OnMsgDetailedInfoResponse(_In_ TL::TLMsgDetailedInfo* response);
				HRESULT OnMsgNewDetailedInfoResponse(_In_ TL::TLMsgNewDetailedInfo* response);
				HRESULT OnMsgsStateInfoResponse(_In_ TL::TLMsgsStateInfo* response);
				HRESULT OnMessageReceived(_In_ TL::TLMemoryBinaryReader* messageReader, UINT32 messageLength);
				HRESULT OnDataReceived(_In_reads_(length) BYTE* buffer, UINT32 length);
				HRESULT OnProxyConnected();
				HRESULT OnProxyGreetingResponse(_In_reads_(length) BYTE* buffer, UINT32 length);
				HRESULT OnProxyAuthenticationResponse(_In_reads_(length) BYTE* buffer, UINT32 length);
				HRESULT OnProxyConnectionRequestResponse(_In_reads_(length) BYTE* buffer, UINT32 length);

				virtual HRESULT OnSocketConnected() override;
				virtual HRESULT OnSocketDataReceived(_In_reads_(length) BYTE* buffer, UINT32 length) override;
				virtual HRESULT OnSocketDisconnected(int wsaError) override;

				static HRESULT GetProxyEndpoint(_In_ IProxySettings* proxySettings, _Out_ ServerEndpoint* endpoint);

				ConnectionType m_type;
				ConnectionFlag m_flags;
				ComPtr<Datacenter> m_datacenter;
				ComPtr<NativeBuffer> m_partialPacketBuffer;
				UINT32 m_failedConnectionCount;
			};

		}
	}
}