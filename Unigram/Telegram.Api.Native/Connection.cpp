#include "pch.h"
#include <Ws2tcpip.h>
#include "Connection.h"
#include "Datacenter.h"
#include "NativeBuffer.h"
#include "TLBinaryReader.h"
#include "TLBinaryWriter.h"
#include "MessageRequest.h"
#include "TLObject.h"
#include "TLTypes.h"
#include "ConnectionManager.h"
#include "Helpers\StringHelper.h"

#if FALSE && _DEBUG
#define NEXT_ENDPOINT_CONNECTION_TIMEOUT INFINITE
#define ACTIVE_CONNECTION_TIMEOUT INFINITE
#define GENERIC_CONNECTION_TIMEOUT INFINITE
#define UPLOAD_CONNECTION_TIMEOUT INFINITE
#else
#define NEXT_ENDPOINT_CONNECTION_TIMEOUT -8000
#define ACTIVE_CONNECTION_TIMEOUT -25000
#define GENERIC_CONNECTION_TIMEOUT -15000
#define UPLOAD_CONNECTION_TIMEOUT -25000
#endif

#define FLAGS_GET_CONNECTIONSTATE(flags) static_cast<ConnectionState>((flags) & ConnectionFlag::ConnectionState)
#define FLAGS_SET_CONNECTIONSTATE(flags, connectionState) ((flags) & ~ConnectionFlag::ConnectionState) | static_cast<ConnectionFlag>(connectionState)
#define FLAGS_GET_PROXYHANDSHAKESTATE(flags) static_cast<ProxyHandshakeState>((flags) & ConnectionFlag::ProxyHandshakeState)
#define FLAGS_SET_PROXYHANDSHAKESTATE(flags, proxyHandshakeState) ((flags) & ~ConnectionFlag::ProxyHandshakeState) | static_cast<ConnectionFlag>(proxyHandshakeState)
#define FLAGS_GET_CURRENTNETWORKTYPE(flags) static_cast<ConnectionNeworkType>(static_cast<int>((flags) & ConnectionFlag::CurrentNeworkType) >> 8)
#define FLAGS_SET_CURRENTNETWORKTYPE(flags, networkType) ((flags) & ~ConnectionFlag::CurrentNeworkType) | static_cast<ConnectionFlag>(static_cast<int>(networkType) << 8)

#define CONNECTION_MAX_ATTEMPTS 5
#define CONNECTION_MAX_PACKET_LENGTH 2 * 1024 * 1024

using ABI::Telegram::Api::Native::IProxyCredentials;
using namespace Telegram::Api::Native;
using namespace Telegram::Api::Native::TL;


Connection::Connection() :
	m_type(ConnectionType::Generic),
	m_flags(ConnectionFlag::None),
	m_failedConnectionCount(0)
{
}

Connection::~Connection()
{
}

HRESULT Connection::RuntimeClassInitialize(Datacenter* datacenter, ConnectionType type)
{
	/*if (datacenter == nullptr)
	{
		return E_INVALIDARG;
	}*/

	m_type = type;
	m_datacenter = datacenter;
	return S_OK;
}

HRESULT Connection::get_Datacenter(IDatacenter** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto lock = LockCriticalSection();

	*value = m_datacenter.Get();
	(*value)->AddRef();
	return S_OK;
}

HRESULT Connection::get_Type(ConnectionType* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_type;
	return S_OK;
}

HRESULT Connection::get_CurrentNetworkType(ConnectionNeworkType* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto lock = LockCriticalSection();

	*value = FLAGS_GET_CURRENTNETWORKTYPE(m_flags);
	return S_OK;
}

HRESULT Connection::get_SessionId(INT64* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = ConnectionSession::GetSessionId();
	return S_OK;
}

HRESULT Connection::get_IsConnected(boolean* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = FLAGS_GET_CONNECTIONSTATE(m_flags) >= ConnectionState::Connected;
	return S_OK;
}

HRESULT Connection::Close()
{
	auto lock = LockCriticalSection();

	if ((m_flags & ConnectionFlag::Closed) == ConnectionFlag::Closed)
	{
		return RO_E_CLOSED;
	}

	ConnectionSocket::Close();
	m_flags = ConnectionFlag::Closed;
	m_datacenter.Reset();

	return S_OK;
}

HRESULT Connection::EnsureConnected()
{
	auto lock = LockCriticalSection();

	switch (FLAGS_GET_CONNECTIONSTATE(m_flags))
	{
	case ConnectionState::Connecting:
	case ConnectionState::Reconnecting:
		return S_FALSE;
	case ConnectionState::Connected:
	case ConnectionState::DataReceived:
		return S_OK;
	default:
		boolean ipv6;
		m_datacenter->GetConnectionManager()->get_IsIPv6Enabled(&ipv6);

		HRESULT result;
		ReturnIfFailed(result, Connect(ipv6));

		return S_FALSE;
	}
}

