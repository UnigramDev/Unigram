#include "pch.h"
#include <stdlib.h>
#include <windows.system.profile.h>
#include <windows.system.userprofile.h>
#include <Windows.ApplicationModel.h>
#include <Windows.Security.ExchangeActiveSyncProvisioning.h>
#include "UserConfiguration.h"
#include "Helpers\COMHelper.h"

using namespace Telegram::Api::Native;


UserConfiguration::UserConfiguration() :
	m_appId(0)
{
}

UserConfiguration::~UserConfiguration()
{
}

HRESULT UserConfiguration::RuntimeClassInitialize()
{
	HRESULT result;
	ComPtr<ABI::Windows::System::Profile::IAnalyticsInfoStatics> analiticsInfo;
	ReturnIfFailed(result, Windows::Foundation::GetActivationFactory(HStringReference(RuntimeClass_Windows_System_Profile_AnalyticsInfo).Get(), &analiticsInfo));

	ComPtr<ABI::Windows::System::Profile::IAnalyticsVersionInfo> versionInfo;
	ReturnIfFailed(result, analiticsInfo->get_VersionInfo(&versionInfo));

	HString deviceFamilyVersion;
	ReturnIfFailed(result, versionInfo->get_DeviceFamilyVersion(deviceFamilyVersion.GetAddressOf()));

	UINT64 version = wcstoll(deviceFamilyVersion.GetRawBuffer(nullptr), nullptr, 0);
	ReturnIfFailed(result, FormatVersion((version & 0xFFFF000000000000LL) >> 48, (version & 0x0000FFFF00000000LL) >> 32,
		(version & 0x00000000FFFF0000LL) >> 16, version & 0x000000000000FFFFLL, m_systemVersion));

	ComPtr<ABI::Windows::System::UserProfile::IGlobalizationPreferencesStatics> globalizationPreferences;
	ReturnIfFailed(result, Windows::Foundation::GetActivationFactory(HStringReference(RuntimeClass_Windows_System_UserProfile_GlobalizationPreferences).Get(), &globalizationPreferences));

	ComPtr<__FIVectorView_1_HSTRING> languages;
	ReturnIfFailed(result, globalizationPreferences->get_Languages(&languages));
	ReturnIfFailed(result, languages->GetAt(0, m_language.GetAddressOf()));

	ComPtr<ABI::Windows::ApplicationModel::IPackageStatics> packageFactory;
	ReturnIfFailed(result, Windows::Foundation::GetActivationFactory(HStringReference(RuntimeClass_Windows_ApplicationModel_Package).Get(), &packageFactory));

	ComPtr<ABI::Windows::ApplicationModel::IPackage> package;
	ReturnIfFailed(result, packageFactory->get_Current(&package));

	ComPtr<ABI::Windows::ApplicationModel::IPackageId> packageId;
	ReturnIfFailed(result, package->get_Id(&packageId));

	ABI::Windows::ApplicationModel::PackageVersion packageVersion;
	ReturnIfFailed(result, packageId->get_Version(&packageVersion));
	ReturnIfFailed(result, FormatVersion(packageVersion.Major, packageVersion.Minor, packageVersion.Build, packageVersion.Revision, m_appVersion));

	ComPtr<ABI::Windows::Security::ExchangeActiveSyncProvisioning::IEasClientDeviceInformation> easClientDeviceInformation;
	ReturnIfFailed(result, Windows::Foundation::ActivateInstance(HStringReference(RuntimeClass_Windows_Security_ExchangeActiveSyncProvisioning_EasClientDeviceInformation).Get(), &easClientDeviceInformation));
	ReturnIfFailed(result, easClientDeviceInformation->get_SystemProductName(m_deviceModel.GetAddressOf()));

	UINT32 deviceModelLength;
	m_deviceModel.GetRawBuffer(&deviceModelLength);

	if (deviceModelLength == 0)
	{
		ReturnIfFailed(result, easClientDeviceInformation->get_FriendlyName(m_deviceModel.GetAddressOf()));
	}

	return m_langPack.Set(L"android");
}

HRESULT UserConfiguration::get_AppId(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_appId;
	return S_OK;
}

HRESULT UserConfiguration::put_AppId(INT32 value)
{
	m_appId = value;
	return S_OK;
}

HRESULT UserConfiguration::get_DeviceModel(HSTRING* value)
{
	return m_deviceModel.CopyTo(value);
}

HRESULT UserConfiguration::put_DeviceModel(HSTRING value)
{
	return m_deviceModel.Set(value);
}

HRESULT UserConfiguration::get_SystemVersion(HSTRING* value)
{
	return m_systemVersion.CopyTo(value);
}

HRESULT UserConfiguration::put_SystemVersion(HSTRING value)
{
	return m_systemVersion.Set(value);
}

HRESULT UserConfiguration::get_AppVersion(HSTRING* value)
{
	return m_appVersion.CopyTo(value);
}

HRESULT UserConfiguration::put_AppVersion(HSTRING value)
{
	return m_appVersion.Set(value);
}

HRESULT UserConfiguration::get_Language(HSTRING* value)
{
	return m_language.CopyTo(value);
}

HRESULT UserConfiguration::put_Language(HSTRING value)
{
	return m_language.Set(value);
}

HRESULT UserConfiguration::get_LangPack(HSTRING* value)
{
	return m_langPack.CopyTo(value);
}

HRESULT UserConfiguration::put_LangPack(HSTRING value)
{
	return m_langPack.Set(value);
}

HRESULT UserConfiguration::FormatVersion(UINT64 major, UINT64 minor, UINT64 build, UINT64 revision, HString& version)
{
	WCHAR versionBuffer[100];
	auto length = swprintf_s(versionBuffer, L"%I64u.%I64u.%I64u.%I64u", major, minor, build, revision);

	return version.Set(versionBuffer, length);
}