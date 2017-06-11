#include "pch.h"
#include <iphlpapi.h>
#include "ConnectionManager.h"
#include "Datacenter.h"
#include "Connection.h"
#include "MessageResponse.h"
#include "MessageRequest.h"
#include "MessageError.h"
#include "TLTypes.h"
#include "TLMethods.h"
#include "DefaultUserConfiguration.h"
#include "Collections.h"
#include "TLBinaryReader.h"
#include "TLBinaryWriter.h"
#include "NativeBuffer.h"
#include "Helpers\COMHelper.h"

#include "MethodDebugInfo.h"

using namespace ABI::Windows::Networking::Connectivity;
using namespace Telegram::Api::Native;
using namespace Telegram::Api::Native::TL;
using Windows::Foundation::Collections::VectorView;

ActivatableStaticOnlyFactory(ConnectionManagerStatics);


ConnectionManager::ConnectionManager() :
	m_connectionState(ConnectionState::Connecting),
	m_currentNetworkType(ConnectionNeworkType::None),
	m_isIpv6Enabled(false),
	m_currentDatacenterId(0),
	m_movingToDatacenterId(0),
	m_timeDifference(0),
	m_lastRequestToken(0),
	m_lastOutgoingMessageId(0),
	m_userId(0),
	m_lastProcessedRequestTime(0)
{
}

ConnectionManager::~ConnectionManager()
{
	m_networkInformation->remove_NetworkStatusChanged(m_networkChangedEventToken);

	for (auto& datacenter : m_datacenters)
	{
		datacenter.second->Close();
	}

	m_datacenters.clear();

	WSACleanup();
}

HRESULT ConnectionManager::RuntimeClassInitialize(UINT32 minimumThreadCount, UINT32 maximumThreadCount)
{
	HRESULT result;
	ReturnIfFailed(result, MakeAndInitialize<DefaultUserConfiguration>(&m_userConfiguration));

	m_requestEnqueuedEvent.Attach(CreateEvent(nullptr, FALSE, FALSE, nullptr));
	if (!m_requestEnqueuedEvent.IsValid())
	{
		return GetLastHRESULT();
	}

	WSADATA wsaData;
	if (WSAStartup(MAKEWORD(2, 2), &wsaData) != NO_ERROR)
	{
		return WSAGetLastHRESULT();
	}

	ReturnIfFailed(result, ThreadpoolManager::RuntimeClassInitialize(minimumThreadCount, maximumThreadCount));

	auto requestEnqueuedWait = CreateThreadpoolWait(ConnectionManager::RequestEnqueuedCallback, this, ThreadpoolManager::GetEnvironment());
	if (requestEnqueuedWait == nullptr)
	{
		return GetLastHRESULT();
	}

	SetThreadpoolWait(requestEnqueuedWait, m_requestEnqueuedEvent.Get(), nullptr);

	ReturnIfFailed(result, Windows::Foundation::GetActivationFactory(HStringReference(RuntimeClass_Windows_Networking_Connectivity_NetworkInformation).Get(), &m_networkInformation));
	ReturnIfFailed(result, m_networkInformation->add_NetworkStatusChanged(Callback<INetworkStatusChangedEventHandler>(this, &ConnectionManager::OnNetworkStatusChanged).Get(), &m_networkChangedEventToken));
	ReturnIfFailed(result, UpdateNetworkStatus(false));

	return InitializeDatacenters();
}

HRESULT ConnectionManager::add_SessionCreated(__FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable* handler, EventRegistrationToken* token)
{
	return m_sessionCreatedEventSource.Add(handler, token);
}

HRESULT ConnectionManager::remove_SessionCreated(EventRegistrationToken token)
{
	return m_sessionCreatedEventSource.Remove(token);
}

HRESULT ConnectionManager::add_CurrentNetworkTypeChanged(__FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable* handler, EventRegistrationToken* token)
{
	return m_currentNetworkTypeChangedEventSource.Add(handler, token);
}

HRESULT ConnectionManager::remove_CurrentNetworkTypeChanged(EventRegistrationToken token)
{
	return m_currentNetworkTypeChangedEventSource.Remove(token);
}

HRESULT ConnectionManager::add_ConnectionStateChanged(__FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable* handler, EventRegistrationToken* token)
{
	return m_connectionStateChangedEventSource.Add(handler, token);
}

HRESULT ConnectionManager::remove_ConnectionStateChanged(EventRegistrationToken token)
{
	return m_connectionStateChangedEventSource.Remove(token);
}

HRESULT ConnectionManager::add_UnprocessedMessageReceived(__FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CMessageResponse* handler, EventRegistrationToken* token)
{
	return m_unprocessedMessageReceivedEventSource.Add(handler, token);
}

HRESULT ConnectionManager::remove_UnprocessedMessageReceived(EventRegistrationToken token)
{
	return m_unprocessedMessageReceivedEventSource.Remove(token);
}

HRESULT ConnectionManager::get_ConnectionState(ConnectionState* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto lock = LockCriticalSection();

	*value = m_connectionState;
	return S_OK;
}

HRESULT ConnectionManager::get_CurrentNetworkType(ConnectionNeworkType* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto lock = LockCriticalSection();

	*value = m_currentNetworkType;
	return S_OK;
}

HRESULT ConnectionManager::get_CurrentDatacenter(IDatacenter** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto lock = LockCriticalSection();

	ComPtr<Datacenter> datacenter;
	if (GetDatacenterById(m_currentDatacenterId, datacenter))
	{
		*value = datacenter.Detach();
	}

	return S_OK;
}

HRESULT ConnectionManager::get_IsIpv6Enabled(boolean* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto lock = LockCriticalSection();

	*value = m_isIpv6Enabled;
	return S_OK;
}

HRESULT ConnectionManager::get_IsNetworkAvailable(boolean* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto lock = LockCriticalSection();

	*value = m_currentNetworkType != ConnectionNeworkType::None;
	return S_OK;
}

HRESULT ConnectionManager::get_UserConfiguration(IUserConfiguration** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto lock = LockCriticalSection();
	return m_userConfiguration.CopyTo(value);
}