HRESULT Connection::Connect(bool ipv6)
{
	auto lock = LockCriticalSection();

	if (FLAGS_GET_CONNECTIONSTATE(m_flags) > ConnectionState::Disconnected)
	{
		return S_FALSE;
	}

	HRESULT result;
	ComPtr<Connection> connection = this;

	auto& connectionManager = m_datacenter->GetConnectionManager();
	if (!connectionManager->IsNetworkAvailable())
	{
		ReturnIfFailed(result, connectionManager->SubmitWork([connection, connectionManager]()-> HRESULT
		{
			return connectionManager->OnConnectionClosed(connection.Get(), ERROR_NO_NETWORK);
		}));

		return HRESULT_FROM_WIN32(ERROR_NO_NETWORK);
	}

	ServerEndpoint* endpoint;
	if (FAILED(result = m_datacenter->GetCurrentEndpoint(m_type, ipv6, &endpoint)))
	{
		if (ipv6)
		{
			ipv6 = false;
			ReturnIfFailed(result, m_datacenter->GetCurrentEndpoint(m_type, false, &endpoint));
		}
		else
		{
			return result;
		}
	}

	if ((m_flags & ConnectionFlag::TryingNextEndpoint) == ConnectionFlag::TryingNextEndpoint)
	{
		ConnectionSocket::SetTimeout(NEXT_ENDPOINT_CONNECTION_TIMEOUT);
	}
	else
	{
		if (m_type == ConnectionType::Upload)
		{
			ConnectionSocket::SetTimeout(UPLOAD_CONNECTION_TIMEOUT);
		}
		else {
			ConnectionSocket::SetTimeout(GENERIC_CONNECTION_TIMEOUT);
		}
	}

	ComPtr<IProxySettings> proxySettings;
	connectionManager->get_ProxySettings(&proxySettings);

	if (proxySettings == nullptr)
	{
		ReturnIfFailed(result, ConnectionSocket::ConnectSocket(connectionManager.Get(), endpoint, ipv6));
	}
	else
	{
		ServerEndpoint proxyEndpoint;
		ReturnIfFailed(result, GetProxyEndpoint(proxySettings.Get(), &proxyEndpoint));
		ReturnIfFailed(result, ConnectionSocket::ConnectSocket(connectionManager.Get(), &proxyEndpoint, ipv6));

		m_flags = FLAGS_SET_PROXYHANDSHAKESTATE(m_flags, ProxyHandshakeState::Initializing);
	}

	ConnectionNeworkType currentNetworkType;
	connectionManager->get_CurrentNetworkType(&currentNetworkType);

	if (ipv6)
	{
		m_flags = m_flags | ConnectionFlag::IPv6;
	}

	m_flags = FLAGS_SET_CURRENTNETWORKTYPE(m_flags, currentNetworkType) | static_cast<ConnectionFlag>(ConnectionState::Connecting);

	return connectionManager->SubmitWork([connection, connectionManager]()-> HRESULT
	{
		return connectionManager->OnConnectionOpening(connection.Get());
	});
}

HRESULT Connection::Reconnect()
{
	auto lock = LockCriticalSection();

	m_flags = FLAGS_SET_CONNECTIONSTATE(m_flags, ConnectionState::Reconnecting);

	return DisconnectSocket(true);
}

HRESULT Connection::CreateMessagePacket(UINT32 messageLength, bool reportAck, ComPtr<TLMemoryBinaryWriter>& writer, BYTE** messageBuffer)
{
	UINT32 packetBufferLength = messageLength;
	UINT32 packetLength = messageLength / 4;

	if (packetLength < 0x7f)
	{
		packetBufferLength += 1;
	}
	else
	{
		packetBufferLength += 4;
	}

	HRESULT result;
	BYTE* packetBufferBytes;
	ComPtr<TLMemoryBinaryWriter> packetWriter;
	if ((m_flags & ConnectionFlag::CryptographyInitialized) == ConnectionFlag::CryptographyInitialized)
	{
		ReturnIfFailed(result, MakeAndInitialize<TLMemoryBinaryWriter>(&packetWriter, packetBufferLength));

		packetBufferBytes = packetWriter->GetBuffer();
	}
	else
	{
		packetBufferLength += 64;

		ReturnIfFailed(result, MakeAndInitialize<TLMemoryBinaryWriter>(&packetWriter, packetBufferLength));

		packetBufferBytes = packetWriter->GetBuffer();

		ReturnIfFailed(result, ConnectionCryptography::Initialize(packetBufferBytes));

		packetBufferBytes += 64;

		ReturnIfFailed(result, packetWriter->put_Position(64));

		m_flags = m_flags | ConnectionFlag::CryptographyInitialized;
	}

	if (packetLength < 0x7f)
	{
		if (reportAck)
		{
			packetLength |= (1 << 7);
		}

		ReturnIfFailed(result, packetWriter->WriteByte(packetLength & 0xff));

		ConnectionCryptography::EncryptBuffer(packetBufferBytes, packetBufferBytes, 1);

		packetBufferBytes += 1;
	}
	else
	{
		packetLength = (packetLength << 8) + 0x7f;

		if (reportAck)
		{
			packetLength |= (1 << 7);
		}

		ReturnIfFailed(result, packetWriter->WriteUInt32(packetLength));

		ConnectionCryptography::EncryptBuffer(packetBufferBytes, packetBufferBytes, 4);

		packetBufferBytes += 4;
	}

	writer.Swap(packetWriter);
	*messageBuffer = packetBufferBytes;
	return S_OK;
}

