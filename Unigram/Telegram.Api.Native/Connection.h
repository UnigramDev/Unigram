#pragma once
#include <wrl.h>
#include "Telegram.Api.Native.h"
#include "EventObject.h"
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
				STDMETHODIMP get_Datacenter(_Out_ IDatacenter** value);
				STDMETHODIMP get_Type(_Out_ ConnectionType* value);

			protected:
				virtual HRESULT OnSocketOpened() override;
				virtual HRESULT OnDataReceived() override;
				virtual HRESULT OnSocketClosed() override;

			private:
				CriticalSection m_criticalSection;
				ComPtr<IDatacenter> m_datacenter;
				ConnectionType m_type;
			};

		}
	}
}