HRESULT ConnectionManager::put_UserConfiguration(IUserConfiguration* value)
{
	auto lock = LockCriticalSection();

	if (value != m_userConfiguration.Get())
	{
		if (value == nullptr)
		{
			ComPtr<IDefaultUserConfiguration> defaultUserConfiguration;
			if (FAILED(m_userConfiguration.As(&defaultUserConfiguration)))
			{
				return MakeAndInitialize<DefaultUserConfiguration>(&m_userConfiguration);
			}
		}
		else
		{
			m_userConfiguration = value;

			I_WANT_TO_DIE_IS_THE_NEW_TODO("Handle UserConfiguration changes");
		}
	}

	return S_OK;
}

HRESULT ConnectionManager::get_UserId(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto lock = LockCriticalSection();

	*value = m_userId;
	return S_OK;
}

HRESULT ConnectionManager::put_UserId(INT32 value)
{
	auto lock = LockCriticalSection();

	if (value != m_userId)
	{
		m_userId = value;

		if (m_userId != 0)
		{
			I_WANT_TO_DIE_IS_THE_NEW_TODO("Handle UserId changes");
		}
	}

	return S_OK;
}

HRESULT ConnectionManager::get_Datacenters(_Out_ __FIVectorView_1_Telegram__CApi__CNative__CDatacenter** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto lock = LockCriticalSection();
	auto vectorView = Make<VectorView<ABI::Telegram::Api::Native::Datacenter*>>();

	std::transform(m_datacenters.begin(), m_datacenters.end(), std::back_inserter(vectorView->GetItems()), [](auto& pair)
	{
		return static_cast<IDatacenter*>(pair.second.Get());
	});

	*value = vectorView.Detach();
	return S_OK;
}

HRESULT ConnectionManager::SendRequest(ITLObject* object, ISendRequestCompletedCallback* onCompleted, IRequestQuickAckReceivedCallback* onQuickAckReceived, ConnectionType connectionType, INT32* value)
{
	return SendRequestWithFlags(object, onCompleted, onQuickAckReceived, DEFAULT_DATACENTER_ID, connectionType, RequestFlag::None, value);
}

HRESULT ConnectionManager::SendRequestWithDatacenter(ITLObject* object, ISendRequestCompletedCallback* onCompleted, IRequestQuickAckReceivedCallback* onQuickAckReceived,
	INT32 datacenterId, ConnectionType connectionType, INT32* value)
{
	return SendRequestWithFlags(object, onCompleted, onQuickAckReceived, datacenterId, connectionType, RequestFlag::None, value);
}

HRESULT ConnectionManager::SendRequestWithFlags(ITLObject* object, ISendRequestCompletedCallback* onCompleted, IRequestQuickAckReceivedCallback* onQuickAckReceived,
	INT32 datacenterId, ConnectionType connectionType, RequestFlag flags, INT32* value)
{
	if (object == nullptr || (connectionType != ConnectionType::Generic && connectionType != ConnectionType::Download && connectionType != ConnectionType::Upload))
	{
		return E_INVALIDARG;
	}

	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto lock = LockCriticalSection();

	if (m_userId == 0 && (flags & RequestFlag::WithoutLogin) != RequestFlag::WithoutLogin)
	{
		return E_INVALIDARG;
	}

	auto requestToken = m_lastRequestToken + 1;

	HRESULT result;
	ComPtr<MessageRequest> request;
	ReturnIfFailed(result, MakeAndInitialize<MessageRequest>(&request, object, requestToken, connectionType, datacenterId, onCompleted, onQuickAckReceived, flags));

	if ((flags & RequestFlag::Immediate) == RequestFlag::Immediate)
	{
		ReturnIfFailed(result, SubmitWork([this, request]() ->HRESULT
		{
			std::map<UINT32, DatacenterRequestContext> datacentersContexts;
			auto currentTime = static_cast<INT32>(GetCurrentMonotonicTime() / 1000);

			auto requestsLock = m_requestsCriticalSection.Lock();

			{
				auto lock = LockCriticalSection();

				HRESULT result;
				ReturnIfFailed(result, ProcessRequest(request.Get(), currentTime, datacentersContexts));

				if (result == S_FALSE)
				{
					m_requestsQueue.push_back(std::move(request));
					return S_OK;
				}
			}

			return ProcessRequests(datacentersContexts);
		}));
	}
	else
	{
		auto requestsLock = m_requestsCriticalSection.Lock();

		m_requestsQueue.push_back(request);
		SetEvent(m_requestEnqueuedEvent.Get());
	}

	*value = requestToken;
	m_lastRequestToken = requestToken;
	return S_OK;
}

HRESULT ConnectionManager::CancelRequest(INT32 requestToken, boolean notifyServer, boolean* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto requestsLock = m_requestsCriticalSection.Lock();
	auto requestIterator = std::find_if(m_requestsQueue.begin(), m_requestsQueue.end(), [&requestToken](auto const& request)
	{
		return request->GetToken() == requestToken;
	});

	if (requestIterator != m_requestsQueue.end())
	{
		m_requestsQueue.erase(requestIterator);

		*value = true;
		return S_OK;
	}

	auto runningRequestIterator = std::find_if(m_runningRequests.begin(), m_runningRequests.end(), [&requestToken](auto const& request)
	{
		return request.second->GetToken() == requestToken;
	});

	if (runningRequestIterator != m_runningRequests.end())
	{
		if (notifyServer)
		{
			auto rpcDropAnswer = Make<Methods::TLRpcDropAnswer>(runningRequestIterator->second->GetMessageContext()->Id);

			HRESULT result;
			INT32 requestToken;
			ReturnIfFailed(result, SendRequestWithFlags(rpcDropAnswer.Get(), nullptr, nullptr, runningRequestIterator->first, runningRequestIterator->second->GetConnectionType(),
				RequestFlag::EnableUnauthorized | RequestFlag::WithoutLogin | RequestFlag::FailOnServerError | RequestFlag::Immediate | REQUEST_FLAG_NO_LAYER, &requestToken));
		}

		m_runningRequests.erase(runningRequestIterator);

		*value = true;
		return S_OK;
	}

	*value = false;
	return S_OK;
}

HRESULT ConnectionManager::GetDatacenterById(INT32 id, IDatacenter** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto lock = LockCriticalSection();

	if (id == DEFAULT_DATACENTER_ID)
	{
		id = m_currentDatacenterId;
	}

	ComPtr<Datacenter> datacenter;
	if (!GetDatacenterById(id, datacenter))
	{
		return E_INVALIDARG;
	}

	*value = datacenter.Detach();
	return S_OK;
}

