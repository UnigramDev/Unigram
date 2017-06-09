#include "pch.h"
#include <iphlpapi.h>
#include "ConnectionManager.h"
#include "Datacenter.h"
#include "Connection.h"
#include "TLUnprocessedMessage.h"
#include "MessageRequest.h"
#include "TLTypes.h"
#include "TLMethods.h"
#include "DefaultUserConfiguration.h"
#include "Collections.h"
#include "TLBinaryReader.h"
#include "TLBinaryWriter.h"
#include "NativeBuffer.h"
#include "Helpers\COMHelper.h"

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
	m_userId(0)
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

HRESULT ConnectionManager::RuntimeClassInitialize(DWORD minimumThreadCount, DWORD maximumThreadCount)
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

	return UpdateNetworkStatus(false);
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

HRESULT ConnectionManager::add_UnprocessedMessageReceived(__FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CTLUnprocessedMessage* handler, EventRegistrationToken* token)
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

		I_WANT_TO_DIE_IS_THE_NEW_TODO("Handle UserId changes");
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
		I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement request immediate processing");
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
	auto requestIterator = std::find_if(m_runningRequests.begin(), m_runningRequests.end(), [&requestToken](auto const& request)
	{
		return request.second->GetToken() == requestToken;
	});

	if (requestIterator == m_runningRequests.end())
	{
		*value = false;
		return S_OK;
	}

	I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement request cancellation");

	*value = true;
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

	m_currentDatacenterId = 2;
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

	ResetRequests([this](auto datacenterId, auto const& request) -> boolean
	{
		return datacenterId == m_currentDatacenterId && request->MatchesConnection(ConnectionType::Generic | ConnectionType::Download | ConnectionType::Upload);
	});

	HRESULT result;
	if (m_userId == 0)
	{

	}
	else
	{
		auto authExportAuthorization = Make<Methods::TLAuthExportAuthorization>(datacenterId);

		INT32 requestToken;
		ReturnIfFailed(result, SendRequestWithFlags(authExportAuthorization.Get(), nullptr, nullptr, m_currentDatacenterId, ConnectionType::Generic, RequestFlag::WithoutLogin, &requestToken));
	}

	m_movingToDatacenterId = datacenterId;
	return S_OK;
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
			return E_FAIL;
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
	HRESULT result;
	std::map<UINT32, DatacenterRequestContext> datacentersContexts;
	auto currentTime = static_cast<INT32>(GetCurrentMonotonicTime() / 1000);

	{
		auto lock = LockCriticalSection();
		auto requestsLock = m_requestsCriticalSection.Lock();
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
	HRESULT result = S_OK;
	ComPtr<IMessageResponseHandler> responseHandler;
	if (SUCCEEDED(messageBody->QueryInterface(IID_PPV_ARGS(&responseHandler))))
	{
		result = responseHandler->HandleResponse(messageContext, this, connection);
	}

	I_WANT_TO_DIE_IS_THE_NEW_TODO("Process errors");

	auto requestsLock = m_requestsCriticalSection.Lock();
	auto requestIterator = std::find_if(m_runningRequests.begin(), m_runningRequests.end(), [&requestMessageId](auto const& request)
	{
		return request.second->MatchesMessage(requestMessageId);
	});

	if (requestIterator == m_runningRequests.end())
	{
		return S_FALSE;
	}

	auto& request = requestIterator->second;
	if (request->GetConnectionType() == connection->GetType())
	{
		if (request->IsInitConnection())
		{
			connection->GetDatacenter()->SetConnectionInitialized();
		}

		result = request->OnSendCompleted(messageContext, messageBody);
	}

	m_runningRequests.erase(requestIterator);
	return result;
}

void ConnectionManager::ResetRequests(std::function<boolean(INT32, ComPtr<MessageRequest> const&)> selector)
{
	auto requestsLock = m_requestsCriticalSection.Lock();

	auto requestsCount = m_requestsQueue.size();
	auto requestIterator = m_runningRequests.begin();
	while (requestIterator != m_runningRequests.end())
	{
		auto& request = *requestIterator;
		if (selector(request.first, request.second))
		{
			request.second->Reset();
			m_requestsQueue.push_back(request.second);

			requestIterator = m_runningRequests.erase(requestIterator);
		}
		else
		{
			requestIterator++;
		}
	}

	if (m_requestsQueue.size() != requestsCount)
	{
		SetEvent(m_requestEnqueuedEvent.Get());
	}
}

