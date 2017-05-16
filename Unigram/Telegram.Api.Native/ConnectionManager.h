#pragma once
#include <vector>
#include <map>
#include <queue>
#include <wrl.h>
#include <Windows.Networking.Connectivity.h>
#include "MultiThreadObject.h"
#include "Telegram.Api.Native.h"

#define THREAD_COUNT 1
#define DEFAULT_DATACENTER_ID INT_MAX
//#define TELEGRAM_API_NATIVE_CONFIGVERSION 2
#define TELEGRAM_API_NATIVE_PROTOVERSION 1
#define TELEGRAM_API_NATIVE_VERSION 1
#define TELEGRAM_API_NATIVE_LAYER 65
#define TELEGRAM_API_NATIVE_APIID 6

using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;
using namespace ABI::Windows::Networking::Connectivity;
using ABI::Telegram::Api::Native::IConnectionManager;
using ABI::Telegram::Api::Native::IConnectionManagerStatics;
using ABI::Telegram::Api::Native::IUserConfiguration;
using ABI::Telegram::Api::Native::Version;
using ABI::Telegram::Api::Native::ConnectionState;
using ABI::Telegram::Api::Native::ConnectionNeworkType;
using ABI::Telegram::Api::Native::ConnectionType;
using ABI::Telegram::Api::Native::ISendRequestCompletedCallback;
using ABI::Telegram::Api::Native::IRequestQuickAckReceivedCallback;
using ABI::Telegram::Api::Native::IDatacenter;
using ABI::Telegram::Api::Native::IConnection;
using ABI::Telegram::Api::Native::TL::ITLObject;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			class Datacenter;
			class Request;

			class ConnectionManager WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, IConnectionManager>, public MultiThreadObject
			{
				friend class Connection;
				friend class EventObject;

				InspectableClass(RuntimeClass_Telegram_Api_Native_ConnectionManager, BaseTrust);

			public:
				ConnectionManager();
				~ConnectionManager();

				//COM exported methods		
				IFACEMETHODIMP add_CurrentNetworkTypeChanged(_In_ __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable* handler, _Out_ EventRegistrationToken* token);
				IFACEMETHODIMP remove_CurrentNetworkTypeChanged(EventRegistrationToken token);
				IFACEMETHODIMP add_ConnectionStateChanged(_In_ __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable* handler, _Out_ EventRegistrationToken* token);
				IFACEMETHODIMP remove_ConnectionStateChanged(EventRegistrationToken token);
				IFACEMETHODIMP add_UnparsedMessageReceived(_In_ __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CTL__CTLUnparsedMessage* handler, _Out_ EventRegistrationToken* token);
				IFACEMETHODIMP remove_UnparsedMessageReceived(EventRegistrationToken token);
				IFACEMETHODIMP get_ConnectionState(_Out_ ConnectionState* value);
				IFACEMETHODIMP get_CurrentNetworkType(_Out_ ConnectionNeworkType* value);
				IFACEMETHODIMP get_IsIpv6Enabled(_Out_ boolean* value);
				IFACEMETHODIMP get_IsNetworkAvailable(_Out_ boolean* value);
				IFACEMETHODIMP SendRequest(_In_ ITLObject* object, _In_ ISendRequestCompletedCallback* onCompleted, _In_ IRequestQuickAckReceivedCallback* onQuickAckReceived,
					UINT32 datacenterId, ConnectionType connectionType, boolean immediate, INT32 requestToken);
				IFACEMETHODIMP CancelRequest(INT32 requestToken, boolean notifyServer);
				IFACEMETHODIMP GetDatacenterById(UINT32 id, _Out_ IDatacenter** value);

				IFACEMETHODIMP BoomBaby(_In_ IUserConfiguration* userConfiguration, _Out_ ITLObject** object, _Out_ IConnection** value);

				//Internal methods
				STDMETHODIMP RuntimeClassInitialize(DWORD minimumThreadCount = THREAD_COUNT, DWORD maximumThreadCount = THREAD_COUNT);
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
				Datacenter* GetDatacenterById(UINT32 id);

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
				EventRegistrationToken m_networkChangedEventToken;
				ConnectionState m_connectionState;
				ConnectionNeworkType m_currentNetworkType;
				boolean m_isIpv6Enabled;
				std::vector<ComPtr<Connection>> m_activeConnections;
				UINT32 m_currentDatacenterId;
				std::map<UINT32, ComPtr<Datacenter>> m_datacenters;
				std::queue<ComPtr<Request>> m_requestsQueue;
				INT32 m_timeDelta;
				INT64 m_lastOutgoingMessageId;

				EventSource<__FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable> m_currentNetworkTypeChangedEventSource;
				EventSource<__FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable> m_connectionStateChangedEventSource;
				EventSource<__FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CTL__CTLUnparsedMessage> m_unparsedMessageReceivedEventSource;
			};


			class ConnectionManagerStatics WrlSealed : public ActivationFactory<IConnectionManagerStatics>
			{
				friend class ConnectionManager;

				InspectableClassStatic(RuntimeClass_Telegram_Api_Native_ConnectionManager, BaseTrust);

			public:
				IFACEMETHODIMP get_Instance(_Out_ IConnectionManager** value);
				IFACEMETHODIMP get_Version(_Out_ Version* value);

			private:
				static ComPtr<ConnectionManager> s_instance;
			};

		}
	}
}