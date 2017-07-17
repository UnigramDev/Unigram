#include "pch.h"
#include "Connection.h"
#include "Datacenter.h"
#include "NativeBuffer.h"
#include "TLBinaryReader.h"
#include "TLBinaryWriter.h"
#include "MessageRequest.h"
#include "TLObject.h"
#include "TLTypes.h"
#include "ConnectionManager.h"

#include "MethodDebug.h"

#if FALSE && _DEBUG
#define NEXT_ENDPOINT_CONNECTION_TIMEOUT INFINITE
#define ACTIVE_CONNECTION_TIMEOUT INFINITE
#define GENERIC_CONNECTION_TIMEOUT INFINITE
#define UPLOAD_CONNECTION_TIMEOUT INFINITE
#else
#define NEXT_ENDPOINT_CONNECTION_TIMEOUT 8000
#define ACTIVE_CONNECTION_TIMEOUT 25000
#define GENERIC_CONNECTION_TIMEOUT 15000
#define UPLOAD_CONNECTION_TIMEOUT 25000
#endif

#define FLAGS_GET_CURRENTNETWORKTYPE(flags) static_cast<ConnectionNeworkType>(static_cast<int>((flags) & ConnectionFlag::CurrentNeworkType) >> 4)
#define FLAGS_SET_CURRENTNETWORKTYPE(flags, networkType) ((flags) & ~ConnectionFlag::CurrentNeworkType) | static_cast<ConnectionFlag>(static_cast<int>(networkType) << 4)
#define FLAGS_GET_CONNECTIONSTATE(flags) static_cast<ConnectionState>(static_cast<int>(flags & ConnectionFlag::ConnectionState))
#define FLAGS_SET_CONNECTIONSTATE(flags, connectionState) ((flags) & ~ConnectionFlag::ConnectionState) | static_cast<ConnectionFlag>(static_cast<int>(connectionState))
#define CONNECTION_RECONNECTION_TIMEOUT 1000
#define CONNECTION_MAX_ATTEMPTS 5
#define CONNECTION_MAX_PACKET_LENGTH 2 * 1024 * 1024

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

