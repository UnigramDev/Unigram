#include "pch.h"
#include "Helpers\LibraryHelper.h"
#include "NetworkExtensions.h"

static LibraryInstance s_iphlpapi(L"Iphlpapi.dll");

NTSTATUS WINAPI NotifyIpInterfaceChange(ADDRESS_FAMILY Family, PIPINTERFACE_CHANGE_CALLBACK Callback, PVOID CallerContext, BOOLEAN InitialNotification, HANDLE* NotificationHandle)
{
	typedef NTSTATUS(WINAPI *pNotifyIpInterfaceChange)(_In_ ADDRESS_FAMILY, _In_ PIPINTERFACE_CHANGE_CALLBACK, _In_opt_ PVOID, _In_ BOOLEAN, _Inout_ HANDLE*);
	static const auto procNotifyIpInterfaceChange = s_iphlpapi.GetMethod<pNotifyIpInterfaceChange>("NotifyIpInterfaceChange");

	return procNotifyIpInterfaceChange(Family, Callback, CallerContext, InitialNotification, NotificationHandle);
}

NTSTATUS WINAPI CancelMibChangeNotify2(HANDLE NotificationHandle)
{
	typedef NTSTATUS(WINAPI *pCancelMibChangeNotify2)(_In_ HANDLE);
	static const auto procCancelMibChangeNotify2 = s_iphlpapi.GetMethod<pCancelMibChangeNotify2>("CancelMibChangeNotify2");

	return procCancelMibChangeNotify2(NotificationHandle);
}

NTSTATUS WINAPI ConvertInterfaceLuidToName(const NET_LUID* InterfaceLuid, PWSTR InterfaceName, SIZE_T Length)
{
	typedef NTSTATUS(WINAPI *pConvertInterfaceLuidToName)(_In_ const NET_LUID*, _Out_ PWSTR, _In_ SIZE_T);
	static const auto procConvertInterfaceLuidToName = s_iphlpapi.GetMethod<pConvertInterfaceLuidToName>("ConvertInterfaceLuidToNameW");

	return procConvertInterfaceLuidToName(InterfaceLuid, InterfaceName, Length);
}


NTSTATUS WINAPI ConvertInterfaceGuidToLuid(const GUID* InterfaceGuid, PNET_LUID InterfaceLuid)
{
	typedef NTSTATUS(WINAPI *pConvertInterfaceGuidToLuid)(_In_ const GUID*, _Out_ PNET_LUID);
	static const auto procConvertInterfaceGuidToLuid = s_iphlpapi.GetMethod<pConvertInterfaceGuidToLuid>("ConvertInterfaceGuidToLuid");

	return procConvertInterfaceGuidToLuid(InterfaceGuid, InterfaceLuid);
}

NTSTATUS WINAPI ConvertInterfaceLuidToIndex(const NET_LUID* InterfaceLuid, PNET_IFINDEX InterfaceIndex)
{
	typedef NTSTATUS(WINAPI *pConvertInterfaceLuidToIndex)(_In_ const NET_LUID*, _Out_ PNET_IFINDEX);
	static const auto procConvertInterfaceLuidToIndex = s_iphlpapi.GetMethod<pConvertInterfaceLuidToIndex>("ConvertInterfaceLuidToIndex");

	return procConvertInterfaceLuidToIndex(InterfaceLuid, InterfaceIndex);
}

NTSTATUS WINAPI GetIpInterfaceEntry(PMIB_IPINTERFACE_ROW Row)
{
	typedef NTSTATUS(WINAPI *pGetIpInterfaceEntry)(_Inout_ PMIB_IPINTERFACE_ROW);
	static const auto procGetIpInterfaceEntry = s_iphlpapi.GetMethod<pGetIpInterfaceEntry>("GetIpInterfaceEntry");

	return procGetIpInterfaceEntry(Row);
}

VOID WINAPI InitializeIpInterfaceEntry(PMIB_IPINTERFACE_ROW Row)
{
	typedef VOID(WINAPI *pInitializeIpInterfaceEntry)(_Inout_ PMIB_IPINTERFACE_ROW);
	static const auto procInitializeIpInterfaceEntry = s_iphlpapi.GetMethod<pInitializeIpInterfaceEntry>("InitializeIpInterfaceEntry");

	return procInitializeIpInterfaceEntry(Row);
}

DWORD WINAPI GetBestInterfaceEx(struct sockaddr* pDestAddr, PDWORD pdwBestIfIndex)
{
	typedef DWORD(WINAPI *pGetBestInterfaceEx)(_In_ struct sockaddr*, _Out_ PDWORD);
	static const auto procGetBestInterfaceEx = s_iphlpapi.GetMethod<pGetBestInterfaceEx>("GetBestInterfaceEx");

	return procGetBestInterfaceEx(pDestAddr, pdwBestIfIndex);
}

DWORD WINAPI GetPerAdapterInfo(ULONG IfIndex, PIP_PER_ADAPTER_INFO pPerAdapterInfo, PULONG pOutBufLen)
{
	typedef DWORD(WINAPI *pGetPerAdapterInfo)(_In_ ULONG, _Out_ PIP_PER_ADAPTER_INFO, _In_ PULONG);
	static const auto procGetPerAdapterInfo = s_iphlpapi.GetMethod<pGetPerAdapterInfo>("GetPerAdapterInfo");

	return procGetPerAdapterInfo(IfIndex, pPerAdapterInfo, pOutBufLen);
}

ULONG WINAPI GetAdaptersAddresses(ULONG Family, ULONG Flags, PVOID Reserved, PIP_ADAPTER_ADDRESSES AdapterAddresses, PULONG SizePointer)
{
	typedef ULONG(WINAPI *pGetAdaptersAddresses)(_In_ ULONG, _In_ ULONG, _In_ PVOID, _Inout_ PIP_ADAPTER_ADDRESSES, _Inout_ PULONG);
	static const auto procGetAdaptersAddresses = s_iphlpapi.GetMethod<pGetAdaptersAddresses>("GetAdaptersAddresses");

	return procGetAdaptersAddresses(Family, Flags, Reserved, AdapterAddresses, SizePointer);
}

ULONG WINAPI GetTcpTable2(_Out_writes_bytes_opt_(*SizePointer) PMIB_TCPTABLE2 TcpTable, _Inout_ PULONG SizePointer, _In_ BOOL Order)
{
	typedef ULONG(WINAPI *pGetTcpTable2)(_In_ PMIB_TCPTABLE2, _Inout_ PULONG, _In_ BOOL);
	static const auto procGetTcpTable2 = s_iphlpapi.GetMethod<pGetTcpTable2>("GetTcpTable2");

	return procGetTcpTable2(TcpTable, SizePointer, Order);
}