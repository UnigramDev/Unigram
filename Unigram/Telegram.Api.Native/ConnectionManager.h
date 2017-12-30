#pragma once
#include <vector>
#include <map>
#include <queue>
#include <functional>
#include <list>
#include <wrl.h>
#include <Windows.Networking.Connectivity.h>
#include "ThreadpoolObject.h"
#include "DatacenterServer.h"
#include "Telegram.Api.Native.h"

#define MIN_THREAD_COUNT UINT32_MAX
#define MAX_THREAD_COUNT UINT32_MAX
#define TELEGRAM_API_NATIVE_PROTOVERSION 2
#define TELEGRAM_API_NATIVE_VERSION 1
#define TELEGRAM_API_NATIVE_LAYER 73
#define TELEGRAM_API_NATIVE_SETTINGS_VERSION 1
#define MILLISECONDS_TO_UNIX_EPOCH 11644473600000LL

using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;
using namespace ABI::Telegram::Api::Native::Diagnostics;
using ABI::Windows::Foundation::TimeSpan;
using ABI::Windows::Networking::Connectivity::INetworkInformationStatics;
using ABI::Windows::Networking::Connectivity::INetworkAdapter;
using ABI::Telegram::Api::Native::IConnectionManager;
using ABI::Telegram::Api::Native::IConnectionManagerStatics;
using ABI::Telegram::Api::Native::IUserConfiguration;
using ABI::Telegram::Api::Native::IProxySettings;
using ABI::Telegram::Api::Native::Version;
using ABI::Telegram::Api::Native::ConnectionNetworkStatistics;
using ABI::Telegram::Api::Native::ConnectionState;
using ABI::Telegram::Api::Native::ConnectionNeworkType;
using ABI::Telegram::Api::Native::ConnectionType;
using ABI::Telegram::Api::Native::BackendType;
using ABI::Telegram::Api::Native::ISendRequestCompletedCallback;
using ABI::Telegram::Api::Native::IRequestQuickAckReceivedCallback;
using ABI::Telegram::Api::Native::IDatacenter;
using ABI::Telegram::Api::Native::IConnection;
using ABI::Telegram::Api::Native::RequestFlag;
using ABI::Telegram::Api::Native::TL::ITLObject;

namespace ABI
{
	namespace Telegram
	{
		namespace Api
		{
			namespace Native
			{
				namespace TL
				{

					struct ITLBinaryReaderEx;
					struct ITLBinaryWriterEx;

				}
			}
		}
	}
}


using ABI::Telegram::Api::Native::TL::ITLBinaryReaderEx;
using ABI::Telegram::Api::Native::TL::ITLBinaryWriterEx;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			enum class ConnectionManagerFlag
			{
				None = 0,
				ConnectionState = 0x3,
				NetworkType = 0xC,
				UseIPv6 = 0x10,
				UseTestBackend = 0x20,
				UpdatingDatacenters = 0x40,
				UpdatingCDNPublicKeys = 0x80
			};

		}
	}
}

DEFINE_ENUM_FLAG_OPERATORS(Telegram::Api::Native::ConnectionManagerFlag);


namespace Telegram
{
	namespace Api
	{
		namespace Native
		{
			namespace TL
			{

				class TLMemoryBinaryReader;
				class TLObject;
				class TLMessage;
				class TLUnparsedObject;
				class TLConfig;
				class TLPong;

			}


			struct DatacenterRequestContext;
			struct MessageContext;
			class MessageRequest;
			class UserConfiguration;

			class ConnectionManager WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, IConnectionManager>, public ThreadpoolManager
			{
				friend class Datacenter;
				friend class Connection;
				friend class TL::TLObject;
				friend class TL::TLUnparsedObject;
				friend class TL::TLPong;

				InspectableClass(RuntimeClass_Telegram_Api_Native_ConnectionManager, BaseTrust);

			public:
				ConnectionManager();
				~ConnectionManager();

