#include "pch.h"
#include "TLTypes.h"
#include "Datacenter.h"
#include "Connection.h"
#include "DatacenterServer.h"
#include "DatacenterCryptography.h"
#include "ConnectionManager.h"
#include "TLBinaryReader.h"
#include "TLBinaryWriter.h"
#include "GZip.h"
#include "Helpers\COMHelper.h"

using namespace Telegram::Api::Native;
using namespace Telegram::Api::Native::TL;

ActivatableClassWithFactory(TLError, TLErrorFactory);

RegisterTLObjectConstructor(TLError);
RegisterTLObjectConstructor(TLRpcError);
RegisterTLObjectConstructor(TLRpcReqError);
RegisterTLObjectConstructor(TLRpcResult);
RegisterTLObjectConstructor(TLMsgsAck);
RegisterTLObjectConstructor(TLMessage);
RegisterTLObjectConstructor(TLMsgContainer);
RegisterTLObjectConstructor(TLMsgCopy);
RegisterTLObjectConstructor(TLMsgsStateReq);
RegisterTLObjectConstructor(TLMsgResendStateReq);
RegisterTLObjectConstructor(TLMsgDetailedInfo);
RegisterTLObjectConstructor(TLMsgNewDetailedInfo);
RegisterTLObjectConstructor(TLGZipPacked);
RegisterTLObjectConstructor(TLAuthExportedAuthorization);
RegisterTLObjectConstructor(TLNewSessionCreated);
RegisterTLObjectConstructor(TLBadMessage);
RegisterTLObjectConstructor(TLBadServerSalt);
RegisterTLObjectConstructor(TLPong);
RegisterTLObjectConstructor(TLDHGenOk);
RegisterTLObjectConstructor(TLDHGenFail);
RegisterTLObjectConstructor(TLDHGenRetry);
RegisterTLObjectConstructor(TLServerDHParamsFail);
RegisterTLObjectConstructor(TLServerDHParamsOk);
RegisterTLObjectConstructor(TLResPQ);
RegisterTLObjectConstructor(TLFutureSalts);
RegisterTLObjectConstructor(TLFutureSalt);


TLError::TLError() :
	m_code(0)
{
}

TLError::~TLError()
{
}

HRESULT TLError::RuntimeClassInitialize(INT32 code, HSTRING text)
{
	m_code = code;
	return m_text.Set(text);
}

HRESULT TLError::get_Code(UINT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_code;
	return S_OK;
}

HRESULT TLError::get_Text(HSTRING* value)
{
	return m_text.CopyTo(value);
}

HRESULT TLError::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, reader->ReadInt32(&m_code));

	return reader->ReadString(m_text.GetAddressOf());
}

HRESULT TLError::WriteBody(ITLBinaryWriterEx* writer)
{
	HRESULT result;
	ReturnIfFailed(result, writer->WriteInt32(m_code));

	return writer->WriteString(m_text.Get());
}


template<typename TLObjectTraits>
HRESULT TLRpcErrorT<TLObjectTraits>::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, reader->ReadInt32(&m_code));

	return reader->ReadString(m_text.GetAddressOf());
}


HRESULT TLRpcError::HandleResponse(MessageContext const* messageContext, ConnectionManager* connectionManager, Connection* connection)
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement TLRpcError response handling");

	return S_OK;
}


HRESULT TLRpcReqError::HandleResponse(MessageContext const* messageContext, ConnectionManager* connectionManager, Connection* connection)
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement TLRpcReqError response handling");

	return S_OK;
}

HRESULT TLRpcReqError::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, TLRpcErrorT::ReadBody(reader));

	return reader->ReadInt64(&m_queryId);
}


TLRpcResult::TLRpcResult() :
	m_requestMessageId(0)
{
}

TLRpcResult::~TLRpcResult()
{
}

HRESULT TLRpcResult::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, reader->ReadInt64(&m_requestMessageId));

	return TLObjectWithQuery::ReadQuery(reader);
}


HRESULT TLMsgsAck::HandleResponse(MessageContext const* messageContext, ConnectionManager* connectionManager, Connection* connection)
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement TLMsgsAck response handling");

	return S_OK;
}