HRESULT Connection::SendEncryptedMessage(MessageContext const* messageContext, ITLObject* messageBody, INT32* quickAckId)
{
	/*if (messageContext == nullptr || messageBody == nullptr)
	{
		return E_INVALIDARG;
	}*/

	auto lock = LockCriticalSection();

	if (FLAGS_GET_CONNECTIONSTATE(m_flags) < ConnectionState::Connected)
	{
		return S_FALSE;
	}

	HRESULT result;
	UINT32 messageBodySize;
	ReturnIfFailed(result, TLObjectSizeCalculator::GetSize(messageBody, &messageBodySize));

	UINT32 messageLength = 3 * sizeof(INT64) + 2 * sizeof(UINT32) + messageBodySize;
	UINT32 padding = messageLength % 16;

	if (padding != 0)
	{
		padding = 16 - padding;
	}

#if TELEGRAM_API_NATIVE_PROTOVERSION == 2

	if (padding < 12)
	{
		padding += 16;
	}

#endif

	UINT32 encryptedMessageLength = 24 + messageLength + padding;

	BYTE* packetBufferBytes;
	ComPtr<TLMemoryBinaryWriter> packetWriter;
	ReturnIfFailed(result, CreateMessagePacket(encryptedMessageLength, quickAckId != nullptr, packetWriter, &packetBufferBytes));
	ReturnIfFailed(result, packetWriter->SeekCurrent(24));
	ReturnIfFailed(result, packetWriter->WriteInt64(m_datacenter->GetServerSalt()));
	ReturnIfFailed(result, packetWriter->WriteInt64(GetSessionId()));
	ReturnIfFailed(result, packetWriter->WriteInt64(messageContext->Id));
	ReturnIfFailed(result, packetWriter->WriteUInt32(messageContext->SequenceNumber));
	ReturnIfFailed(result, packetWriter->WriteUInt32(messageBodySize));
	ReturnIfFailed(result, packetWriter->WriteObject(messageBody));

	if (padding != 0)
	{
		RAND_bytes(packetBufferBytes + 24 + messageLength, padding);
	}

	ReturnIfFailed(result, m_datacenter->EncryptMessage(packetBufferBytes, encryptedMessageLength, padding, quickAckId));

	ConnectionCryptography::EncryptBuffer(packetBufferBytes, packetBufferBytes, encryptedMessageLength);

	if (SUCCEEDED(result = ConnectionSocket::SendData(packetWriter->GetBuffer(), packetWriter->GetCapacity())))
	{
		auto& connectionManager = m_datacenter->GetConnectionManager();
		connectionManager->IncrementConnectionSentBytes(m_type, packetWriter->GetCapacity());

		LOG_TRACE(connectionManager.Get(), LogLevel::Information, L"Sending %d bytes on connection with type=%d in datacenter=%d\n", packetWriter->GetCapacity(), m_type, m_datacenter->GetId());
	}

	return result;
}

HRESULT Connection::SendEncryptedMessageWithConfirmation(MessageContext const* messageContext, ITLObject* messageBody, INT32* quickAckId)
{
	auto lock = LockCriticalSection();

	if (FLAGS_GET_CONNECTIONSTATE(m_flags) < ConnectionState::Connected)
	{
		return S_FALSE;
	}

	if (ConnectionSession::HasMessagesToConfirm())
	{
		auto& connectionManager = m_datacenter->GetConnectionManager();

		HRESULT result;
		ComPtr<TLMessage> transportMessages[2];
		ReturnIfFailed(result, MakeAndInitialize<TLMessage>(&transportMessages[0], messageContext, messageBody));
		ReturnIfFailed(result, ConnectionSession::CreateConfirmationMessage(connectionManager.Get(), &transportMessages[1]));

		auto msgContainer = Make<TLMsgContainer>();
		auto& messages = msgContainer->GetMessages();
		messages.insert(messages.begin(), std::begin(transportMessages), std::end(transportMessages));

		MessageContext containerMessageContext = { connectionManager->GenerateMessageId(), GenerateMessageSequenceNumber(false) };
		return SendEncryptedMessage(&containerMessageContext, msgContainer.Get(), nullptr);
	}
	else
	{
		return SendEncryptedMessage(messageContext, messageBody, nullptr);
	}
}