HRESULT ConnectionManager::UpdateDatacenters()
{
	auto lock = LockCriticalSection();

	return S_OK;
}

HRESULT ConnectionManager::InitializeDatacenters()
{
	HRESULT result;

#if _DEBUG

	if (m_datacenters.find(1) == m_datacenters.end())
	{
		auto datacenter = Make<Datacenter>(1);
		ReturnIfFailed(result, datacenter->AddEndpoint({ L"149.154.175.40", 443 }, ConnectionType::Generic, false));
		ReturnIfFailed(result, datacenter->AddEndpoint({ L"2001:b28:f23d:f001:0000:0000:0000:000e", 443 }, ConnectionType::Generic, true));

		m_datacenters[1] = datacenter;
	}

	if (m_datacenters.find(2) == m_datacenters.end())
	{
		auto datacenter = Make<Datacenter>(2);
		ReturnIfFailed(result, datacenter->AddEndpoint({ L"149.154.167.40", 443 }, ConnectionType::Generic, false));
		ReturnIfFailed(result, datacenter->AddEndpoint({ L"2001:67c:4e8:f002:0000:0000:0000:000e", 443 }, ConnectionType::Generic, true));

		m_datacenters[2] = datacenter;
	}

	if (m_datacenters.find(3) == m_datacenters.end())
	{
		auto datacenter = Make<Datacenter>(3);
		ReturnIfFailed(result, datacenter->AddEndpoint({ L"149.154.175.117", 443 }, ConnectionType::Generic, false));
		ReturnIfFailed(result, datacenter->AddEndpoint({ L"2001:b28:f23d:f003:0000:0000:0000:000e", 443 }, ConnectionType::Generic, true));

		m_datacenters[3] = datacenter;
	}

#else

	if (m_datacenters.find(1) == m_datacenters.end())
	{
		auto datacenter = Make<Datacenter>(1);
		ReturnIfFailed(result, datacenter->AddEndpoint({ L"149.154.175.50", 443 }, ConnectionType::Generic, false));
		ReturnIfFailed(result, datacenter->AddEndpoint({ L"2001:b28:f23d:f001:0000:0000:0000:000a", 443 }, ConnectionType::Generic, true));

		m_datacenters[1] = datacenter;
	}

	if (m_datacenters.find(2) == m_datacenters.end())
	{
		auto datacenter = Make<Datacenter>(2);
		ReturnIfFailed(result, datacenter->AddEndpoint({ L"149.154.167.51", 443 }, ConnectionType::Generic, false));
		ReturnIfFailed(result, datacenter->AddEndpoint({ L"2001:67c:4e8:f002:0000:0000:0000:000a", 443 }, ConnectionType::Generic, true));

		m_datacenters[2] = datacenter;
	}

	if (m_datacenters.find(3) == m_datacenters.end())
	{
		auto datacenter = Make<Datacenter>(3);
		ReturnIfFailed(result, datacenter->AddEndpoint({ L"149.154.175.100", 443 }, ConnectionType::Generic, false));
		ReturnIfFailed(result, datacenter->AddEndpoint({ L"2001:b28:f23d:f003:0000:0000:0000:000a", 443 }, ConnectionType::Generic, true));

		m_datacenters[3] = datacenter;
	}

	if (m_datacenters.find(4) == m_datacenters.end())
	{
		auto datacenter = Make<Datacenter>(4);
		ReturnIfFailed(result, datacenter->AddEndpoint({ L"149.154.167.91", 443 }, ConnectionType::Generic, false));
		ReturnIfFailed(result, datacenter->AddEndpoint({ L"2001:67c:4e8:f004:0000:0000:0000:000a", 443 }, ConnectionType::Generic, true));

		m_datacenters[4] = datacenter;
	}

	if (m_datacenters.find(5) == m_datacenters.end())
	{
		auto datacenter = Make<Datacenter>(5);
		ReturnIfFailed(result, datacenter->AddEndpoint({ L"149.154.171.5", 443 }, ConnectionType::Generic, false));
		ReturnIfFailed(result, datacenter->AddEndpoint({ L"2001:b28:f23f:f005:0000:0000:0000:000a", 443 }, ConnectionType::Generic, true));

		m_datacenters[5] = datacenter;
	}

#endif

	m_currentDatacenterId = 1;
	m_movingToDatacenterId = DEFAULT_DATACENTER_ID;
	return S_OK;
}

HRESULT ConnectionManager::UpdateNetworkStatus(boolean raiseEvent)
{
	HRESULT result;
	ComPtr<IConnectionProfile> connectionProfile;
	ReturnIfFailed(result, m_networkInformation->GetInternetConnectionProfile(&connectionProfile));

	ConnectionNeworkType currentNetworkType;
	if (connectionProfile == nullptr)
	{
		currentNetworkType = ConnectionNeworkType::None;
	}
	else
	{
		ComPtr<IConnectionCost> connectionCost;
		ReturnIfFailed(result, connectionProfile->GetConnectionCost(&connectionCost));

		NetworkCostType networkCostType;
		ReturnIfFailed(result, connectionCost->get_NetworkCostType(&networkCostType));

		boolean isRoaming;
		ReturnIfFailed(result, connectionCost->get_Roaming(&isRoaming));

		if (isRoaming)
		{
			currentNetworkType = ConnectionNeworkType::Roaming;
		}
		else
		{
			ComPtr<INetworkAdapter> networkAdapter;
			ReturnIfFailed(result, connectionProfile->get_NetworkAdapter(&networkAdapter));

			UINT32 interfaceIanaType;
			ReturnIfFailed(result, networkAdapter->get_IanaInterfaceType(&interfaceIanaType));

			switch (interfaceIanaType)
			{
			case IF_TYPE_ETHERNET_CSMACD:
			case IF_TYPE_IEEE80211:
				currentNetworkType = ConnectionNeworkType::WiFi;
				break;
			case IF_TYPE_WWANPP:
			case IF_TYPE_WWANPP2:
				currentNetworkType = ConnectionNeworkType::Mobile;
				break;
			default:
				currentNetworkType = ConnectionNeworkType::None;
				break;
			}
		}
	}

	if (currentNetworkType != m_currentNetworkType)
	{
		m_currentNetworkType = currentNetworkType;
		return m_currentNetworkTypeChangedEventSource.InvokeAll(this, nullptr);
	}

	return S_OK;
}