HRESULT TLMsgsAck::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	UINT32 constructor;
	ReturnIfFailed(result, reader->ReadUInt32(&constructor));

	if (constructor != TLARRAY_CONSTRUCTOR)
	{
		return E_FAIL;
	}

	UINT32 count;
	ReturnIfFailed(result, reader->ReadUInt32(&count));

	m_messagesIds.resize(count);

	for (UINT32 i = 0; i < count; i++)
	{
		ReturnIfFailed(result, reader->ReadInt64(&m_messagesIds[i]));
	}

	return S_OK;
}

HRESULT TLMsgsAck::WriteBody(ITLBinaryWriterEx* writer)
{
	HRESULT result;
	ReturnIfFailed(result, writer->WriteInt32(TLARRAY_CONSTRUCTOR));
	ReturnIfFailed(result, writer->WriteUInt32(static_cast<UINT32>(m_messagesIds.size())));

	for (size_t i = 0; i < m_messagesIds.size(); i++)
	{
		ReturnIfFailed(result, writer->WriteInt64(m_messagesIds[i]));
	}

	return S_OK;
}


TLMessage::TLMessage()
{
	ZeroMemory(&m_messageContext, sizeof(MessageContext));
}

TLMessage::~TLMessage()
{
}

HRESULT TLMessage::RuntimeClassInitialize(INT64 messageId, UINT32 sequenceNumber, ITLObject* object)
{
	m_messageContext.Id = messageId;
	m_messageContext.SequenceNumber = sequenceNumber;
	return TLObjectWithQuery::RuntimeClassInitialize(object);
}

HRESULT TLMessage::HandleResponse(MessageContext const* messageContext, ConnectionManager* connectionManager, Connection* connection)
{
	return TLObjectWithQuery::HandleResponse(&m_messageContext, connectionManager, connection);
}

HRESULT TLMessage::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, reader->ReadInt64(&m_messageContext.Id));
	ReturnIfFailed(result, reader->ReadUInt32(&m_messageContext.SequenceNumber));

	UINT32 bodyLength;
	ReturnIfFailed(result, reader->ReadUInt32(&bodyLength));

	UINT32 constructor;
	return reader->ReadObjectAndConstructor(bodyLength, &constructor, GetQuery().ReleaseAndGetAddressOf());
}

HRESULT TLMessage::WriteBody(ITLBinaryWriterEx* writer)
{
	HRESULT result;
	ReturnIfFailed(result, writer->WriteInt64(m_messageContext.Id));
	ReturnIfFailed(result, writer->WriteUInt32(m_messageContext.SequenceNumber));

	UINT32 bodyLength;
	ReturnIfFailed(result, TLObjectSizeCalculator::GetSize(GetQuery().Get(), &bodyLength));
	ReturnIfFailed(result, writer->WriteUInt32(bodyLength));

	return TLObjectWithQuery::WriteQuery(writer);
}


HRESULT TLMsgContainer::HandleResponse(MessageContext const* messageContext, ConnectionManager* connectionManager, Connection* connection)
{
	HRESULT result;
	for (size_t i = 0; i < m_messages.size(); i++)
	{
		ReturnIfFailed(result, m_messages[i]->HandleResponse(messageContext, connectionManager, connection));
	}

	return S_OK;
}

HRESULT TLMsgContainer::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	UINT32 count;
	ReturnIfFailed(result, reader->ReadUInt32(&count));

	m_messages.resize(count);

	for (UINT32 i = 0; i < count; i++)
	{
		m_messages[i] = Make<TLMessage>();
		ReturnIfFailed(result, m_messages[i]->ReadBody(reader));
	}

	return S_OK;
}

HRESULT TLMsgContainer::WriteBody(ITLBinaryWriterEx* writer)
{
	HRESULT result;
	ReturnIfFailed(result, writer->WriteUInt32(static_cast<UINT32>(m_messages.size())));

	for (size_t i = 0; i < m_messages.size(); i++)
	{
		ReturnIfFailed(result, m_messages[i]->WriteBody(writer));
	}

	return S_OK;
}


HRESULT TLMsgCopy::RuntimeClassInitialize(TLMessage* message)
{
	if (message == nullptr)
	{
		return E_INVALIDARG;
	}

	m_message = message;
	return S_OK;
}