HRESULT Connection::SendUnencryptedMessage(ITLObject* messageBody, bool reportAck)
{
	/*if (messageBody == nullptr)
	{
		return E_INVALIDARG;
	}*/

	auto lock = LockCriticalSection();

	if (FLAGS_GET_CONNECTIONSTATE(m_flags) < ConnectionState::Connected)
	{
		return S_FALSE;
	}

	HRESULT result;
	UINT32 messageBodySize;
	ReturnIfFailed(result, TLObjectSizeCalculator::GetSize(messageBody, &messageBodySize));

	UINT32 messageLength = 2 * sizeof(INT64) + sizeof(INT32) + messageBodySize;

	auto& connectionManager = m_datacenter->GetConnectionManager();

	BYTE* packetBufferBytes;
	ComPtr<TLMemoryBinaryWriter> packetWriter;
	ReturnIfFailed(result, CreateMessagePacket(messageLength, reportAck, packetWriter, &packetBufferBytes));
	ReturnIfFailed(result, packetWriter->WriteInt64(0));
	ReturnIfFailed(result, packetWriter->WriteInt64(connectionManager->GenerateMessageId()));
	ReturnIfFailed(result, packetWriter->WriteUInt32(messageBodySize));
	ReturnIfFailed(result, packetWriter->WriteObject(messageBody));

	ConnectionCryptography::EncryptBuffer(packetBufferBytes, packetBufferBytes, messageLength);

	if (SUCCEEDED(result = ConnectionSocket::SendData(packetWriter->GetBuffer(), packetWriter->GetCapacity())))
	{
		auto& connectionManager = m_datacenter->GetConnectionManager();
		connectionManager->IncrementConnectionSentBytes(m_type, packetWriter->GetCapacity());

		LOG_TRACE(connectionManager.Get(), LogLevel::Information, L"Sending %d bytes on connection with type=%d in datacenter=%d\n", packetWriter->GetCapacity(), m_type, m_datacenter->GetId());
	}

	return result;
}

HRESULT Connection::HandleMessageResponse(MessageContext const* messageContext, ITLObject* messageBody)
{
	HRESULT result;

	{
		auto lock = LockCriticalSection();

		if (messageContext->SequenceNumber % 2)
		{
			AddMessageToConfirm(messageContext->Id);
		}

		if (IsMessageIdProcessed(messageContext->Id))
		{
			if (ConnectionSession::HasMessagesToConfirm())
			{
				auto& connectionManager = m_datacenter->GetConnectionManager();

				ComPtr<TLMessage> confirmationMessageBody;
				ReturnIfFailed(result, ConnectionSession::CreateConfirmationMessage(connectionManager.Get(), &confirmationMessageBody));

				return SendEncryptedMessage(confirmationMessageBody->GetMessageContext(), confirmationMessageBody.Get(), nullptr);
			}

			return S_OK;
		}
	}

	ComPtr<IMessageResponseHandler> responseHandler;
	if (SUCCEEDED(messageBody->QueryInterface(IID_PPV_ARGS(&responseHandler))))
	{
		ReturnIfFailed(result, responseHandler->HandleResponse(messageContext, this));
	}
	else
	{
		auto& connectionManager = m_datacenter->GetConnectionManager();
		ReturnIfFailed(result, connectionManager->OnUnprocessedMessageResponse(messageContext, messageBody, this));
	}

	{
		auto lock = LockCriticalSection();

		AddProcessedMessageId(messageContext->Id);
	}

	return S_OK;
}

void Connection::ConfirmAndResetRequest(INT64 messageId)
{
	auto& connectionManager = m_datacenter->GetConnectionManager();

	{
		auto lock = LockCriticalSection();

		AddMessageToConfirm(messageId);
	}

	connectionManager->ExecuteActionForRequest([messageId](INT32 token, ComPtr<MessageRequest> request) -> HRESULT
	{
		if (request->MatchesMessage(messageId))
		{
			request->Reset(true);

			return S_FALSE;
		}
		else
		{
			return S_OK;
		}
	});
}

HRESULT Connection::OnNewSessionCreatedResponse(TLNewSessionCreated* response)
{
	auto& connectionManager = m_datacenter->GetConnectionManager();

	{
		auto lock = LockCriticalSection();

		if (IsSessionProcessed(response->GetUniqueId()))
		{
			return S_OK;
		}

		ServerSalt salt = {};
		salt.ValidSince = connectionManager->GetCurrentTime();
		salt.ValidUntil = salt.ValidSince + 30 * 60;
		salt.Salt = response->GetServerSalt();

		m_datacenter->AddServerSalt(salt);

		AddProcessedSession(response->GetUniqueId());
	}

	return connectionManager->OnConnectionSessionCreated(this, response->GetFirstMessageId());
}

