#include "pch.h"
#include "TLProtocolScheme.h"
#include "Datacenter.h"
#include "DatacenterCryptography.h"
#include "ConnectionManager.h"
#include "TLBinaryReader.h"
#include "TLBinaryWriter.h"
#include "Helpers\COMHelper.h"

using namespace Telegram::Api::Native;
using namespace Telegram::Api::Native::TL;

ActivatableClassWithFactory(TLError, TLErrorFactory);

RegisterTLObjectConstructor(TLError);
RegisterTLObjectConstructor(TLMsgsAck);
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

	if (constructor != 0x1cb5c415)
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
	ReturnIfFailed(result, writer->WriteInt32(0x1cb5c415));
	ReturnIfFailed(result, writer->WriteUInt32(static_cast<UINT32>(m_msgIds.size())));

	for (size_t i = 0; i < m_msgIds.size(); i++)
	{
		ReturnIfFailed(result, writer->WriteInt64(m_msgIds[i]));
	}

	return S_OK;
}


HRESULT TLReqDHParams::RuntimeClassInitialize(TLAuthNonce16 nonce, TLResPQ* pqResponse)
{
	if (nonce == nullptr || pqResponse == nullptr)
	{
		return E_INVALIDARG;
	}

	if (!CheckNonces(nonce, pqResponse->GetNonce()))
	{
		return E_INVALIDARG;
	}

	ServerPublicKey const* serverPublicKey;
	if (!SelectPublicKey(pqResponse->GetServerPublicKeyFingerprints(), &serverPublicKey))
	{
		return E_FAIL;
	}

	auto pq = pqResponse->GetPQ();
	UINT64 pq64 = ((pq[0] & 0xffULL) << 56ULL) | ((pq[1] & 0xffULL) << 48ULL) | ((pq[2] & 0xffULL) << 40ULL) | ((pq[3] & 0xffULL) << 32ULL) |
		((pq[4] & 0xffULL) << 24ULL) | ((pq[5] & 0xffULL) << 16ULL) | ((pq[6] & 0xffULL) << 8ULL) | ((pq[7] & 0xffULL));

	UINT32 p;
	UINT32 q;
	if (!FactorizePQ(pq64, p, q))
	{
		return E_FAIL;
	}

	m_p[0] = (p >> 24) & 0xff;
	m_p[1] = (p >> 16) & 0xff;
	m_p[2] = (p >> 8) & 0xff;
	m_p[3] = p & 0xff;

	m_q[0] = (q >> 24) & 0xff;
	m_q[1] = (q >> 16) & 0xff;
	m_q[2] = (q >> 8) & 0xff;
	m_q[3] = q & 0xff;

	CopyMemory(m_nonce, nonce, sizeof(m_nonce));
	CopyMemory(m_serverNonce, pqResponse->GetNonce(), sizeof(m_serverNonce));
	RAND_bytes(m_newNonce, sizeof(m_newNonce));
	m_publicKeyFingerprint = serverPublicKey->Fingerprint;

	HRESULT result;
	ReturnIfFailed(result, MakeAndInitialize<NativeBuffer>(&m_innerDataBuffer, sizeof(UINT32) + 2 * sizeof(TLAuthNonce16) + sizeof(TLAuthNonce32) + 28 + SHA_DIGEST_LENGTH));

	ComPtr<TLBinaryWriter> innerDataWriter;
	ReturnIfFailed(result, MakeAndInitialize<TLBinaryWriter>(&innerDataWriter, m_innerDataBuffer.Get()));
	ReturnIfFailed(result, innerDataWriter->WriteUInt32(0x83c95aec));
	ReturnIfFailed(result, innerDataWriter->WriteBuffer(pq, sizeof(TLAuthPQ)));
	ReturnIfFailed(result, innerDataWriter->WriteBuffer(m_p, sizeof(m_p)));
	ReturnIfFailed(result, innerDataWriter->WriteBuffer(m_q, sizeof(m_q)));
	ReturnIfFailed(result, innerDataWriter->WriteRawBuffer(sizeof(m_nonce), m_nonce));
	ReturnIfFailed(result, innerDataWriter->WriteRawBuffer(sizeof(m_serverNonce), m_serverNonce));
	ReturnIfFailed(result, innerDataWriter->WriteRawBuffer(sizeof(m_newNonce), m_newNonce));

	return S_OK;
}

HRESULT TLReqDHParams::WriteBody(ITLBinaryWriterEx* writer)
{
	return S_OK;
}


TLReqPQ::TLReqPQ()
{
	RAND_bytes(m_nonce, sizeof(m_nonce));
}

TLReqPQ::~TLReqPQ()
{
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

	if (constructor != 0x1cb5c415)
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