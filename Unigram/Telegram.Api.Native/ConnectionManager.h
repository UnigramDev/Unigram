#pragma once
#include <vector>
#include <wrl.h>
#include "Telegram.Api.Native.h"
#include "Thread.h"

using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			struct IEventObject;

			class ConnectionManager WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, IConnectionManager, FtmBase>
			{
				friend class Connection;

				InspectableClass(RuntimeClass_Telegram_Api_Native_ConnectionManager, BaseTrust);

			public:
				ConnectionManager();
				~ConnectionManager();

				STDMETHODIMP RuntimeClassInitialize();
				STDMETHODIMP get_ConnectionState(_Out_ ConnectionState* value);
				STDMETHODIMP get_CurrentNetworkType(_Out_ ConnectionNeworkType* value);
				STDMETHODIMP get_IsIpv6Enabled(_Out_ boolean* value);
				STDMETHODIMP SendRequest(_In_ ITLObject* object, UINT32 datacenterId, ConnectionType connetionType, boolean immediate, _Out_ INT32* requestToken);
				STDMETHODIMP CancelRequest(INT32 requestToken, boolean notifyServer);

			private:
				HRESULT OnConnectionOpened(_In_ Connection* connection);
				HRESULT OnConnectionDataReceived(_In_ Connection* connection);
				HRESULT OnConnectionClosed(_In_ Connection* connection);

				static DWORD WINAPI WorkerThread(_In_ LPVOID parameter);

				CriticalSection m_criticalSection;
				CHAR m_working;
				ConnectionState m_connectionState;
				ConnectionNeworkType m_currentNetworkType;
				boolean m_isIpv6Enabled;
				Thread m_workerThread;
				std::vector<ComPtr<IConnection>> m_activeConnections;
			};


			class ConnectionManagerStatics WrlSealed : public ActivationFactory<IConnectionManagerStatics, FtmBase>
			{
				InspectableClassStatic(RuntimeClass_Telegram_Api_Native_ConnectionManager, BaseTrust);

			public:
				ConnectionManagerStatics();
				~ConnectionManagerStatics();

				STDMETHODIMP get_Instance(_Out_ IConnectionManager** value);

				static HRESULT GetInstance(_Out_ ComPtr<ConnectionManager>& value);

			private:
				static ComPtr<ConnectionManager> s_instance;
			};

		}
	}
}