HRESULT Connection::OnMsgDetailedInfoResponse(TLMsgDetailedInfo* response)
{
	bool requestResend = false;
	bool confirm = true;
	auto& connectionManager = m_datacenter->GetConnectionManager();

	connectionManager->ExecuteActionForRequest([response, &requestResend, &confirm](INT32 requestId, ComPtr<MessageRequest> request) -> HRESULT
	{
		if (requestId == response->GetMessageId())
		{
			auto currentTime = static_cast<INT32>(ConnectionManager::GetCurrentSystemTime() / 1000);
			if (std::abs(currentTime - request->GetStartTime()) >= 60)
			{
				request->SetStartTime(currentTime);
				requestResend = true;
			}
			else
			{
				confirm = false;
			}

			return S_FALSE;
		}
		else
		{
			return S_OK;
		}
	});

	if (requestResend)
	{
		MessageContext messageContext = { connectionManager->GenerateMessageId(), GenerateMessageSequenceNumber(true) };

		auto resendReq = Make<TLMsgResendReq>();
		resendReq->GetMessagesIds().push_back(response->GetAnswerMessageId());

		connectionManager->PushResendRequest(response->GetMessageId(), response->GetAnswerMessageId());

		return SendEncryptedMessage(&messageContext, resendReq.Get(), nullptr);

		//return SendEncryptedMessageWithConfirmation(&messageContext, resendReq.Get(), nullptr);
	}
	else if (confirm)
	{
		auto lock = LockCriticalSection();

		AddMessageToConfirm(response->GetAnswerMessageId());
	}

	return S_OK;
}

HRESULT Connection::OnMsgNewDetailedInfoResponse(TLMsgNewDetailedInfo* response)
{
	auto& connectionManager = m_datacenter->GetConnectionManager();
	MessageContext messageContext = { connectionManager->GenerateMessageId(), GenerateMessageSequenceNumber(true) };

	auto resendReq = Make<TLMsgResendReq>();
	resendReq->GetMessagesIds().push_back(response->GetAnswerMessageId());

	return SendEncryptedMessageWithConfirmation(&messageContext, resendReq.Get(), nullptr);
}

HRESULT Connection::OnMsgsStateInfoResponse(TLMsgsStateInfo* response)
{
	auto& connectionManager = m_datacenter->GetConnectionManager();

	INT64 answerMessageId;
	if (connectionManager->PopResendRequest(response->GetMessageId(), &answerMessageId))
	{
		ConfirmAndResetRequest(answerMessageId);
	}

	return S_OK;
}

HRESULT Connection::OnSocketConnected()
{
	if (FLAGS_GET_PROXYHANDSHAKESTATE(m_flags) != ProxyHandshakeState::None)
	{
		return OnProxyConnected();
	}

	m_flags = m_flags | static_cast<ConnectionFlag>(ConnectionState::Connected);
	m_failedConnectionCount = 0;

	HRESULT result;
	ReturnIfFailed(result, m_datacenter->OnConnectionOpened(this));

	ComPtr<Connection> connection = this;
	auto& connectionManager = m_datacenter->GetConnectionManager();
	return connectionManager->SubmitWork([connection, connectionManager]()-> HRESULT
	{
		return connectionManager->OnConnectionOpened(connection.Get());
	});
}

HRESULT Connection::OnSocketDisconnected(int wsaError)
{
	auto connectionState = FLAGS_GET_CONNECTIONSTATE(m_flags);
	m_flags = m_flags & ~(ConnectionFlag::ConnectionState | ConnectionFlag::ProxyHandshakeState | ConnectionFlag::CryptographyInitialized);
	m_partialPacketBuffer.Reset();

	HRESULT result;
	ReturnIfFailed(result, m_datacenter->OnConnectionClosed(this));

	auto& connectionManager = m_datacenter->GetConnectionManager();

	{
		ComPtr<Connection> connection = this;
		ReturnIfFailed(result, connectionManager->SubmitWork([connection, connectionManager, wsaError]()-> HRESULT
		{
			return connectionManager->OnConnectionClosed(connection.Get(), wsaError);
		}));
	}

	if (connectionState == ConnectionState::Reconnecting)
	{
		return Connect((m_flags & ConnectionFlag::IPv6) == ConnectionFlag::IPv6);
	}
	else if (m_datacenter->IsHandshaking() || connectionManager->IsCurrentDatacenter(m_datacenter->GetId()))
	{
		m_failedConnectionCount++;

		if (connectionManager->IsNetworkAvailable())
		{
			UINT32 maximumConnectionRetries = connectionState == ConnectionState::DataReceived ? CONNECTION_MAX_ATTEMPTS : 1;
			auto switchToNextEndpoint = m_failedConnectionCount > maximumConnectionRetries || (connectionState != ConnectionState::DataReceived && wsaError == ERROR_TIMEOUT);

			if (switchToNextEndpoint)
			{
				m_flags = m_flags | ConnectionFlag::TryingNextEndpoint;
				m_failedConnectionCount = 0;

				return m_datacenter->NextEndpoint(m_type, (m_flags & ConnectionFlag::IPv6) == ConnectionFlag::IPv6);
			}
		}
	}

	return S_OK;
}