HRESULT ConnectionManager::OnUnprocessedMessageResponse(MessageContext const* messageContext, ITLObject* messageBody, Connection* connection)
{
	auto unprocessedMessage = Make<TLUnprocessedMessage>(messageContext->Id, connection->GetType(), messageBody);
	return m_unprocessedMessageReceivedEventSource.InvokeAll(this, unprocessedMessage.Get());
}

HRESULT ConnectionManager::OnConfigResponse(TLConfig* response)
{
	auto lock = LockCriticalSection();

	return S_OK;
}

HRESULT ConnectionManager::OnExportedAuthorizationResponse(TLAuthExportedAuthorization* response)
{
	auto lock = LockCriticalSection();

	auto datacenterIterator = m_datacenters.find(m_movingToDatacenterId);
	if (datacenterIterator == m_datacenters.end())
	{
		I_WANT_TO_DIE_IS_THE_NEW_TODO("We should reload datacenters list if that happens");
		return E_INVALIDARG;
	}

	HRESULT result;
	ComPtr<Methods::TLAuthImportAuthorization> authImportAuthorization;
	ReturnIfFailed(result, MakeAndInitialize<Methods::TLAuthImportAuthorization>(&authImportAuthorization, response->GetId(), response->GetBytes()));

	INT32 requestToken;
	return SendRequestWithFlags(authImportAuthorization.Get(), nullptr, nullptr, m_movingToDatacenterId, ConnectionType::Generic, RequestFlag::WithoutLogin, &requestToken);
}

HRESULT ConnectionManager::OnNetworkStatusChanged(IInspectable* sender)
{
	auto lock = LockCriticalSection();
	return UpdateNetworkStatus(true);
}

HRESULT ConnectionManager::OnConnectionOpened(Connection* connection)
{
	auto datacenter = connection->GetDatacenter();

	if (connection->GetType() == ConnectionType::Generic)
	{
		HRESULT result;
		auto lock = LockCriticalSection();

		if (datacenter->GetId() == m_currentDatacenterId && m_connectionState != ConnectionState::Connected)
		{
			m_connectionState = ConnectionState::Connected;
			ReturnIfFailed(result, m_connectionStateChangedEventSource.InvokeAll(this, nullptr));
		}
	}

	if (datacenter->IsAuthenticated())
	{
		return ProcessDatacenterRequests(datacenter, connection->GetType());
	}

	return S_OK;
}

HRESULT ConnectionManager::OnConnectionClosed(Connection* connection)
{
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
	auto lock = LockCriticalSection();

	if (datacenter->GetId() == m_currentDatacenterId || datacenter->GetId() == m_movingToDatacenterId)
	{
		m_timeDifference = timeDifference;

		datacenter->RecreateSessions();
	}

	datacenter->SendPing();

	return ProcessDatacenterRequests(datacenter, ConnectionType::Generic | ConnectionType::Download | ConnectionType::Upload);
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
		ReturnIfFailed(result, request->OnQuickAckReceived());
	}

	m_quickAckRequests.erase(requestsIterator);
	return S_OK;
}

HRESULT ConnectionManager::OnConnectionSessionCreated(Connection* connection, INT64 firstMessageId)
{
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
	HRESULT result;
	std::map<UINT32, DatacenterRequestContext> datacentersContexts;
	auto currentTime = static_cast<INT32>(GetCurrentMonotonicTime() / 1000);

	{
		auto lock = LockCriticalSection();

		{
			auto requestsLock = m_requestsCriticalSection.Lock();
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

	return ProcessRequests(datacentersContexts);
}

HRESULT ConnectionManager::BoomBaby(IUserConfiguration* userConfiguration, ITLObject** object, IConnection** value)
{
	if (object == nullptr || value == nullptr)
	{
		return E_POINTER;
	}

	HRESULT result;
	ReturnIfFailed(result, InitializeDatacenters());

	ComPtr<TLError> errorObject;
	ReturnIfFailed(result, MakeAndInitialize<TLError>(&errorObject, E_POINTER));

	*object = errorObject.Detach();

	ComPtr<Datacenter> datacenter;
	GetDatacenterById(m_currentDatacenterId, datacenter);
	ReturnIfFailed(result, datacenter->BeginHandshake(false, false));

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
	I_WANT_TO_DIE_IS_THE_NEW_TODO("Check if CriticalSection is really required");

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