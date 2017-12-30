#include "pch.h"
#include "TLMethods.h"
#include "ConnectionManager.h"
#include "UserConfiguration.h"
#include "TLBinaryReader.h"
#include "TLBinaryWriter.h"
#include "Helpers\COMHelper.h"

using namespace Telegram::Api::Native;
using namespace Telegram::Api::Native::TL;
using namespace Telegram::Api::Native::TL::Methods;


TLRpcDropAnswer::TLRpcDropAnswer(INT64 requestMessageId) :
	m_requestMessageId(requestMessageId)
{
}

TLRpcDropAnswer::~TLRpcDropAnswer()
{
}

HRESULT TLRpcDropAnswer::WriteBody(ITLBinaryWriterEx* writer)
{
	return writer->WriteInt64(m_requestMessageId);
}


TLAuthExportAuthorization::TLAuthExportAuthorization(INT32 datacenterId) :
	m_datacenterId(datacenterId)
{
}

TLAuthExportAuthorization::~TLAuthExportAuthorization()
{
}

HRESULT TLAuthExportAuthorization::WriteBody(ITLBinaryWriterEx* writer)
{
	return writer->WriteInt32(m_datacenterId);
}


HRESULT TLAuthImportAuthorization::RuntimeClassInitialize(INT32 id, NativeBuffer* bytes)
{
	if (bytes == nullptr)
	{
		return E_INVALIDARG;
	}

	m_id = id;
	m_bytes = bytes;
	return S_OK;
}

HRESULT TLAuthImportAuthorization::WriteBody(ITLBinaryWriterEx* writer)
{
	HRESULT result;
	ReturnIfFailed(result, writer->WriteInt32(m_id));

	return writer->WriteBuffer(m_bytes->GetBuffer(), m_bytes->GetCapacity());
}


TLDestroySession::TLDestroySession(INT64 sessionId) :
	m_sessionId(sessionId)
{
}

TLDestroySession::~TLDestroySession()
{
}

HRESULT TLDestroySession::WriteBody(ITLBinaryWriterEx* writer)
{
	return writer->WriteInt64(m_sessionId);
}


TLPing::TLPing(INT64 pingId) :
	m_pingId(pingId)
{
}

TLPing::~TLPing()
{
}

HRESULT TLPing::WriteBody(ITLBinaryWriterEx* writer)
{
	return writer->WriteInt64(m_pingId);
}


TLPingDelayDisconnect::TLPingDelayDisconnect(INT64 pingId, INT32 disconnectDelay) :
	m_pingId(pingId),
	m_disconnectDelay(disconnectDelay)
{
}

TLPingDelayDisconnect::~TLPingDelayDisconnect()
{
}

HRESULT TLPingDelayDisconnect::WriteBody(ITLBinaryWriterEx* writer)
{
	HRESULT result;
	ReturnIfFailed(result, writer->WriteInt64(m_pingId));

	return writer->WriteInt32(m_disconnectDelay);
}


HRESULT TLSetClientDHParams::RuntimeClassInitialize(TLInt128 nonce, TLInt128 serverNonce, UINT32 encryptedDataLength)
{
	CopyMemory(m_nonce, nonce, sizeof(m_nonce));
	CopyMemory(m_serverNonce, serverNonce, sizeof(m_serverNonce));

	return MakeAndInitialize<NativeBuffer>(&m_encryptedData, encryptedDataLength);
}

HRESULT TLSetClientDHParams::WriteBody(ITLBinaryWriterEx* writer)
{
	HRESULT result;
	ReturnIfFailed(result, writer->WriteRawBuffer(sizeof(m_nonce), m_nonce));
	ReturnIfFailed(result, writer->WriteRawBuffer(sizeof(m_serverNonce), m_serverNonce));

	return writer->WriteBuffer(m_encryptedData->GetBuffer(), m_encryptedData->GetCapacity());
}