HRESULT Connection::Connect(bool ipv6)
{
	auto lock = LockCriticalSection();

	if (static_cast<ConnectionState>(m_flags & ConnectionFlag::ConnectionState) > ConnectionState::Disconnected)
	{
		return S_FALSE;
	}

	auto& connectionManager = m_datacenter->GetConnectionManager();
	if (!connectionManager->IsNetworkAvailable())
	{
		connectionManager->OnConnectionClosed(this);

		return HRESULT_FROM_WIN32(ERROR_NO_NETWORK);
	}

	HRESULT result;
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

	ReturnIfFailed(result, ConnectionSocket::ConnectSocket(connectionManager.Get(), endpoint, ipv6));

	ConnectionNeworkType currentNetworkType;
	ReturnIfFailed(result, connectionManager->get_CurrentNetworkType(&currentNetworkType));

	if (ipv6)
	{
		m_flags |= ConnectionFlag::IPv6;
	}

	m_flags = FLAGS_SET_CURRENTNETWORKTYPE(m_flags, currentNetworkType) | static_cast<ConnectionFlag>(ConnectionState::Connecting);

	ComPtr<Connection> connection = this;
	return connectionManager->SubmitWork([connection, connectionManager]()-> void
	{
		connectionManager->OnConnectionOpening(connection.Get());
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

		m_flags |= ConnectionFlag::CryptographyInitialized;
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

	auto lock = LockCriticalSection();

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

	if (static_cast<ConnectionState>(m_flags & ConnectionFlag::ConnectionState) < ConnectionState::Connecting)
	{
		boolean ipv6;
		auto& connectionManager = m_datacenter->GetConnectionManager();
		ReturnIfFailed(result, connectionManager->get_IsIPv6Enabled(&ipv6));
		ReturnIfFailed(result, Connect(ipv6));
	}

	return ConnectionSocket::SendData(packetWriter->GetBuffer(), packetWriter->GetCapacity());
}

HRESULT Connection::SendEncryptedMessageWithConfirmation(MessageContext const* messageContext, ITLObject* messageBody, INT32* quickAckId)
{
	if (HasMessagesToConfirm())
	{
		auto& connectionManager = m_datacenter->GetConnectionManager();

		HRESULT result;
		ComPtr<TLMessage> transportMessages[2];
		ReturnIfFailed(result, MakeAndInitialize<TLMessage>(&transportMessages[0], messageContext, messageBody));
		ReturnIfFailed(result, CreateConfirmationMessage(connectionManager.Get(), &transportMessages[1]));

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

	HRESULT result;
	UINT32 messageBodySize;
	ReturnIfFailed(result, TLObjectSizeCalculator::GetSize(messageBody, &messageBodySize));

	UINT32 messageLength = 2 * sizeof(INT64) + sizeof(INT32) + messageBodySize;

	auto lock = LockCriticalSection();
	auto& connectionManager = m_datacenter->GetConnectionManager();

	BYTE* packetBufferBytes;
	ComPtr<TLMemoryBinaryWriter> packetWriter;
	ReturnIfFailed(result, CreateMessagePacket(messageLength, reportAck, packetWriter, &packetBufferBytes));
	ReturnIfFailed(result, packetWriter->WriteInt64(0));
	ReturnIfFailed(result, packetWriter->WriteInt64(connectionManager->GenerateMessageId()));
	ReturnIfFailed(result, packetWriter->WriteUInt32(messageBodySize));
	ReturnIfFailed(result, packetWriter->WriteObject(messageBody));

	ConnectionCryptography::EncryptBuffer(packetBufferBytes, packetBufferBytes, messageLength);

	if (static_cast<ConnectionState>(m_flags & ConnectionFlag::ConnectionState) < ConnectionState::Connecting)
	{
		boolean ipv6;
		ReturnIfFailed(result, connectionManager->get_IsIPv6Enabled(&ipv6));
		ReturnIfFailed(result, Connect(ipv6));
	}

	return ConnectionSocket::SendData(packetWriter->GetBuffer(), packetWriter->GetCapacity());
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

	return connectionManager->OnConnectionSessionCreated(this, response->GetFirstMesssageId());
}

HRESULT Connection::OnMsgDetailedInfoResponse(TLMsgDetailedInfo* response)
{
	bool requestResend = false;
	auto& connectionManager = m_datacenter->GetConnectionManager();

	ComPtr<MessageRequest> request;
	if (connectionManager->GetRequestByMessageId(response->GetMessageId(), request))
	{
		auto currentTime = static_cast<INT32>(ConnectionManager::GetCurrentSystemTime() / 1000);

		if (currentTime - request->GetStartTime() >= 60)
		{
			request->SetStartTime(currentTime);
			requestResend = true;
		}
		else
		{
			return S_OK;
		}
	}
	else
	{
		requestResend = true;
	}

	if (requestResend)
	{
		MessageContext messageContext = { connectionManager->GenerateMessageId(), GenerateMessageSequenceNumber(true) };

		auto resendReq = Make<TLMsgResendReq>();
		resendReq->GetMessagesIds().push_back(response->GetAnswerMessageId());

		return SendEncryptedMessageWithConfirmation(&messageContext, resendReq.Get(), nullptr);
	}
	else
	{
		auto lock = LockCriticalSection();

		AddMessageToConfirm(response->GetAnswerMessageId());
		return S_OK;
	}
}

HRESULT Connection::OnMsgNewDetailedInfoResponse(TLMsgNewDetailedInfo* response)
{
	auto& connectionManager = m_datacenter->GetConnectionManager();
	MessageContext messageContext = { connectionManager->GenerateMessageId(), GenerateMessageSequenceNumber(true) };

	auto resendReq = Make<TLMsgResendReq>();
	resendReq->GetMessagesIds().push_back(response->GetAnswerMessageId());

	return SendEncryptedMessageWithConfirmation(&messageContext, resendReq.Get(), nullptr);
}

HRESULT Connection::OnSocketConnected()
{
	OutputDebugStringFormat(L"Datacenter %d, Connection %d, connection opened\n", m_datacenter->GetId(), (int)m_type);

	m_flags |= static_cast<ConnectionFlag>(ConnectionState::Connected);
	m_failedConnectionCount = 0;

	HRESULT result;
	ReturnIfFailed(result, m_datacenter->OnConnectionOpened(this));

	ComPtr<Connection> connection = this;
	auto& connectionManager = m_datacenter->GetConnectionManager();
	return connectionManager->SubmitWork([connection, connectionManager]()-> void
	{
		connectionManager->OnConnectionOpened(connection.Get());
	});
}

HRESULT Connection::OnSocketDisconnected(int wsaError)
{
	OutputDebugStringFormat(L"Datacenter %d, Connection %d, connection closed\n", m_datacenter->GetId(), (int)m_type);

	auto connectionState = FLAGS_GET_CONNECTIONSTATE(m_flags);
	m_flags &= ~(ConnectionFlag::ConnectionState | ConnectionFlag::CryptographyInitialized);
	m_partialPacketBuffer.Reset();

	HRESULT result;
	ReturnIfFailed(result, m_datacenter->OnConnectionClosed(this));

	auto& connectionManager = m_datacenter->GetConnectionManager();

	{
		ComPtr<Connection> connection = this;
		ReturnIfFailed(result, connectionManager->SubmitWork([connection, connectionManager]()-> void
		{
			connectionManager->OnConnectionClosed(connection.Get());
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
				m_flags |= ConnectionFlag::TryingNextEndpoint;
				m_failedConnectionCount = 0;
				m_datacenter->NextEndpoint(m_type, (m_flags & ConnectionFlag::IPv6) == ConnectionFlag::IPv6);
			}
		}
	}

	return S_OK;
}

HRESULT Connection::OnDataReceived(BYTE* buffer, UINT32 length)
{
	if (static_cast<ConnectionState>(m_flags & ConnectionFlag::ConnectionState) != ConnectionState::DataReceived)
	{
		ConnectionSocket::SetTimeout(ACTIVE_CONNECTION_TIMEOUT);
		m_flags = (m_flags & ~ConnectionFlag::TryingNextEndpoint) | static_cast<ConnectionFlag>(ConnectionState::DataReceived);
	}

	HRESULT result;
	ComPtr<IBuffer> packetBuffer;
	if (m_partialPacketBuffer == nullptr)
	{
		OutputDebugStringFormat(L"Datacenter %d, Connection %d, new packet of %d bytes received", m_datacenter->GetId(), (int)m_type, length);

		ReturnIfFailed(result, MakeAndInitialize<NativeBufferWrapper>(&packetBuffer, buffer, length));

		ConnectionCryptography::DecryptBuffer(buffer, buffer, length);
	}
	else
	{
		OutputDebugStringFormat(L"Datacenter %d, Connection %d, next packet part of %d bytes received, already have %d bytes", m_datacenter->GetId(), (int)m_type, length, m_partialPacketBuffer->GetCapacity());

		auto partialPacketLength = m_partialPacketBuffer->GetCapacity();
		ReturnIfFailed(result, m_partialPacketBuffer->Merge(buffer, length));

		ConnectionCryptography::DecryptBuffer(buffer, m_partialPacketBuffer->GetBuffer() + partialPacketLength, length);

		packetBuffer = m_partialPacketBuffer;
	}

	ComPtr<TLMemoryBinaryReader> packetReader;
	ReturnIfFailed(result, MakeAndInitialize<TLMemoryBinaryReader>(&packetReader, packetBuffer.Get()));

	UINT32 packetPosition;
	auto& connectionManager = m_datacenter->GetConnectionManager();

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
			BreakIfFailed(result, connectionManager->SubmitWork([connection, connectionManager, ackId]()-> void
			{
				connectionManager->OnConnectionQuickAckReceived(connection.Get(), ackId & ~(1 << 31));
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
			OutputDebugString(L", partial, waiting for next part\n");
			result = E_NOT_SUFFICIENT_BUFFER;
			break;
		}

		if (packetLength % 4 != 0 || packetLength > CONNECTION_MAX_PACKET_LENGTH || FAILED(OnMessageReceived(packetReader.Get(), packetLength)))
		{
			__debugbreak();
			return Reconnect();
		}

		if (FAILED(packetReader->put_Position(packetPosition + (firstByte == 0x7f ? 4 : 1) + packetLength)))
		{
			OutputDebugString(L", partial, waiting for next part\n");
			result = E_NOT_SUFFICIENT_BUFFER;
			break;
		}

		OutputDebugString(L"\n");
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

HRESULT Connection::OnMessageReceived(TLMemoryBinaryReader* messageReader, UINT32 messageLength)
{
	// METHOD_DEBUG();

	HRESULT result;
	if (messageLength == 4)
	{
		INT32 errorCode;
		ReturnIfFailed(result, messageReader->ReadInt32(&errorCode));

		I_WANT_TO_DIE_IS_THE_NEW_TODO("Handle message error");

		OutputDebugStringFormat(L"Connection error %d\n", errorCode);
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
	return connectionManager->SubmitWork([messageContext, messageObject, connection]()-> void
	{
		connection->HandleMessageResponse(&messageContext, messageObject.Get());
	});
}