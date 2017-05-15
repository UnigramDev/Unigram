#pragma once
#include <wrl.h>
#include <windows.foundation.h>
#include "Telegram.Api.Native.h"
#include "EventObject.h"
#include "Timer.h"
#include "ConnectionSession.h"
#include "ConnectionSocket.h"
#include "ConnectionCryptograpy.h"

using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;
using ABI::Telegram::Api::Native::IConnection;
using ABI::Windows::Foundation::IClosable;
using ABI::Telegram::Api::Native::IDatacenter;
using ABI::Telegram::Api::Native::ConnectionNeworkType;
using ABI::Telegram::Api::Native::ConnectionType;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			class Datacenter;

			class Connection WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, IConnection, CloakedIid<IClosable>>,
				public virtual EventObjectT<EventTraits::WaitTraits>, public ConnectionSession, public ConnectionSocket, public ConnectionCryptograpy
			{
				friend class ConnectionManager;

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

				//Internal methods
				IFACEMETHODIMP Close();
				HRESULT Connect();
				HRESULT Reconnect();
				HRESULT Suspend();

			private:
				inline Datacenter* GetDatacenter() const
				{
					return m_datacenter.Get();
				}

				inline ConnectionType GetType() const
				{
					return m_type;
				}

				inline ConnectionNeworkType GetCurrentNeworkType() const
				{
					return m_currentNetworkType;
				}

				virtual HRESULT OnSocketCreated() override;
				virtual HRESULT OnSocketConnected() override;
				virtual HRESULT OnDataReceived(_In_reads_(length) BYTE const* buffer, UINT32 length) override;
				virtual HRESULT OnSocketDisconnected() override;
				virtual HRESULT OnSocketClosed(int wsaError) override;
				void OnEventObjectError(_In_ EventObject* eventObject, HRESULT result);

				UINT32 m_token;
				ConnectionType m_type;
				ConnectionNeworkType m_currentNetworkType;
				ComPtr<Datacenter> m_datacenter;
				ComPtr<Timer> m_reconnectionTimer;

				UINT32 m_failedConnectionCount;
				UINT32 m_connectionAttemptCount;
			};

		}
	}
}