HRESULT ConnectionManager::MoveToDatacenter(INT32 datacenterId)
{
	if (datacenterId == m_movingToDatacenterId)
	{
		return S_OK;
	}

	auto datacenterIterator = m_datacenters.find(datacenterId);
	if (datacenterIterator == m_datacenters.end())
	{
		return E_INVALIDARG;
	}

	ResetRequests([this](auto datacenterId, auto const& request) -> boolean
	{
		return datacenterId == m_currentDatacenterId;
	});

	if (m_userId == 0)
	{
		m_currentDatacenterId = datacenterId;
		m_movingToDatacenterId = DEFAULT_DATACENTER_ID;

		datacenterIterator->second->RecreateSessions();

		ResetRequests([this](auto datacenterId, auto const& request) -> boolean
		{
			return datacenterId == m_currentDatacenterId;
		});

		return ProcessDatacenterRequests(datacenterIterator->second.Get(), ConnectionType::Generic | ConnectionType::Download | ConnectionType::Upload);
	}
	else
	{
		m_movingToDatacenterId = datacenterId;

		auto authExportAuthorization = Make<Methods::TLAuthExportAuthorization>(datacenterId);

		auto datacenter = datacenterIterator->second;
		datacenter->SetImportingAuthorization(true);

		HRESULT result;
		INT32 requestToken;
		if (FAILED(result = SendRequestWithFlags(authExportAuthorization.Get(),
			Callback<ISendRequestCompletedCallback>([this, datacenter](IMessageResponse* response, IMessageError* error) -> HRESULT
		{
			auto lock = LockCriticalSection();

			if (error == nullptr)
			{
				datacenter->RecreateSessions();

				ResetRequests([this](auto datacenterId, auto const& request) -> boolean
				{
					return datacenterId == m_movingToDatacenterId;
				});

				if (!datacenter->IsAuthenticated())
				{
					datacenter->ClearServerSalts();

					HRESULT result;
					ReturnIfFailed(result, datacenter->BeginHandshake(true, false));
				}

				HRESULT result;
				ComPtr<Methods::TLAuthImportAuthorization> authImportAuthorization;
				auto authExportedAuthorization = static_cast<TLAuthExportedAuthorization*>(static_cast<MessageResponse*>(response)->GetObject().Get());
				ReturnIfFailed(result, MakeAndInitialize<Methods::TLAuthImportAuthorization>(&authImportAuthorization, authExportedAuthorization->GetId(), authExportedAuthorization->GetBytes().Get()));

				INT32 requestToken;
				return SendRequestWithFlags(authImportAuthorization.Get(),
					Callback<ISendRequestCompletedCallback>([this, datacenter](IMessageResponse* response, IMessageError* error) -> HRESULT
				{
					auto lock = LockCriticalSection();

					datacenter->SetImportingAuthorization(false);

					if (error == nullptr)
					{
						m_currentDatacenterId = m_movingToDatacenterId;
						m_movingToDatacenterId = DEFAULT_DATACENTER_ID;
						return S_OK;
					}
					else
					{
						auto movingToDatacenterId = m_movingToDatacenterId;
						m_movingToDatacenterId = DEFAULT_DATACENTER_ID;

						return MoveToDatacenter(movingToDatacenterId);
					}
				}).Get(), nullptr, m_movingToDatacenterId, ConnectionType::Generic, RequestFlag::EnableUnauthorized | RequestFlag::Immediate, &requestToken);
			}
			else
			{
				datacenter->SetImportingAuthorization(false);

				auto movingToDatacenterId = m_movingToDatacenterId;
				m_movingToDatacenterId = DEFAULT_DATACENTER_ID;

				return MoveToDatacenter(movingToDatacenterId);
			}
		}).Get(), nullptr, m_currentDatacenterId, ConnectionType::Generic, RequestFlag::Immediate, &requestToken)))
		{
			datacenter->SetImportingAuthorization(false);
			return result;
		}

		return S_OK;
	}
}

HRESULT ConnectionManager::CreateTransportMessage(MessageRequest* request, INT64& lastRpcMessageId, boolean& requiresLayer, TLMessage** message)
{
	ComPtr<ITLObject> object = request->GetObject();

	if (requiresLayer)
	{
		if (request->IsLayerRequired())
		{
			HRESULT result;
			ComPtr<Methods::TLInitConnection> initConnectionObject;
			ReturnIfFailed(result, MakeAndInitialize<Methods::TLInitConnection>(&initConnectionObject, m_userConfiguration.Get(), object.Get()));
			ReturnIfFailed(result, MakeAndInitialize<Methods::TLInvokeWithLayer>(&object, initConnectionObject.Get()));

			request->SetInitConnection();
			requiresLayer = false;
		}
	}

	if (lastRpcMessageId != 0 && request->InvokeAfter())
	{
		auto rpcMessageId = request->GetMessageContext()->Id;
		if (rpcMessageId != lastRpcMessageId)
		{
			HRESULT result;
			ComPtr<ITLObject> invokeAfter;
			ReturnIfFailed(result, MakeAndInitialize<Methods::TLInvokeAfterMsg>(&invokeAfter, lastRpcMessageId, object.Get()));

			object.Swap(invokeAfter);
			lastRpcMessageId = rpcMessageId;
		}
	}

	if (request->CanCompress())
	{
		HRESULT result;
		ComPtr<ITLObject> gzipPacked;
		ReturnIfFailed(result, MakeAndInitialize<TLGZipPacked>(&gzipPacked, object.Get()));

		object.Swap(gzipPacked);
	}

	return MakeAndInitialize<TLMessage>(message, request->GetMessageContext(), object.Get());
}

