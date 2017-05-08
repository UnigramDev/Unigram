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
using ABI::Windows::Foundation::IClosable;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			class Connection WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, IConnection, CloakedIid<IClosable>, FtmBase>,
				public virtual EventObjectT<EventTraits::WaitTraits>, ConnectionSession, ConnectionSocket, ConnectionCryptograpy
			{
				friend class Datacenter;
				friend class ConnectionManager;

				InspectableClass(RuntimeClass_Telegram_Api_Native_Connection, BaseTrust);

			public:
				Connection();
				~Connection();

				STDMETHODIMP RuntimeClassInitialize(_In_ Datacenter* datacenter, ConnectionType type);
				STDMETHODIMP get_Token(_Out_ UINT32* value);
				STDMETHODIMP get_Datacenter(_Out_ IDatacenter** value);
				STDMETHODIMP get_Type(_Out_ ConnectionType* value);
				STDMETHODIMP get_CurrentNetworkType(_Out_ ConnectionNeworkType* value);

			private:
				inline Datacenter* GetDatacenter() const
				{
					return m_datacenter.Get();
				}

				STDMETHODIMP Close();
				HRESULT OnEvent(_In_ PTP_CALLBACK_INSTANCE callbackInstance);
				HRESULT Connect();
				HRESULT Reconnect();
				HRESULT Suspend();
				virtual HRESULT OnSocketCreated() override;
				virtual HRESULT OnSocketConnected() override;
				virtual HRESULT OnDataReceived(_In_reads_(length) BYTE const* buffer, UINT32 length) override;
				virtual HRESULT OnSocketDisconnected() override;
				virtual HRESULT OnSocketClosed(int wsaError) override;
				void OnEventObjectError(_In_ EventObject* eventObject, HRESULT result);

				CriticalSection m_criticalSection;
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