				//COM exported methods
				IFACEMETHODIMP add_SessionCreated(_In_ __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable* handler, _Out_ EventRegistrationToken* token);
				IFACEMETHODIMP remove_SessionCreated(EventRegistrationToken token);
				IFACEMETHODIMP add_AuthenticationRequired(_In_ __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable* handler, _Out_ EventRegistrationToken* token);
				IFACEMETHODIMP remove_AuthenticationRequired(EventRegistrationToken token);
				IFACEMETHODIMP add_UserConfigurationRequired(_In_ __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CUserConfiguration* handler, _Out_ EventRegistrationToken* token);
				IFACEMETHODIMP remove_UserConfigurationRequired(EventRegistrationToken token);
				IFACEMETHODIMP add_CurrentNetworkTypeChanged(_In_ __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable* handler, _Out_ EventRegistrationToken* token);
				IFACEMETHODIMP remove_CurrentNetworkTypeChanged(EventRegistrationToken token);
				IFACEMETHODIMP add_ConnectionStateChanged(_In_ __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable* handler, _Out_ EventRegistrationToken* token);
				IFACEMETHODIMP remove_ConnectionStateChanged(EventRegistrationToken token);
				IFACEMETHODIMP add_UnprocessedMessageReceived(_In_ __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CMessageResponse* handler, _Out_ EventRegistrationToken* token);
				IFACEMETHODIMP remove_UnprocessedMessageReceived(EventRegistrationToken token);
				IFACEMETHODIMP get_CurrentDatacenter(_Out_ IDatacenter** value);
				IFACEMETHODIMP get_ConnectionState(_Out_ ConnectionState* value);
				IFACEMETHODIMP get_CurrentNetworkType(_Out_ ConnectionNeworkType* value);
				IFACEMETHODIMP get_CurrentBackendType(_Out_ BackendType* value);
				IFACEMETHODIMP get_CurrentRoundTripTime(_Out_ TimeSpan* value);
				IFACEMETHODIMP get_IsIPv6Enabled(_Out_ boolean* value);
				IFACEMETHODIMP get_IsNetworkAvailable(_Out_ boolean* value);
				IFACEMETHODIMP get_UserId(_Out_ INT32* value);
				IFACEMETHODIMP put_UserId(INT32 value);
				IFACEMETHODIMP get_ProxySettings(_Out_ IProxySettings** value);
				IFACEMETHODIMP put_ProxySettings(_In_ IProxySettings* value);
				IFACEMETHODIMP get_TimeDifference(_Out_ INT32* value);
				IFACEMETHODIMP get_Datacenters(_Out_ __FIVectorView_1_Telegram__CApi__CNative__CDatacenter** value);
				IFACEMETHODIMP get_Logger(_Out_ ILogger** value);
				IFACEMETHODIMP put_Logger(_In_ ILogger* value);
				IFACEMETHODIMP SendRequest(_In_ ITLObject* object, _In_ ISendRequestCompletedCallback* onCompleted, _In_ IRequestQuickAckReceivedCallback* onQuickAckReceived, ConnectionType connectionType, _Out_ INT32* value);
				IFACEMETHODIMP SendRequestWithDatacenter(_In_ ITLObject* object, _In_ ISendRequestCompletedCallback* onCompleted, _In_ IRequestQuickAckReceivedCallback* onQuickAckReceived,
					INT32 datacenterId, ConnectionType connectionType, _Out_ INT32* value);
				IFACEMETHODIMP SendRequestWithFlags(_In_ ITLObject* object, _In_ ISendRequestCompletedCallback* onCompleted, _In_ IRequestQuickAckReceivedCallback* onQuickAckReceived,
					INT32 datacenterId, ConnectionType connectionType, RequestFlag flags, _Out_ INT32* value);
				IFACEMETHODIMP CancelRequest(INT32 requestToken, boolean notifyServer, _Out_ boolean* value);
				IFACEMETHODIMP UpdateDatacenters();
				IFACEMETHODIMP Reset();
				IFACEMETHODIMP SwitchBackend();
				IFACEMETHODIMP GetConnectionStatistics(ConnectionType connectionType, _Out_ ConnectionNetworkStatistics* value);

				//Internal methods
				STDMETHODIMP RuntimeClassInitialize(UINT32 account, UINT32 minimumThreadCount = MIN_THREAD_COUNT, UINT32 maximumThreadCount = MAX_THREAD_COUNT);
				INT32 GetCurrentTime();
				INT64 GenerateMessageId();

				inline const std::wstring& GetSettingsFolderPath() const
				{
					return m_settingsFolderPath;
				}

				inline bool IsNetworkAvailable()
				{
					auto lock = LockCriticalSection();
					return static_cast<ConnectionNeworkType>(m_flags & ConnectionManagerFlag::NetworkType) != ConnectionNeworkType::None;
				}

