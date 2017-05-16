#include "pch.h"
#include "TLProtocolScheme.h"
#include "ConnectionManager.h"

using namespace Telegram::Api::Native;
using namespace Telegram::Api::Native::TL;

HRESULT TLObjectWithQuery::RuntimeClassInitialize(ITLObject* query)
{
	if (query == nullptr)
	{
		return E_POINTER;
	}

	m_query = query;
	return S_OK;
}

HRESULT TLObjectWithQuery::get_Query(ITLObject** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	return m_query.CopyTo(value);
}


HRESULT TLInvokeWithLayerObject::RuntimeClassInitialize(ITLObject* query)
{
	return TLObjectWithQuery::RuntimeClassInitialize(query);
}

HRESULT TLInvokeWithLayerObject::ReadBody(ITLBinaryReaderEx* reader)
{
	return E_NOTIMPL;
}

HRESULT TLInvokeWithLayerObject::WriteBody(ITLBinaryWriterEx* writer)
{
	HRESULT result;
	ReturnIfFailed(result, writer->WriteInt32(TELEGRAM_API_NATIVE_LAYER));

	return TLObjectWithQuery::Write(writer);
}


HRESULT TLInitConnectionObject::RuntimeClassInitialize(IUserConfiguration* userConfiguration, ITLObject* query)
{
	if (userConfiguration == nullptr || query == nullptr)
	{
		return E_POINTER;
	}

	m_userConfiguration = userConfiguration;
	return TLObjectWithQuery::RuntimeClassInitialize(query);
}

HRESULT TLInitConnectionObject::ReadBody(ITLBinaryReaderEx* reader)
{
	return E_NOTIMPL;
}

HRESULT TLInitConnectionObject::WriteBody(ITLBinaryWriterEx* writer)
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