HRESULT ConnectionManager::ProcessRequest(MessageRequest* request, INT32 currentTime, std::map<UINT32, DatacenterRequestContext>& datacentersContexts)
{
	auto datacenterId = request->GetDatacenterId();
	if (datacenterId == DEFAULT_DATACENTER_ID)
	{
		if (m_movingToDatacenterId != DEFAULT_DATACENTER_ID)
		{
			return S_FALSE;
		}

		datacenterId = m_currentDatacenterId;
	}

	ComPtr<Datacenter> datacenter;
	if (!GetDatacenterById(datacenterId, datacenter))
	{
		I_WANT_TO_DIE_IS_THE_NEW_TODO("We should reload datacenters list if that happens");
		return E_UNEXPECTED;
	}

	auto datacenterContextIterator = datacentersContexts.find(datacenterId);
	if (datacenterContextIterator == datacentersContexts.end())
	{
		datacenterContextIterator = datacentersContexts.insert(datacenterContextIterator, std::pair<UINT32, DatacenterRequestContext>(datacenterId, DatacenterRequestContext(datacenter.Get())));
	}

	if (!datacenter->IsAuthenticated())
	{
		datacenterContextIterator->second.Flags |= DatacenterRequestContextFlag::RequiresHandshake;
		return S_FALSE;
	}

	if (!(request->EnableUnauthorized() || datacenterId == m_currentDatacenterId || datacenter->IsAuthorized()))
	{
		datacenterContextIterator->second.Flags |= DatacenterRequestContextFlag::RequiresAuthorization;
		return S_FALSE;
	}

	if (request->GetStartTime() > currentTime)
	{
		return S_FALSE;
	}

	if (request->TryDifferentDc())
	{

	}

	HRESULT result;
	ComPtr<Connection> connection;
	switch (request->GetConnectionType())
	{
	case ConnectionType::Generic:
		ReturnIfFailed(result, datacenter->GetGenericConnection(true, connection));

		datacenterContextIterator->second.GenericRequests.push_back(request);
		break;
	case ConnectionType::Download:
		ReturnIfFailed(result, datacenter->GetDownloadConnection(0, true, connection));

		I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement Download requests processing");
		break;
	case ConnectionType::Upload:
		ReturnIfFailed(result, datacenter->GetUploadConnection(0, true, connection));

		I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement Upload requests processing");
		break;
	}

	request->SetMessageContext({ GenerateMessageId(), connection->GenerateMessageSequenceNumber(true) });
	request->SetStartTime(currentTime);
	return S_OK;
}

HRESULT ConnectionManager::ProcessRequests(std::map<UINT32, DatacenterRequestContext> const& datacentersContexts)
{
	HRESULT result;

	for (auto& datacenterContext : datacentersContexts)
	{
		if ((datacenterContext.second.Flags & DatacenterRequestContextFlag::RequiresHandshake) == DatacenterRequestContextFlag::RequiresHandshake)
		{
			ReturnIfFailed(result, datacenterContext.second.Datacenter->BeginHandshake(true, false));
		}
		else
		{
			ReturnIfFailed(result, ProcessDatacenterRequests(datacenterContext.second));

			if ((datacenterContext.second.Flags & DatacenterRequestContextFlag::RequiresAuthorization) == DatacenterRequestContextFlag::RequiresAuthorization)
			{
				ReturnIfFailed(result, datacenterContext.second.Datacenter->ImportAuthorization());
			}
		}
	}

	return S_OK;
}

HRESULT ConnectionManager::ProcessDatacenterRequests(DatacenterRequestContext const& datacenterContext)
{
	HRESULT result;
	ComPtr<Connection> genericConnection;
	ReturnIfFailed(result, datacenterContext.Datacenter->GetGenericConnection(true, genericConnection));

	INT64 lastRpcMessageId = 0;
	boolean requiresQuickAck = false;

	for (auto& request : datacenterContext.GenericRequests)
	{
		if (request->InvokeAfter())
		{
			lastRpcMessageId = request->GetMessageContext()->Id;
		}

		requiresQuickAck |= request->RequiresQuickAck();
	}


	boolean requiresLayer = !datacenterContext.Datacenter->IsConnectionInitialized();
	auto requestCount = datacenterContext.GenericRequests.size();
	std::vector<ComPtr<TLMessage>> transportMessages(requestCount);

	for (size_t i = 0; i < requestCount; i++)
	{
		ReturnIfFailed(result, CreateTransportMessage(datacenterContext.GenericRequests[i].Get(), lastRpcMessageId, requiresLayer, &transportMessages[i]));
	}

	ReturnIfFailed(result, genericConnection->AddConfirmationMessage(this, transportMessages));

	if (transportMessages.empty())
	{
		return S_OK;
	}

	MessageContext messageContext;
	ComPtr<ITLObject> messageBody;

	if (transportMessages.size() > 1)
	{
		auto msgContainer = Make<TLMsgContainer>();
		auto& messages = msgContainer->GetMessages();
		messages.insert(messages.begin(), transportMessages.begin(), transportMessages.end());

		messageContext.Id = GenerateMessageId();
		messageContext.SequenceNumber = genericConnection->GenerateMessageSequenceNumber(false);
		messageBody = msgContainer;
	}
	else
	{
		auto& transportMessage = transportMessages.front();

		CopyMemory(&messageContext, transportMessage->GetMessageContext(), sizeof(MessageContext));
		messageBody = transportMessage->GetQuery();
	}

	if (requiresQuickAck)
	{
		INT32 quickAckId;
		ReturnIfFailed(result, genericConnection->SendEncryptedMessage(&messageContext, messageBody.Get(), &quickAckId));

		auto& quickAckRequests = m_quickAckRequests[quickAckId];
		quickAckRequests.insert(quickAckRequests.begin(), datacenterContext.GenericRequests.begin(), datacenterContext.GenericRequests.end());
	}
	else
	{
		ReturnIfFailed(result, genericConnection->SendEncryptedMessage(&messageContext, messageBody.Get(), nullptr));
	}

	if (!datacenterContext.GenericRequests.empty())
	{
		auto datacenterId = datacenterContext.Datacenter->GetId();

		for (auto& request : datacenterContext.GenericRequests)
		{
			m_runningRequests.push_back(std::pair<INT32, ComPtr<MessageRequest>>(datacenterId, request));
		}
	}

	return S_OK;
}