HRESULT TLMsgCopy::ReadBody(ITLBinaryReaderEx* reader)
{
	m_message = Make<TLMessage>();

	return m_message->ReadBody(reader);
}

HRESULT TLMsgCopy::WriteBody(ITLBinaryWriterEx* writer)
{
	return m_message->WriteBody(writer);
}


HRESULT TLMsgsStateReq::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	UINT32 constructor;
	ReturnIfFailed(result, reader->ReadUInt32(&constructor));

	if (constructor != TLARRAY_CONSTRUCTOR)
	{
		return E_FAIL;
	}

	UINT32 count;
	ReturnIfFailed(result, reader->ReadUInt32(&count));

	m_messagesIds.resize(count);

	for (UINT32 i = 0; i < count; i++)
	{
		ReturnIfFailed(result, reader->ReadInt64(&m_messagesIds[i]));
	}

	return S_OK;
}

HRESULT TLMsgsStateReq::WriteBody(ITLBinaryWriterEx* writer)
{
	HRESULT result;
	ReturnIfFailed(result, writer->WriteInt32(TLARRAY_CONSTRUCTOR));
	ReturnIfFailed(result, writer->WriteUInt32(static_cast<UINT32>(m_messagesIds.size())));

	for (size_t i = 0; i < m_messagesIds.size(); i++)
	{
		ReturnIfFailed(result, writer->WriteInt64(m_messagesIds[i]));
	}

	return S_OK;
}


HRESULT TLMsgResendStateReq::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	UINT32 constructor;
	ReturnIfFailed(result, reader->ReadUInt32(&constructor));

	if (constructor != TLARRAY_CONSTRUCTOR)
	{
		return E_FAIL;
	}

	UINT32 count;
	ReturnIfFailed(result, reader->ReadUInt32(&count));

	m_messagesIds.resize(count);

	for (UINT32 i = 0; i < count; i++)
	{
		ReturnIfFailed(result, reader->ReadInt64(&m_messagesIds[i]));
	}

	return S_OK;
}

HRESULT TLMsgResendStateReq::WriteBody(ITLBinaryWriterEx* writer)
{
	HRESULT result;
	ReturnIfFailed(result, writer->WriteInt32(TLARRAY_CONSTRUCTOR));
	ReturnIfFailed(result, writer->WriteUInt32(static_cast<UINT32>(m_messagesIds.size())));

	for (size_t i = 0; i < m_messagesIds.size(); i++)
	{
		ReturnIfFailed(result, writer->WriteInt64(m_messagesIds[i]));
	}

	return S_OK;
}


template<typename TLObjectTraits>
HRESULT TLMsgDetailedInfoT<TLObjectTraits>::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, reader->ReadInt64(&m_answerMessageId));
	ReturnIfFailed(result, reader->ReadInt32(&m_datacenterId));

	return reader->ReadInt32(&m_status);
}


TLMsgDetailedInfo::TLMsgDetailedInfo() :
	m_messageId(0)
{
}

TLMsgDetailedInfo::~TLMsgDetailedInfo()
{
}

HRESULT TLMsgDetailedInfo::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, reader->ReadInt64(&m_messageId));

	return TLMsgDetailedInfoT::ReadBody(reader);
}


HRESULT TLGZipPacked::RuntimeClassInitialize(NativeBuffer* rawData)
{
	if (rawData == nullptr)
	{
		return E_INVALIDARG;
	}

	return GZipCompressBuffer(rawData->GetBuffer(), rawData->GetCapacity(), &m_packedData);
}

HRESULT TLGZipPacked::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	BYTE const* buffer;
	UINT32 bufferLength;
	ReturnIfFailed(result, reader->ReadBuffer2(&buffer, &bufferLength));

	return GZipDecompressBuffer(buffer, bufferLength, &m_packedData);
}

HRESULT TLGZipPacked::WriteBody(ITLBinaryWriterEx* writer)
{
	return writer->WriteBuffer(m_packedData->GetBuffer(), m_packedData->GetCapacity());
}


TLAuthExportedAuthorization::TLAuthExportedAuthorization() :
	m_datacenterId(0)
{
}

TLAuthExportedAuthorization::~TLAuthExportedAuthorization()
{
}

