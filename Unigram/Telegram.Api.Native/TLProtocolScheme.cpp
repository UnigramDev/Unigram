#include "pch.h"
#include "TLProtocolScheme.h"
#include "Datacenter.h"
#include "DatacenterServer.h"
#include "DatacenterCryptography.h"
#include "ConnectionManager.h"
#include "TLBinaryReader.h"
#include "TLBinaryWriter.h"
#include "Helpers\COMHelper.h"

#define TLARRAY_CONSTRUCTOR 0x1cb5c415

using namespace Telegram::Api::Native;
using namespace Telegram::Api::Native::TL;

ActivatableClassWithFactory(TLError, TLErrorFactory);

RegisterTLObjectConstructor(TLError);
RegisterTLObjectConstructor(TLMsgsAck);
RegisterTLObjectConstructor(TLServerDHParamsFail);
RegisterTLObjectConstructor(TLServerDHParamsOk);
RegisterTLObjectConstructor(TLResPQ);
RegisterTLObjectConstructor(TLFutureSalts);
RegisterTLObjectConstructor(TLFutureSalt);


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

	m_msgIds.resize(count);

	for (UINT32 i = 0; i < count; i++)
	{
		ReturnIfFailed(result, reader->ReadInt64(&m_msgIds[i]));
	}

	return S_OK;
}

HRESULT TLMsgsAck::WriteBody(ITLBinaryWriterEx* writer)
{
	HRESULT result;
	ReturnIfFailed(result, writer->WriteInt32(TLARRAY_CONSTRUCTOR));
	ReturnIfFailed(result, writer->WriteUInt32(static_cast<UINT32>(m_msgIds.size())));

	for (size_t i = 0; i < m_msgIds.size(); i++)
	{
		ReturnIfFailed(result, writer->WriteInt64(m_msgIds[i]));
	}

	return S_OK;
}


HRESULT TLSetDHParams::RuntimeClassInitialize(TLInt128 nonce, TLInt128 serverNonce, UINT32 encryptedDataLength)
{
	CopyMemory(m_nonce, nonce, sizeof(m_nonce));
	CopyMemory(m_serverNonce, serverNonce, sizeof(m_serverNonce));

	return MakeAndInitialize<NativeBuffer>(&m_encryptedData, encryptedDataLength);
}

HRESULT TLSetDHParams::WriteBody(ITLBinaryWriterEx* writer)
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


HRESULT TLServerDHParams::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, reader->ReadRawBuffer(sizeof(m_nonce), m_nonce));

	return reader->ReadRawBuffer(sizeof(m_serverNonce), m_serverNonce);
}


HRESULT TLServerDHParamsFail::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, TLServerDHParams::ReadBody(reader));

	return reader->ReadRawBuffer(sizeof(m_newNonceHash), m_newNonceHash);
}


HRESULT TLServerDHParamsOk::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, TLServerDHParams::ReadBody(reader));

	BYTE const* buffer;
	UINT32 bufferLength;
	ReturnIfFailed(result, reader->ReadBuffer2(&buffer, &bufferLength));
	ReturnIfFailed(result, MakeAndInitialize<NativeBuffer>(&m_encryptedData, bufferLength));

	CopyMemory(m_encryptedData->GetBuffer(), buffer, bufferLength);
	return S_OK;
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


TLResPQ::TLResPQ()
{
	ZeroMemory(m_nonce, sizeof(m_nonce));
	ZeroMemory(m_pq, sizeof(m_pq));
	ZeroMemory(m_serverNonce, sizeof(m_serverNonce));
}

TLResPQ::~TLResPQ()
{
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


HRESULT TLInitConnection::RuntimeClassInitialize(IUserConfiguration* userConfiguration, ITLObject* query)
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
	ReturnIfFailed(result, writer->WriteInt32(TELEGRAM_API_NATIVE_APIID));

	HSTRING deviceModel;
	ReturnIfFailed(result, m_userConfiguration->get_DeviceModel(&deviceModel));
	ReturnIfFailed(result, writer->WriteString(deviceModel));

	HSTRING systemVersion;
	ReturnIfFailed(result, m_userConfiguration->get_SystemVersion(&systemVersion));
	ReturnIfFailed(result, writer->WriteString(systemVersion));

	HSTRING appVersion;
	ReturnIfFailed(result, m_userConfiguration->get_AppVersion(&appVersion));
	ReturnIfFailed(result, writer->WriteString(appVersion));

	HSTRING language;
	ReturnIfFailed(result, m_userConfiguration->get_Language(&language));
	ReturnIfFailed(result, writer->WriteString(language));

	return TLObjectWithQuery::WriteQuery(writer);
}


HRESULT TLErrorFactory::CreateTLError(UINT32 code, HSTRING text, ITLError** instance)
{
	return MakeAndInitialize<TLError>(instance, code, text);
}