HRESULT Connection::OnSocketDataReceived(BYTE* buffer, UINT32 length)
{
	{
		auto& connectionManager = m_datacenter->GetConnectionManager();
		connectionManager->IncrementConnectionReceivedBytes(m_type, length);
	}

	switch (FLAGS_GET_PROXYHANDSHAKESTATE(m_flags))
	{
	case ProxyHandshakeState::SendingGreeting:
		return OnProxyGreetingResponse(buffer, length);
	case ProxyHandshakeState::Authenticating:
		return OnProxyAuthenticationResponse(buffer, length);
	case ProxyHandshakeState::RequestingConnection:
		return OnProxyConnectionRequestResponse(buffer, length);
	default:
		return OnDataReceived(buffer, length);
	}
}

HRESULT Connection::OnMessageReceived(TLMemoryBinaryReader* messageReader, UINT32 messageLength)
{
	HRESULT result;
	if (messageLength == 4)
	{
		INT32 errorCode;
		ReturnIfFailed(result, messageReader->ReadInt32(&errorCode));

		ReturnIfFailed(result, m_datacenter->GetConnectionManager()->LogTrace(LogLevel::Error, L"Error %d received from Telegram\n", errorCode));

		return E_FAIL;
	}

	INT64 authKeyId;
	ReturnIfFailed(result, messageReader->ReadInt64(&authKeyId));

	UINT32 constructor;
	ComPtr<ITLObject> messageObject;
	MessageContext messageContext = {};

	if (authKeyId == 0)
	{
		ReturnIfFailed(result, messageReader->ReadInt64(&messageContext.Id));

		UINT32 objectSize;
		ReturnIfFailed(result, messageReader->ReadUInt32(&objectSize));
		ReturnIfFailed(result, messageReader->ReadObjectAndConstructor(objectSize, &constructor, &messageObject));
	}
	else
	{
		if ((messageLength - 24) % 16 != 0)
		{
			return E_FAIL;
		}

		ReturnIfFailed(result, m_datacenter->DecryptMessage(authKeyId, messageReader->GetBufferAtPosition() - sizeof(INT64), messageLength));
		ReturnIfFailed(result, messageReader->SeekCurrent(16));

		INT64 salt;
		ReturnIfFailed(result, messageReader->ReadInt64(&salt));

		INT64 sessionId;
		ReturnIfFailed(result, messageReader->ReadInt64(&sessionId));

		if (sessionId != GetSessionId())
		{
			return S_OK;
		}

		ReturnIfFailed(result, messageReader->ReadInt64(&messageContext.Id));
		ReturnIfFailed(result, messageReader->ReadUInt32(&messageContext.SequenceNumber));

		UINT32 objectSize;
		ReturnIfFailed(result, messageReader->ReadUInt32(&objectSize));
		ReturnIfFailed(result, messageReader->ReadObjectAndConstructor(objectSize, &constructor, &messageObject));
	}

	ComPtr<Connection> connection = this;
	auto& connectionManager = m_datacenter->GetConnectionManager();
	return connectionManager->SubmitWork([messageContext, messageObject, connection]()-> HRESULT
	{
		return connection->HandleMessageResponse(&messageContext, messageObject.Get());
	});
}

HRESULT Connection::OnProxyConnected()
{
	auto& connectionManager = m_datacenter->GetConnectionManager();

	HRESULT result;
	ComPtr<IProxySettings> proxySettings;
	connectionManager->get_ProxySettings(&proxySettings);

	if (proxySettings == nullptr)
	{
		return E_INVALID_PROTOCOL_OPERATION;
	}

	ComPtr<IProxyCredentials> proxyCredentials;
	proxySettings->get_Credentials(&proxyCredentials);

	if (proxyCredentials == nullptr)
	{
		BYTE buffer[3];
		buffer[0] = 0x05;
		buffer[1] = 0x01;
		buffer[2] = 0x00;

		ReturnIfFailed(result, ConnectionSocket::SendData(buffer, 3));
	}
	else
	{
		BYTE buffer[4];
		buffer[0] = 0x05;
		buffer[1] = 0x02;
		buffer[2] = 0x00;
		buffer[3] = 0x02;

		ReturnIfFailed(result, ConnectionSocket::SendData(buffer, 4));
	}

	m_flags = FLAGS_SET_PROXYHANDSHAKESTATE(m_flags, ProxyHandshakeState::SendingGreeting);
	return S_OK;
}