				inline static UINT64 GetCurrentSystemTime()
				{
					FILETIME time;
					GetSystemTimePreciseAsFileTime(&time);

					return ((static_cast<UINT64>(time.dwHighDateTime) << 32) | static_cast<UINT64>(time.dwLowDateTime)) / 10000ULL - MILLISECONDS_TO_UNIX_EPOCH;
				}

				inline static UINT64 GetCurrentMonotonicTime()
				{
					return GetTickCount64();
				}

			private:
				struct ProcessRequestsContext
				{
					INT32 CurrentTime;
					INT32 CurrentDatacenterId;
					INT32 MovingToDatacenterId;
					std::map<UINT32, DatacenterRequestContext> Datacenters;
				};

				HRESULT InitializeDefaultDatacenters();
				HRESULT InitializeSettings(UINT32 account);
				HRESULT LoadSettings();
				HRESULT SaveSettings();
				HRESULT LoadCDNPublicKeys();
				HRESULT SaveCDNPublicKeys();
				HRESULT SendPing(INT32 datacenterId);
				HRESULT AdjustCurrentTime(INT64 messageId);
				HRESULT UpdateNetworkStatus(_In_ INetworkInformationStatics* networkInformation, bool raiseEvent);
				HRESULT MoveToDatacenter(INT32 datacenterId);
				HRESULT UpdateCDNPublicKeys();
				HRESULT CreateTransportMessage(_In_ MessageRequest* request, _Inout_ INT64& lastRpcMessageId, _Inout_ bool& requiresLayer, _Out_ TL::TLMessage** message);
				HRESULT ProcessRequests();
				HRESULT ProcessRequestsForDatacenter(_In_ Datacenter* datacenter, ConnectionType connectionType);
				HRESULT ProcessRequest(_In_ MessageRequest* request, _In_ ProcessRequestsContext* context);
				HRESULT ProcessContextRequests(_In_ ProcessRequestsContext* context);
				HRESULT ProcessDatacenterRequests(_In_ DatacenterRequestContext const* datacenterContext);
				HRESULT ProcessConnectionRequest(_In_ Connection* connection, _In_ MessageRequest* request);
				void ResetContextRequests(_In_ ProcessRequestsContext const* context);
				void ResetRequests(std::function<bool(INT32, ComPtr<MessageRequest> const&)> selector, bool resetStartTime);
				HRESULT ExecuteActionForRequest(std::function<HRESULT(INT32, ComPtr<MessageRequest>)> action);
				HRESULT CompleteMessageRequest(INT64 requestMessageId, _In_ MessageContext const* messageContext, _In_ ITLObject* messageBody, _In_ Connection* connection);
				HRESULT HandleRequestError(_In_ Datacenter* datacenter, _In_ MessageRequest* request, INT32 code, _In_ HString const& message);
				HRESULT OnUnprocessedMessageResponse(_In_ MessageContext const* messageContext, _In_ ITLObject* messageBody, _In_ Connection* connection);
				HRESULT OnNetworkStatusChanged(_In_ IInspectable* sender);
				HRESULT OnApplicationResuming(_In_ IInspectable* sender, _In_ IInspectable* args);
				HRESULT OnConnectionOpening(_In_ Connection* connection);
				HRESULT OnConnectionOpened(_In_ Connection* connection);
				HRESULT OnConnectionQuickAckReceived(_In_ Connection* connection, INT32 ack);
				HRESULT OnConnectionClosed(_In_ Connection* connection, int wsaError);
				HRESULT OnDatacenterHandshakeCompleted(_In_ Datacenter* datacenter, INT32 timeDifference);
				HRESULT OnDatacenterImportAuthorizationCompleted(_In_ Datacenter* datacenter);
				HRESULT OnDatacenterBadServerSalt(_In_ Datacenter* datacenter, INT64 requestMessageId, INT64 responseMessageId);
				HRESULT OnDatacenterBadMessage(_In_ Datacenter* datacenter, INT64 requestMessageId, INT64 responseMessageId);
				HRESULT OnDatacenterPongReceived(_In_ Datacenter* datacenter, INT64 pingStartTime);
				HRESULT OnConnectionSessionCreated(_In_ Connection* connection, INT64 firstMessageId);
				bool GetDatacenterById(UINT32 id, _Out_ ComPtr<Datacenter>& datacenter);
				bool GetRequestByMessageId(INT64 messageId, _Out_ ComPtr<MessageRequest>& request);
				bool GetCDNPublicKey(INT32 datacenterId, _In_ std::vector<INT64> const& fingerprints, _Out_ ServerPublicKey const** publicKey);
				void PushResendRequest(INT64 messageId, INT64 answerMessageId);
				bool PopResendRequest(INT64 messageId, INT64* answerMessageId);

