#include "pch.h"
#include "TLProtocolScheme.h"
#include "ConnectionManager.h"

using namespace Telegram::Api::Native;
using namespace Telegram::Api::Native::TL;

ActivatableClassWithFactory(TLError, TLErrorFactory);

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


HRESULT TLInvokeWithLayer::RuntimeClassInitialize(ITLObject* query)
{
	return TLObjectWithQuery::RuntimeClassInitialize(query);
}

HRESULT TLInvokeWithLayer::ReadBody(ITLBinaryReaderEx* reader)
{
	return E_NOTIMPL;
}

HRESULT TLInvokeWithLayer::WriteBody(ITLBinaryWriterEx* writer)
{
	HRESULT result;
	ReturnIfFailed(result, writer->WriteInt32(TELEGRAM_API_NATIVE_LAYER));

	return TLObjectWithQuery::Write(writer);
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

HRESULT TLInitConnection::ReadBody(ITLBinaryReaderEx* reader)
{
	return E_NOTIMPL;
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

	return TLObjectWithQuery::Write(writer);
}


HRESULT TLErrorFactory::CreateTLError(UINT32 code, HSTRING text, ITLError** instance)
{
	return MakeAndInitialize<TLError>(instance, code, text);
}