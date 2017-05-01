#pragma once
#include <wrl.h>
#include "Telegram.Api.Native.h"
#include "EventObject.h"
#include "Timer.h"
#include "ConnectionSession.h"
#include "ConnectionSocket.h"

using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			class Connection WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, IConnection, EventObjectT<EventTraits::WSAEventTraits>, FtmBase>,
				public ConnectionSession, public ConnectionSocket
			{
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

			protected:
				HRESULT Connect();
				HRESULT Reconnect();
				HRESULT Suspend();

				virtual HRESULT OnSocketOpened() override;
				virtual HRESULT OnDataReceived() override;
				virtual HRESULT OnSocketClosed() override;

			private:
				inline Datacenter* GetDatacenter() const
				{
					return m_datacenter.Get();
				}

				CriticalSection m_criticalSection;
				UINT32 m_token;
				ConnectionType m_type;
				ConnectionNeworkType m_currentNetworkType;
				ComPtr<Datacenter> m_datacenter;
				ComPtr<Timer> m_reconnectionTimer;
			};

		}
	}
}