HRESULT TLReqDHParams::RuntimeClassInitialize(TLInt128 nonce, TLInt128 serverNonce, TLInt256 newNonce, UINT32 p, UINT32 q, INT64 publicKeyFingerprint, UINT32 encryptedDataLength)
{
	CopyMemory(m_nonce, nonce, sizeof(m_nonce));
	CopyMemory(m_serverNonce, serverNonce, sizeof(m_serverNonce));
	CopyMemory(m_newNonce, newNonce, sizeof(m_newNonce));

	m_p[0] = (p >> 24) & 0xff;
	m_p[1] = (p >> 16) & 0xff;
	m_p[2] = (p >> 8) & 0xff;
	m_p[3] = p & 0xff;

	m_q[0] = (q >> 24) & 0xff;
	m_q[1] = (q >> 16) & 0xff;
	m_q[2] = (q >> 8) & 0xff;
	m_q[3] = q & 0xff;

	m_publicKeyFingerprint = publicKeyFingerprint;

	return MakeAndInitialize<NativeBuffer>(&m_encryptedData, encryptedDataLength);
}

HRESULT TLReqDHParams::WriteBody(ITLBinaryWriterEx* writer)
{
	HRESULT result;
	ReturnIfFailed(result, writer->WriteRawBuffer(sizeof(m_nonce), m_nonce));
	ReturnIfFailed(result, writer->WriteRawBuffer(sizeof(m_serverNonce), m_serverNonce));
	ReturnIfFailed(result, writer->WriteBuffer(m_p, sizeof(m_p)));
	ReturnIfFailed(result, writer->WriteBuffer(m_q, sizeof(m_q)));
	ReturnIfFailed(result, writer->WriteInt64(m_publicKeyFingerprint));

	return writer->WriteBuffer(m_encryptedData->GetBuffer(), m_encryptedData->GetCapacity());
}


HRESULT TLReqPQ::RuntimeClassInitialize(TLInt128 nonce)
{
	CopyMemory(m_nonce, nonce, sizeof(m_nonce));

	return S_OK;
}

HRESULT TLReqPQ::WriteBody(ITLBinaryWriterEx* writer)
{
	return writer->WriteRawBuffer(sizeof(m_nonce), m_nonce);
}


TLGetFutureSalts::TLGetFutureSalts(UINT32 count) :
	m_count(count)
{
}

TLGetFutureSalts::~TLGetFutureSalts()
{
}

HRESULT TLGetFutureSalts::WriteBody(ITLBinaryWriterEx* writer)
{
	return writer->WriteUInt32(m_count);
}


HRESULT TLInvokeAfterMsg::RuntimeClassInitialize(INT64 messageId, ITLObject* query)
{
	m_messageId = messageId;
	return TLObjectWithQuery::RuntimeClassInitialize(query);
}

HRESULT TLInvokeAfterMsg::WriteBody(ITLBinaryWriterEx* writer)
{
	HRESULT result;
	ReturnIfFailed(result, writer->WriteInt64(m_messageId));

	return TLObjectWithQuery::WriteQuery(writer);
}


HRESULT TLInvokeWithLayer::RuntimeClassInitialize(ITLObject* query)
{
	return TLObjectWithQuery::RuntimeClassInitialize(query);
}

HRESULT TLInvokeWithLayer::WriteBody(ITLBinaryWriterEx* writer)
{
	HRESULT result;
	ReturnIfFailed(result, writer->WriteInt32(TELEGRAM_API_NATIVE_LAYER));

	return TLObjectWithQuery::WriteQuery(writer);
}


HRESULT TLInitConnection::RuntimeClassInitialize(UserConfiguration* userConfiguration, ITLObject* query)
{
	if (userConfiguration == nullptr || query == nullptr)
	{
		return E_INVALIDARG;
	}

	m_userConfiguration = userConfiguration;
	return TLObjectWithQuery::RuntimeClassInitialize(query);
}

HRESULT TLInitConnection::WriteBody(ITLBinaryWriterEx* writer)
{
	HRESULT result;
	ReturnIfFailed(result, writer->WriteInt32(m_userConfiguration->GetAppId()));
	ReturnIfFailed(result, writer->WriteString(m_userConfiguration->GetDeviceModel().Get()));
	ReturnIfFailed(result, writer->WriteString(m_userConfiguration->GetSystemVersion().Get()));
	ReturnIfFailed(result, writer->WriteString(m_userConfiguration->GetAppVersion().Get()));

	auto& language = m_userConfiguration->GetLanguage();
	ReturnIfFailed(result, writer->WriteString(language.Get()));
	ReturnIfFailed(result, writer->WriteString(m_userConfiguration->GetLangPack().Get()));
	ReturnIfFailed(result, writer->WriteString(language.Get()));

	return TLObjectWithQuery::WriteQuery(writer);
}