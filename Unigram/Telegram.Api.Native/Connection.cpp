#include "pch.h"
#include "Connection.h"
#include "Datacenter.h"
#include "NativeBuffer.h"
#include "TLBinaryReader.h"
#include "TLBinaryWriter.h"
#include "Request.h"
#include "ConnectionManager.h"

#define CONNECTION_MAX_ATTEMPTS 5
#define CONNECTION_MAX_PACKET_LENGTH 2 * 1024 * 1024

using namespace Telegram::Api::Native;
using namespace Telegram::Api::Native::TL;


Connection::Connection(Datacenter* datacenter, ConnectionType type) :
	m_token(0),
	m_type(type),
	m_datacenter(datacenter),
	m_currentNetworkType(ConnectionNeworkType::WiFi),
	m_failedConnectionCount(0),
	m_connectionAttemptCount(CONNECTION_MAX_ATTEMPTS)
{
	m_reconnectionTimer = Make<Timer>([&]
	{
		return Connect();
	});
}

Connection::~Connection()
{
}

HRESULT Connection::get_Token(UINT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto lock = LockCriticalSection();

	*value = m_token;
	return S_OK;
}

HRESULT Connection::get_Datacenter(IDatacenter** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto lock = LockCriticalSection();
	return m_datacenter.CopyTo(value);
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

HRESULT Connection::Connect()
{
	HRESULT result;
	auto lock = LockCriticalSection();

	ComPtr<ConnectionManager> connectionManager;
	ReturnIfFailed(result, ConnectionManager::GetInstance(connectionManager));

	if (!connectionManager->IsNetworkAvailable())
	{
		connectionManager->OnConnectionClosed(this);

		return HRESULT_FROM_WIN32(ERROR_NO_NETWORK);
	}

	//I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement connection start");

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

	ReturnIfFailed(result, m_reconnectionTimer->Stop());
	ReturnIfFailed(result, ConnectionSocket::ConnectSocket(connectionManager.Get(), endpoint, ipv6));
	ReturnIfFailed(result, connectionManager->get_CurrentNetworkType(&m_currentNetworkType));

	return S_OK;
}

HRESULT Connection::Reconnect()
{
	HRESULT result;
	ReturnIfFailed(result, Suspend());

	return Connect();
}

HRESULT Connection::Suspend()
{
	HRESULT result;
	auto lock = LockCriticalSection();

	I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement connection suspension");

	ReturnIfFailed(result, m_reconnectionTimer->Stop());

	return S_OK;
}

HRESULT Connection::SendUnencryptedMessage(ITLObject* object, boolean reportAck)
{
	if (object == nullptr)
	{
		return E_INVALIDARG;
	}

	HRESULT result;
	ComPtr<ConnectionManager> connectionManager;
	ReturnIfFailed(result, ConnectionManager::GetInstance(connectionManager));

	UINT32 objectSize;
	ReturnIfFailed(result, TLObjectSizeCalculator::GetSize(object, &objectSize));

	UINT32 messageLength = 2 * sizeof(INT64) + sizeof(INT32) + objectSize;
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

	LockCriticalSection();

	BYTE* packetBufferBytes;
	ComPtr<TLBinaryWriter> packetWriter;
	if (ConnectionCryptography::IsInitialized())
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

	ReturnIfFailed(result, packetWriter->WriteInt64(0));
	ReturnIfFailed(result, packetWriter->WriteInt64(connectionManager->GenerateMessageId()));
	ReturnIfFailed(result, packetWriter->WriteInt32(objectSize));
	ReturnIfFailed(result, packetWriter->WriteObject(object));

	ConnectionCryptography::EncryptBuffer(packetBufferBytes, packetBufferBytes, messageLength);

	return ConnectionSocket::SendData(packetWriter->GetBuffer(), packetWriter->GetPosition());
}

HRESULT Connection::OnSocketConnected()
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement socket connected event handling");

	//HRESULT result;
	//ComPtr<ConnectionManager> connectionManager;
	//ReturnIfFailed(result, ConnectionManager::GetInstance(connectionManager));
	//ReturnIfFailed(result, connectionManager->OnConnectionOpened(this));

	//std::string requestBuffer("GET /?gfe_rd=cr&ei=GnEKWfHFIczw8Aeh7LDABQ&gws_rd=cr HTTP/1.1\n"
	//	"User-Agent: Mozilla / 4.0 (compatible; MSIE5.01; Windows NT)\nHost: www.google.com\nAccept-Language: en-us\nConnection: Keep-Alive\n\nSTOCAZZO h@çk3r");

	//ReturnIfFailed(result, ConnectionSocket::SendData(reinterpret_cast<const BYTE*>(requestBuffer.data()), static_cast<UINT32>(requestBuffer.size())));
	return S_OK;
}

