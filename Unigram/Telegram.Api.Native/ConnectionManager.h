#pragma once
#include <vector>
#include <wrl.h>
#include "Telegram.Api.Native.h"

#define THREAD_COUNT 1

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
				friend class EventObject;

				InspectableClass(RuntimeClass_Telegram_Api_Native_ConnectionManager, BaseTrust);

			public:
				ConnectionManager();
				~ConnectionManager();

				STDMETHODIMP RuntimeClassInitialize(DWORD minimumThreadCount = THREAD_COUNT, DWORD maximumThreadCount = THREAD_COUNT);
				STDMETHODIMP get_ConnectionState(_Out_ ConnectionState* value);
				STDMETHODIMP get_CurrentNetworkType(_Out_ ConnectionNeworkType* value);
				STDMETHODIMP get_IsIpv6Enabled(_Out_ boolean* value);
				STDMETHODIMP SendRequest(_In_ ITLObject* object, UINT32 datacenterId, ConnectionType connetionType, boolean immediate, _Out_ INT32* requestToken);
				STDMETHODIMP CancelRequest(INT32 requestToken, boolean notifyServer);

			private:
				HRESULT OnConnectionOpened(_In_ Connection* connection);
				HRESULT OnConnectionDataReceived(_In_ Connection* connection);
				HRESULT OnConnectionClosed(_In_ Connection* connection);
				void OnEventObjectError(_In_ EventObject const* eventObject, HRESULT error);

				CriticalSection m_criticalSection;
				TP_CALLBACK_ENVIRON m_threadpoolEnvironment;
				PTP_POOL m_threadpool;
				PTP_CLEANUP_GROUP m_threadpoolCleanupGroup;
				ConnectionState m_connectionState;
				ConnectionNeworkType m_currentNetworkType;
				boolean m_isIpv6Enabled;
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