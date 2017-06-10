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

#include "MethodDebugInfo.h"

#if _DEBUG
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

#define CONNECTION_RECONNECTION_TIMEOUT 1000
#define CONNECTION_MAX_ATTEMPTS 5
#define CONNECTION_MAX_PACKET_LENGTH 2 * 1024 * 1024

using namespace Telegram::Api::Native;
using namespace Telegram::Api::Native::TL;


Connection::Connection(Datacenter* datacenter, ConnectionType type) :
	m_type(type),
	m_datacenter(datacenter),
	m_currentNetworkType(ConnectionNeworkType::WiFi),
	m_flags(ConnectionFlag::None),
	m_failedConnectionCount(0)
{
}

Connection::~Connection()
{
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

	*value = m_currentNetworkType;
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
	m_reconnectionTimer.Reset();

	return S_OK;
}

HRESULT Connection::Connect()
{
	HRESULT result;
	ComPtr<ConnectionManager> connectionManager;
	ReturnIfFailed(result, ConnectionManager::GetInstance(connectionManager));

	return Connect(connectionManager);
}

HRESULT Connection::Connect(ComPtr<ConnectionManager> const& connectionManager)
{
	METHOD_DEBUG_INFO();

	auto lock = LockCriticalSection();

	if (static_cast<ConnectionState>(m_flags & ConnectionFlag::ConnectionState) > ConnectionState::Disconnected)
	{
		return E_NOT_VALID_STATE;
	}

	HRESULT result;
	if (m_reconnectionTimer != nullptr)
	{
		ReturnIfFailed(result, m_reconnectionTimer->Stop());
	}

	if (!connectionManager->IsNetworkAvailable())
	{
		connectionManager->OnConnectionClosed(this);

		return HRESULT_FROM_WIN32(ERROR_NO_NETWORK);
	}

	boolean ipv6;
	ReturnIfFailed(result, connectionManager->get_IsIpv6Enabled(&ipv6));

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
	ReturnIfFailed(result, connectionManager->get_CurrentNetworkType(&m_currentNetworkType));

	if (ipv6)
	{
		m_flags |= ConnectionFlag::Ipv6;
	}

	m_flags |= static_cast<ConnectionFlag>(ConnectionState::Connecting);
	return S_OK;
}

HRESULT Connection::Disconnect()
{
	METHOD_DEBUG_INFO();

	auto lock = LockCriticalSection();

	return DisconnectSocket(true);
}

HRESULT Connection::CreateMessagePacket(UINT32 messageLength, boolean reportAck, ComPtr<TLBinaryWriter>& writer, BYTE** messageBuffer)
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
	ComPtr<TLBinaryWriter> packetWriter;
	if ((m_flags & ConnectionFlag::CryptographyInitialized) == ConnectionFlag::CryptographyInitialized)
	{
		ReturnIfFailed(result, MakeAndInitialize<TLBinaryWriter>(&packetWriter, packetBufferLength));

		packetBufferBytes = packetWriter->GetBuffer();
	}
	else
	{
		packetBufferLength += 64;

		ReturnIfFailed(result, MakeAndInitialize<TLBinaryWriter>(&packetWriter, packetBufferLength));

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
	METHOD_DEBUG_INFO();

	if (messageContext == nullptr || messageBody == nullptr)
	{
		return E_INVALIDARG;
	}

	HRESULT result;
	ComPtr<ConnectionManager> connectionManager;
	ReturnIfFailed(result, ConnectionManager::GetInstance(connectionManager));

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
	ComPtr<TLBinaryWriter> packetWriter;
	ReturnIfFailed(result, CreateMessagePacket(encryptedMessageLength, quickAckId != nullptr, packetWriter, &packetBufferBytes));
	ReturnIfFailed(result, packetWriter->SeekCurrent(24));
	ReturnIfFailed(result, packetWriter->WriteInt64(m_datacenter->GetServerSalt(connectionManager.Get())));
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
		Connect(connectionManager);
	}

	return ConnectionSocket::SendData(packetWriter->GetBuffer(), packetWriter->GetCapacity());
}

