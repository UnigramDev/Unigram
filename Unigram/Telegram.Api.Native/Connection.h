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

			class Connection WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, IConnection>,
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
				IFACEMETHODIMP get_Token(_Out_ UINT32* value);
				IFACEMETHODIMP get_Datacenter(_Out_ IDatacenter** value);
				IFACEMETHODIMP get_Type(_Out_ ConnectionType* value);
				IFACEMETHODIMP get_CurrentNetworkType(_Out_ ConnectionNeworkType* value);
				IFACEMETHODIMP get_SessionId(_Out_ INT64* value);

				inline Datacenter* GetDatacenter() const
				{
					return m_datacenter.Get();
				}

				inline ConnectionType GetType() const
				{
					return m_type;
				}

			private:
				inline ConnectionNeworkType GetCurrentNeworkType() const
				{
					return m_currentNetworkType;
				}

				HRESULT Connect();
				HRESULT Reconnect();
				HRESULT Suspend();
				HRESULT CreateMessagePacket(UINT32 messageLength, boolean reportAck, _Out_ TL::TLBinaryWriter** writer, _Out_ BYTE** messageBuffer);
				HRESULT SendEncryptedMessage(_In_ MessageContext const* messageContext, _In_ ITLObject* messageBody, _Outptr_opt_ INT32* quickAckId);
				HRESULT SendUnencryptedMessage(_In_ ITLObject* messageBody, boolean reportAck);
				HRESULT HandleMessageResponse(_In_ MessageContext const* messageContext, _In_ ITLObject* messageBody, _In_::Telegram::Api::Native::ConnectionManager* connectionManager);
				HRESULT HandleNewSessionCreatedResponse(_In_::Telegram::Api::Native::ConnectionManager* connectionManager, _In_ TL::TLNewSessionCreated* response);
				virtual HRESULT OnSocketConnected() override;
				virtual HRESULT OnDataReceived(_In_reads_(length) BYTE const* buffer, UINT32 length) override;
				virtual HRESULT OnSocketDisconnected(int wsaError) override;
				HRESULT OnMessageReceived(_In_ ConnectionManager* connectionManager, _In_ TL::TLBinaryReader* messageReader, UINT32 messageLength);
				void OnEventObjectError(_In_ EventObject* eventObject, HRESULT result);

				UINT32 m_token;
				ConnectionType m_type;
				ConnectionNeworkType m_currentNetworkType;
				ComPtr<Datacenter> m_datacenter;
				ComPtr<Timer> m_reconnectionTimer;
				ComPtr<NativeBuffer> m_partialPacketBuffer;

				UINT32 m_failedConnectionCount;
				UINT32 m_connectionAttemptCount;
			};

		}
	}
}