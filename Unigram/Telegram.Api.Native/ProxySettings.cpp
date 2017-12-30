#include "pch.h"
#include "ProxySettings.h"
#include "Helpers\COMHelper.h"

using namespace Telegram::Api::Native;

ActivatableClassWithFactory(ProxySettings, ProxySettingsFactory);


HRESULT ProxyCredentials::get_UserName(HSTRING* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	return m_userName.CopyTo(value);
}

HRESULT ProxyCredentials::get_Password(HSTRING* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	return m_password.CopyTo(value);
}

HRESULT ProxyCredentials::RuntimeClassInitialize(HSTRING userName, HSTRING password)
{
	if (userName == nullptr || password == nullptr)
	{
		return E_INVALIDARG;
	}

	HRESULT result;
	ReturnIfFailed(result, m_userName.Set(userName));

	return m_password.Set(password);
}


ProxySettings::ProxySettings() :
	m_port(0)
{
}

ProxySettings::~ProxySettings()
{
}

HRESULT ProxySettings::get_Host(HSTRING* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	return m_host.CopyTo(value);
}

HRESULT ProxySettings::get_Port(UINT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_port;
	return S_OK;
}

HRESULT ProxySettings::get_Credentials(IProxyCredentials** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	return m_credentials.CopyTo(value);
}

HRESULT ProxySettings::RuntimeClassInitialize(HSTRING host, UINT32 port, IProxyCredentials* credentials)
{
	if (host == nullptr)
	{
		return E_INVALIDARG;
	}

	m_port = port;
	m_credentials = credentials;
	return m_host.Set(host);
}


HRESULT ProxySettingsFactory::CreateInstance(HSTRING host, UINT32 port, IProxySettings** value)
{
	return MakeAndInitialize<ProxySettings>(value, host, port, nullptr);
}

HRESULT ProxySettingsFactory::CreateInstanceWithCredentials(HSTRING host, UINT32 port, HSTRING userName, HSTRING password, IProxySettings** value)
{
	HRESULT result;
	ComPtr<ProxyCredentials> credentials;
	ReturnIfFailed(result, MakeAndInitialize<ProxyCredentials>(&credentials, userName, password));

	return MakeAndInitialize<ProxySettings>(value, host, port, credentials.Get());
}