HRESULT TLAuthExportedAuthorization::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, reader->ReadInt32(&m_datacenterId));

	BYTE const* buffer;
	UINT32 bufferLength;
	ReturnIfFailed(result, reader->ReadBuffer2(&buffer, &bufferLength));
	ReturnIfFailed(result, MakeAndInitialize<NativeBuffer>(&m_bytes, bufferLength));

	CopyMemory(m_bytes->GetBuffer(), buffer, bufferLength);
	return S_OK;
}


TLNewSessionCreated::TLNewSessionCreated() :
	m_firstMesssageId(0),
	m_uniqueId(0),
	m_serverSalt(0)
{
}

TLNewSessionCreated::~TLNewSessionCreated()
{
}

HRESULT TLNewSessionCreated::HandleResponse(MessageContext const* messageContext, ConnectionManager* connectionManager, Connection* connection)
{
	return connection->HandleNewSessionCreatedResponse(this);
}

HRESULT TLNewSessionCreated::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, reader->ReadInt64(&m_firstMesssageId));
	ReturnIfFailed(result, reader->ReadInt64(&m_uniqueId));

	return reader->ReadInt64(&m_serverSalt);
}


template<typename TLObjectTraits>
HRESULT TLBadMsgNotificationT<TLObjectTraits>::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, reader->ReadInt64(&m_badMessageContext.Id));
	ReturnIfFailed(result, reader->ReadUInt32(&m_badMessageContext.SequenceNumber));

	return reader->ReadInt32(&m_errorCode);
}


HRESULT TLBadMessage::HandleResponse(MessageContext const* messageContext, ConnectionManager* connectionManager, Connection* connection)
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement TLBadMessage response handling");

	return S_OK;
}


HRESULT TLBadServerSalt::HandleResponse(MessageContext const* messageContext, ConnectionManager* connectionManager, Connection* connection)
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement TLBadServerSalt response handling");

	return S_OK;
}

HRESULT TLBadServerSalt::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, TLBadMsgNotificationT::ReadBody(reader));

	return reader->ReadInt64(&m_newServerSalt);
}


TLPong::TLPong() :
	m_messageId(0),
	m_pingId(0)
{
}

TLPong::~TLPong()
{
}

HRESULT TLPong::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, reader->ReadInt64(&m_messageId));

	return reader->ReadInt64(&m_pingId);
}


template<typename TLObjectTraits>
HRESULT TLDHGenT<TLObjectTraits>::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, reader->ReadRawBuffer(sizeof(m_nonce), m_nonce));
	ReturnIfFailed(result, reader->ReadRawBuffer(sizeof(m_serverNonce), m_serverNonce));

	return reader->ReadRawBuffer(sizeof(m_newNonceHash), m_newNonceHash);
}


HRESULT TLDHGenOk::HandleResponse(MessageContext const* messageContext, ConnectionManager* connectionManager, Connection* connection)
{
	HRESULT result;
	auto datacenter = connection->GetDatacenter();
	if (FAILED(result = datacenter->HandleHandshakeClientDHResponse(connectionManager, connection, this)))
	{
		return datacenter->HandleHandshakeError(result);
	}

	return S_OK;
}


HRESULT TLDHGenFail::HandleResponse(MessageContext const* messageContext, ConnectionManager* connectionManager, Connection* connection)
{
	return connection->GetDatacenter()->HandleHandshakeError(E_FAIL);
}


HRESULT TLDHGenRetry::HandleResponse(MessageContext const* messageContext, ConnectionManager* connectionManager, Connection* connection)
{
	return connection->GetDatacenter()->HandleHandshakeError(E_FAIL);
}


template<typename TLObjectTraits>
HRESULT TLServerDHParamsT<TLObjectTraits>::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, reader->ReadRawBuffer(sizeof(m_nonce), m_nonce));

	return reader->ReadRawBuffer(sizeof(m_serverNonce), m_serverNonce);
}


HRESULT TLServerDHParamsFail::HandleResponse(MessageContext const* messageContext, ConnectionManager* connectionManager, Connection* connection)
{
	return connection->GetDatacenter()->HandleHandshakeError(E_FAIL);
}

HRESULT TLServerDHParamsFail::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, TLServerDHParamsT::ReadBody(reader));

	return reader->ReadRawBuffer(sizeof(m_newNonceHash), m_newNonceHash);
}