HRESULT Connection::SendUnencryptedMessage(ITLObject* messageBody, boolean reportAck)
{
	METHOD_DEBUG_INFO();

	if (messageBody == nullptr)
	{
		return E_INVALIDARG;
	}

	HRESULT result;
	ComPtr<ConnectionManager> connectionManager;
	ReturnIfFailed(result, ConnectionManager::GetInstance(connectionManager));

	UINT32 messageBodySize;
	ReturnIfFailed(result, TLObjectSizeCalculator::GetSize(messageBody, &messageBodySize));

	UINT32 messageLength = 2 * sizeof(INT64) + sizeof(INT32) + messageBodySize;

	auto lock = LockCriticalSection();

	BYTE* packetBufferBytes;
	ComPtr<TLBinaryWriter> packetWriter;
	ReturnIfFailed(result, CreateMessagePacket(messageLength, reportAck, packetWriter, &packetBufferBytes));
	ReturnIfFailed(result, packetWriter->WriteInt64(0));
	ReturnIfFailed(result, packetWriter->WriteInt64(connectionManager->GenerateMessageId()));
	ReturnIfFailed(result, packetWriter->WriteUInt32(messageBodySize));
	ReturnIfFailed(result, packetWriter->WriteObject(messageBody));

	ConnectionCryptography::EncryptBuffer(packetBufferBytes, packetBufferBytes, messageLength);

	if (static_cast<ConnectionState>(m_flags & ConnectionFlag::ConnectionState) < ConnectionState::Connecting)
	{
		Connect(connectionManager);
	}

	return ConnectionSocket::SendData(packetWriter->GetBuffer(), packetWriter->GetCapacity());
}

HRESULT Connection::HandleMessageResponse(MessageContext const* messageContext, ITLObject* messageBody, ConnectionManager* connectionManager)
{
	if (messageContext->SequenceNumber % 2)
	{
		AddMessageToConfirm(messageContext->Id);
	}

	if (IsMessageIdProcessed(messageContext->Id))
	{
		return S_OK;
	}

	HRESULT result;
	ComPtr<IMessageResponseHandler> responseHandler;
	if (SUCCEEDED(messageBody->QueryInterface(IID_PPV_ARGS(&responseHandler))))
	{
		ReturnIfFailed(result, responseHandler->HandleResponse(messageContext, connectionManager, this));
	}
	else
	{
		ReturnIfFailed(result, connectionManager->OnUnprocessedMessageResponse(messageContext, messageBody, this));
	}

	AddProcessedMessageId(messageContext->Id);
	return S_OK;
}

HRESULT Connection::OnNewSessionCreatedResponse(ConnectionManager* connectionManager, TLNewSessionCreated* response)
{
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

	return connectionManager->OnConnectionSessionCreated(this, response->GetFirstMesssageId());
}

HRESULT Connection::OnSocketConnected()
{
	m_flags |= static_cast<ConnectionFlag>(ConnectionState::Connected);
	m_failedConnectionCount = 0;

	HRESULT result;
	ComPtr<ConnectionManager> connectionManager;
	ReturnIfFailed(result, ConnectionManager::GetInstance(connectionManager));

	ComPtr<Connection> connection = this;
	return connectionManager->SubmitWork([connection, connectionManager]()-> void
	{
		auto lock = connection->LockCriticalSection();

		if (connection->m_datacenter != nullptr)
		{
			connection->m_datacenter->OnConnectionOpened(connectionManager.Get(), connection.Get());
		}
	});
}