HRESULT Connection::OnDataReceived(BYTE const* buffer, UINT32 length)
{
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
			INT32 ackId;
			BreakIfFailed(result, packetReader->ReadBigEndianInt32(&ackId));
			BreakIfFailed(result, connectionManager->OnConnectionQuickAckReceived(this, ackId & ~(1 << 31)));

			continue;
		}

		UINT32 packetLength;
		if (firstByte == 0x7f)
		{
			BreakIfFailed(result, packetReader->ReadUInt32(&packetLength));

			packetLength *= 4;
		}
		else
		{
			packetLength = static_cast<UINT32>(firstByte) * 4;
		}

		/*if (packetLength % 4 != 0 || packetLength > CONNECTION_MAX_PACKET_LENGTH ||
			FAILED(connectionManager->OnConnectionPacketReceived(this, packetReader.Get(), packetLength)))
		{
			return Reconnect();
		}*/

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

	if (result = E_NOT_SUFFICIENT_BUFFER)
	{
		auto newBufferLength = m_partialPacketBuffer->GetCapacity() - packetPosition;
		MoveMemory(m_partialPacketBuffer->GetBuffer(), m_partialPacketBuffer->GetBuffer() + packetPosition, newBufferLength);

		return m_partialPacketBuffer->Resize(newBufferLength);
	}

	m_partialPacketBuffer.Reset();
	return S_OK;
}

HRESULT Connection::OnSocketDisconnected(int wsaError)
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement socket disconnected event handling");

	WCHAR* errorMessage;
	FormatMessage(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS, NULL, wsaError, MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT), reinterpret_cast<LPWSTR>(&errorMessage), 0, NULL);
	OutputDebugString(errorMessage);
	LocalFree(errorMessage);

	HRESULT result;
	ReturnIfFailed(result, m_reconnectionTimer->Stop());

	ComPtr<ConnectionManager> connectionManager;
	ReturnIfFailed(result, ConnectionManager::GetInstance(connectionManager));
	ReturnIfFailed(result, connectionManager->OnConnectionClosed(this));

	return S_OK;
}

HRESULT Connection::OnMessageReceived(TLBinaryReader* messageReader, UINT32 messageLength)
{
	HRESULT result;

	if (messageLength == 4)
	{
		INT32 errorCode;
		ReturnIfFailed(result, messageReader->ReadInt32(&errorCode));

		return E_FAIL;
	}

	INT64 keyId;
	ReturnIfFailed(result, messageReader->ReadInt64(&keyId));

	if (keyId == 0)
	{
		INT64 messageId;;
		ReturnIfFailed(result, messageReader->ReadInt64(&messageId));

		UINT32 objectSize;
		ReturnIfFailed(result, messageReader->ReadUInt32(&objectSize));

		ComPtr<ITLObject> object;
		ReturnIfFailed(result, messageReader->ReadObject(&object));

		return m_datacenter->OnHandshakeResponseReceived(this, messageId, object.Get());
	}
	else
	{
		I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement encrypted packet handling");
	}

	return S_OK;
}