HRESULT TLServerDHParamsOk::HandleResponse(MessageContext const* messageContext, ConnectionManager* connectionManager, Connection* connection)
{
	HRESULT result;
	auto datacenter = connection->GetDatacenter();
	if (FAILED(result = datacenter->HandleHandshakeServerDHResponse(connection, this)))
	{
		return datacenter->HandleHandshakeError(result);
	}

	return S_OK;
}

HRESULT TLServerDHParamsOk::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, TLServerDHParamsT::ReadBody(reader));

	BYTE const* buffer;
	UINT32 bufferLength;
	ReturnIfFailed(result, reader->ReadBuffer2(&buffer, &bufferLength));
	ReturnIfFailed(result, MakeAndInitialize<NativeBuffer>(&m_encryptedData, bufferLength));

	CopyMemory(m_encryptedData->GetBuffer(), buffer, bufferLength);
	return S_OK;
}


TLResPQ::TLResPQ()
{
	ZeroMemory(m_nonce, sizeof(m_nonce));
	ZeroMemory(m_pq, sizeof(m_pq));
	ZeroMemory(m_serverNonce, sizeof(m_serverNonce));
}

TLResPQ::~TLResPQ()
{
}

HRESULT TLResPQ::HandleResponse(MessageContext const* messageContext, ConnectionManager* connectionManager, Connection* connection)
{
	HRESULT result;
	auto datacenter = connection->GetDatacenter();
	if (FAILED(result = datacenter->HandleHandshakePQResponse(connection, this)))
	{
		return datacenter->HandleHandshakeError(result);
	}

	return S_OK;
}

HRESULT TLResPQ::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, reader->ReadRawBuffer(sizeof(m_nonce), m_nonce));
	ReturnIfFailed(result, reader->ReadRawBuffer(sizeof(m_serverNonce), m_serverNonce));
	ReturnIfFailed(result, reader->ReadBuffer(m_pq, sizeof(m_pq)));

	UINT32 constructor;
	ReturnIfFailed(result, reader->ReadUInt32(&constructor));

	if (constructor != TLARRAY_CONSTRUCTOR)
	{
		return E_FAIL;
	}

	UINT32 count;
	ReturnIfFailed(result, reader->ReadUInt32(&count));

	m_serverPublicKeyFingerprints.resize(count);

	for (UINT32 i = 0; i < count; i++)
	{
		ReturnIfFailed(result, reader->ReadInt64(&m_serverPublicKeyFingerprints[i]));
	}

	return S_OK;
}


TLFutureSalts::TLFutureSalts() :
	m_reqMessageId(0),
	m_now(0)
{
}

TLFutureSalts::~TLFutureSalts()
{
}

HRESULT TLFutureSalts::HandleResponse(MessageContext const* messageContext, ConnectionManager* connectionManager, Connection* connection)
{
	return connection->GetDatacenter()->HandleFutureSaltsResponse(this);
}

HRESULT TLFutureSalts::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, reader->ReadInt64(&m_reqMessageId));
	ReturnIfFailed(result, reader->ReadInt32(&m_now));

	UINT32 count;
	ReturnIfFailed(result, reader->ReadUInt32(&count));

	m_salts.resize(count);

	for (UINT32 i = 0; i < count; i++)
	{
		ReturnIfFailed(result, reader->ReadInt32(&m_salts[i].ValidSince));
		ReturnIfFailed(result, reader->ReadInt32(&m_salts[i].ValidUntil));
		ReturnIfFailed(result, reader->ReadInt64(&m_salts[i].Salt));
	}

	return S_OK;
}


TLFutureSalt::TLFutureSalt() :
	m_salt({})
{
}

TLFutureSalt::~TLFutureSalt()
{
}

HRESULT TLFutureSalt::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, reader->ReadInt32(&m_salt.ValidSince));
	ReturnIfFailed(result, reader->ReadInt32(&m_salt.ValidUntil));

	return reader->ReadInt64(&m_salt.Salt);
}


HRESULT TLErrorFactory::CreateTLError(UINT32 code, HSTRING text, ITLError** instance)
{
	return MakeAndInitialize<TLError>(instance, code, text);
}