#pragma once
#include <vector>
#include <map>
#include <queue>
#include <wrl.h>
#include <Windows.Networking.Connectivity.h>
#include "MultiThreadObject.h"
#include "Request.h"
#include "Telegram.Api.Native.h"

#define THREAD_COUNT 2
#define DEFAULT_DATACENTER_ID INT_MAX
#define TELEGRAM_API_NATIVE_PROTOVERSION 2
#define TELEGRAM_API_NATIVE_VERSION 1
#define TELEGRAM_API_NATIVE_LAYER 66
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
using ABI::Telegram::Api::Native::RequestFlag;
using ABI::Telegram::Api::Native::TL::ITLObject;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{
			namespace TL
			{

				class TLBinaryReader;
				class TLObjectWithQuery;
				class TLUnparsedObject;
	
			}


			class ConnectionManager WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, IConnectionManager>, public MultiThreadObject
			{
				friend class Datacenter;
				friend class Connection;
				friend class EventObject;
				friend class TL::TLObjectWithQuery;
				friend class TL::TLUnparsedObject;

				InspectableClass(RuntimeClass_Telegram_Api_Native_ConnectionManager, BaseTrust);

			public:
				ConnectionManager();
				~ConnectionManager();

				//COM exported methods		
				IFACEMETHODIMP add_CurrentNetworkTypeChanged(_In_ __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable* handler, _Out_ EventRegistrationToken* token);
				IFACEMETHODIMP remove_CurrentNetworkTypeChanged(EventRegistrationToken token);
				IFACEMETHODIMP add_ConnectionStateChanged(_In_ __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable* handler, _Out_ EventRegistrationToken* token);
				IFACEMETHODIMP remove_ConnectionStateChanged(EventRegistrationToken token);
				IFACEMETHODIMP add_UnprocessedMessageReceived(_In_ __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CTLUnprocessedMessage* handler, _Out_ EventRegistrationToken* token);
				IFACEMETHODIMP remove_UnprocessedMessageReceived(EventRegistrationToken token);
				IFACEMETHODIMP get_ConnectionState(_Out_ ConnectionState* value);
				IFACEMETHODIMP get_CurrentNetworkType(_Out_ ConnectionNeworkType* value);
				IFACEMETHODIMP get_IsIpv6Enabled(_Out_ boolean* value);
				IFACEMETHODIMP get_IsNetworkAvailable(_Out_ boolean* value);
				IFACEMETHODIMP get_UserConfiguration(_Out_ IUserConfiguration** value);
				IFACEMETHODIMP put_UserConfiguration(_In_ IUserConfiguration* value);
				IFACEMETHODIMP get_UserId(_Out_ INT32* value);
				IFACEMETHODIMP put_UserId(INT32 value);
				IFACEMETHODIMP get_Datacenters(_Out_ __FIVectorView_1_Telegram__CApi__CNative__CDatacenter** value);
				IFACEMETHODIMP SendRequest(_In_ ITLObject* object, _In_ ISendRequestCompletedCallback* onCompleted, _In_ IRequestQuickAckReceivedCallback* onQuickAckReceived,
					INT32 datacenterId, ConnectionType connectionType, boolean immediate, INT32 requestToken);
				IFACEMETHODIMP SendRequestWithFlags(_In_ ITLObject* object, _In_ ISendRequestCompletedCallback* onCompleted, _In_ IRequestQuickAckReceivedCallback* onQuickAckReceived,
					INT32 datacenterId, ConnectionType connectionType, boolean immediate, INT32 requestToken, RequestFlag flags);
				IFACEMETHODIMP CancelRequest(INT32 requestToken, boolean notifyServer);
				IFACEMETHODIMP GetDatacenterById(INT32 id, _Out_ IDatacenter** value);

				IFACEMETHODIMP BoomBaby(_In_ IUserConfiguration* userConfiguration, _Out_ ITLObject** object, _Out_ IConnection** value);

				//Internal methods
				STDMETHODIMP RuntimeClassInitialize(DWORD minimumThreadCount = THREAD_COUNT, DWORD maximumThreadCount = THREAD_COUNT);
				boolean IsNetworkAvailable();
				INT32 GetCurrentTime();
				INT64 GenerateMessageId();
				HRESULT AttachEventObject(_In_ EventObject* eventObject);

				static HRESULT GetInstance(_Out_ ComPtr<ConnectionManager>& value);

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

			private:
				HRESULT InitializeDatacenters();
				HRESULT UpdateNetworkStatus(boolean raiseEvent);
				HRESULT CreateRequest(_In_ ITLObject* object, _In_ ISendRequestCompletedCallback* onCompleted, _In_ IRequestQuickAckReceivedCallback* onQuickAckReceived,
					INT32 datacenterId, ConnectionType connectionType, INT32 requestToken, RequestFlag flags, _Out_ ComPtr<MessageRequest>& request);
				HRESULT HandleUnprocessedResponse(_In_ MessageContext const* messageContext, _In_ ITLObject* object, _In_ Connection* connection);
				HRESULT OnNetworkStatusChanged(_In_ IInspectable* sender);
				HRESULT OnConnectionOpened(_In_ Connection* connection);
				HRESULT OnConnectionQuickAckReceived(_In_ Connection* connection, INT32 ack);
				HRESULT OnConnectionClosed(_In_ Connection* connection);
				HRESULT OnRequestEnqueued(_In_ PTP_CALLBACK_INSTANCE instance);
				HRESULT OnDatacenterHandshakeComplete(_In_ Datacenter* datacenter, INT32 timeDifference);
				void OnEventObjectError(_In_ EventObject const* eventObject, HRESULT error);
				Datacenter* GetDatacenterById(UINT32 id);

				static void NTAPI RequestEnqueuedCallback(_Inout_ PTP_CALLBACK_INSTANCE instance, _Inout_opt_ PVOID context, _Inout_ PTP_WAIT wait, _In_ TP_WAIT_RESULT waitResult);

				TP_CALLBACK_ENVIRON m_threadpoolEnvironment;
				PTP_POOL m_threadpool;
				PTP_CLEANUP_GROUP m_threadpoolCleanupGroup;
				ComPtr<INetworkInformationStatics> m_networkInformation;
				EventRegistrationToken m_networkChangedEventToken;
				ConnectionState m_connectionState;
				ConnectionNeworkType m_currentNetworkType;
				boolean m_isIpv6Enabled;
				UINT32 m_currentDatacenterId;
				std::map<INT32, ComPtr<Datacenter>> m_datacenters;
				Event m_requestEnqueuedEvent;
				CriticalSection m_requestsQueueCriticalSection;
				std::queue<ComPtr<MessageRequest>> m_requestsQueue;
				INT32 m_timeDifference;
				INT64 m_lastOutgoingMessageId;
				INT32 m_userId;
				ComPtr<IUserConfiguration> m_userConfiguration;

				EventSource<__FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable> m_currentNetworkTypeChangedEventSource;
				EventSource<__FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable> m_connectionStateChangedEventSource;
				EventSource<__FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CTLUnprocessedMessage> m_unprocessedMessageReceivedEventSource;
			};


			class ConnectionManagerStatics WrlSealed : public AgileActivationFactory<IConnectionManagerStatics>
			{
				InspectableClassStatic(RuntimeClass_Telegram_Api_Native_ConnectionManager, BaseTrust);

			public:
				IFACEMETHODIMP get_Instance(_Out_ IConnectionManager** value);
				IFACEMETHODIMP get_Version(_Out_ Version* value);
			};

		}
	}
}