HRESULT Connection::OnProxyGreetingResponse(BYTE* buffer, UINT32 length)
{
	if (length != 2 || buffer[0] != 0x5)
	{
		return E_PROTOCOL_VERSION_NOT_SUPPORTED;
	}

	switch (buffer[1])
	{
	case 0x00:
		m_flags = FLAGS_SET_PROXYHANDSHAKESTATE(m_flags, ProxyHandshakeState::None);
		break;
	case 0x02:
	{
		auto& connectionManager = m_datacenter->GetConnectionManager();

		HRESULT result;
		ComPtr<IProxySettings> proxySettings;
		connectionManager->get_ProxySettings(&proxySettings);

		if (proxySettings == nullptr)
		{
			return E_INVALID_PROTOCOL_OPERATION;
		}

		ComPtr<IProxyCredentials> proxyCredentials;
		proxySettings->get_Credentials(&proxyCredentials);

		if (proxyCredentials == nullptr)
		{
			return E_INVALID_PROTOCOL_OPERATION;
		}

		HString userName;
		ReturnIfFailed(result, proxyCredentials->get_UserName(userName.GetAddressOf()));

		HString password;
		ReturnIfFailed(result, proxyCredentials->get_Password(password.GetAddressOf()));

		std::string mbUserName;
		WideCharToMultiByte(userName, mbUserName);

		std::string mbPassword;
		WideCharToMultiByte(password, mbPassword);

		std::vector<BYTE> buffer(3 + mbUserName.size() + mbPassword.size());
		buffer[0] = 0x1;
		buffer[1] = static_cast<BYTE>(mbUserName.size());
		CopyMemory(buffer.data() + 2, mbUserName.data(), mbUserName.size());

		buffer[2 + mbUserName.size()] = static_cast<BYTE>(mbPassword.size());
		CopyMemory(buffer.data() + 3 + mbUserName.size(), mbPassword.data(), mbPassword.size());

		ReturnIfFailed(result, ConnectionSocket::SendData(buffer.data(), static_cast<UINT32>(buffer.size())));

		m_flags = FLAGS_SET_PROXYHANDSHAKESTATE(m_flags, ProxyHandshakeState::Authenticating);
	}
	break;
	case 0xff:
		return E_FAIL;
	};

	return S_OK;
}

HRESULT Connection::OnProxyAuthenticationResponse(BYTE* buffer, UINT32 length)
{
	if (length != 2 || buffer[1] != 0x0)
	{
		return E_INVALID_PROTOCOL_FORMAT;
	}

	HRESULT result;
	ServerEndpoint* endpoint;
	bool ipv6 = (m_flags & ConnectionFlag::IPv6) == ConnectionFlag::IPv6;
	ReturnIfFailed(result, m_datacenter->GetCurrentEndpoint(m_type, ipv6, &endpoint));

	if (ipv6)
	{
		BYTE buffer[22];
		buffer[0] = 0x05;
		buffer[1] = 0x01;
		buffer[2] = 0x00;
		buffer[3] = 0x04;

		if (InetPton(AF_INET6, endpoint->Address.c_str(), buffer + 4) != 1)
		{
			return WSAGetLastHRESULT();
		}

		buffer[20] = (endpoint->Port >> 8) & 0xff;
		buffer[21] = endpoint->Port & 0xff;

		ReturnIfFailed(result, ConnectionSocket::SendData(buffer, 22));
	}
	else
	{
		BYTE buffer[10];
		buffer[0] = 0x05;
		buffer[1] = 0x01;
		buffer[2] = 0x00;
		buffer[3] = 0x01;

		if (InetPton(AF_INET, endpoint->Address.c_str(), buffer + 4) != 1)
		{
			return WSAGetLastHRESULT();
		}

		buffer[8] = (endpoint->Port >> 8) & 0xff;
		buffer[9] = endpoint->Port & 0xff;

		ReturnIfFailed(result, ConnectionSocket::SendData(buffer, 10));
	}

	m_flags = FLAGS_SET_PROXYHANDSHAKESTATE(m_flags, ProxyHandshakeState::RequestingConnection);
	return S_OK;
}

HRESULT Connection::OnProxyConnectionRequestResponse(BYTE* buffer, UINT32 length)
{
	if (length < 2 || buffer[1] != 0x0)
	{
		return E_INVALID_PROTOCOL_FORMAT;
	}

	m_flags = FLAGS_SET_PROXYHANDSHAKESTATE(m_flags, ProxyHandshakeState::None);
	return OnSocketConnected();
}