HRESULT Connection::OnDataReceived(BYTE const* buffer, UINT32 length)
{
	if (static_cast<ConnectionState>(m_flags & ConnectionFlag::ConnectionState) != ConnectionState::DataReceived)
	{
		ConnectionSocket::SetTimeout(ACTIVE_CONNECTION_TIMEOUT);
		m_flags = (m_flags & ~ConnectionFlag::TryingNextEndpoint) | static_cast<ConnectionFlag>(ConnectionState::DataReceived);
	}

	HRESULT result;
	ComPtr<ConnectionManager> connectionManager;
	ReturnIfFailed(result, ConnectionManager::GetInstance(connectionManager));

	if (m_partialPacketBuffer == nullptr)
	{
		ReturnIfFailed(result, MakeAndInitialize<NativeBuffer>(&m_partialPacketBuffer, length));

		ConnectionCryptography::DecryptBuffer(buffer, m_partialPacketBuffer->GetBuffer(), length);
	}
	else
	{
		auto partialPacketLength = m_partialPacketBuffer->GetCapacity();
		ReturnIfFailed(result, m_partialPacketBuffer->Merge(buffer, length));

		ConnectionCryptography::DecryptBuffer(buffer, m_partialPacketBuffer->GetBuffer() + partialPacketLength, length);
	}

	ComPtr<TLBinaryReader> packetReader;
	ReturnIfFailed(result, MakeAndInitialize<TLBinaryReader>(&packetReader, m_partialPacketBuffer.Get()));

	UINT32 packetPosition;
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
			result = E_NOT_SUFFICIENT_BUFFER;
			break;
		}

		if (packetLength % 4 != 0 || packetLength > CONNECTION_MAX_PACKET_LENGTH || FAILED(OnMessageReceived(connectionManager, packetReader.Get(), packetLength)))
		{
			ComPtr<Connection> connection = this;
			return connectionManager->SubmitWork([connectionManager, connection]()-> void
			{
				connection->Disconnect();
				connection->Connect(connectionManager);
			});
		}

		if (FAILED(packetReader->put_Position(packetPosition + (firstByte == 0x7f ? 4 : 1) + packetLength)))
		{
			result = E_NOT_SUFFICIENT_BUFFER;
			break;
		}
	}

	if (result == E_NOT_SUFFICIENT_BUFFER)
	{
		if (packetPosition == 0)
		{
			return S_OK;
		}

		auto newBufferLength = m_partialPacketBuffer->GetCapacity() - packetPosition;
		MoveMemory(m_partialPacketBuffer->GetBuffer(), m_partialPacketBuffer->GetBuffer() + packetPosition, newBufferLength);

		return m_partialPacketBuffer->Resize(newBufferLength);
	}

	m_partialPacketBuffer.Reset();
	return S_OK;
}

HRESULT Connection::OnSocketDisconnected(int wsaError)
{
	auto connectionState = static_cast<ConnectionState>(m_flags & ConnectionFlag::ConnectionState);
	m_reconnectionTimer.Reset();
	m_partialPacketBuffer.Reset();
	m_flags &= ~ConnectionFlag::ConnectionState;

	HRESULT result;
	ComPtr<ConnectionManager> connectionManager;
	ReturnIfFailed(result, ConnectionManager::GetInstance(connectionManager));

	ComPtr<Connection> connection = this;
	ReturnIfFailed(result, connectionManager->SubmitWork([connection, connectionManager]()-> void
	{
		auto lock = connection->LockCriticalSection();

		if (connection->m_datacenter != nullptr)
		{
			connection->m_datacenter->OnConnectionClosed(connectionManager.Get(), connection.Get());
		}
	}));

	if (m_datacenter->IsHandshaking() || connectionManager->IsCurrentDatacenter(m_datacenter->GetId()))
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
				m_datacenter->NextEndpoint(m_type, (m_flags & ConnectionFlag::Ipv6) == ConnectionFlag::Ipv6);
			}

			m_reconnectionTimer = Make<Timer>([this, connectionManager]() -> void
			{
				Connect(connectionManager);
			});

			ReturnIfFailed(result, connectionManager->AttachEventObject(m_reconnectionTimer.Get()));
			ReturnIfFailed(result, m_reconnectionTimer->SetTimeout(CONNECTION_RECONNECTION_TIMEOUT, false));

			return m_reconnectionTimer->Start();
		}
	}

	return S_OK;
}

HRESULT Connection::OnMessageReceived(ComPtr<ConnectionManager> const& connectionManager, TLBinaryReader* messageReader, UINT32 messageLength)
{
	METHOD_DEBUG_INFO();

	HRESULT result;
	if (messageLength == 4)
	{
		INT32 errorCode;
		ReturnIfFailed(result, messageReader->ReadInt32(&errorCode));

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
	return connectionManager->SubmitWork([messageContext, messageObject, connection, connectionManager]()-> void
	{
		auto lock = connection->LockCriticalSection();
		connection->HandleMessageResponse(&messageContext, messageObject.Get(), connectionManager.Get());
	});
}