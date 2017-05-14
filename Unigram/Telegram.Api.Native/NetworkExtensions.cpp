#include "pch.h"
#include "Helpers\LibraryHelper.h"
#include "NetworkExtensions.h"

static LibraryInstance s_iphlpapi(L"Iphlpapi.dll");

NTSTATUS NotifyIpInterfaceChange(ADDRESS_FAMILY Family, PIPINTERFACE_CHANGE_CALLBACK Callback, PVOID CallerContext, BOOLEAN InitialNotification, HANDLE* NotificationHandle)
{
	typedef NTSTATUS(WINAPI *pNotifyIpInterfaceChange)(_In_ ADDRESS_FAMILY, _In_ PIPINTERFACE_CHANGE_CALLBACK, _In_opt_ PVOID, _In_ BOOLEAN, _Inout_ HANDLE*);
	static const auto procNotifyIpInterfaceChange = s_iphlpapi.GetMethod<pNotifyIpInterfaceChange>("NotifyIpInterfaceChange");

	return procNotifyIpInterfaceChange(Family, Callback, CallerContext, InitialNotification, NotificationHandle);
}

NTSTATUS CancelMibChangeNotify2(HANDLE NotificationHandle)
{
	typedef NTSTATUS(WINAPI *pCancelMibChangeNotify2)(_In_ HANDLE);
	static const auto procCancelMibChangeNotify2 = s_iphlpapi.GetMethod<pCancelMibChangeNotify2>("CancelMibChangeNotify2");

	return procCancelMibChangeNotify2(NotificationHandle);
}

NTSTATUS ConvertInterfaceLuidToName(_In_ const NET_LUID* InterfaceLuid, _Out_ PWSTR InterfaceName, _In_ SIZE_T Length)
{
	typedef NTSTATUS(WINAPI *pConvertInterfaceLuidToName)(_In_ const NET_LUID*, _Out_ PWSTR, _In_ SIZE_T);
	static const auto procConvertInterfaceLuidToName = s_iphlpapi.GetMethod<pConvertInterfaceLuidToName>("ConvertInterfaceLuidToNameW");

	return procConvertInterfaceLuidToName(InterfaceLuid, InterfaceName, Length);
}

DWORD GetBestInterfaceEx(_In_ struct sockaddr* pDestAddr, _Out_ PDWORD pdwBestIfIndex)
{
	typedef DWORD(WINAPI *pGetBestInterfaceEx)(_In_ struct sockaddr*, _Out_ PDWORD);
	static const auto procGetBestInterfaceEx = s_iphlpapi.GetMethod<pGetBestInterfaceEx>("GetBestInterfaceEx");

	return procGetBestInterfaceEx(pDestAddr, pdwBestIfIndex);
}