HRESULT Connection::OnDataReceived(BYTE* buffer, UINT32 length)
{
	if (FLAGS_GET_CONNECTIONSTATE(m_flags) != ConnectionState::DataReceived)
	{
		ConnectionSocket::SetTimeout(ACTIVE_CONNECTION_TIMEOUT);
		m_flags = (m_flags & ~ConnectionFlag::TryingNextEndpoint) | static_cast<ConnectionFlag>(ConnectionState::DataReceived);
	}

	HRESULT result;
	ComPtr<IBuffer> packetBuffer;
	if (m_partialPacketBuffer == nullptr)
	{
		ReturnIfFailed(result, MakeAndInitialize<NativeBufferWrapper>(&packetBuffer, buffer, length));

		ConnectionCryptography::DecryptBuffer(buffer, buffer, length);
	}
	else
	{
		auto partialPacketLength = m_partialPacketBuffer->GetCapacity();
		ReturnIfFailed(result, m_partialPacketBuffer->Merge(buffer, length));

		ConnectionCryptography::DecryptBuffer(buffer, m_partialPacketBuffer->GetBuffer() + partialPacketLength, length);

		packetBuffer = m_partialPacketBuffer;
	}

	ComPtr<TLMemoryBinaryReader> packetReader;
	ReturnIfFailed(result, MakeAndInitialize<TLMemoryBinaryReader>(&packetReader, packetBuffer.Get()));

	UINT32 packetPosition;
	auto& connectionManager = m_datacenter->GetConnectionManager();

	LOG_TRACE(connectionManager.Get(), LogLevel::Information, L"Received %d bytes on connection with type=%d in datacenter=%d\n", length, m_type, m_datacenter->GetId());

	while (packetReader->HasUnconsumedBuffer())
	{
		packetPosition = packetReader->GetPosition();

		BYTE firstByte;
		BreakIfFailed(result, packetReader->ReadByte(&firstByte));

		if ((firstByte & (1 << 7)) != 0)
		{
			packetReader->put_Position(packetPosition);

			INT32 ackId;
			BreakIfFailed(result, packetReader->ReadBigEndianInt32(&ackId));

			ComPtr<Connection> connection = this;
			BreakIfFailed(result, connectionManager->SubmitWork([connection, connectionManager, ackId]()-> HRESULT
			{
				return connectionManager->OnConnectionQuickAckReceived(connection.Get(), ackId & ~(1 << 31));
			}));
			continue;
		}

		UINT32 packetLength;
		if (firstByte == 0x7f)
		{
			packetReader->put_Position(packetPosition);

			BreakIfFailed(result, packetReader->ReadUInt32(&packetLength));

			packetLength = (packetLength >> 8) * 4;
		}
		else
		{
			packetLength = static_cast<UINT32>(firstByte) * 4;
		}

		if (packetLength > packetReader->GetUnconsumedBufferLength())
		{
			result = E_NOT_SUFFICIENT_BUFFER;
			break;
		}

		if (packetLength % 4 != 0 || packetLength > CONNECTION_MAX_PACKET_LENGTH || FAILED(OnMessageReceived(packetReader.Get(), packetLength)))
		{
			return Reconnect();
		}

		if (FAILED(packetReader->put_Position(packetPosition + (firstByte == 0x7f ? 4 : 1) + packetLength)))
		{
			result = E_NOT_SUFFICIENT_BUFFER;
			break;
		}
	}

	if (result == E_NOT_SUFFICIENT_BUFFER)
	{
		if (m_partialPacketBuffer == nullptr)
		{
			auto newBufferLength = packetReader->GetCapacity() - packetPosition;
			ReturnIfFailed(result, MakeAndInitialize<NativeBuffer>(&m_partialPacketBuffer, newBufferLength));

			CopyMemory(m_partialPacketBuffer->GetBuffer(), packetReader->GetBuffer() + packetPosition, newBufferLength);
			return S_OK;
		}
		else
		{
			if (packetPosition == 0)
			{
				return S_OK;
			}

			auto newBufferLength = m_partialPacketBuffer->GetCapacity() - packetPosition;
			MoveMemory(m_partialPacketBuffer->GetBuffer(), m_partialPacketBuffer->GetBuffer() + packetPosition, newBufferLength);

			return m_partialPacketBuffer->Resize(newBufferLength);
		}
	}

	m_partialPacketBuffer.Reset();
	return S_OK;
}

HRESULT Connection::GetProxyEndpoint(IProxySettings* proxySettings, ServerEndpoint* endpoint)
{
	HRESULT result;
	HString hostName;
	ReturnIfFailed(result, proxySettings->get_Host(hostName.GetAddressOf()));

	UINT32 hostNameBufferLength;
	auto hostNameBuffer = hostName.GetRawBuffer(&hostNameBufferLength);

	IN_ADDR socketAddress;
	if (InetPton(AF_INET, hostNameBuffer, &socketAddress) == 1)
	{
		endpoint->Address = std::wstring(hostNameBuffer, hostNameBufferLength);
	}
	else
	{
		int wsaError;
		ADDRINFOW* addressInfo;
		if ((wsaError = GetAddrInfo(hostNameBuffer, nullptr, nullptr, &addressInfo)) != NO_ERROR)
		{
			return HRESULT_FROM_WIN32(wsaError);
		}

		WCHAR ipBuffer[15];
		DWORD ipBufferLength = 15;
		if (WSAAddressToString(addressInfo->ai_addr, static_cast<DWORD>(addressInfo->ai_addrlen), nullptr, ipBuffer, &ipBufferLength) == SOCKET_ERROR)
		{
			FreeAddrInfo(addressInfo);

			return WSAGetLastHRESULT();
		}

		endpoint->Address = std::wstring(ipBuffer, ipBufferLength);

		FreeAddrInfo(addressInfo);
	}

	return proxySettings->get_Port(&endpoint->Port);
}