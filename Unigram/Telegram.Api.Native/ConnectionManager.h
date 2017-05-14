#pragma once
#include <vector>
#include <map>
#include <wrl.h>
#include <Windows.Networking.Connectivity.h>
#include "MultiThreadObject.h"
#include "Telegram.Api.Native.h"

#define THREAD_COUNT 1
#define DEFAULT_DATACENTER_ID INT_MAX

using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;
using namespace ABI::Windows::Networking::Connectivity;
using ABI::Telegram::Api::Native::ConnectionState;
using ABI::Telegram::Api::Native::ConnectionNeworkType;
using ABI::Telegram::Api::Native::ConnectionType;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			class Datacenter;

			class ConnectionManager WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, ABI::Telegram::Api::Native::IConnectionManager, FtmBase>, public MultiThreadObject
			{
				friend class Connection;
				friend class EventObject;

				InspectableClass(RuntimeClass_Telegram_Api_Native_ConnectionManager, BaseTrust);

			public:
				ConnectionManager();
				~ConnectionManager();

				//COM exported methods
				STDMETHODIMP RuntimeClassInitialize(DWORD minimumThreadCount = THREAD_COUNT, DWORD maximumThreadCount = THREAD_COUNT);
				STDMETHODIMP add_CurrentNetworkTypeChanged(_In_ __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable* handler, _Out_ EventRegistrationToken* token);
				STDMETHODIMP remove_CurrentNetworkTypeChanged(EventRegistrationToken token);
				STDMETHODIMP get_ConnectionState(_Out_ ConnectionState* value);
				STDMETHODIMP get_CurrentNetworkType(_Out_ ConnectionNeworkType* value);
				STDMETHODIMP get_IsIpv6Enabled(_Out_ boolean* value);
				STDMETHODIMP get_IsNetworkAvailable(_Out_ boolean* value);
				STDMETHODIMP SendRequest(_In_ ABI::Telegram::Api::Native::ITLObject* object, UINT32 datacenterId, ConnectionType connetionType, boolean immediate, _Out_ INT32* requestToken);
				STDMETHODIMP CancelRequest(INT32 requestToken, boolean notifyServer);
				STDMETHODIMP GetDatacenterById(UINT32 id, _Out_ ABI::Telegram::Api::Native::IDatacenter** value);

				STDMETHODIMP BoomBaby(_Out_ ABI::Telegram::Api::Native::IConnection** value);

				//Internal methods
				INT64 GenerateMessageId();
				boolean IsNetworkAvailable();

				static HRESULT GetInstance(_Out_ ComPtr<ConnectionManager>& value);

			private:
				HRESULT UpdateNetworkStatus(boolean raiseEvent);
				HRESULT OnNetworkStatusChanged(_In_ IInspectable* sender);
				HRESULT OnConnectionOpened(_In_ Connection* connection);
				HRESULT OnConnectionDataReceived(_In_ Connection* connection);
				HRESULT OnConnectionQuickAckReceived(_In_ Connection* connection, INT32 ack);
				HRESULT OnConnectionClosed(_In_ Connection* connection);
				void OnEventObjectError(_In_ EventObject const* eventObject, HRESULT error);

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
				ComPtr<INetworkInformationStatics> m_networkInformation;
				EventSource<__FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable> m_currentNetworkTypeChangedEventSource;
				EventRegistrationToken m_networkChangedEventToken;
				ConnectionState m_connectionState;
				ConnectionNeworkType m_currentNetworkType;
				boolean m_isIpv6Enabled;
				std::vector<ComPtr<Connection>> m_activeConnections;
				UINT32 m_currentDatacenterId;
				std::map<UINT32, ComPtr<Datacenter>> m_datacenters;
				INT32 m_timeDelta;
				INT64 m_lastOutgoingMessageId;
			};


			class ConnectionManagerStatics WrlSealed : public ActivationFactory<ABI::Telegram::Api::Native::IConnectionManagerStatics, FtmBase>
			{
				friend class ConnectionManager;

				InspectableClassStatic(RuntimeClass_Telegram_Api_Native_ConnectionManager, BaseTrust);

			public:
				ConnectionManagerStatics();
				~ConnectionManagerStatics();

				STDMETHODIMP get_Instance(_Out_ ABI::Telegram::Api::Native::IConnectionManager** value);
			private:
				static ComPtr<ConnectionManager> s_instance;
			};

		}
	}
}