				inline bool IsCurrentDatacenter(INT32 datacenterId)
				{
					auto lock = LockCriticalSection();
					return datacenterId == m_currentDatacenterId || datacenterId == m_movingToDatacenterId;
				}

				inline bool HasCDNPublicKey(INT32 datacenterId)
				{
					auto lock = LockCriticalSection();
					return m_cdnPublicKeys.find(datacenterId) != m_cdnPublicKeys.end();
				}

				inline void GetDatacenterSettingsFileName(INT32 datacenterId, std::wstring& fileName)
				{
					fileName.resize(MAX_PATH);
					fileName.resize(swprintf_s(&fileName[0], MAX_PATH, L"%s\\DC_%d.dat", m_settingsFolderPath.data(), datacenterId));
				}

				inline void IncrementConnectionSentBytes(ConnectionType connectionType, UINT32 sentBytes)
				{
					auto lock = LockCriticalSection();
					m_connectionsStatistics[static_cast<UINT32>(connectionType) >> 1].TotalBytesSent += sentBytes;
				}

				inline void IncrementConnectionReceivedBytes(ConnectionType connectionType, UINT32 receivedBytes)
				{
					auto lock = LockCriticalSection();
					m_connectionsStatistics[static_cast<UINT32>(connectionType) >> 1].TotalBytesReceived += receivedBytes;
				}

				static HRESULT IsIPv6Enabled(_In_ INetworkInformationStatics* networkInformation, _In_ INetworkAdapter* networkAdapter, _Out_ bool* enabled);

				EventRegistrationToken m_eventTokens[2];
				ConnectionManagerFlag m_flags;
				INT32 m_currentDatacenterId;
				INT32 m_movingToDatacenterId;
				INT32 m_datacentersExpirationTime;
				INT64 m_currentRoundTripTime;
				std::map<INT32, ComPtr<Datacenter>> m_datacenters;
				std::map<INT32, ServerPublicKey> m_cdnPublicKeys;
				ThreadpoolScheduledWork m_processRequestsWork;
				ThreadpoolScheduledWork m_updateDatacentersWork;
				ThreadpoolPeriodicWork m_sendPingWork;
				CriticalSection m_requestsCriticalSection;
				std::list<ComPtr<MessageRequest>> m_requestsQueue;
				std::list<std::pair<INT32, ComPtr<MessageRequest>>> m_runningRequests;
				std::map<INT64, INT64> m_resendRequests;
				std::map<INT32, std::vector<ComPtr<MessageRequest>>> m_quickAckRequests;
				UINT32 m_runningRequestCount[3];
				ConnectionNetworkStatistics m_connectionsStatistics[3];
				INT32 m_userId;
				INT32 m_lastRequestToken;
				INT64 m_lastOutgoingMessageId;
				INT32 m_timeDifference;
				ComPtr<IProxySettings> m_proxySettings;
				std::wstring m_settingsFolderPath;
				EventSource<__FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable> m_sessionCreatedEventSource;
				EventSource<__FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable> m_authenticationRequiredEventSource;
				EventSource<__FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CUserConfiguration> m_userConfigurationRequiredEventSource;
				EventSource<__FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable> m_currentNetworkTypeChangedEventSource;
				EventSource<__FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable> m_connectionStateChangedEventSource;
				EventSource<__FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CMessageResponse> m_unprocessedMessageReceivedEventSource;
			};


			class ConnectionManagerStatics WrlSealed : public AgileActivationFactory<IConnectionManagerStatics>
			{
				InspectableClassStatic(RuntimeClass_Telegram_Api_Native_ConnectionManager, BaseTrust);

			public:
				//COM exported methods
				IFACEMETHODIMP get_Instance(_Out_ IConnectionManager** value);
				IFACEMETHODIMP get_Version(_Out_ Version* value);
				IFACEMETHODIMP get_DefaultDatacenterId(_Out_ INT32* value);

				//Internal methods
				STDMETHODIMP RuntimeClassInitialize();

			private:
				static ComPtr<IConnectionManager> s_instance;
			};

		}
	}
}