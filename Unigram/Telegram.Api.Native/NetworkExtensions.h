#pragma once
#include <winsock2.h>
#include <iphlpapi.h>
#include <IPTypes.h>

#ifndef NDIS_IF_MAX_STRING_SIZE
#define NDIS_IF_MAX_STRING_SIZE 256 
#endif

#ifndef NETIOAPI_API
#define NETIOAPI_API NTSTATUS WINAPI
#endif 

#define GAA_FLAG_SKIP_UNICAST 0x0001
#define GAA_FLAG_SKIP_ANYCAST 0x0002
#define GAA_FLAG_SKIP_MULTICAST 0x0004
#define GAA_FLAG_SKIP_DNS_SERVER 0x0008
#define GAA_FLAG_INCLUDE_PREFIX 0x0010
#define GAA_FLAG_SKIP_FRIENDLY_NAME 0x0020
#define GAA_FLAG_INCLUDE_WINS_INFO 0x0040
#define GAA_FLAG_INCLUDE_GATEWAYS 0x0080
#define GAA_FLAG_INCLUDE_ALL_INTERFACES 0x0100
#define GAA_FLAG_INCLUDE_ALL_COMPARTMENTS 0x0200
#define GAA_FLAG_INCLUDE_TUNNEL_BINDINGORDER 0x0400


typedef _Return_type_success_(return >= 0) LONG NTSTATUS;
typedef ULONG NET_IFINDEX, *PNET_IFINDEX;
typedef UINT16 NET_IFTYPE, *PNET_IFTYPE;

typedef enum _MIB_NOTIFICATION_TYPE
{
	MibParameterNotification = 0,
	MibAddInstance = 1,
	MibDeleteInstance = 2,
	MibInitialNotification = 3
} MIB_NOTIFICATION_TYPE, *PMIB_NOTIFICATION_TYPE;


typedef struct _MIB_IPINTERFACE_ROW {
	ADDRESS_FAMILY Family;
	NET_LUID InterfaceLuid;
	NET_IFINDEX InterfaceIndex;
	ULONG MaxReassemblySize;
	ULONG64 InterfaceIdentifier;
	ULONG MinRouterAdvertisementInterval;
	ULONG MaxRouterAdvertisementInterval;
	BOOLEAN AdvertisingEnabled;
	BOOLEAN ForwardingEnabled;
	BOOLEAN WeakHostSend;
	BOOLEAN WeakHostReceive;
	BOOLEAN UseAutomaticMetric;
	BOOLEAN UseNeighborUnreachabilityDetection;
	BOOLEAN ManagedAddressConfigurationSupported;
	BOOLEAN OtherStatefulConfigurationSupported;
	BOOLEAN AdvertiseDefaultRoute;
	NL_ROUTER_DISCOVERY_BEHAVIOR   RouterDiscoveryBehavior;
	ULONG DadTransmits;
	ULONG BaseReachableTime;
	ULONG RetransmitTime;
	ULONG PathMtuDiscoveryTimeout;
	NL_LINK_LOCAL_ADDRESS_BEHAVIOR LinkLocalAddressBehavior;
	ULONG LinkLocalAddressTimeout;
	ULONG ZoneIndices[ScopeLevelCount];
	ULONG SitePrefixLength;
	ULONG Metric;
	ULONG NlMtu;
	BOOLEAN Connected;
	BOOLEAN SupportsWakeUpPatterns;
	BOOLEAN SupportsNeighborDiscovery;
	BOOLEAN SupportsRouterDiscovery;
	ULONG ReachableTime;
	NL_INTERFACE_OFFLOAD_ROD TransmitOffload;
	NL_INTERFACE_OFFLOAD_ROD ReceiveOffload;
	BOOLEAN DisableDefaultRoutes;
} MIB_IPINTERFACE_ROW, *PMIB_IPINTERFACE_ROW;

typedef VOID(WINAPI *PIPINTERFACE_CHANGE_CALLBACK) (_In_ PVOID CallerContext, _In_ PMIB_IPINTERFACE_ROW Row OPTIONAL, _In_ MIB_NOTIFICATION_TYPE NotificationType);


NETIOAPI_API NotifyIpInterfaceChange(_In_ ADDRESS_FAMILY Family, _In_ PIPINTERFACE_CHANGE_CALLBACK Callback, _In_opt_ PVOID CallerContext, _In_ BOOLEAN InitialNotification, _Inout_ HANDLE* NotificationHandle);
NETIOAPI_API CancelMibChangeNotify2(_In_ HANDLE NotificationHandle);
NETIOAPI_API ConvertInterfaceLuidToName(_In_ const NET_LUID* InterfaceLuid, _Out_ PWSTR InterfaceName, _In_ SIZE_T Length);
NETIOAPI_API ConvertInterfaceGuidToLuid(_In_ const GUID* InterfaceGuid, _Out_ PNET_LUID InterfaceLuid);
NETIOAPI_API ConvertInterfaceLuidToIndex(_In_ const NET_LUID* InterfaceLuid, _Out_ PNET_IFINDEX InterfaceIndex);
NETIOAPI_API GetIpInterfaceEntry(_Inout_ PMIB_IPINTERFACE_ROW Row);
VOID WINAPI InitializeIpInterfaceEntry(_Inout_ PMIB_IPINTERFACE_ROW Row);
DWORD WINAPI GetBestInterfaceEx(_In_  struct sockaddr* pDestAddr, _Out_ PDWORD pdwBestIfIndex);
DWORD WINAPI GetPerAdapterInfo(_In_ ULONG IfIndex, _Out_ PIP_PER_ADAPTER_INFO pPerAdapterInfo, _In_ PULONG pOutBufLen);
ULONG WINAPI GetAdaptersAddresses(_In_ ULONG Family, _In_ ULONG Flags, _In_ PVOID Reserved, _Inout_ PIP_ADAPTER_ADDRESSES AdapterAddresses, _Inout_ PULONG SizePointer);
ULONG WINAPI GetTcpTable2( _Out_writes_bytes_opt_(*SizePointer) PMIB_TCPTABLE2 TcpTable, _Inout_ PULONG SizePointer, _In_ BOOL Order);