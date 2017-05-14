#pragma once
#include <vector>
#include <map>
#include <wrl.h>
#include "NetworkExtensions.h"
#include "MultiThreadObject.h"
#include "Telegram.Api.Native.h"

#define THREAD_COUNT 1
#define DEFAULT_DATACENTER_ID INT_MAX

using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			struct IEventObject;

			class ConnectionManager WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, IConnectionManager, FtmBase>, public MultiThreadObject
			{
				friend class Connection;
				friend class EventObject;

				InspectableClass(RuntimeClass_Telegram_Api_Native_ConnectionManager, BaseTrust);

			public:
				ConnectionManager();
				~ConnectionManager();

				//COM exported methods
				STDMETHODIMP RuntimeClassInitialize(DWORD minimumThreadCount = THREAD_COUNT, DWORD maximumThreadCount = THREAD_COUNT);
				STDMETHODIMP get_ConnectionState(_Out_ ConnectionState* value);
				STDMETHODIMP get_CurrentNetworkType(_Out_ ConnectionNeworkType* value);
				STDMETHODIMP get_IsIpv6Enabled(_Out_ boolean* value);
				STDMETHODIMP get_IsNetworkAvailable(_Out_ boolean* value);
				STDMETHODIMP SendRequest(_In_ ITLObject* object, UINT32 datacenterId, ConnectionType connetionType, boolean immediate, _Out_ INT32* requestToken);
				STDMETHODIMP CancelRequest(INT32 requestToken, boolean notifyServer);
				STDMETHODIMP GetDatacenterById(UINT32 id, _Out_ IDatacenter** value);

				STDMETHODIMP BoomBaby(_Out_ IConnection** value);

				//Internal methods
				INT64 GenerateMessageId();

				static HRESULT GetInstance(_Out_ ComPtr<ConnectionManager>& value);

			private:
				HRESULT OnConnectionOpened(_In_ Connection* connection);
				HRESULT OnConnectionDataReceived(_In_ Connection* connection);
				HRESULT OnConnectionQuickAckReceived(_In_ Connection* connection, INT32 ack);
				HRESULT OnConnectionClosed(_In_ Connection* connection);
				void OnEventObjectError(_In_ EventObject const* eventObject, HRESULT error);

				static void WINAPI OnInterfaceChanged(_In_ PVOID callerContext, _In_ PMIB_IPINTERFACE_ROW row OPTIONAL, _In_ MIB_NOTIFICATION_TYPE notificationType);

				inline static UINT64 GetCurrentRealTime()
				{
					FILETIME time;
					GetSystemTimePreciseAsFileTime(&time);

					return ((static_cast<UINT64>(time.dwHighDateTime) << 32) | static_cast<UINT64>(time.dwLowDateTime)) / 1000000ULL;
				}

				inline static UINT64 GetCurrentMonotonicTime()
				{
					return GetTickCount64();
				}

				TP_CALLBACK_ENVIRON m_threadpoolEnvironment;
				PTP_POOL m_threadpool;
				PTP_CLEANUP_GROUP m_threadpoolCleanupGroup;
				HANDLE m_networkChangedNotificationHandle;
				ConnectionState m_connectionState;
				ConnectionNeworkType m_currentNetworkType;
				boolean m_isIpv6Enabled;
				std::vector<ComPtr<Connection>> m_activeConnections;
				UINT32 m_currentDatacenterId;
				std::map<UINT32, ComPtr<Datacenter>> m_datacenters;
				INT32 m_timeDelta;
				INT64 m_lastOutgoingMessageId;
			};


			class ConnectionManagerStatics WrlSealed : public ActivationFactory<IConnectionManagerStatics, FtmBase>
			{
				friend class ConnectionManager;

				InspectableClassStatic(RuntimeClass_Telegram_Api_Native_ConnectionManager, BaseTrust);

			public:
				ConnectionManagerStatics();
				~ConnectionManagerStatics();

				STDMETHODIMP get_Instance(_Out_ IConnectionManager** value);
			private:
				static ComPtr<ConnectionManager> s_instance;
			};

		}
	}
}