HRESULT ConnectionManager::ProcessDatacenterRequests(Datacenter* datacenter, ConnectionType connectionType)
{
	METHOD_DEBUG_INFO();

	HRESULT result;
	std::map<UINT32, DatacenterRequestContext> datacentersContexts;
	auto currentTime = static_cast<INT32>(GetCurrentMonotonicTime() / 1000);

	auto requestsLock = m_requestsCriticalSection.Lock();

	{
		auto lock = LockCriticalSection();
		auto requestIterator = m_requestsQueue.begin();

		while (requestIterator != m_requestsQueue.end())
		{
			auto& request = *requestIterator;
			auto datacenterId = request->GetDatacenterId();
			if (datacenterId == DEFAULT_DATACENTER_ID)
			{
				datacenterId = m_currentDatacenterId;
			}

			if (datacenter->GetId() == datacenterId && request->MatchesConnection(connectionType))
			{
				ReturnIfFailed(result, ProcessRequest(request.Get(), currentTime, datacentersContexts));
				if (result == S_OK)
				{
					requestIterator = m_requestsQueue.erase(requestIterator);
					continue;
				}
			}

			requestIterator++;
		}
	}

	auto datacenterContextIterator = datacentersContexts.find(datacenter->GetId());
	if (datacenterContextIterator == datacentersContexts.end())
	{
		ComPtr<Connection> genericConnection;
		ReturnIfFailed(result, datacenter->GetGenericConnection(false, genericConnection));

		if (genericConnection != nullptr && genericConnection->HasMessagesToConfirm())
		{
			datacentersContexts.insert(datacenterContextIterator, std::pair<UINT32, DatacenterRequestContext>(datacenter->GetId(), DatacenterRequestContext(datacenter)));
		}
	}

	return ProcessRequests(datacentersContexts);
}

HRESULT ConnectionManager::CompleteMessageRequest(INT64 requestMessageId, MessageContext const* messageContext, ITLObject* messageBody, Connection* connection)
{
	METHOD_DEBUG_INFO();

	HRESULT result = S_OK;
	ComPtr<MessageRequest> request;

	{
		auto requestsLock = m_requestsCriticalSection.Lock();
		auto requestIterator = std::find_if(m_runningRequests.begin(), m_runningRequests.end(), [&requestMessageId](auto const& request)
		{
			return request.second->MatchesMessage(requestMessageId);
		});

		if (requestIterator == m_runningRequests.end())
		{
			return S_FALSE;
		}

		request = requestIterator->second;
		m_runningRequests.erase(requestIterator);
	}


	ComPtr<ITLRPCError> rpcError;
	if (SUCCEEDED(messageBody->QueryInterface(IID_PPV_ARGS(&rpcError))))
	{
		INT32 errorCode;
		ReturnIfFailed(result, rpcError->get_Code(&errorCode));

		HString errorText;
		ReturnIfFailed(result, rpcError->get_Text(errorText.GetAddressOf()));

		if ((result = HandleRequestError(connection->GetDatacenter().Get(), request.Get(), errorCode, errorText)) == S_OK)
		{
			auto& sendCompletedCallback = request->GetSendCompletedCallback();
			if (sendCompletedCallback != nullptr)
			{
				auto messageError = Make<MessageError>(errorCode, std::move(errorText));
				auto messageResponse = Make<MessageResponse>(messageContext->Id, request->GetConnectionType(), messageBody);
				return sendCompletedCallback->Invoke(messageResponse.Get(), messageError.Get());
			}

			return S_OK;
		}
		else if (result == S_FALSE)
		{
			auto requestsLock = m_requestsCriticalSection.Lock();

			m_requestsQueue.push_back(std::move(request));
			SetEvent(m_requestEnqueuedEvent.Get());

			return S_OK;
		}
	}

	if (request->IsInitConnection())
	{
		connection->GetDatacenter()->SetConnectionInitialized();
	}

	auto& sendCompletedCallback = request->GetSendCompletedCallback();
	if (sendCompletedCallback != nullptr)
	{
		auto messageResponse = Make<MessageResponse>(messageContext->Id, request->GetConnectionType(), messageBody);

		if (SUCCEEDED(result))
		{
			return sendCompletedCallback->Invoke(messageResponse.Get(), nullptr);
		}
		else
		{
			ComPtr<MessageError> messageError;
			ReturnIfFailed(result, MakeAndInitialize<MessageError>(&messageError, result));

			return sendCompletedCallback->Invoke(messageResponse.Get(), messageError.Get());
		}
	}

	return S_OK;
}

HRESULT ConnectionManager::HandleRequestError(Datacenter* datacenter, MessageRequest* request, INT32 code, HString const& text)
{
	static const std::wstring knownErrors[] = { L"NETWORK_MIGRATE_", L"PHONE_MIGRATE_", L"USER_MIGRATE_", L"MSG_WAIT_FAILED", L"SESSION_PASSWORD_NEEDED", L"FLOOD_WAIT_" };

	auto lock = LockCriticalSection();
	auto errorText = text.GetRawBuffer(nullptr);

	if (code == 303)
	{
		HRESULT result;
		for (size_t i = 0; i < 3; i++)
		{
			if (wcsstr(errorText, knownErrors[i].c_str()) != nullptr)
			{
				INT32 datacenterId = _wtoi(errorText + knownErrors[i].size());
				ReturnIfFailed(result, MoveToDatacenter(datacenterId));
				return S_FALSE;
			}
		}
	}

	if (request->FailOnServerError())
	{
		return S_OK;
	}

	INT32 waitTime = 0;

	switch (code)
	{
	case 400:
		if (wcsstr(errorText, knownErrors[3].c_str()) == nullptr)
		{
			return S_OK;
		}

		waitTime = 1;
		break;
	case 401:
		if (wcsstr(errorText, knownErrors[4].c_str()) == nullptr)
		{
			if ((datacenter->GetId() == m_currentDatacenterId || datacenter->GetId() == m_movingToDatacenterId) &&
				request->GetConnectionType() == ConnectionType::Generic && m_userId != 0)
			{
				I_WANT_TO_DIE_IS_THE_NEW_TODO("Handle user logout");
			}

			datacenter->SetUnauthorized();
		}
		else
		{
			return S_OK;
		}
		break;
	case 420:
		if (wcsstr(errorText, knownErrors[5].c_str()) == nullptr)
		{
			return S_OK;
		}
		else if ((waitTime = _wtoi(errorText + knownErrors[5].size())) <= 0)
		{
			waitTime = 2;
		}
		break;
	default:
		if (code == 500 || code < 0)
		{
			waitTime = max(request->GetRetriesCount(), 10);
		}
		else
		{
			return S_OK;
		}
		break;
	}

	request->SetStartTime(static_cast<INT32>(GetCurrentMonotonicTime() / 1000) + waitTime);
	return S_FALSE;
}

void ConnectionManager::ResetRequests(std::function<boolean(INT32, ComPtr<MessageRequest> const&)> selector)
{
	auto requestsLock = m_requestsCriticalSection.Lock();

	//auto requestsCount = m_requestsQueue.size();

	auto requestIterator = m_runningRequests.begin();
	while (requestIterator != m_runningRequests.end())
	{
		auto& request = *requestIterator;
		if (selector(request.first, request.second))
		{
			request.second->Reset();
			m_requestsQueue.push_back(std::move(request.second));

			requestIterator = m_runningRequests.erase(requestIterator);
		}
		else
		{
			requestIterator++;
		}
	}

	/*if (m_requestsQueue.size() != requestsCount)
	{
		SetEvent(m_requestEnqueuedEvent.Get());
	}*/
}

HRESULT ConnectionManager::OnUnprocessedMessageResponse(MessageContext const* messageContext, ITLObject* messageBody, Connection* connection)
{
	auto unprocessedMessage = Make<MessageResponse>(messageContext->Id, connection->GetType(), messageBody);
	return m_unprocessedMessageReceivedEventSource.InvokeAll(this, unprocessedMessage.Get());
}

HRESULT ConnectionManager::OnConfigResponse(TLConfig* response)
{
	auto lock = LockCriticalSection();

	return S_OK;
}

HRESULT ConnectionManager::OnNetworkStatusChanged(IInspectable* sender)
{
	auto lock = LockCriticalSection();
	return UpdateNetworkStatus(true);
}

HRESULT ConnectionManager::OnConnectionOpened(Connection* connection)
{
	METHOD_DEBUG_INFO();

	auto& datacenter = connection->GetDatacenter();

	if (connection->GetType() == ConnectionType::Generic)
	{
		auto lock = LockCriticalSection();

		if (datacenter->GetId() == m_currentDatacenterId && m_connectionState != ConnectionState::Connected)
		{
			m_connectionState = ConnectionState::Connected;

			HRESULT result;
			ReturnIfFailed(result, m_connectionStateChangedEventSource.InvokeAll(this, nullptr));
		}
	}

	if (datacenter->IsAuthenticated())
	{
		return ProcessDatacenterRequests(datacenter.Get(), connection->GetType());
	}

	return S_OK;
}

HRESULT ConnectionManager::OnConnectionClosed(Connection* connection)
{
	METHOD_DEBUG_INFO();

	if (connection->GetType() == ConnectionType::Generic)
	{
		auto lock = LockCriticalSection();
		auto datacenter = connection->GetDatacenter();

		if (datacenter->GetId() == m_currentDatacenterId)
		{
			if (m_currentNetworkType == ConnectionNeworkType::None)
			{
				if (m_connectionState != ConnectionState::WaitingForNetwork)
				{
					m_connectionState = ConnectionState::WaitingForNetwork;
					return m_connectionStateChangedEventSource.InvokeAll(this, nullptr);
				}
			}
			else
			{
				if (m_connectionState != ConnectionState::Connecting)
				{
					m_connectionState = ConnectionState::Connecting;
					return m_connectionStateChangedEventSource.InvokeAll(this, nullptr);
				}
			}
		}
	}

	return S_OK;
}

HRESULT ConnectionManager::OnDatacenterHandshakeComplete(Datacenter* datacenter, INT32 timeDifference)
{
	METHOD_DEBUG_INFO();

	auto lock = LockCriticalSection();

	if (datacenter->GetId() == m_currentDatacenterId || datacenter->GetId() == m_movingToDatacenterId)
	{
		m_timeDifference = timeDifference;

		datacenter->RecreateSessions();
	}

	return ProcessDatacenterRequests(datacenter, ConnectionType::Generic | ConnectionType::Download | ConnectionType::Upload);
}

HRESULT ConnectionManager::OnDatacenterImportAuthorizationComplete(Datacenter* datacenter)
{
	METHOD_DEBUG_INFO();

	return ProcessDatacenterRequests(datacenter, ConnectionType::Generic | ConnectionType::Download | ConnectionType::Upload);
}

HRESULT ConnectionManager::OnDatacenterBadServerSalt(Datacenter* datacenter, INT64 requestMessageId, INT64 responseMessageId)
{
	METHOD_DEBUG_INFO();

	//auto lock = LockCriticalSection();

	//INT64 messageTime = (responseMessageId / 4294967296.0) * 1000LL;
	//INT64 currentTime = ConnectionManager::GetCurrentRealTime();

	//m_timeDifference = static_cast<INT32>((messageTime - currentTime) / 1000); // -currentPingTime / 2);
	//m_lastOutgoingMessageId = requestMessageId > (lastOutgoingMessageId ? messageId : lastOutgoingMessageId);

	ResetRequests([datacenter](auto datacenterId, auto const& request) -> boolean
	{
		return datacenterId == datacenter->GetId();
	});

	HRESULT result;
	ReturnIfFailed(result, datacenter->RequestFutureSalts(32));

	if (datacenter->IsAuthenticated())
	{
		return ProcessDatacenterRequests(datacenter, ConnectionType::Generic | ConnectionType::Download | ConnectionType::Upload);
	}

	return S_OK;
}

HRESULT ConnectionManager::OnConnectionQuickAckReceived(Connection* connection, INT32 ackId)
{
	auto requestsLock = m_requestsCriticalSection.Lock();

	auto requestsIterator = m_quickAckRequests.find(ackId);
	if (requestsIterator == m_quickAckRequests.end())
	{
		return S_FALSE;
	}

	HRESULT result;
	for (auto& request : requestsIterator->second)
	{
		auto& quickAckReceivedCallback = request->GetQuickAckReceivedCallback();
		if (quickAckReceivedCallback != nullptr)
		{
			ReturnIfFailed(result, quickAckReceivedCallback->Invoke());
		}
	}

	m_quickAckRequests.erase(requestsIterator);
	return S_OK;
}

HRESULT ConnectionManager::OnConnectionSessionCreated(Connection* connection, INT64 firstMessageId)
{
	METHOD_DEBUG_INFO();

	auto lock = LockCriticalSection();
	auto datacenter = connection->GetDatacenter();

	ResetRequests([datacenter, connection, firstMessageId](auto datacenterId, auto const& request) -> boolean
	{
		return datacenterId == datacenter->GetId() && request->MatchesConnection(connection->GetType()) && request->GetMessageContext()->Id < firstMessageId;
	});

	if (connection->GetType() == ConnectionType::Generic && datacenter->GetId() == m_currentDatacenterId && m_userId != 0)
	{
		return m_sessionCreatedEventSource.InvokeAll(this, nullptr);
	}

	return S_OK;
}

HRESULT ConnectionManager::OnRequestEnqueued(PTP_CALLBACK_INSTANCE instance)
{
	METHOD_DEBUG_INFO();

	HRESULT result;
	std::map<UINT32, DatacenterRequestContext> datacentersContexts;
	auto currentTime = static_cast<INT32>(GetCurrentMonotonicTime() / 1000);

	auto requestsLock = m_requestsCriticalSection.Lock();

	{
		auto lock = LockCriticalSection();

		auto requestIterator = m_requestsQueue.begin();

		while (requestIterator != m_requestsQueue.end())
		{
			ReturnIfFailed(result, ProcessRequest(requestIterator->Get(), currentTime, datacentersContexts));

			if (result == S_OK)
			{
				requestIterator = m_requestsQueue.erase(requestIterator);
			}
			else
			{
				requestIterator++;
			}
		}

		for (auto& datacenter : m_datacenters)
		{
			auto datacenterContextIterator = datacentersContexts.find(datacenter.first);
			if (datacenterContextIterator == datacentersContexts.end())
			{
				ComPtr<Connection> genericConnection;
				ReturnIfFailed(result, datacenter.second->GetGenericConnection(false, genericConnection));

				if (genericConnection != nullptr && genericConnection->HasMessagesToConfirm())
				{
					datacentersContexts.insert(datacenterContextIterator, std::pair<UINT32, DatacenterRequestContext>(datacenter.first, DatacenterRequestContext(datacenter.second.Get())));
				}
			}
		}
	}

	ReturnIfFailed(result, ProcessRequests(datacentersContexts));

	m_lastProcessedRequestTime = currentTime;
	return S_OK;
}

HRESULT ConnectionManager::BoomBaby(IUserConfiguration* userConfiguration, ITLObject** object, IConnection** value)
{
	if (object == nullptr || value == nullptr)
	{
		return E_POINTER;
	}

	//*object = Make<TLRPCError>().Detach();

	HRESULT result;
	ComPtr<Datacenter> datacenter;
	GetDatacenterById(m_currentDatacenterId, datacenter);
	ReturnIfFailed(result, datacenter->RequestFutureSalts(10));

	return S_OK;
}

boolean ConnectionManager::GetDatacenterById(UINT32 id, ComPtr<Datacenter>& datacenter)
{
	auto datacenterIterator = m_datacenters.find(id);
	if (datacenterIterator == m_datacenters.end())
	{
		return false;
	}

	datacenter = datacenterIterator->second;
	return true;
}

boolean ConnectionManager::GetRequestByMessageId(INT64 messageId, ComPtr<MessageRequest>& request)
{
	auto requestIterator = std::find_if(m_runningRequests.begin(), m_runningRequests.end(), [&messageId](auto const& request)
	{
		return request.second->MatchesMessage(messageId);
	});

	if (requestIterator == m_runningRequests.end())
	{
		return false;
	}

	request = requestIterator->second;
	return true;
}

INT64 ConnectionManager::GenerateMessageId()
{
	auto lock = LockCriticalSection();
	auto messageId = static_cast<INT64>(((static_cast<double>(GetCurrentRealTime()) + static_cast<double>(m_timeDifference) * 1000) * 4294967296.0) / 1000.0);
	if (messageId <= m_lastOutgoingMessageId)
	{
		messageId = m_lastOutgoingMessageId + 1;
	}

	while ((messageId % 4) != 0)
	{
		messageId++;
	}

	m_lastOutgoingMessageId = messageId;
	return messageId;
}

boolean ConnectionManager::IsNetworkAvailable()
{
	auto lock = LockCriticalSection();
	return m_currentNetworkType != ConnectionNeworkType::None;
}

INT32 ConnectionManager::GetCurrentTime()
{
	auto lock = LockCriticalSection();
	return static_cast<INT32>(ConnectionManager::GetCurrentRealTime() / 1000) + m_timeDifference;
}

HRESULT ConnectionManager::GetInstance(ComPtr<ConnectionManager>& value)
{
	static ComPtr<ConnectionManager> instance;
	if (instance == nullptr)
	{
		HRESULT result;
		ReturnIfFailed(result, MakeAndInitialize<ConnectionManager>(&instance));
	}

	value = instance;
	return S_OK;
}

void ConnectionManager::RequestEnqueuedCallback(PTP_CALLBACK_INSTANCE instance, PVOID context, PTP_WAIT wait, TP_WAIT_RESULT waitResult)
{
	if (waitResult == WAIT_OBJECT_0)
	{
		auto connectionManager = reinterpret_cast<ConnectionManager*>(context);
		connectionManager->OnRequestEnqueued(instance);

		SetThreadpoolWait(wait, connectionManager->m_requestEnqueuedEvent.Get(), nullptr);
	}
}


HRESULT ConnectionManagerStatics::get_Instance(IConnectionManager** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	HRESULT result;
	ComPtr<ConnectionManager> connectionManager;
	ReturnIfFailed(result, ConnectionManager::GetInstance(connectionManager));

	*value = connectionManager.Detach();
	return S_OK;
}

HRESULT ConnectionManagerStatics::get_Version(Version* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	value->ProtocolVersion = TELEGRAM_API_NATIVE_PROTOVERSION;
	value->Layer = TELEGRAM_API_NATIVE_LAYER;
	value->ApiId = TELEGRAM_API_NATIVE_APIID;
	return S_OK;
}

HRESULT ConnectionManagerStatics::get_DefaultDatacenterId(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = DEFAULT_DATACENTER_ID;
	return S_OK;
}