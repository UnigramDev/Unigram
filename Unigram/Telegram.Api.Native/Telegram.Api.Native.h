

/* this ALWAYS GENERATED file contains the definitions for the interfaces */


 /* File created by MIDL compiler version 8.01.0622 */
/* at Tue Jan 19 04:14:07 2038
 */
/* Compiler settings for C:\Users\loren\AppData\Local\Temp\Telegram.Api.Native.idl-4dd622b9:
    Oicf, W1, Zp8, env=Win32 (32b run), target_arch=X86 8.01.0622 
    protocol : dce , ms_ext, c_ext, robust
    error checks: allocation ref bounds_check enum stub_data 
    VC __declspec() decoration level: 
         __declspec(uuid()), __declspec(selectany), __declspec(novtable)
         DECLSPEC_UUID(), MIDL_INTERFACE()
*/
/* @@MIDL_FILE_HEADING(  ) */

#pragma warning( disable: 4049 )  /* more than 64k source lines */


/* verify that the <rpcndr.h> version is high enough to compile this file*/
#ifndef __REQUIRED_RPCNDR_H_VERSION__
#define __REQUIRED_RPCNDR_H_VERSION__ 475
#endif

#include "rpc.h"
#include "rpcndr.h"

#ifndef __RPCNDR_H_VERSION__
#error this stub requires an updated version of <rpcndr.h>
#endif /* __RPCNDR_H_VERSION__ */

#ifndef COM_NO_WINDOWS_H
#include "windows.h"
#include "ole2.h"
#endif /*COM_NO_WINDOWS_H*/

#ifndef __Telegram2EApi2ENative_h__
#define __Telegram2EApi2ENative_h__

#if defined(_MSC_VER) && (_MSC_VER >= 1020)
#pragma once
#endif

#if defined(__cplusplus)
#if defined(__MIDL_USE_C_ENUM)
#define MIDL_ENUM enum
#else
#define MIDL_ENUM enum class
#endif
#endif


/* Forward Declarations */ 

#ifndef ____FIIterator_1_Telegram__CApi__CNative__CDatacenter_FWD_DEFINED__
#define ____FIIterator_1_Telegram__CApi__CNative__CDatacenter_FWD_DEFINED__
typedef interface __FIIterator_1_Telegram__CApi__CNative__CDatacenter __FIIterator_1_Telegram__CApi__CNative__CDatacenter;

#endif 	/* ____FIIterator_1_Telegram__CApi__CNative__CDatacenter_FWD_DEFINED__ */


#ifndef ____FIIterable_1_Telegram__CApi__CNative__CDatacenter_FWD_DEFINED__
#define ____FIIterable_1_Telegram__CApi__CNative__CDatacenter_FWD_DEFINED__
typedef interface __FIIterable_1_Telegram__CApi__CNative__CDatacenter __FIIterable_1_Telegram__CApi__CNative__CDatacenter;

#endif 	/* ____FIIterable_1_Telegram__CApi__CNative__CDatacenter_FWD_DEFINED__ */


#ifndef ____FIVectorView_1_Telegram__CApi__CNative__CDatacenter_FWD_DEFINED__
#define ____FIVectorView_1_Telegram__CApi__CNative__CDatacenter_FWD_DEFINED__
typedef interface __FIVectorView_1_Telegram__CApi__CNative__CDatacenter __FIVectorView_1_Telegram__CApi__CNative__CDatacenter;

#endif 	/* ____FIVectorView_1_Telegram__CApi__CNative__CDatacenter_FWD_DEFINED__ */


#ifndef ____x_ABI_CTelegram_CApi_CNative_CTL_CITLObject_FWD_DEFINED__
#define ____x_ABI_CTelegram_CApi_CNative_CTL_CITLObject_FWD_DEFINED__
typedef interface __x_ABI_CTelegram_CApi_CNative_CTL_CITLObject __x_ABI_CTelegram_CApi_CNative_CTL_CITLObject;

#ifdef __cplusplus
namespace ABI {
    namespace Telegram {
        namespace Api {
            namespace Native {
                namespace TL {
                    interface ITLObject;
                } /* end namespace */
            } /* end namespace */
        } /* end namespace */
    } /* end namespace */
} /* end namespace */

#endif /* __cplusplus */

#endif 	/* ____x_ABI_CTelegram_CApi_CNative_CTL_CITLObject_FWD_DEFINED__ */


#ifndef ____x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader_FWD_DEFINED__
#define ____x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader_FWD_DEFINED__
typedef interface __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader;

#ifdef __cplusplus
namespace ABI {
    namespace Telegram {
        namespace Api {
            namespace Native {
                namespace TL {
                    interface ITLBinaryReader;
                } /* end namespace */
            } /* end namespace */
        } /* end namespace */
    } /* end namespace */
} /* end namespace */

#endif /* __cplusplus */

#endif 	/* ____x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader_FWD_DEFINED__ */


#ifndef ____x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter_FWD_DEFINED__
#define ____x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter_FWD_DEFINED__
typedef interface __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter;

#ifdef __cplusplus
namespace ABI {
    namespace Telegram {
        namespace Api {
            namespace Native {
                namespace TL {
                    interface ITLBinaryWriter;
                } /* end namespace */
            } /* end namespace */
        } /* end namespace */
    } /* end namespace */
} /* end namespace */

#endif /* __cplusplus */

#endif 	/* ____x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter_FWD_DEFINED__ */


#ifndef ____x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectConstructorDelegate_FWD_DEFINED__
#define ____x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectConstructorDelegate_FWD_DEFINED__
typedef interface __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectConstructorDelegate __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectConstructorDelegate;

#ifdef __cplusplus
namespace ABI {
    namespace Telegram {
        namespace Api {
            namespace Native {
                namespace TL {
                    interface ITLObjectConstructorDelegate;
                } /* end namespace */
            } /* end namespace */
        } /* end namespace */
    } /* end namespace */
} /* end namespace */

#endif /* __cplusplus */

#endif 	/* ____x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectConstructorDelegate_FWD_DEFINED__ */


#ifndef ____x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializerStatics_FWD_DEFINED__
#define ____x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializerStatics_FWD_DEFINED__
typedef interface __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializerStatics __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializerStatics;

#ifdef __cplusplus
namespace ABI {
    namespace Telegram {
        namespace Api {
            namespace Native {
                namespace TL {
                    interface ITLObjectSerializerStatics;
                } /* end namespace */
            } /* end namespace */
        } /* end namespace */
    } /* end namespace */
} /* end namespace */

#endif /* __cplusplus */

#endif 	/* ____x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializerStatics_FWD_DEFINED__ */


#ifndef ____x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializer_FWD_DEFINED__
#define ____x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializer_FWD_DEFINED__
typedef interface __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializer __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializer;

#ifdef __cplusplus
namespace ABI {
    namespace Telegram {
        namespace Api {
            namespace Native {
                namespace TL {
                    interface ITLObjectSerializer;
                } /* end namespace */
            } /* end namespace */
        } /* end namespace */
    } /* end namespace */
} /* end namespace */

#endif /* __cplusplus */

#endif 	/* ____x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializer_FWD_DEFINED__ */


#ifndef ____x_ABI_CTelegram_CApi_CNative_CTL_CITLError_FWD_DEFINED__
#define ____x_ABI_CTelegram_CApi_CNative_CTL_CITLError_FWD_DEFINED__
typedef interface __x_ABI_CTelegram_CApi_CNative_CTL_CITLError __x_ABI_CTelegram_CApi_CNative_CTL_CITLError;

#ifdef __cplusplus
namespace ABI {
    namespace Telegram {
        namespace Api {
            namespace Native {
                namespace TL {
                    interface ITLError;
                } /* end namespace */
            } /* end namespace */
        } /* end namespace */
    } /* end namespace */
} /* end namespace */

#endif /* __cplusplus */

#endif 	/* ____x_ABI_CTelegram_CApi_CNative_CTL_CITLError_FWD_DEFINED__ */


#ifndef ____x_ABI_CTelegram_CApi_CNative_CTL_CITLErrorFactory_FWD_DEFINED__
#define ____x_ABI_CTelegram_CApi_CNative_CTL_CITLErrorFactory_FWD_DEFINED__
typedef interface __x_ABI_CTelegram_CApi_CNative_CTL_CITLErrorFactory __x_ABI_CTelegram_CApi_CNative_CTL_CITLErrorFactory;

#ifdef __cplusplus
namespace ABI {
    namespace Telegram {
        namespace Api {
            namespace Native {
                namespace TL {
                    interface ITLErrorFactory;
                } /* end namespace */
            } /* end namespace */
        } /* end namespace */
    } /* end namespace */
} /* end namespace */

#endif /* __cplusplus */

#endif 	/* ____x_ABI_CTelegram_CApi_CNative_CTL_CITLErrorFactory_FWD_DEFINED__ */


#ifndef ____x_ABI_CTelegram_CApi_CNative_CITLUnparsedMessage_FWD_DEFINED__
#define ____x_ABI_CTelegram_CApi_CNative_CITLUnparsedMessage_FWD_DEFINED__
typedef interface __x_ABI_CTelegram_CApi_CNative_CITLUnparsedMessage __x_ABI_CTelegram_CApi_CNative_CITLUnparsedMessage;

#ifdef __cplusplus
namespace ABI {
    namespace Telegram {
        namespace Api {
            namespace Native {
                interface ITLUnparsedMessage;
            } /* end namespace */
        } /* end namespace */
    } /* end namespace */
} /* end namespace */

#endif /* __cplusplus */

#endif 	/* ____x_ABI_CTelegram_CApi_CNative_CITLUnparsedMessage_FWD_DEFINED__ */


#ifndef ____x_ABI_CTelegram_CApi_CNative_CISendRequestCompletedCallback_FWD_DEFINED__
#define ____x_ABI_CTelegram_CApi_CNative_CISendRequestCompletedCallback_FWD_DEFINED__
typedef interface __x_ABI_CTelegram_CApi_CNative_CISendRequestCompletedCallback __x_ABI_CTelegram_CApi_CNative_CISendRequestCompletedCallback;

#ifdef __cplusplus
namespace ABI {
    namespace Telegram {
        namespace Api {
            namespace Native {
                interface ISendRequestCompletedCallback;
            } /* end namespace */
        } /* end namespace */
    } /* end namespace */
} /* end namespace */

#endif /* __cplusplus */

#endif 	/* ____x_ABI_CTelegram_CApi_CNative_CISendRequestCompletedCallback_FWD_DEFINED__ */


#ifndef ____x_ABI_CTelegram_CApi_CNative_CIRequestQuickAckReceivedCallback_FWD_DEFINED__
#define ____x_ABI_CTelegram_CApi_CNative_CIRequestQuickAckReceivedCallback_FWD_DEFINED__
typedef interface __x_ABI_CTelegram_CApi_CNative_CIRequestQuickAckReceivedCallback __x_ABI_CTelegram_CApi_CNative_CIRequestQuickAckReceivedCallback;

#ifdef __cplusplus
namespace ABI {
    namespace Telegram {
        namespace Api {
            namespace Native {
                interface IRequestQuickAckReceivedCallback;
            } /* end namespace */
        } /* end namespace */
    } /* end namespace */
} /* end namespace */

#endif /* __cplusplus */

#endif 	/* ____x_ABI_CTelegram_CApi_CNative_CIRequestQuickAckReceivedCallback_FWD_DEFINED__ */


#ifndef ____x_ABI_CTelegram_CApi_CNative_CIUserConfiguration_FWD_DEFINED__
#define ____x_ABI_CTelegram_CApi_CNative_CIUserConfiguration_FWD_DEFINED__
typedef interface __x_ABI_CTelegram_CApi_CNative_CIUserConfiguration __x_ABI_CTelegram_CApi_CNative_CIUserConfiguration;

#ifdef __cplusplus
namespace ABI {
    namespace Telegram {
        namespace Api {
            namespace Native {
                interface IUserConfiguration;
            } /* end namespace */
        } /* end namespace */
    } /* end namespace */
} /* end namespace */

#endif /* __cplusplus */

#endif 	/* ____x_ABI_CTelegram_CApi_CNative_CIUserConfiguration_FWD_DEFINED__ */


#ifndef ____x_ABI_CTelegram_CApi_CNative_CIDatacenter_FWD_DEFINED__
#define ____x_ABI_CTelegram_CApi_CNative_CIDatacenter_FWD_DEFINED__
typedef interface __x_ABI_CTelegram_CApi_CNative_CIDatacenter __x_ABI_CTelegram_CApi_CNative_CIDatacenter;

#ifdef __cplusplus
namespace ABI {
    namespace Telegram {
        namespace Api {
            namespace Native {
                interface IDatacenter;
            } /* end namespace */
        } /* end namespace */
    } /* end namespace */
} /* end namespace */

#endif /* __cplusplus */

#endif 	/* ____x_ABI_CTelegram_CApi_CNative_CIDatacenter_FWD_DEFINED__ */


#ifndef ____x_ABI_CTelegram_CApi_CNative_CIConnection_FWD_DEFINED__
#define ____x_ABI_CTelegram_CApi_CNative_CIConnection_FWD_DEFINED__
typedef interface __x_ABI_CTelegram_CApi_CNative_CIConnection __x_ABI_CTelegram_CApi_CNative_CIConnection;

#ifdef __cplusplus
namespace ABI {
    namespace Telegram {
        namespace Api {
            namespace Native {
                interface IConnection;
            } /* end namespace */
        } /* end namespace */
    } /* end namespace */
} /* end namespace */

#endif /* __cplusplus */

#endif 	/* ____x_ABI_CTelegram_CApi_CNative_CIConnection_FWD_DEFINED__ */


#ifndef ____x_ABI_CTelegram_CApi_CNative_CIConnectionManagerStatics_FWD_DEFINED__
#define ____x_ABI_CTelegram_CApi_CNative_CIConnectionManagerStatics_FWD_DEFINED__
typedef interface __x_ABI_CTelegram_CApi_CNative_CIConnectionManagerStatics __x_ABI_CTelegram_CApi_CNative_CIConnectionManagerStatics;

#ifdef __cplusplus
namespace ABI {
    namespace Telegram {
        namespace Api {
            namespace Native {
                interface IConnectionManagerStatics;
            } /* end namespace */
        } /* end namespace */
    } /* end namespace */
} /* end namespace */

#endif /* __cplusplus */

#endif 	/* ____x_ABI_CTelegram_CApi_CNative_CIConnectionManagerStatics_FWD_DEFINED__ */


#ifndef ____FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable_FWD_DEFINED__
#define ____FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable_FWD_DEFINED__
typedef interface __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable;

#endif 	/* ____FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable_FWD_DEFINED__ */


#ifndef ____FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CTLUnparsedMessage_FWD_DEFINED__
#define ____FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CTLUnparsedMessage_FWD_DEFINED__
typedef interface __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CTLUnparsedMessage __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CTLUnparsedMessage;

#endif 	/* ____FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CTLUnparsedMessage_FWD_DEFINED__ */


#ifndef ____x_ABI_CTelegram_CApi_CNative_CIConnectionManager_FWD_DEFINED__
#define ____x_ABI_CTelegram_CApi_CNative_CIConnectionManager_FWD_DEFINED__
typedef interface __x_ABI_CTelegram_CApi_CNative_CIConnectionManager __x_ABI_CTelegram_CApi_CNative_CIConnectionManager;

#ifdef __cplusplus
namespace ABI {
    namespace Telegram {
        namespace Api {
            namespace Native {
                interface IConnectionManager;
            } /* end namespace */
        } /* end namespace */
    } /* end namespace */
} /* end namespace */

#endif /* __cplusplus */

#endif 	/* ____x_ABI_CTelegram_CApi_CNative_CIConnectionManager_FWD_DEFINED__ */


/* header files for imported files */
#include "Windows.Foundation.h"
#include "Windows.Storage.Streams.h"

#ifdef __cplusplus
extern "C"{
#endif 


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0000 */
/* [local] */ 

#ifdef __cplusplus
} /*extern "C"*/ 
#endif
#include <windows.foundation.collections.h>
#ifdef __cplusplus
extern "C" {
#endif
#ifdef __cplusplus
namespace ABI {
namespace Telegram {
namespace Api {
namespace Native {
class Datacenter;
} /*Native*/
} /*Api*/
} /*Telegram*/
}
#endif

#ifdef __cplusplus
namespace ABI {
namespace Telegram {
namespace Api {
namespace Native {
interface IDatacenter;
} /*Native*/
} /*Api*/
} /*Telegram*/
}
#endif


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0000 */
/* [local] */ 




extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0000_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0000_v0_0_s_ifspec;

/* interface __MIDL_itf_Telegram2EApi2ENative2Eidl_0000_4657 */




/* interface __MIDL_itf_Telegram2EApi2ENative2Eidl_0000_4657 */




extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative2Eidl_0000_4657_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative2Eidl_0000_4657_v0_0_s_ifspec;

/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0001 */
/* [local] */ 

#ifndef DEF___FIIterator_1_Telegram__CApi__CNative__CDatacenter_USE
#define DEF___FIIterator_1_Telegram__CApi__CNative__CDatacenter_USE
#if defined(__cplusplus) && !defined(RO_NO_TEMPLATE_NAME)
} /*extern "C"*/ 
namespace ABI { namespace Windows { namespace Foundation { namespace Collections {
template <>
struct __declspec(uuid("d20aab0e-c566-5029-9883-1b17f8db83bb"))
IIterator<ABI::Telegram::Api::Native::Datacenter*> : IIterator_impl<ABI::Windows::Foundation::Internal::AggregateType<ABI::Telegram::Api::Native::Datacenter*, ABI::Telegram::Api::Native::IDatacenter*>> {
static const wchar_t* z_get_rc_name_impl() {
return L"Windows.Foundation.Collections.IIterator`1<Telegram.Api.Native.Datacenter>"; }
};
typedef IIterator<ABI::Telegram::Api::Native::Datacenter*> __FIIterator_1_Telegram__CApi__CNative__CDatacenter_t;
#define ____FIIterator_1_Telegram__CApi__CNative__CDatacenter_FWD_DEFINED__
#define __FIIterator_1_Telegram__CApi__CNative__CDatacenter ABI::Windows::Foundation::Collections::__FIIterator_1_Telegram__CApi__CNative__CDatacenter_t

/* ABI */ } /* Windows */ } /* Foundation */ } /* Collections */ }
extern "C" {
#endif //__cplusplus
#endif /* DEF___FIIterator_1_Telegram__CApi__CNative__CDatacenter_USE */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0001 */
/* [local] */ 




extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0001_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0001_v0_0_s_ifspec;

/* interface __MIDL_itf_Telegram2EApi2ENative2Eidl_0000_4658 */




/* interface __MIDL_itf_Telegram2EApi2ENative2Eidl_0000_4658 */




extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative2Eidl_0000_4658_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative2Eidl_0000_4658_v0_0_s_ifspec;

/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0002 */
/* [local] */ 

#ifndef DEF___FIIterable_1_Telegram__CApi__CNative__CDatacenter_USE
#define DEF___FIIterable_1_Telegram__CApi__CNative__CDatacenter_USE
#if defined(__cplusplus) && !defined(RO_NO_TEMPLATE_NAME)
} /*extern "C"*/ 
namespace ABI { namespace Windows { namespace Foundation { namespace Collections {
template <>
struct __declspec(uuid("50801597-1c0e-5f11-87b0-cc7a9d5966ad"))
IIterable<ABI::Telegram::Api::Native::Datacenter*> : IIterable_impl<ABI::Windows::Foundation::Internal::AggregateType<ABI::Telegram::Api::Native::Datacenter*, ABI::Telegram::Api::Native::IDatacenter*>> {
static const wchar_t* z_get_rc_name_impl() {
return L"Windows.Foundation.Collections.IIterable`1<Telegram.Api.Native.Datacenter>"; }
};
typedef IIterable<ABI::Telegram::Api::Native::Datacenter*> __FIIterable_1_Telegram__CApi__CNative__CDatacenter_t;
#define ____FIIterable_1_Telegram__CApi__CNative__CDatacenter_FWD_DEFINED__
#define __FIIterable_1_Telegram__CApi__CNative__CDatacenter ABI::Windows::Foundation::Collections::__FIIterable_1_Telegram__CApi__CNative__CDatacenter_t

/* ABI */ } /* Windows */ } /* Foundation */ } /* Collections */ }
extern "C" {
#endif //__cplusplus
#endif /* DEF___FIIterable_1_Telegram__CApi__CNative__CDatacenter_USE */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0002 */
/* [local] */ 




extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0002_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0002_v0_0_s_ifspec;

/* interface __MIDL_itf_Telegram2EApi2ENative2Eidl_0000_4659 */




/* interface __MIDL_itf_Telegram2EApi2ENative2Eidl_0000_4659 */




extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative2Eidl_0000_4659_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative2Eidl_0000_4659_v0_0_s_ifspec;

/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0003 */
/* [local] */ 

#ifndef DEF___FIVectorView_1_Telegram__CApi__CNative__CDatacenter_USE
#define DEF___FIVectorView_1_Telegram__CApi__CNative__CDatacenter_USE
#if defined(__cplusplus) && !defined(RO_NO_TEMPLATE_NAME)
} /*extern "C"*/ 
namespace ABI { namespace Windows { namespace Foundation { namespace Collections {
template <>
struct __declspec(uuid("59e97070-4e4b-5775-9716-c3584529beaa"))
IVectorView<ABI::Telegram::Api::Native::Datacenter*> : IVectorView_impl<ABI::Windows::Foundation::Internal::AggregateType<ABI::Telegram::Api::Native::Datacenter*, ABI::Telegram::Api::Native::IDatacenter*>> {
static const wchar_t* z_get_rc_name_impl() {
return L"Windows.Foundation.Collections.IVectorView`1<Telegram.Api.Native.Datacenter>"; }
};
typedef IVectorView<ABI::Telegram::Api::Native::Datacenter*> __FIVectorView_1_Telegram__CApi__CNative__CDatacenter_t;
#define ____FIVectorView_1_Telegram__CApi__CNative__CDatacenter_FWD_DEFINED__
#define __FIVectorView_1_Telegram__CApi__CNative__CDatacenter ABI::Windows::Foundation::Collections::__FIVectorView_1_Telegram__CApi__CNative__CDatacenter_t

/* ABI */ } /* Windows */ } /* Foundation */ } /* Collections */ }
extern "C" {
#endif //__cplusplus
#endif /* DEF___FIVectorView_1_Telegram__CApi__CNative__CDatacenter_USE */
#ifdef __cplusplus
namespace ABI {
namespace Telegram {
namespace Api {
namespace Native {
class ConnectionManager;
} /*Native*/
} /*Api*/
} /*Telegram*/
}
#endif

#ifdef __cplusplus
namespace ABI {
namespace Telegram {
namespace Api {
namespace Native {
interface IConnectionManager;
} /*Native*/
} /*Api*/
} /*Telegram*/
}
#endif

interface IInspectable;


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0003 */
/* [local] */ 






extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0003_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0003_v0_0_s_ifspec;

/* interface __MIDL_itf_Telegram2EApi2ENative2Eidl_0000_4660 */




/* interface __MIDL_itf_Telegram2EApi2ENative2Eidl_0000_4660 */




extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative2Eidl_0000_4660_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative2Eidl_0000_4660_v0_0_s_ifspec;

/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0004 */
/* [local] */ 

#ifndef DEF___FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable_USE
#define DEF___FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable_USE
#if defined(__cplusplus) && !defined(RO_NO_TEMPLATE_NAME)
} /*extern "C"*/ 
namespace ABI { namespace Windows { namespace Foundation {
template <>
struct __declspec(uuid("11ee97ee-6b16-52b7-a286-d68783596699"))
ITypedEventHandler<ABI::Telegram::Api::Native::ConnectionManager*,IInspectable*> : ITypedEventHandler_impl<ABI::Windows::Foundation::Internal::AggregateType<ABI::Telegram::Api::Native::ConnectionManager*, ABI::Telegram::Api::Native::IConnectionManager*>,IInspectable*> {
static const wchar_t* z_get_rc_name_impl() {
return L"Windows.Foundation.TypedEventHandler`2<Telegram.Api.Native.ConnectionManager, Object>"; }
};
typedef ITypedEventHandler<ABI::Telegram::Api::Native::ConnectionManager*,IInspectable*> __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable_t;
#define ____FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable_FWD_DEFINED__
#define __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable ABI::Windows::Foundation::__FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable_t

/* ABI */ } /* Windows */ } /* Foundation */ }
extern "C" {
#endif //__cplusplus
#endif /* DEF___FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable_USE */
#ifdef __cplusplus
namespace ABI {
namespace Telegram {
namespace Api {
namespace Native {
class TLUnparsedMessage;
} /*Native*/
} /*Api*/
} /*Telegram*/
}
#endif

#ifdef __cplusplus
namespace ABI {
namespace Telegram {
namespace Api {
namespace Native {
interface ITLUnparsedMessage;
} /*Native*/
} /*Api*/
} /*Telegram*/
}
#endif


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0004 */
/* [local] */ 





extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0004_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0004_v0_0_s_ifspec;

/* interface __MIDL_itf_Telegram2EApi2ENative2Eidl_0000_4661 */




/* interface __MIDL_itf_Telegram2EApi2ENative2Eidl_0000_4661 */




extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative2Eidl_0000_4661_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative2Eidl_0000_4661_v0_0_s_ifspec;

/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0005 */
/* [local] */ 

#ifndef DEF___FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CTLUnparsedMessage_USE
#define DEF___FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CTLUnparsedMessage_USE
#if defined(__cplusplus) && !defined(RO_NO_TEMPLATE_NAME)
} /*extern "C"*/ 
namespace ABI { namespace Windows { namespace Foundation {
template <>
struct __declspec(uuid("d21077de-9066-581f-aab4-244b0006f1fb"))
ITypedEventHandler<ABI::Telegram::Api::Native::ConnectionManager*,ABI::Telegram::Api::Native::TLUnparsedMessage*> : ITypedEventHandler_impl<ABI::Windows::Foundation::Internal::AggregateType<ABI::Telegram::Api::Native::ConnectionManager*, ABI::Telegram::Api::Native::IConnectionManager*>,ABI::Windows::Foundation::Internal::AggregateType<ABI::Telegram::Api::Native::TLUnparsedMessage*, ABI::Telegram::Api::Native::ITLUnparsedMessage*>> {
static const wchar_t* z_get_rc_name_impl() {
return L"Windows.Foundation.TypedEventHandler`2<Telegram.Api.Native.ConnectionManager, Telegram.Api.Native.TLUnparsedMessage>"; }
};
typedef ITypedEventHandler<ABI::Telegram::Api::Native::ConnectionManager*,ABI::Telegram::Api::Native::TLUnparsedMessage*> __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CTLUnparsedMessage_t;
#define ____FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CTLUnparsedMessage_FWD_DEFINED__
#define __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CTLUnparsedMessage ABI::Windows::Foundation::__FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CTLUnparsedMessage_t

/* ABI */ } /* Windows */ } /* Foundation */ }
extern "C" {
#endif //__cplusplus
#endif /* DEF___FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CTLUnparsedMessage_USE */
#if !defined(__cplusplus)
#if !defined(__cplusplus)

typedef enum __x_ABI_CTelegram_CApi_CNative_CConnectionType __x_ABI_CTelegram_CApi_CNative_CConnectionType;


#endif /* end if !defined(__cplusplus) */


#endif
#if !defined(__cplusplus)
#if !defined(__cplusplus)

typedef enum __x_ABI_CTelegram_CApi_CNative_CConnectionNeworkType __x_ABI_CTelegram_CApi_CNative_CConnectionNeworkType;


#endif /* end if !defined(__cplusplus) */


#endif
#if !defined(__cplusplus)
#if !defined(__cplusplus)

typedef enum __x_ABI_CTelegram_CApi_CNative_CConnectionState __x_ABI_CTelegram_CApi_CNative_CConnectionState;


#endif /* end if !defined(__cplusplus) */


#endif
#if !defined(__cplusplus)
#if !defined(__cplusplus)

typedef enum __x_ABI_CTelegram_CApi_CNative_CHandshakeState __x_ABI_CTelegram_CApi_CNative_CHandshakeState;


#endif /* end if !defined(__cplusplus) */


#endif
#if !defined(__cplusplus)
#if !defined(__cplusplus)

typedef enum __x_ABI_CTelegram_CApi_CNative_CRequestFlag __x_ABI_CTelegram_CApi_CNative_CRequestFlag;


#endif /* end if !defined(__cplusplus) */


#endif
#if !defined(__cplusplus)
typedef struct __x_ABI_CTelegram_CApi_CNative_CVersion __x_ABI_CTelegram_CApi_CNative_CVersion;

#endif

#ifdef __cplusplus
namespace ABI {
namespace Telegram {
namespace Api {
namespace Native {
class Connection;
} /*Native*/
} /*Api*/
} /*Telegram*/
}
#endif






#ifdef __cplusplus
namespace ABI {
namespace Telegram {
namespace Api {
namespace Native {
namespace TL {
class TLBinaryReader;
} /*TL*/
} /*Native*/
} /*Api*/
} /*Telegram*/
}
#endif
#ifdef __cplusplus
namespace ABI {
namespace Telegram {
namespace Api {
namespace Native {
namespace TL {
class TLBinaryWriter;
} /*TL*/
} /*Native*/
} /*Api*/
} /*Telegram*/
}
#endif
#ifdef __cplusplus
namespace ABI {
namespace Telegram {
namespace Api {
namespace Native {
namespace TL {
class TLObjectSerializer;
} /*TL*/
} /*Native*/
} /*Api*/
} /*Telegram*/
}
#endif

#ifdef __cplusplus
namespace ABI {
namespace Telegram {
namespace Api {
namespace Native {
namespace TL {
class TLError;
} /*TL*/
} /*Native*/
} /*Api*/
} /*Telegram*/
}
#endif




/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0005 */
/* [local] */ 


#ifdef __cplusplus

} /* end extern "C" */
namespace ABI {
    namespace Telegram {
        namespace Api {
            namespace Native {
                
                typedef MIDL_ENUM ConnectionType ConnectionType;
                
            } /* end namespace */
        } /* end namespace */
    } /* end namespace */
} /* end namespace */

extern "C" { 
#endif

#ifdef __cplusplus

} /* end extern "C" */
namespace ABI {
    namespace Telegram {
        namespace Api {
            namespace Native {
                
                typedef MIDL_ENUM ConnectionNeworkType ConnectionNeworkType;
                
            } /* end namespace */
        } /* end namespace */
    } /* end namespace */
} /* end namespace */

extern "C" { 
#endif

#ifdef __cplusplus

} /* end extern "C" */
namespace ABI {
    namespace Telegram {
        namespace Api {
            namespace Native {
                
                typedef MIDL_ENUM ConnectionState ConnectionState;
                
            } /* end namespace */
        } /* end namespace */
    } /* end namespace */
} /* end namespace */

extern "C" { 
#endif

#ifdef __cplusplus

} /* end extern "C" */
namespace ABI {
    namespace Telegram {
        namespace Api {
            namespace Native {
                
                typedef MIDL_ENUM HandshakeState HandshakeState;
                
            } /* end namespace */
        } /* end namespace */
    } /* end namespace */
} /* end namespace */

extern "C" { 
#endif

#ifdef __cplusplus

} /* end extern "C" */
namespace ABI {
    namespace Telegram {
        namespace Api {
            namespace Native {
                
                typedef MIDL_ENUM RequestFlag RequestFlag;
                
            } /* end namespace */
        } /* end namespace */
    } /* end namespace */
} /* end namespace */

extern "C" { 
#endif

#ifdef __cplusplus

} /* end extern "C" */
namespace ABI {
    namespace Telegram {
        namespace Api {
            namespace Native {
                
                typedef struct Version Version;
                
            } /* end namespace */
        } /* end namespace */
    } /* end namespace */
} /* end namespace */

extern "C" { 
#endif













extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0005_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0005_v0_0_s_ifspec;

/* interface __MIDL_itf_Telegram2EApi2ENative2Eidl_0000_4662 */




/* interface __MIDL_itf_Telegram2EApi2ENative2Eidl_0000_4662 */




extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative2Eidl_0000_4662_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative2Eidl_0000_4662_v0_0_s_ifspec;

/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0006 */
/* [local] */ 

#ifndef DEF___FIIterator_1_Telegram__CApi__CNative__CDatacenter
#define DEF___FIIterator_1_Telegram__CApi__CNative__CDatacenter
#if !defined(__cplusplus) || defined(RO_NO_TEMPLATE_NAME)


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0006 */
/* [local] */ 



extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0006_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0006_v0_0_s_ifspec;

#ifndef ____FIIterator_1_Telegram__CApi__CNative__CDatacenter_INTERFACE_DEFINED__
#define ____FIIterator_1_Telegram__CApi__CNative__CDatacenter_INTERFACE_DEFINED__

/* interface __FIIterator_1_Telegram__CApi__CNative__CDatacenter */
/* [unique][uuid][object] */ 



/* interface __FIIterator_1_Telegram__CApi__CNative__CDatacenter */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID___FIIterator_1_Telegram__CApi__CNative__CDatacenter;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("d20aab0e-c566-5029-9883-1b17f8db83bb")
    __FIIterator_1_Telegram__CApi__CNative__CDatacenter : public IInspectable
    {
    public:
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Current( 
            /* [retval][out] */ ABI::Telegram::Api::Native::IDatacenter **current) = 0;
        
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_HasCurrent( 
            /* [retval][out] */ boolean *hasCurrent) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE MoveNext( 
            /* [retval][out] */ boolean *hasCurrent) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetMany( 
            /* [in] */ unsigned int capacity,
            /* [size_is][length_is][out] */ ABI::Telegram::Api::Native::IDatacenter **items,
            /* [retval][out] */ unsigned int *actual) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct __FIIterator_1_Telegram__CApi__CNative__CDatacenterVtbl
    {
        BEGIN_INTERFACE
        
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __FIIterator_1_Telegram__CApi__CNative__CDatacenter * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __FIIterator_1_Telegram__CApi__CNative__CDatacenter * This);
        
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __FIIterator_1_Telegram__CApi__CNative__CDatacenter * This);
        
        HRESULT ( STDMETHODCALLTYPE *GetIids )( 
            __FIIterator_1_Telegram__CApi__CNative__CDatacenter * This,
            /* [out] */ ULONG *iidCount,
            /* [size_is][size_is][out] */ IID **iids);
        
        HRESULT ( STDMETHODCALLTYPE *GetRuntimeClassName )( 
            __FIIterator_1_Telegram__CApi__CNative__CDatacenter * This,
            /* [out] */ HSTRING *className);
        
        HRESULT ( STDMETHODCALLTYPE *GetTrustLevel )( 
            __FIIterator_1_Telegram__CApi__CNative__CDatacenter * This,
            /* [out] */ TrustLevel *trustLevel);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Current )( 
            __FIIterator_1_Telegram__CApi__CNative__CDatacenter * This,
            /* [retval][out] */ __x_ABI_CTelegram_CApi_CNative_CIDatacenter **current);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_HasCurrent )( 
            __FIIterator_1_Telegram__CApi__CNative__CDatacenter * This,
            /* [retval][out] */ boolean *hasCurrent);
        
        HRESULT ( STDMETHODCALLTYPE *MoveNext )( 
            __FIIterator_1_Telegram__CApi__CNative__CDatacenter * This,
            /* [retval][out] */ boolean *hasCurrent);
        
        HRESULT ( STDMETHODCALLTYPE *GetMany )( 
            __FIIterator_1_Telegram__CApi__CNative__CDatacenter * This,
            /* [in] */ unsigned int capacity,
            /* [size_is][length_is][out] */ __x_ABI_CTelegram_CApi_CNative_CIDatacenter **items,
            /* [retval][out] */ unsigned int *actual);
        
        END_INTERFACE
    } __FIIterator_1_Telegram__CApi__CNative__CDatacenterVtbl;

    interface __FIIterator_1_Telegram__CApi__CNative__CDatacenter
    {
        CONST_VTBL struct __FIIterator_1_Telegram__CApi__CNative__CDatacenterVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define __FIIterator_1_Telegram__CApi__CNative__CDatacenter_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define __FIIterator_1_Telegram__CApi__CNative__CDatacenter_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define __FIIterator_1_Telegram__CApi__CNative__CDatacenter_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define __FIIterator_1_Telegram__CApi__CNative__CDatacenter_GetIids(This,iidCount,iids)	\
    ( (This)->lpVtbl -> GetIids(This,iidCount,iids) ) 

#define __FIIterator_1_Telegram__CApi__CNative__CDatacenter_GetRuntimeClassName(This,className)	\
    ( (This)->lpVtbl -> GetRuntimeClassName(This,className) ) 

#define __FIIterator_1_Telegram__CApi__CNative__CDatacenter_GetTrustLevel(This,trustLevel)	\
    ( (This)->lpVtbl -> GetTrustLevel(This,trustLevel) ) 


#define __FIIterator_1_Telegram__CApi__CNative__CDatacenter_get_Current(This,current)	\
    ( (This)->lpVtbl -> get_Current(This,current) ) 

#define __FIIterator_1_Telegram__CApi__CNative__CDatacenter_get_HasCurrent(This,hasCurrent)	\
    ( (This)->lpVtbl -> get_HasCurrent(This,hasCurrent) ) 

#define __FIIterator_1_Telegram__CApi__CNative__CDatacenter_MoveNext(This,hasCurrent)	\
    ( (This)->lpVtbl -> MoveNext(This,hasCurrent) ) 

#define __FIIterator_1_Telegram__CApi__CNative__CDatacenter_GetMany(This,capacity,items,actual)	\
    ( (This)->lpVtbl -> GetMany(This,capacity,items,actual) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* ____FIIterator_1_Telegram__CApi__CNative__CDatacenter_INTERFACE_DEFINED__ */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0007 */
/* [local] */ 

#endif /* pinterface */
#endif /* DEF___FIIterator_1_Telegram__CApi__CNative__CDatacenter */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0007 */
/* [local] */ 



extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0007_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0007_v0_0_s_ifspec;

/* interface __MIDL_itf_Telegram2EApi2ENative2Eidl_0000_4663 */




/* interface __MIDL_itf_Telegram2EApi2ENative2Eidl_0000_4663 */




extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative2Eidl_0000_4663_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative2Eidl_0000_4663_v0_0_s_ifspec;

/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0008 */
/* [local] */ 

#ifndef DEF___FIIterable_1_Telegram__CApi__CNative__CDatacenter
#define DEF___FIIterable_1_Telegram__CApi__CNative__CDatacenter
#if !defined(__cplusplus) || defined(RO_NO_TEMPLATE_NAME)


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0008 */
/* [local] */ 



extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0008_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0008_v0_0_s_ifspec;

#ifndef ____FIIterable_1_Telegram__CApi__CNative__CDatacenter_INTERFACE_DEFINED__
#define ____FIIterable_1_Telegram__CApi__CNative__CDatacenter_INTERFACE_DEFINED__

/* interface __FIIterable_1_Telegram__CApi__CNative__CDatacenter */
/* [unique][uuid][object] */ 



/* interface __FIIterable_1_Telegram__CApi__CNative__CDatacenter */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID___FIIterable_1_Telegram__CApi__CNative__CDatacenter;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("50801597-1c0e-5f11-87b0-cc7a9d5966ad")
    __FIIterable_1_Telegram__CApi__CNative__CDatacenter : public IInspectable
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE First( 
            /* [retval][out] */ __FIIterator_1_Telegram__CApi__CNative__CDatacenter **first) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct __FIIterable_1_Telegram__CApi__CNative__CDatacenterVtbl
    {
        BEGIN_INTERFACE
        
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __FIIterable_1_Telegram__CApi__CNative__CDatacenter * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __FIIterable_1_Telegram__CApi__CNative__CDatacenter * This);
        
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __FIIterable_1_Telegram__CApi__CNative__CDatacenter * This);
        
        HRESULT ( STDMETHODCALLTYPE *GetIids )( 
            __FIIterable_1_Telegram__CApi__CNative__CDatacenter * This,
            /* [out] */ ULONG *iidCount,
            /* [size_is][size_is][out] */ IID **iids);
        
        HRESULT ( STDMETHODCALLTYPE *GetRuntimeClassName )( 
            __FIIterable_1_Telegram__CApi__CNative__CDatacenter * This,
            /* [out] */ HSTRING *className);
        
        HRESULT ( STDMETHODCALLTYPE *GetTrustLevel )( 
            __FIIterable_1_Telegram__CApi__CNative__CDatacenter * This,
            /* [out] */ TrustLevel *trustLevel);
        
        HRESULT ( STDMETHODCALLTYPE *First )( 
            __FIIterable_1_Telegram__CApi__CNative__CDatacenter * This,
            /* [retval][out] */ __FIIterator_1_Telegram__CApi__CNative__CDatacenter **first);
        
        END_INTERFACE
    } __FIIterable_1_Telegram__CApi__CNative__CDatacenterVtbl;

    interface __FIIterable_1_Telegram__CApi__CNative__CDatacenter
    {
        CONST_VTBL struct __FIIterable_1_Telegram__CApi__CNative__CDatacenterVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define __FIIterable_1_Telegram__CApi__CNative__CDatacenter_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define __FIIterable_1_Telegram__CApi__CNative__CDatacenter_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define __FIIterable_1_Telegram__CApi__CNative__CDatacenter_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define __FIIterable_1_Telegram__CApi__CNative__CDatacenter_GetIids(This,iidCount,iids)	\
    ( (This)->lpVtbl -> GetIids(This,iidCount,iids) ) 

#define __FIIterable_1_Telegram__CApi__CNative__CDatacenter_GetRuntimeClassName(This,className)	\
    ( (This)->lpVtbl -> GetRuntimeClassName(This,className) ) 

#define __FIIterable_1_Telegram__CApi__CNative__CDatacenter_GetTrustLevel(This,trustLevel)	\
    ( (This)->lpVtbl -> GetTrustLevel(This,trustLevel) ) 


#define __FIIterable_1_Telegram__CApi__CNative__CDatacenter_First(This,first)	\
    ( (This)->lpVtbl -> First(This,first) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* ____FIIterable_1_Telegram__CApi__CNative__CDatacenter_INTERFACE_DEFINED__ */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0009 */
/* [local] */ 

#endif /* pinterface */
#endif /* DEF___FIIterable_1_Telegram__CApi__CNative__CDatacenter */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0009 */
/* [local] */ 



extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0009_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0009_v0_0_s_ifspec;

/* interface __MIDL_itf_Telegram2EApi2ENative2Eidl_0000_4664 */




/* interface __MIDL_itf_Telegram2EApi2ENative2Eidl_0000_4664 */




extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative2Eidl_0000_4664_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative2Eidl_0000_4664_v0_0_s_ifspec;

/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0010 */
/* [local] */ 

#ifndef DEF___FIVectorView_1_Telegram__CApi__CNative__CDatacenter
#define DEF___FIVectorView_1_Telegram__CApi__CNative__CDatacenter
#if !defined(__cplusplus) || defined(RO_NO_TEMPLATE_NAME)


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0010 */
/* [local] */ 



extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0010_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0010_v0_0_s_ifspec;

#ifndef ____FIVectorView_1_Telegram__CApi__CNative__CDatacenter_INTERFACE_DEFINED__
#define ____FIVectorView_1_Telegram__CApi__CNative__CDatacenter_INTERFACE_DEFINED__

/* interface __FIVectorView_1_Telegram__CApi__CNative__CDatacenter */
/* [unique][uuid][object] */ 



/* interface __FIVectorView_1_Telegram__CApi__CNative__CDatacenter */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID___FIVectorView_1_Telegram__CApi__CNative__CDatacenter;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("59e97070-4e4b-5775-9716-c3584529beaa")
    __FIVectorView_1_Telegram__CApi__CNative__CDatacenter : public IInspectable
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE GetAt( 
            /* [in] */ unsigned int index,
            /* [retval][out] */ ABI::Telegram::Api::Native::IDatacenter **item) = 0;
        
        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Size( 
            /* [retval][out] */ unsigned int *size) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE IndexOf( 
            /* [in] */ ABI::Telegram::Api::Native::IDatacenter *item,
            /* [out] */ unsigned int *index,
            /* [retval][out] */ boolean *found) = 0;
        
        virtual HRESULT STDMETHODCALLTYPE GetMany( 
            /* [in] */ unsigned int startIndex,
            /* [in] */ unsigned int capacity,
            /* [size_is][length_is][out] */ ABI::Telegram::Api::Native::IDatacenter **items,
            /* [retval][out] */ unsigned int *actual) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct __FIVectorView_1_Telegram__CApi__CNative__CDatacenterVtbl
    {
        BEGIN_INTERFACE
        
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __FIVectorView_1_Telegram__CApi__CNative__CDatacenter * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __FIVectorView_1_Telegram__CApi__CNative__CDatacenter * This);
        
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __FIVectorView_1_Telegram__CApi__CNative__CDatacenter * This);
        
        HRESULT ( STDMETHODCALLTYPE *GetIids )( 
            __FIVectorView_1_Telegram__CApi__CNative__CDatacenter * This,
            /* [out] */ ULONG *iidCount,
            /* [size_is][size_is][out] */ IID **iids);
        
        HRESULT ( STDMETHODCALLTYPE *GetRuntimeClassName )( 
            __FIVectorView_1_Telegram__CApi__CNative__CDatacenter * This,
            /* [out] */ HSTRING *className);
        
        HRESULT ( STDMETHODCALLTYPE *GetTrustLevel )( 
            __FIVectorView_1_Telegram__CApi__CNative__CDatacenter * This,
            /* [out] */ TrustLevel *trustLevel);
        
        HRESULT ( STDMETHODCALLTYPE *GetAt )( 
            __FIVectorView_1_Telegram__CApi__CNative__CDatacenter * This,
            /* [in] */ unsigned int index,
            /* [retval][out] */ __x_ABI_CTelegram_CApi_CNative_CIDatacenter **item);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Size )( 
            __FIVectorView_1_Telegram__CApi__CNative__CDatacenter * This,
            /* [retval][out] */ unsigned int *size);
        
        HRESULT ( STDMETHODCALLTYPE *IndexOf )( 
            __FIVectorView_1_Telegram__CApi__CNative__CDatacenter * This,
            /* [in] */ __x_ABI_CTelegram_CApi_CNative_CIDatacenter *item,
            /* [out] */ unsigned int *index,
            /* [retval][out] */ boolean *found);
        
        HRESULT ( STDMETHODCALLTYPE *GetMany )( 
            __FIVectorView_1_Telegram__CApi__CNative__CDatacenter * This,
            /* [in] */ unsigned int startIndex,
            /* [in] */ unsigned int capacity,
            /* [size_is][length_is][out] */ __x_ABI_CTelegram_CApi_CNative_CIDatacenter **items,
            /* [retval][out] */ unsigned int *actual);
        
        END_INTERFACE
    } __FIVectorView_1_Telegram__CApi__CNative__CDatacenterVtbl;

    interface __FIVectorView_1_Telegram__CApi__CNative__CDatacenter
    {
        CONST_VTBL struct __FIVectorView_1_Telegram__CApi__CNative__CDatacenterVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define __FIVectorView_1_Telegram__CApi__CNative__CDatacenter_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define __FIVectorView_1_Telegram__CApi__CNative__CDatacenter_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define __FIVectorView_1_Telegram__CApi__CNative__CDatacenter_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define __FIVectorView_1_Telegram__CApi__CNative__CDatacenter_GetIids(This,iidCount,iids)	\
    ( (This)->lpVtbl -> GetIids(This,iidCount,iids) ) 

#define __FIVectorView_1_Telegram__CApi__CNative__CDatacenter_GetRuntimeClassName(This,className)	\
    ( (This)->lpVtbl -> GetRuntimeClassName(This,className) ) 

#define __FIVectorView_1_Telegram__CApi__CNative__CDatacenter_GetTrustLevel(This,trustLevel)	\
    ( (This)->lpVtbl -> GetTrustLevel(This,trustLevel) ) 


#define __FIVectorView_1_Telegram__CApi__CNative__CDatacenter_GetAt(This,index,item)	\
    ( (This)->lpVtbl -> GetAt(This,index,item) ) 

#define __FIVectorView_1_Telegram__CApi__CNative__CDatacenter_get_Size(This,size)	\
    ( (This)->lpVtbl -> get_Size(This,size) ) 

#define __FIVectorView_1_Telegram__CApi__CNative__CDatacenter_IndexOf(This,item,index,found)	\
    ( (This)->lpVtbl -> IndexOf(This,item,index,found) ) 

#define __FIVectorView_1_Telegram__CApi__CNative__CDatacenter_GetMany(This,startIndex,capacity,items,actual)	\
    ( (This)->lpVtbl -> GetMany(This,startIndex,capacity,items,actual) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* ____FIVectorView_1_Telegram__CApi__CNative__CDatacenter_INTERFACE_DEFINED__ */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0011 */
/* [local] */ 

#endif /* pinterface */
#endif /* DEF___FIVectorView_1_Telegram__CApi__CNative__CDatacenter */
#if !defined(____x_ABI_CTelegram_CApi_CNative_CTL_CITLObject_INTERFACE_DEFINED__)
extern const __declspec(selectany) _Null_terminated_ WCHAR InterfaceName_Telegram_Api_Native_TL_ITLObject[] = L"Telegram.Api.Native.TL.ITLObject";
#endif /* !defined(____x_ABI_CTelegram_CApi_CNative_CTL_CITLObject_INTERFACE_DEFINED__) */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0011 */
/* [local] */ 



extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0011_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0011_v0_0_s_ifspec;

#ifndef ____x_ABI_CTelegram_CApi_CNative_CTL_CITLObject_INTERFACE_DEFINED__
#define ____x_ABI_CTelegram_CApi_CNative_CTL_CITLObject_INTERFACE_DEFINED__

/* interface __x_ABI_CTelegram_CApi_CNative_CTL_CITLObject */
/* [uuid][object] */ 



/* interface ABI::Telegram::Api::Native::TL::ITLObject */
/* [uuid][object] */ 


EXTERN_C const IID IID___x_ABI_CTelegram_CApi_CNative_CTL_CITLObject;

#if defined(__cplusplus) && !defined(CINTERFACE)
    } /* end extern "C" */
    namespace ABI {
        namespace Telegram {
            namespace Api {
                namespace Native {
                    namespace TL {
                        
                        MIDL_INTERFACE("B93C4F8A-0308-4598-8C0A-52ACC91E45E3")
                        ITLObject : public IInspectable
                        {
                        public:
                            virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Constructor( 
                                /* [out][retval] */ UINT32 *value) = 0;
                            
                            virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_IsLayerNeeded( 
                                /* [out][retval] */ boolean *value) = 0;
                            
                            virtual HRESULT STDMETHODCALLTYPE Read( 
                                /* [in] */ ABI::Telegram::Api::Native::TL::ITLBinaryReader *reader) = 0;
                            
                            virtual HRESULT STDMETHODCALLTYPE Write( 
                                /* [in] */ ABI::Telegram::Api::Native::TL::ITLBinaryWriter *writer) = 0;
                            
                        };

                        extern const __declspec(selectany) IID & IID_ITLObject = __uuidof(ITLObject);

                        
                    }  /* end namespace */
                }  /* end namespace */
            }  /* end namespace */
        }  /* end namespace */
    }  /* end namespace */
    extern "C" { 
    
#else 	/* C style interface */

    typedef struct __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectVtbl
    {
        BEGIN_INTERFACE
        
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLObject * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLObject * This);
        
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLObject * This);
        
        HRESULT ( STDMETHODCALLTYPE *GetIids )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLObject * This,
            /* [out] */ ULONG *iidCount,
            /* [size_is][size_is][out] */ IID **iids);
        
        HRESULT ( STDMETHODCALLTYPE *GetRuntimeClassName )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLObject * This,
            /* [out] */ HSTRING *className);
        
        HRESULT ( STDMETHODCALLTYPE *GetTrustLevel )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLObject * This,
            /* [out] */ TrustLevel *trustLevel);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Constructor )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLObject * This,
            /* [out][retval] */ UINT32 *value);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_IsLayerNeeded )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLObject * This,
            /* [out][retval] */ boolean *value);
        
        HRESULT ( STDMETHODCALLTYPE *Read )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLObject * This,
            /* [in] */ __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader *reader);
        
        HRESULT ( STDMETHODCALLTYPE *Write )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLObject * This,
            /* [in] */ __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter *writer);
        
        END_INTERFACE
    } __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectVtbl;

    interface __x_ABI_CTelegram_CApi_CNative_CTL_CITLObject
    {
        CONST_VTBL struct __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLObject_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLObject_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLObject_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLObject_GetIids(This,iidCount,iids)	\
    ( (This)->lpVtbl -> GetIids(This,iidCount,iids) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLObject_GetRuntimeClassName(This,className)	\
    ( (This)->lpVtbl -> GetRuntimeClassName(This,className) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLObject_GetTrustLevel(This,trustLevel)	\
    ( (This)->lpVtbl -> GetTrustLevel(This,trustLevel) ) 


#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLObject_get_Constructor(This,value)	\
    ( (This)->lpVtbl -> get_Constructor(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLObject_get_IsLayerNeeded(This,value)	\
    ( (This)->lpVtbl -> get_IsLayerNeeded(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLObject_Read(This,reader)	\
    ( (This)->lpVtbl -> Read(This,reader) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLObject_Write(This,writer)	\
    ( (This)->lpVtbl -> Write(This,writer) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* ____x_ABI_CTelegram_CApi_CNative_CTL_CITLObject_INTERFACE_DEFINED__ */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0012 */
/* [local] */ 

#if !defined(____x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader_INTERFACE_DEFINED__)
extern const __declspec(selectany) _Null_terminated_ WCHAR InterfaceName_Telegram_Api_Native_TL_ITLBinaryReader[] = L"Telegram.Api.Native.TL.ITLBinaryReader";
#endif /* !defined(____x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader_INTERFACE_DEFINED__) */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0012 */
/* [local] */ 



extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0012_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0012_v0_0_s_ifspec;

#ifndef ____x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader_INTERFACE_DEFINED__
#define ____x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader_INTERFACE_DEFINED__

/* interface __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader */
/* [uuid][object] */ 



/* interface ABI::Telegram::Api::Native::TL::ITLBinaryReader */
/* [uuid][object] */ 


EXTERN_C const IID IID___x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader;

#if defined(__cplusplus) && !defined(CINTERFACE)
    } /* end extern "C" */
    namespace ABI {
        namespace Telegram {
            namespace Api {
                namespace Native {
                    namespace TL {
                        
                        MIDL_INTERFACE("7F865F69-62F1-4BC9-AD8C-717D0D8DD7F8")
                        ITLBinaryReader : public IInspectable
                        {
                        public:
                            virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Position( 
                                /* [out][retval] */ UINT32 *value) = 0;
                            
                            virtual /* [propput] */ HRESULT STDMETHODCALLTYPE put_Position( 
                                /* [in] */ UINT32 value) = 0;
                            
                            virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_UnconsumedBufferLength( 
                                /* [out][retval] */ UINT32 *value) = 0;
                            
                            virtual HRESULT STDMETHODCALLTYPE ReadByte( 
                                /* [out][retval] */ BYTE *value) = 0;
                            
                            virtual HRESULT STDMETHODCALLTYPE ReadInt16( 
                                /* [out][retval] */ INT16 *value) = 0;
                            
                            virtual HRESULT STDMETHODCALLTYPE ReadUInt16( 
                                /* [out][retval] */ UINT16 *value) = 0;
                            
                            virtual HRESULT STDMETHODCALLTYPE ReadInt32( 
                                /* [out][retval] */ INT32 *value) = 0;
                            
                            virtual HRESULT STDMETHODCALLTYPE ReadUInt32( 
                                /* [out][retval] */ UINT32 *value) = 0;
                            
                            virtual HRESULT STDMETHODCALLTYPE ReadInt64( 
                                /* [out][retval] */ INT64 *value) = 0;
                            
                            virtual HRESULT STDMETHODCALLTYPE ReadUInt64( 
                                /* [out][retval] */ UINT64 *value) = 0;
                            
                            virtual HRESULT STDMETHODCALLTYPE ReadBool( 
                                /* [out][retval] */ boolean *value) = 0;
                            
                            virtual HRESULT STDMETHODCALLTYPE ReadString( 
                                /* [out][retval] */ HSTRING *value) = 0;
                            
                            virtual HRESULT STDMETHODCALLTYPE ReadByteArray( 
                                /* [out] */ UINT32 *__valueSize,
                                /* [out][retval][size_is][size_is] */ BYTE **value) = 0;
                            
                            virtual HRESULT STDMETHODCALLTYPE ReadRawBuffer( 
                                /* [in] */ UINT32 __valueSize,
                                /* [in][size_is] */ BYTE *value) = 0;
                            
                            virtual HRESULT STDMETHODCALLTYPE ReadDouble( 
                                /* [out][retval] */ double *value) = 0;
                            
                            virtual HRESULT STDMETHODCALLTYPE ReadFloat( 
                                /* [out][retval] */ float *value) = 0;
                            
                            virtual HRESULT STDMETHODCALLTYPE ReadObject( 
                                /* [out][retval] */ ABI::Telegram::Api::Native::TL::ITLObject **value) = 0;
                            
                        };

                        extern const __declspec(selectany) IID & IID_ITLBinaryReader = __uuidof(ITLBinaryReader);

                        
                    }  /* end namespace */
                }  /* end namespace */
            }  /* end namespace */
        }  /* end namespace */
    }  /* end namespace */
    extern "C" { 
    
#else 	/* C style interface */

    typedef struct __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReaderVtbl
    {
        BEGIN_INTERFACE
        
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader * This);
        
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader * This);
        
        HRESULT ( STDMETHODCALLTYPE *GetIids )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader * This,
            /* [out] */ ULONG *iidCount,
            /* [size_is][size_is][out] */ IID **iids);
        
        HRESULT ( STDMETHODCALLTYPE *GetRuntimeClassName )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader * This,
            /* [out] */ HSTRING *className);
        
        HRESULT ( STDMETHODCALLTYPE *GetTrustLevel )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader * This,
            /* [out] */ TrustLevel *trustLevel);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Position )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader * This,
            /* [out][retval] */ UINT32 *value);
        
        /* [propput] */ HRESULT ( STDMETHODCALLTYPE *put_Position )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader * This,
            /* [in] */ UINT32 value);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_UnconsumedBufferLength )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader * This,
            /* [out][retval] */ UINT32 *value);
        
        HRESULT ( STDMETHODCALLTYPE *ReadByte )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader * This,
            /* [out][retval] */ BYTE *value);
        
        HRESULT ( STDMETHODCALLTYPE *ReadInt16 )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader * This,
            /* [out][retval] */ INT16 *value);
        
        HRESULT ( STDMETHODCALLTYPE *ReadUInt16 )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader * This,
            /* [out][retval] */ UINT16 *value);
        
        HRESULT ( STDMETHODCALLTYPE *ReadInt32 )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader * This,
            /* [out][retval] */ INT32 *value);
        
        HRESULT ( STDMETHODCALLTYPE *ReadUInt32 )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader * This,
            /* [out][retval] */ UINT32 *value);
        
        HRESULT ( STDMETHODCALLTYPE *ReadInt64 )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader * This,
            /* [out][retval] */ INT64 *value);
        
        HRESULT ( STDMETHODCALLTYPE *ReadUInt64 )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader * This,
            /* [out][retval] */ UINT64 *value);
        
        HRESULT ( STDMETHODCALLTYPE *ReadBool )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader * This,
            /* [out][retval] */ boolean *value);
        
        HRESULT ( STDMETHODCALLTYPE *ReadString )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader * This,
            /* [out][retval] */ HSTRING *value);
        
        HRESULT ( STDMETHODCALLTYPE *ReadByteArray )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader * This,
            /* [out] */ UINT32 *__valueSize,
            /* [out][retval][size_is][size_is] */ BYTE **value);
        
        HRESULT ( STDMETHODCALLTYPE *ReadRawBuffer )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader * This,
            /* [in] */ UINT32 __valueSize,
            /* [in][size_is] */ BYTE *value);
        
        HRESULT ( STDMETHODCALLTYPE *ReadDouble )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader * This,
            /* [out][retval] */ double *value);
        
        HRESULT ( STDMETHODCALLTYPE *ReadFloat )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader * This,
            /* [out][retval] */ float *value);
        
        HRESULT ( STDMETHODCALLTYPE *ReadObject )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader * This,
            /* [out][retval] */ __x_ABI_CTelegram_CApi_CNative_CTL_CITLObject **value);
        
        END_INTERFACE
    } __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReaderVtbl;

    interface __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader
    {
        CONST_VTBL struct __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReaderVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader_GetIids(This,iidCount,iids)	\
    ( (This)->lpVtbl -> GetIids(This,iidCount,iids) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader_GetRuntimeClassName(This,className)	\
    ( (This)->lpVtbl -> GetRuntimeClassName(This,className) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader_GetTrustLevel(This,trustLevel)	\
    ( (This)->lpVtbl -> GetTrustLevel(This,trustLevel) ) 


#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader_get_Position(This,value)	\
    ( (This)->lpVtbl -> get_Position(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader_put_Position(This,value)	\
    ( (This)->lpVtbl -> put_Position(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader_get_UnconsumedBufferLength(This,value)	\
    ( (This)->lpVtbl -> get_UnconsumedBufferLength(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader_ReadByte(This,value)	\
    ( (This)->lpVtbl -> ReadByte(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader_ReadInt16(This,value)	\
    ( (This)->lpVtbl -> ReadInt16(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader_ReadUInt16(This,value)	\
    ( (This)->lpVtbl -> ReadUInt16(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader_ReadInt32(This,value)	\
    ( (This)->lpVtbl -> ReadInt32(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader_ReadUInt32(This,value)	\
    ( (This)->lpVtbl -> ReadUInt32(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader_ReadInt64(This,value)	\
    ( (This)->lpVtbl -> ReadInt64(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader_ReadUInt64(This,value)	\
    ( (This)->lpVtbl -> ReadUInt64(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader_ReadBool(This,value)	\
    ( (This)->lpVtbl -> ReadBool(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader_ReadString(This,value)	\
    ( (This)->lpVtbl -> ReadString(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader_ReadByteArray(This,__valueSize,value)	\
    ( (This)->lpVtbl -> ReadByteArray(This,__valueSize,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader_ReadRawBuffer(This,__valueSize,value)	\
    ( (This)->lpVtbl -> ReadRawBuffer(This,__valueSize,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader_ReadDouble(This,value)	\
    ( (This)->lpVtbl -> ReadDouble(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader_ReadFloat(This,value)	\
    ( (This)->lpVtbl -> ReadFloat(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader_ReadObject(This,value)	\
    ( (This)->lpVtbl -> ReadObject(This,value) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* ____x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader_INTERFACE_DEFINED__ */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0013 */
/* [local] */ 

#ifndef RUNTIMECLASS_Telegram_Api_Native_TL_TLBinaryReader_DEFINED
#define RUNTIMECLASS_Telegram_Api_Native_TL_TLBinaryReader_DEFINED
extern const __declspec(selectany) _Null_terminated_ WCHAR RuntimeClass_Telegram_Api_Native_TL_TLBinaryReader[] = L"Telegram.Api.Native.TL.TLBinaryReader";
#endif
#if !defined(____x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter_INTERFACE_DEFINED__)
extern const __declspec(selectany) _Null_terminated_ WCHAR InterfaceName_Telegram_Api_Native_TL_ITLBinaryWriter[] = L"Telegram.Api.Native.TL.ITLBinaryWriter";
#endif /* !defined(____x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter_INTERFACE_DEFINED__) */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0013 */
/* [local] */ 



extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0013_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0013_v0_0_s_ifspec;

#ifndef ____x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter_INTERFACE_DEFINED__
#define ____x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter_INTERFACE_DEFINED__

/* interface __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter */
/* [uuid][object] */ 



/* interface ABI::Telegram::Api::Native::TL::ITLBinaryWriter */
/* [uuid][object] */ 


EXTERN_C const IID IID___x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter;

#if defined(__cplusplus) && !defined(CINTERFACE)
    } /* end extern "C" */
    namespace ABI {
        namespace Telegram {
            namespace Api {
                namespace Native {
                    namespace TL {
                        
                        MIDL_INTERFACE("732B4B01-0603-4ADB-8F29-24096FCDF7C6")
                        ITLBinaryWriter : public IInspectable
                        {
                        public:
                            virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Position( 
                                /* [out][retval] */ UINT32 *value) = 0;
                            
                            virtual /* [propput] */ HRESULT STDMETHODCALLTYPE put_Position( 
                                /* [in] */ UINT32 value) = 0;
                            
                            virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_UnstoredBufferLength( 
                                /* [out][retval] */ UINT32 *value) = 0;
                            
                            virtual HRESULT STDMETHODCALLTYPE WriteByte( 
                                /* [in] */ BYTE value) = 0;
                            
                            virtual HRESULT STDMETHODCALLTYPE WriteInt16( 
                                /* [in] */ INT16 value) = 0;
                            
                            virtual HRESULT STDMETHODCALLTYPE WriteUInt16( 
                                /* [in] */ UINT16 value) = 0;
                            
                            virtual HRESULT STDMETHODCALLTYPE WriteInt32( 
                                /* [in] */ INT32 value) = 0;
                            
                            virtual HRESULT STDMETHODCALLTYPE WriteUInt32( 
                                /* [in] */ UINT32 value) = 0;
                            
                            virtual HRESULT STDMETHODCALLTYPE WriteInt64( 
                                /* [in] */ INT64 value) = 0;
                            
                            virtual HRESULT STDMETHODCALLTYPE WriteUInt64( 
                                /* [in] */ UINT64 value) = 0;
                            
                            virtual HRESULT STDMETHODCALLTYPE WriteBool( 
                                /* [in] */ boolean value) = 0;
                            
                            virtual HRESULT STDMETHODCALLTYPE WriteString( 
                                /* [in] */ HSTRING value) = 0;
                            
                            virtual HRESULT STDMETHODCALLTYPE WriteByteArray( 
                                /* [in] */ UINT32 __valueSize,
                                /* [in][size_is] */ BYTE *value) = 0;
                            
                            virtual HRESULT STDMETHODCALLTYPE WriteRawBuffer( 
                                /* [in] */ UINT32 __valueSize,
                                /* [in][size_is] */ BYTE *value) = 0;
                            
                            virtual HRESULT STDMETHODCALLTYPE WriteDouble( 
                                /* [in] */ double value) = 0;
                            
                            virtual HRESULT STDMETHODCALLTYPE WriteFloat( 
                                /* [in] */ float value) = 0;
                            
                            virtual HRESULT STDMETHODCALLTYPE WriteObject( 
                                /* [in] */ ABI::Telegram::Api::Native::TL::ITLObject *value) = 0;
                            
                        };

                        extern const __declspec(selectany) IID & IID_ITLBinaryWriter = __uuidof(ITLBinaryWriter);

                        
                    }  /* end namespace */
                }  /* end namespace */
            }  /* end namespace */
        }  /* end namespace */
    }  /* end namespace */
    extern "C" { 
    
#else 	/* C style interface */

    typedef struct __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriterVtbl
    {
        BEGIN_INTERFACE
        
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter * This);
        
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter * This);
        
        HRESULT ( STDMETHODCALLTYPE *GetIids )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter * This,
            /* [out] */ ULONG *iidCount,
            /* [size_is][size_is][out] */ IID **iids);
        
        HRESULT ( STDMETHODCALLTYPE *GetRuntimeClassName )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter * This,
            /* [out] */ HSTRING *className);
        
        HRESULT ( STDMETHODCALLTYPE *GetTrustLevel )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter * This,
            /* [out] */ TrustLevel *trustLevel);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Position )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter * This,
            /* [out][retval] */ UINT32 *value);
        
        /* [propput] */ HRESULT ( STDMETHODCALLTYPE *put_Position )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter * This,
            /* [in] */ UINT32 value);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_UnstoredBufferLength )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter * This,
            /* [out][retval] */ UINT32 *value);
        
        HRESULT ( STDMETHODCALLTYPE *WriteByte )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter * This,
            /* [in] */ BYTE value);
        
        HRESULT ( STDMETHODCALLTYPE *WriteInt16 )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter * This,
            /* [in] */ INT16 value);
        
        HRESULT ( STDMETHODCALLTYPE *WriteUInt16 )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter * This,
            /* [in] */ UINT16 value);
        
        HRESULT ( STDMETHODCALLTYPE *WriteInt32 )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter * This,
            /* [in] */ INT32 value);
        
        HRESULT ( STDMETHODCALLTYPE *WriteUInt32 )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter * This,
            /* [in] */ UINT32 value);
        
        HRESULT ( STDMETHODCALLTYPE *WriteInt64 )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter * This,
            /* [in] */ INT64 value);
        
        HRESULT ( STDMETHODCALLTYPE *WriteUInt64 )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter * This,
            /* [in] */ UINT64 value);
        
        HRESULT ( STDMETHODCALLTYPE *WriteBool )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter * This,
            /* [in] */ boolean value);
        
        HRESULT ( STDMETHODCALLTYPE *WriteString )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter * This,
            /* [in] */ HSTRING value);
        
        HRESULT ( STDMETHODCALLTYPE *WriteByteArray )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter * This,
            /* [in] */ UINT32 __valueSize,
            /* [in][size_is] */ BYTE *value);
        
        HRESULT ( STDMETHODCALLTYPE *WriteRawBuffer )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter * This,
            /* [in] */ UINT32 __valueSize,
            /* [in][size_is] */ BYTE *value);
        
        HRESULT ( STDMETHODCALLTYPE *WriteDouble )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter * This,
            /* [in] */ double value);
        
        HRESULT ( STDMETHODCALLTYPE *WriteFloat )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter * This,
            /* [in] */ float value);
        
        HRESULT ( STDMETHODCALLTYPE *WriteObject )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter * This,
            /* [in] */ __x_ABI_CTelegram_CApi_CNative_CTL_CITLObject *value);
        
        END_INTERFACE
    } __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriterVtbl;

    interface __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter
    {
        CONST_VTBL struct __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriterVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter_GetIids(This,iidCount,iids)	\
    ( (This)->lpVtbl -> GetIids(This,iidCount,iids) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter_GetRuntimeClassName(This,className)	\
    ( (This)->lpVtbl -> GetRuntimeClassName(This,className) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter_GetTrustLevel(This,trustLevel)	\
    ( (This)->lpVtbl -> GetTrustLevel(This,trustLevel) ) 


#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter_get_Position(This,value)	\
    ( (This)->lpVtbl -> get_Position(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter_put_Position(This,value)	\
    ( (This)->lpVtbl -> put_Position(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter_get_UnstoredBufferLength(This,value)	\
    ( (This)->lpVtbl -> get_UnstoredBufferLength(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter_WriteByte(This,value)	\
    ( (This)->lpVtbl -> WriteByte(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter_WriteInt16(This,value)	\
    ( (This)->lpVtbl -> WriteInt16(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter_WriteUInt16(This,value)	\
    ( (This)->lpVtbl -> WriteUInt16(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter_WriteInt32(This,value)	\
    ( (This)->lpVtbl -> WriteInt32(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter_WriteUInt32(This,value)	\
    ( (This)->lpVtbl -> WriteUInt32(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter_WriteInt64(This,value)	\
    ( (This)->lpVtbl -> WriteInt64(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter_WriteUInt64(This,value)	\
    ( (This)->lpVtbl -> WriteUInt64(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter_WriteBool(This,value)	\
    ( (This)->lpVtbl -> WriteBool(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter_WriteString(This,value)	\
    ( (This)->lpVtbl -> WriteString(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter_WriteByteArray(This,__valueSize,value)	\
    ( (This)->lpVtbl -> WriteByteArray(This,__valueSize,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter_WriteRawBuffer(This,__valueSize,value)	\
    ( (This)->lpVtbl -> WriteRawBuffer(This,__valueSize,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter_WriteDouble(This,value)	\
    ( (This)->lpVtbl -> WriteDouble(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter_WriteFloat(This,value)	\
    ( (This)->lpVtbl -> WriteFloat(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter_WriteObject(This,value)	\
    ( (This)->lpVtbl -> WriteObject(This,value) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* ____x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryWriter_INTERFACE_DEFINED__ */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0014 */
/* [local] */ 

#ifndef RUNTIMECLASS_Telegram_Api_Native_TL_TLBinaryWriter_DEFINED
#define RUNTIMECLASS_Telegram_Api_Native_TL_TLBinaryWriter_DEFINED
extern const __declspec(selectany) _Null_terminated_ WCHAR RuntimeClass_Telegram_Api_Native_TL_TLBinaryWriter[] = L"Telegram.Api.Native.TL.TLBinaryWriter";
#endif


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0014 */
/* [local] */ 



extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0014_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0014_v0_0_s_ifspec;

#ifndef ____x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectConstructorDelegate_INTERFACE_DEFINED__
#define ____x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectConstructorDelegate_INTERFACE_DEFINED__

/* interface __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectConstructorDelegate */
/* [uuid][object] */ 



/* interface ABI::Telegram::Api::Native::TL::ITLObjectConstructorDelegate */
/* [uuid][object] */ 


EXTERN_C const IID IID___x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectConstructorDelegate;

#if defined(__cplusplus) && !defined(CINTERFACE)
    } /* end extern "C" */
    namespace ABI {
        namespace Telegram {
            namespace Api {
                namespace Native {
                    namespace TL {
                        
                        MIDL_INTERFACE("4A716007-A9C4-4E26-88F4-91DD8800413F")
                        ITLObjectConstructorDelegate : public IUnknown
                        {
                        public:
                            virtual HRESULT STDMETHODCALLTYPE Invoke( 
                                /* [out][retval] */ ABI::Telegram::Api::Native::TL::ITLObject **value) = 0;
                            
                        };

                        extern const __declspec(selectany) IID & IID_ITLObjectConstructorDelegate = __uuidof(ITLObjectConstructorDelegate);

                        
                    }  /* end namespace */
                }  /* end namespace */
            }  /* end namespace */
        }  /* end namespace */
    }  /* end namespace */
    extern "C" { 
    
#else 	/* C style interface */

    typedef struct __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectConstructorDelegateVtbl
    {
        BEGIN_INTERFACE
        
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectConstructorDelegate * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectConstructorDelegate * This);
        
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectConstructorDelegate * This);
        
        HRESULT ( STDMETHODCALLTYPE *Invoke )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectConstructorDelegate * This,
            /* [out][retval] */ __x_ABI_CTelegram_CApi_CNative_CTL_CITLObject **value);
        
        END_INTERFACE
    } __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectConstructorDelegateVtbl;

    interface __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectConstructorDelegate
    {
        CONST_VTBL struct __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectConstructorDelegateVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectConstructorDelegate_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectConstructorDelegate_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectConstructorDelegate_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectConstructorDelegate_Invoke(This,value)	\
    ( (This)->lpVtbl -> Invoke(This,value) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* ____x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectConstructorDelegate_INTERFACE_DEFINED__ */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0015 */
/* [local] */ 

#if !defined(____x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializerStatics_INTERFACE_DEFINED__)
extern const __declspec(selectany) _Null_terminated_ WCHAR InterfaceName_Telegram_Api_Native_TL_ITLObjectSerializerStatics[] = L"Telegram.Api.Native.TL.ITLObjectSerializerStatics";
#endif /* !defined(____x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializerStatics_INTERFACE_DEFINED__) */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0015 */
/* [local] */ 



extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0015_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0015_v0_0_s_ifspec;

#ifndef ____x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializerStatics_INTERFACE_DEFINED__
#define ____x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializerStatics_INTERFACE_DEFINED__

/* interface __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializerStatics */
/* [uuid][object] */ 



/* interface ABI::Telegram::Api::Native::TL::ITLObjectSerializerStatics */
/* [uuid][object] */ 


EXTERN_C const IID IID___x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializerStatics;

#if defined(__cplusplus) && !defined(CINTERFACE)
    } /* end extern "C" */
    namespace ABI {
        namespace Telegram {
            namespace Api {
                namespace Native {
                    namespace TL {
                        
                        MIDL_INTERFACE("3AD8B674-5A82-4CC3-A1D6-9BBBF550EC27")
                        ITLObjectSerializerStatics : public IInspectable
                        {
                        public:
                            virtual HRESULT STDMETHODCALLTYPE Serialize( 
                                /* [in] */ ABI::Telegram::Api::Native::TL::ITLObject *object,
                                /* [out][retval] */ ABI::Windows::Storage::Streams::IBuffer **value) = 0;
                            
                            virtual HRESULT STDMETHODCALLTYPE Deserialize( 
                                /* [in] */ ABI::Windows::Storage::Streams::IBuffer *buffer,
                                /* [out][retval] */ ABI::Telegram::Api::Native::TL::ITLObject **value) = 0;
                            
                            virtual HRESULT STDMETHODCALLTYPE GetObjectSize( 
                                /* [in] */ ABI::Telegram::Api::Native::TL::ITLObject *object,
                                /* [out][retval] */ UINT32 *value) = 0;
                            
                            virtual HRESULT STDMETHODCALLTYPE RegisterObjectConstructor( 
                                /* [in] */ UINT32 constructor,
                                /* [in] */ ABI::Telegram::Api::Native::TL::ITLObjectConstructorDelegate *constructorDelegate) = 0;
                            
                        };

                        extern const __declspec(selectany) IID & IID_ITLObjectSerializerStatics = __uuidof(ITLObjectSerializerStatics);

                        
                    }  /* end namespace */
                }  /* end namespace */
            }  /* end namespace */
        }  /* end namespace */
    }  /* end namespace */
    extern "C" { 
    
#else 	/* C style interface */

    typedef struct __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializerStaticsVtbl
    {
        BEGIN_INTERFACE
        
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializerStatics * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializerStatics * This);
        
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializerStatics * This);
        
        HRESULT ( STDMETHODCALLTYPE *GetIids )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializerStatics * This,
            /* [out] */ ULONG *iidCount,
            /* [size_is][size_is][out] */ IID **iids);
        
        HRESULT ( STDMETHODCALLTYPE *GetRuntimeClassName )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializerStatics * This,
            /* [out] */ HSTRING *className);
        
        HRESULT ( STDMETHODCALLTYPE *GetTrustLevel )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializerStatics * This,
            /* [out] */ TrustLevel *trustLevel);
        
        HRESULT ( STDMETHODCALLTYPE *Serialize )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializerStatics * This,
            /* [in] */ __x_ABI_CTelegram_CApi_CNative_CTL_CITLObject *object,
            /* [out][retval] */ __x_ABI_CWindows_CStorage_CStreams_CIBuffer **value);
        
        HRESULT ( STDMETHODCALLTYPE *Deserialize )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializerStatics * This,
            /* [in] */ __x_ABI_CWindows_CStorage_CStreams_CIBuffer *buffer,
            /* [out][retval] */ __x_ABI_CTelegram_CApi_CNative_CTL_CITLObject **value);
        
        HRESULT ( STDMETHODCALLTYPE *GetObjectSize )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializerStatics * This,
            /* [in] */ __x_ABI_CTelegram_CApi_CNative_CTL_CITLObject *object,
            /* [out][retval] */ UINT32 *value);
        
        HRESULT ( STDMETHODCALLTYPE *RegisterObjectConstructor )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializerStatics * This,
            /* [in] */ UINT32 constructor,
            /* [in] */ __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectConstructorDelegate *constructorDelegate);
        
        END_INTERFACE
    } __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializerStaticsVtbl;

    interface __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializerStatics
    {
        CONST_VTBL struct __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializerStaticsVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializerStatics_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializerStatics_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializerStatics_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializerStatics_GetIids(This,iidCount,iids)	\
    ( (This)->lpVtbl -> GetIids(This,iidCount,iids) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializerStatics_GetRuntimeClassName(This,className)	\
    ( (This)->lpVtbl -> GetRuntimeClassName(This,className) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializerStatics_GetTrustLevel(This,trustLevel)	\
    ( (This)->lpVtbl -> GetTrustLevel(This,trustLevel) ) 


#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializerStatics_Serialize(This,object,value)	\
    ( (This)->lpVtbl -> Serialize(This,object,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializerStatics_Deserialize(This,buffer,value)	\
    ( (This)->lpVtbl -> Deserialize(This,buffer,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializerStatics_GetObjectSize(This,object,value)	\
    ( (This)->lpVtbl -> GetObjectSize(This,object,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializerStatics_RegisterObjectConstructor(This,constructor,constructorDelegate)	\
    ( (This)->lpVtbl -> RegisterObjectConstructor(This,constructor,constructorDelegate) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* ____x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializerStatics_INTERFACE_DEFINED__ */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0016 */
/* [local] */ 

#if !defined(____x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializer_INTERFACE_DEFINED__)
extern const __declspec(selectany) _Null_terminated_ WCHAR InterfaceName_Telegram_Api_Native_TL_ITLObjectSerializer[] = L"Telegram.Api.Native.TL.ITLObjectSerializer";
#endif /* !defined(____x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializer_INTERFACE_DEFINED__) */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0016 */
/* [local] */ 



extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0016_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0016_v0_0_s_ifspec;

#ifndef ____x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializer_INTERFACE_DEFINED__
#define ____x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializer_INTERFACE_DEFINED__

/* interface __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializer */
/* [uuid][object] */ 



/* interface ABI::Telegram::Api::Native::TL::ITLObjectSerializer */
/* [uuid][object] */ 


EXTERN_C const IID IID___x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializer;

#if defined(__cplusplus) && !defined(CINTERFACE)
    } /* end extern "C" */
    namespace ABI {
        namespace Telegram {
            namespace Api {
                namespace Native {
                    namespace TL {
                        
                        MIDL_INTERFACE("99B0AD68-1843-4F3F-A2A9-9B0144912557")
                        ITLObjectSerializer : public IInspectable
                        {
                        public:
                        };

                        extern const __declspec(selectany) IID & IID_ITLObjectSerializer = __uuidof(ITLObjectSerializer);

                        
                    }  /* end namespace */
                }  /* end namespace */
            }  /* end namespace */
        }  /* end namespace */
    }  /* end namespace */
    extern "C" { 
    
#else 	/* C style interface */

    typedef struct __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializerVtbl
    {
        BEGIN_INTERFACE
        
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializer * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializer * This);
        
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializer * This);
        
        HRESULT ( STDMETHODCALLTYPE *GetIids )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializer * This,
            /* [out] */ ULONG *iidCount,
            /* [size_is][size_is][out] */ IID **iids);
        
        HRESULT ( STDMETHODCALLTYPE *GetRuntimeClassName )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializer * This,
            /* [out] */ HSTRING *className);
        
        HRESULT ( STDMETHODCALLTYPE *GetTrustLevel )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializer * This,
            /* [out] */ TrustLevel *trustLevel);
        
        END_INTERFACE
    } __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializerVtbl;

    interface __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializer
    {
        CONST_VTBL struct __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializerVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializer_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializer_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializer_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializer_GetIids(This,iidCount,iids)	\
    ( (This)->lpVtbl -> GetIids(This,iidCount,iids) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializer_GetRuntimeClassName(This,className)	\
    ( (This)->lpVtbl -> GetRuntimeClassName(This,className) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializer_GetTrustLevel(This,trustLevel)	\
    ( (This)->lpVtbl -> GetTrustLevel(This,trustLevel) ) 


#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* ____x_ABI_CTelegram_CApi_CNative_CTL_CITLObjectSerializer_INTERFACE_DEFINED__ */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0017 */
/* [local] */ 

#ifndef RUNTIMECLASS_Telegram_Api_Native_TL_TLObjectSerializer_DEFINED
#define RUNTIMECLASS_Telegram_Api_Native_TL_TLObjectSerializer_DEFINED
extern const __declspec(selectany) _Null_terminated_ WCHAR RuntimeClass_Telegram_Api_Native_TL_TLObjectSerializer[] = L"Telegram.Api.Native.TL.TLObjectSerializer";
#endif
#if !defined(____x_ABI_CTelegram_CApi_CNative_CTL_CITLError_INTERFACE_DEFINED__)
extern const __declspec(selectany) _Null_terminated_ WCHAR InterfaceName_Telegram_Api_Native_TL_ITLError[] = L"Telegram.Api.Native.TL.ITLError";
#endif /* !defined(____x_ABI_CTelegram_CApi_CNative_CTL_CITLError_INTERFACE_DEFINED__) */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0017 */
/* [local] */ 



extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0017_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0017_v0_0_s_ifspec;

#ifndef ____x_ABI_CTelegram_CApi_CNative_CTL_CITLError_INTERFACE_DEFINED__
#define ____x_ABI_CTelegram_CApi_CNative_CTL_CITLError_INTERFACE_DEFINED__

/* interface __x_ABI_CTelegram_CApi_CNative_CTL_CITLError */
/* [uuid][object] */ 



/* interface ABI::Telegram::Api::Native::TL::ITLError */
/* [uuid][object] */ 


EXTERN_C const IID IID___x_ABI_CTelegram_CApi_CNative_CTL_CITLError;

#if defined(__cplusplus) && !defined(CINTERFACE)
    } /* end extern "C" */
    namespace ABI {
        namespace Telegram {
            namespace Api {
                namespace Native {
                    namespace TL {
                        
                        MIDL_INTERFACE("7E992965-E9B1-4804-9C1C-E578B5C397AF")
                        ITLError : public IInspectable
                        {
                        public:
                            virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Code( 
                                /* [out][retval] */ UINT32 *value) = 0;
                            
                            virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Text( 
                                /* [out][retval] */ HSTRING *value) = 0;
                            
                        };

                        extern const __declspec(selectany) IID & IID_ITLError = __uuidof(ITLError);

                        
                    }  /* end namespace */
                }  /* end namespace */
            }  /* end namespace */
        }  /* end namespace */
    }  /* end namespace */
    extern "C" { 
    
#else 	/* C style interface */

    typedef struct __x_ABI_CTelegram_CApi_CNative_CTL_CITLErrorVtbl
    {
        BEGIN_INTERFACE
        
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLError * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLError * This);
        
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLError * This);
        
        HRESULT ( STDMETHODCALLTYPE *GetIids )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLError * This,
            /* [out] */ ULONG *iidCount,
            /* [size_is][size_is][out] */ IID **iids);
        
        HRESULT ( STDMETHODCALLTYPE *GetRuntimeClassName )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLError * This,
            /* [out] */ HSTRING *className);
        
        HRESULT ( STDMETHODCALLTYPE *GetTrustLevel )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLError * This,
            /* [out] */ TrustLevel *trustLevel);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Code )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLError * This,
            /* [out][retval] */ UINT32 *value);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Text )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLError * This,
            /* [out][retval] */ HSTRING *value);
        
        END_INTERFACE
    } __x_ABI_CTelegram_CApi_CNative_CTL_CITLErrorVtbl;

    interface __x_ABI_CTelegram_CApi_CNative_CTL_CITLError
    {
        CONST_VTBL struct __x_ABI_CTelegram_CApi_CNative_CTL_CITLErrorVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLError_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLError_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLError_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLError_GetIids(This,iidCount,iids)	\
    ( (This)->lpVtbl -> GetIids(This,iidCount,iids) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLError_GetRuntimeClassName(This,className)	\
    ( (This)->lpVtbl -> GetRuntimeClassName(This,className) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLError_GetTrustLevel(This,trustLevel)	\
    ( (This)->lpVtbl -> GetTrustLevel(This,trustLevel) ) 


#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLError_get_Code(This,value)	\
    ( (This)->lpVtbl -> get_Code(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLError_get_Text(This,value)	\
    ( (This)->lpVtbl -> get_Text(This,value) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* ____x_ABI_CTelegram_CApi_CNative_CTL_CITLError_INTERFACE_DEFINED__ */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0018 */
/* [local] */ 

#if !defined(____x_ABI_CTelegram_CApi_CNative_CTL_CITLErrorFactory_INTERFACE_DEFINED__)
extern const __declspec(selectany) _Null_terminated_ WCHAR InterfaceName_Telegram_Api_Native_TL_ITLErrorFactory[] = L"Telegram.Api.Native.TL.ITLErrorFactory";
#endif /* !defined(____x_ABI_CTelegram_CApi_CNative_CTL_CITLErrorFactory_INTERFACE_DEFINED__) */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0018 */
/* [local] */ 



extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0018_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0018_v0_0_s_ifspec;

#ifndef ____x_ABI_CTelegram_CApi_CNative_CTL_CITLErrorFactory_INTERFACE_DEFINED__
#define ____x_ABI_CTelegram_CApi_CNative_CTL_CITLErrorFactory_INTERFACE_DEFINED__

/* interface __x_ABI_CTelegram_CApi_CNative_CTL_CITLErrorFactory */
/* [uuid][object] */ 



/* interface ABI::Telegram::Api::Native::TL::ITLErrorFactory */
/* [uuid][object] */ 


EXTERN_C const IID IID___x_ABI_CTelegram_CApi_CNative_CTL_CITLErrorFactory;

#if defined(__cplusplus) && !defined(CINTERFACE)
    } /* end extern "C" */
    namespace ABI {
        namespace Telegram {
            namespace Api {
                namespace Native {
                    namespace TL {
                        
                        MIDL_INTERFACE("80E1E4F5-C91C-4785-A6B0-25CE6A6C7825")
                        ITLErrorFactory : public IInspectable
                        {
                        public:
                            virtual HRESULT STDMETHODCALLTYPE CreateTLError( 
                                /* [in] */ UINT32 code,
                                /* [in] */ HSTRING text,
                                /* [out][retval] */ ABI::Telegram::Api::Native::TL::ITLError **instance) = 0;
                            
                        };

                        extern const __declspec(selectany) IID & IID_ITLErrorFactory = __uuidof(ITLErrorFactory);

                        
                    }  /* end namespace */
                }  /* end namespace */
            }  /* end namespace */
        }  /* end namespace */
    }  /* end namespace */
    extern "C" { 
    
#else 	/* C style interface */

    typedef struct __x_ABI_CTelegram_CApi_CNative_CTL_CITLErrorFactoryVtbl
    {
        BEGIN_INTERFACE
        
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLErrorFactory * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLErrorFactory * This);
        
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLErrorFactory * This);
        
        HRESULT ( STDMETHODCALLTYPE *GetIids )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLErrorFactory * This,
            /* [out] */ ULONG *iidCount,
            /* [size_is][size_is][out] */ IID **iids);
        
        HRESULT ( STDMETHODCALLTYPE *GetRuntimeClassName )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLErrorFactory * This,
            /* [out] */ HSTRING *className);
        
        HRESULT ( STDMETHODCALLTYPE *GetTrustLevel )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLErrorFactory * This,
            /* [out] */ TrustLevel *trustLevel);
        
        HRESULT ( STDMETHODCALLTYPE *CreateTLError )( 
            __x_ABI_CTelegram_CApi_CNative_CTL_CITLErrorFactory * This,
            /* [in] */ UINT32 code,
            /* [in] */ HSTRING text,
            /* [out][retval] */ __x_ABI_CTelegram_CApi_CNative_CTL_CITLError **instance);
        
        END_INTERFACE
    } __x_ABI_CTelegram_CApi_CNative_CTL_CITLErrorFactoryVtbl;

    interface __x_ABI_CTelegram_CApi_CNative_CTL_CITLErrorFactory
    {
        CONST_VTBL struct __x_ABI_CTelegram_CApi_CNative_CTL_CITLErrorFactoryVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLErrorFactory_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLErrorFactory_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLErrorFactory_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLErrorFactory_GetIids(This,iidCount,iids)	\
    ( (This)->lpVtbl -> GetIids(This,iidCount,iids) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLErrorFactory_GetRuntimeClassName(This,className)	\
    ( (This)->lpVtbl -> GetRuntimeClassName(This,className) ) 

#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLErrorFactory_GetTrustLevel(This,trustLevel)	\
    ( (This)->lpVtbl -> GetTrustLevel(This,trustLevel) ) 


#define __x_ABI_CTelegram_CApi_CNative_CTL_CITLErrorFactory_CreateTLError(This,code,text,instance)	\
    ( (This)->lpVtbl -> CreateTLError(This,code,text,instance) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* ____x_ABI_CTelegram_CApi_CNative_CTL_CITLErrorFactory_INTERFACE_DEFINED__ */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0019 */
/* [local] */ 

#ifndef RUNTIMECLASS_Telegram_Api_Native_TL_TLError_DEFINED
#define RUNTIMECLASS_Telegram_Api_Native_TL_TLError_DEFINED
extern const __declspec(selectany) _Null_terminated_ WCHAR RuntimeClass_Telegram_Api_Native_TL_TLError[] = L"Telegram.Api.Native.TL.TLError";
#endif
#if !defined(__cplusplus)
struct __x_ABI_CTelegram_CApi_CNative_CVersion
    {
    UINT32 ProtocolVersion;
    UINT32 Layer;
    UINT32 ApiId;
    } ;
#endif
#if !defined(__cplusplus)

#if !defined(__cplusplus)
/* [v1_enum] */ 
enum __x_ABI_CTelegram_CApi_CNative_CConnectionType
    {
        ConnectionType_Generic	= 1,
        ConnectionType_Download	= 2,
        ConnectionType_Upload	= 4
    } ;
#endif /* end if !defined(__cplusplus) */

#endif
#if !defined(__cplusplus)

#if !defined(__cplusplus)
/* [v1_enum] */ 
enum __x_ABI_CTelegram_CApi_CNative_CHandshakeState
    {
        HandshakeState_None	= 0,
        HandshakeState_Started	= 1,
        HandshakeState_PQ	= 2,
        HandshakeState_ServerDH	= 3,
        HandshakeState_ClientDH	= 4
    } ;
#endif /* end if !defined(__cplusplus) */

#endif
#if !defined(__cplusplus)

#if !defined(__cplusplus)
/* [v1_enum] */ 
enum __x_ABI_CTelegram_CApi_CNative_CConnectionNeworkType
    {
        ConnectionNeworkType_None	= 0,
        ConnectionNeworkType_Mobile	= 1,
        ConnectionNeworkType_WiFi	= 2,
        ConnectionNeworkType_Roaming	= 3
    } ;
#endif /* end if !defined(__cplusplus) */

#endif
#if !defined(__cplusplus)

#if !defined(__cplusplus)
/* [v1_enum] */ 
enum __x_ABI_CTelegram_CApi_CNative_CConnectionState
    {
        ConnectionState_Connecting	= 1,
        ConnectionState_WaitingForNetwork	= 2,
        ConnectionState_Connected	= 3
    } ;
#endif /* end if !defined(__cplusplus) */

#endif
#if !defined(__cplusplus)

#if !defined(__cplusplus)
/* [v1_enum] */ 
enum __x_ABI_CTelegram_CApi_CNative_CRequestFlag
    {
        RequestFlag_None	= 0,
        RequestFlag_EnableUnauthorized	= 1,
        RequestFlag_FailOnServerErrors	= 2,
        RequestFlag_CanCompress	= 4,
        RequestFlag_WithoutLogin	= 8,
        RequestFlag_TryDifferentDc	= 16,
        RequestFlag_ForceDownload	= 32,
        RequestFlag_InvokeAfter	= 64,
        RequestFlag_NeedQuickAck	= 128
    } ;
#endif /* end if !defined(__cplusplus) */

#endif
DEFINE_ENUM_FLAG_OPERATORS(ABI::Telegram::Api::Native::RequestFlag);
#if !defined(____x_ABI_CTelegram_CApi_CNative_CITLUnparsedMessage_INTERFACE_DEFINED__)
extern const __declspec(selectany) _Null_terminated_ WCHAR InterfaceName_Telegram_Api_Native_ITLUnparsedMessage[] = L"Telegram.Api.Native.ITLUnparsedMessage";
#endif /* !defined(____x_ABI_CTelegram_CApi_CNative_CITLUnparsedMessage_INTERFACE_DEFINED__) */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0019 */
/* [local] */ 

#ifdef __cplusplus
} /* end extern "C" */
namespace ABI {
    namespace Telegram {
        namespace Api {
            namespace Native {
                
                struct Version
                    {
                    UINT32 ProtocolVersion;
                    UINT32 Layer;
                    UINT32 ApiId;
                    } ;
            } /* end namespace */
        } /* end namespace */
    } /* end namespace */
} /* end namespace */

extern "C" { 
#endif

#ifdef __cplusplus
} /* end extern "C" */
namespace ABI {
    namespace Telegram {
        namespace Api {
            namespace Native {
                
                /* [v1_enum] */ 
                MIDL_ENUM ConnectionType
                    {
                        Generic	= 1,
                        Download	= 2,
                        Upload	= 4
                    } ;

                const MIDL_ENUM ConnectionType ConnectionType_Generic = ConnectionType::Generic;
                const MIDL_ENUM ConnectionType ConnectionType_Download = ConnectionType::Download;
                const MIDL_ENUM ConnectionType ConnectionType_Upload = ConnectionType::Upload;
                
            } /* end namespace */
        } /* end namespace */
    } /* end namespace */
} /* end namespace */

extern "C" { 
#endif

#ifdef __cplusplus
} /* end extern "C" */
namespace ABI {
    namespace Telegram {
        namespace Api {
            namespace Native {
                
                /* [v1_enum] */ 
                MIDL_ENUM HandshakeState
                    {
                        None	= 0,
                        Started	= 1,
                        PQ	= 2,
                        ServerDH	= 3,
                        ClientDH	= 4
                    } ;

                const MIDL_ENUM HandshakeState HandshakeState_None = HandshakeState::None;
                const MIDL_ENUM HandshakeState HandshakeState_Started = HandshakeState::Started;
                const MIDL_ENUM HandshakeState HandshakeState_PQ = HandshakeState::PQ;
                const MIDL_ENUM HandshakeState HandshakeState_ServerDH = HandshakeState::ServerDH;
                const MIDL_ENUM HandshakeState HandshakeState_ClientDH = HandshakeState::ClientDH;
                
            } /* end namespace */
        } /* end namespace */
    } /* end namespace */
} /* end namespace */

extern "C" { 
#endif

#ifdef __cplusplus
} /* end extern "C" */
namespace ABI {
    namespace Telegram {
        namespace Api {
            namespace Native {
                
                /* [v1_enum] */ 
                MIDL_ENUM ConnectionNeworkType
                    {
                        None	= 0,
                        Mobile	= 1,
                        WiFi	= 2,
                        Roaming	= 3
                    } ;

                const MIDL_ENUM ConnectionNeworkType ConnectionNeworkType_None = ConnectionNeworkType::None;
                const MIDL_ENUM ConnectionNeworkType ConnectionNeworkType_Mobile = ConnectionNeworkType::Mobile;
                const MIDL_ENUM ConnectionNeworkType ConnectionNeworkType_WiFi = ConnectionNeworkType::WiFi;
                const MIDL_ENUM ConnectionNeworkType ConnectionNeworkType_Roaming = ConnectionNeworkType::Roaming;
                
            } /* end namespace */
        } /* end namespace */
    } /* end namespace */
} /* end namespace */

extern "C" { 
#endif

#ifdef __cplusplus
} /* end extern "C" */
namespace ABI {
    namespace Telegram {
        namespace Api {
            namespace Native {
                
                /* [v1_enum] */ 
                MIDL_ENUM ConnectionState
                    {
                        Connecting	= 1,
                        WaitingForNetwork	= 2,
                        Connected	= 3
                    } ;

                const MIDL_ENUM ConnectionState ConnectionState_Connecting = ConnectionState::Connecting;
                const MIDL_ENUM ConnectionState ConnectionState_WaitingForNetwork = ConnectionState::WaitingForNetwork;
                const MIDL_ENUM ConnectionState ConnectionState_Connected = ConnectionState::Connected;
                
            } /* end namespace */
        } /* end namespace */
    } /* end namespace */
} /* end namespace */

extern "C" { 
#endif

#ifdef __cplusplus
} /* end extern "C" */
namespace ABI {
    namespace Telegram {
        namespace Api {
            namespace Native {
                
                /* [v1_enum] */ 
                MIDL_ENUM RequestFlag
                    {
                        None	= 0,
                        EnableUnauthorized	= 1,
                        FailOnServerErrors	= 2,
                        CanCompress	= 4,
                        WithoutLogin	= 8,
                        TryDifferentDc	= 16,
                        ForceDownload	= 32,
                        InvokeAfter	= 64,
                        NeedQuickAck	= 128
                    } ;

                const MIDL_ENUM RequestFlag RequestFlag_None = RequestFlag::None;
                const MIDL_ENUM RequestFlag RequestFlag_EnableUnauthorized = RequestFlag::EnableUnauthorized;
                const MIDL_ENUM RequestFlag RequestFlag_FailOnServerErrors = RequestFlag::FailOnServerErrors;
                const MIDL_ENUM RequestFlag RequestFlag_CanCompress = RequestFlag::CanCompress;
                const MIDL_ENUM RequestFlag RequestFlag_WithoutLogin = RequestFlag::WithoutLogin;
                const MIDL_ENUM RequestFlag RequestFlag_TryDifferentDc = RequestFlag::TryDifferentDc;
                const MIDL_ENUM RequestFlag RequestFlag_ForceDownload = RequestFlag::ForceDownload;
                const MIDL_ENUM RequestFlag RequestFlag_InvokeAfter = RequestFlag::InvokeAfter;
                const MIDL_ENUM RequestFlag RequestFlag_NeedQuickAck = RequestFlag::NeedQuickAck;
                
            } /* end namespace */
        } /* end namespace */
    } /* end namespace */
} /* end namespace */

extern "C" { 
#endif



extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0019_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0019_v0_0_s_ifspec;

#ifndef ____x_ABI_CTelegram_CApi_CNative_CITLUnparsedMessage_INTERFACE_DEFINED__
#define ____x_ABI_CTelegram_CApi_CNative_CITLUnparsedMessage_INTERFACE_DEFINED__

/* interface __x_ABI_CTelegram_CApi_CNative_CITLUnparsedMessage */
/* [uuid][object] */ 



/* interface ABI::Telegram::Api::Native::ITLUnparsedMessage */
/* [uuid][object] */ 


EXTERN_C const IID IID___x_ABI_CTelegram_CApi_CNative_CITLUnparsedMessage;

#if defined(__cplusplus) && !defined(CINTERFACE)
    } /* end extern "C" */
    namespace ABI {
        namespace Telegram {
            namespace Api {
                namespace Native {
                    
                    MIDL_INTERFACE("7C4C00BC-3D6A-4623-B6BB-C8CF22F5A839")
                    ITLUnparsedMessage : public IInspectable
                    {
                    public:
                        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_MessageId( 
                            /* [out][retval] */ INT64 *value) = 0;
                        
                        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_ConnectionType( 
                            /* [out][retval] */ ABI::Telegram::Api::Native::ConnectionType *value) = 0;
                        
                        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Reader( 
                            /* [out][retval] */ ABI::Telegram::Api::Native::TL::ITLBinaryReader **value) = 0;
                        
                    };

                    extern const __declspec(selectany) IID & IID_ITLUnparsedMessage = __uuidof(ITLUnparsedMessage);

                    
                }  /* end namespace */
            }  /* end namespace */
        }  /* end namespace */
    }  /* end namespace */
    extern "C" { 
    
#else 	/* C style interface */

    typedef struct __x_ABI_CTelegram_CApi_CNative_CITLUnparsedMessageVtbl
    {
        BEGIN_INTERFACE
        
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __x_ABI_CTelegram_CApi_CNative_CITLUnparsedMessage * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __x_ABI_CTelegram_CApi_CNative_CITLUnparsedMessage * This);
        
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __x_ABI_CTelegram_CApi_CNative_CITLUnparsedMessage * This);
        
        HRESULT ( STDMETHODCALLTYPE *GetIids )( 
            __x_ABI_CTelegram_CApi_CNative_CITLUnparsedMessage * This,
            /* [out] */ ULONG *iidCount,
            /* [size_is][size_is][out] */ IID **iids);
        
        HRESULT ( STDMETHODCALLTYPE *GetRuntimeClassName )( 
            __x_ABI_CTelegram_CApi_CNative_CITLUnparsedMessage * This,
            /* [out] */ HSTRING *className);
        
        HRESULT ( STDMETHODCALLTYPE *GetTrustLevel )( 
            __x_ABI_CTelegram_CApi_CNative_CITLUnparsedMessage * This,
            /* [out] */ TrustLevel *trustLevel);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_MessageId )( 
            __x_ABI_CTelegram_CApi_CNative_CITLUnparsedMessage * This,
            /* [out][retval] */ INT64 *value);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_ConnectionType )( 
            __x_ABI_CTelegram_CApi_CNative_CITLUnparsedMessage * This,
            /* [out][retval] */ __x_ABI_CTelegram_CApi_CNative_CConnectionType *value);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Reader )( 
            __x_ABI_CTelegram_CApi_CNative_CITLUnparsedMessage * This,
            /* [out][retval] */ __x_ABI_CTelegram_CApi_CNative_CTL_CITLBinaryReader **value);
        
        END_INTERFACE
    } __x_ABI_CTelegram_CApi_CNative_CITLUnparsedMessageVtbl;

    interface __x_ABI_CTelegram_CApi_CNative_CITLUnparsedMessage
    {
        CONST_VTBL struct __x_ABI_CTelegram_CApi_CNative_CITLUnparsedMessageVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define __x_ABI_CTelegram_CApi_CNative_CITLUnparsedMessage_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define __x_ABI_CTelegram_CApi_CNative_CITLUnparsedMessage_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define __x_ABI_CTelegram_CApi_CNative_CITLUnparsedMessage_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define __x_ABI_CTelegram_CApi_CNative_CITLUnparsedMessage_GetIids(This,iidCount,iids)	\
    ( (This)->lpVtbl -> GetIids(This,iidCount,iids) ) 

#define __x_ABI_CTelegram_CApi_CNative_CITLUnparsedMessage_GetRuntimeClassName(This,className)	\
    ( (This)->lpVtbl -> GetRuntimeClassName(This,className) ) 

#define __x_ABI_CTelegram_CApi_CNative_CITLUnparsedMessage_GetTrustLevel(This,trustLevel)	\
    ( (This)->lpVtbl -> GetTrustLevel(This,trustLevel) ) 


#define __x_ABI_CTelegram_CApi_CNative_CITLUnparsedMessage_get_MessageId(This,value)	\
    ( (This)->lpVtbl -> get_MessageId(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CITLUnparsedMessage_get_ConnectionType(This,value)	\
    ( (This)->lpVtbl -> get_ConnectionType(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CITLUnparsedMessage_get_Reader(This,value)	\
    ( (This)->lpVtbl -> get_Reader(This,value) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* ____x_ABI_CTelegram_CApi_CNative_CITLUnparsedMessage_INTERFACE_DEFINED__ */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0020 */
/* [local] */ 

#ifndef RUNTIMECLASS_Telegram_Api_Native_TLUnparsedMessage_DEFINED
#define RUNTIMECLASS_Telegram_Api_Native_TLUnparsedMessage_DEFINED
extern const __declspec(selectany) _Null_terminated_ WCHAR RuntimeClass_Telegram_Api_Native_TLUnparsedMessage[] = L"Telegram.Api.Native.TLUnparsedMessage";
#endif


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0020 */
/* [local] */ 



extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0020_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0020_v0_0_s_ifspec;

#ifndef ____x_ABI_CTelegram_CApi_CNative_CISendRequestCompletedCallback_INTERFACE_DEFINED__
#define ____x_ABI_CTelegram_CApi_CNative_CISendRequestCompletedCallback_INTERFACE_DEFINED__

/* interface __x_ABI_CTelegram_CApi_CNative_CISendRequestCompletedCallback */
/* [uuid][object] */ 



/* interface ABI::Telegram::Api::Native::ISendRequestCompletedCallback */
/* [uuid][object] */ 


EXTERN_C const IID IID___x_ABI_CTelegram_CApi_CNative_CISendRequestCompletedCallback;

#if defined(__cplusplus) && !defined(CINTERFACE)
    } /* end extern "C" */
    namespace ABI {
        namespace Telegram {
            namespace Api {
                namespace Native {
                    
                    MIDL_INTERFACE("CA172FA9-416D-4BD9-AB8C-B8696391D38F")
                    ISendRequestCompletedCallback : public IUnknown
                    {
                    public:
                        virtual HRESULT STDMETHODCALLTYPE Invoke( 
                            /* [in] */ ABI::Telegram::Api::Native::ITLUnparsedMessage *response,
                            HRESULT error) = 0;
                        
                    };

                    extern const __declspec(selectany) IID & IID_ISendRequestCompletedCallback = __uuidof(ISendRequestCompletedCallback);

                    
                }  /* end namespace */
            }  /* end namespace */
        }  /* end namespace */
    }  /* end namespace */
    extern "C" { 
    
#else 	/* C style interface */

    typedef struct __x_ABI_CTelegram_CApi_CNative_CISendRequestCompletedCallbackVtbl
    {
        BEGIN_INTERFACE
        
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __x_ABI_CTelegram_CApi_CNative_CISendRequestCompletedCallback * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __x_ABI_CTelegram_CApi_CNative_CISendRequestCompletedCallback * This);
        
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __x_ABI_CTelegram_CApi_CNative_CISendRequestCompletedCallback * This);
        
        HRESULT ( STDMETHODCALLTYPE *Invoke )( 
            __x_ABI_CTelegram_CApi_CNative_CISendRequestCompletedCallback * This,
            /* [in] */ __x_ABI_CTelegram_CApi_CNative_CITLUnparsedMessage *response,
            HRESULT error);
        
        END_INTERFACE
    } __x_ABI_CTelegram_CApi_CNative_CISendRequestCompletedCallbackVtbl;

    interface __x_ABI_CTelegram_CApi_CNative_CISendRequestCompletedCallback
    {
        CONST_VTBL struct __x_ABI_CTelegram_CApi_CNative_CISendRequestCompletedCallbackVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define __x_ABI_CTelegram_CApi_CNative_CISendRequestCompletedCallback_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define __x_ABI_CTelegram_CApi_CNative_CISendRequestCompletedCallback_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define __x_ABI_CTelegram_CApi_CNative_CISendRequestCompletedCallback_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define __x_ABI_CTelegram_CApi_CNative_CISendRequestCompletedCallback_Invoke(This,response,error)	\
    ( (This)->lpVtbl -> Invoke(This,response,error) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* ____x_ABI_CTelegram_CApi_CNative_CISendRequestCompletedCallback_INTERFACE_DEFINED__ */


#ifndef ____x_ABI_CTelegram_CApi_CNative_CIRequestQuickAckReceivedCallback_INTERFACE_DEFINED__
#define ____x_ABI_CTelegram_CApi_CNative_CIRequestQuickAckReceivedCallback_INTERFACE_DEFINED__

/* interface __x_ABI_CTelegram_CApi_CNative_CIRequestQuickAckReceivedCallback */
/* [uuid][object] */ 



/* interface ABI::Telegram::Api::Native::IRequestQuickAckReceivedCallback */
/* [uuid][object] */ 


EXTERN_C const IID IID___x_ABI_CTelegram_CApi_CNative_CIRequestQuickAckReceivedCallback;

#if defined(__cplusplus) && !defined(CINTERFACE)
    } /* end extern "C" */
    namespace ABI {
        namespace Telegram {
            namespace Api {
                namespace Native {
                    
                    MIDL_INTERFACE("8F88DCA2-FC97-4C94-B564-3DDBF1675477")
                    IRequestQuickAckReceivedCallback : public IUnknown
                    {
                    public:
                        virtual HRESULT STDMETHODCALLTYPE Invoke( void) = 0;
                        
                    };

                    extern const __declspec(selectany) IID & IID_IRequestQuickAckReceivedCallback = __uuidof(IRequestQuickAckReceivedCallback);

                    
                }  /* end namespace */
            }  /* end namespace */
        }  /* end namespace */
    }  /* end namespace */
    extern "C" { 
    
#else 	/* C style interface */

    typedef struct __x_ABI_CTelegram_CApi_CNative_CIRequestQuickAckReceivedCallbackVtbl
    {
        BEGIN_INTERFACE
        
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __x_ABI_CTelegram_CApi_CNative_CIRequestQuickAckReceivedCallback * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __x_ABI_CTelegram_CApi_CNative_CIRequestQuickAckReceivedCallback * This);
        
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __x_ABI_CTelegram_CApi_CNative_CIRequestQuickAckReceivedCallback * This);
        
        HRESULT ( STDMETHODCALLTYPE *Invoke )( 
            __x_ABI_CTelegram_CApi_CNative_CIRequestQuickAckReceivedCallback * This);
        
        END_INTERFACE
    } __x_ABI_CTelegram_CApi_CNative_CIRequestQuickAckReceivedCallbackVtbl;

    interface __x_ABI_CTelegram_CApi_CNative_CIRequestQuickAckReceivedCallback
    {
        CONST_VTBL struct __x_ABI_CTelegram_CApi_CNative_CIRequestQuickAckReceivedCallbackVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define __x_ABI_CTelegram_CApi_CNative_CIRequestQuickAckReceivedCallback_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIRequestQuickAckReceivedCallback_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIRequestQuickAckReceivedCallback_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define __x_ABI_CTelegram_CApi_CNative_CIRequestQuickAckReceivedCallback_Invoke(This)	\
    ( (This)->lpVtbl -> Invoke(This) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* ____x_ABI_CTelegram_CApi_CNative_CIRequestQuickAckReceivedCallback_INTERFACE_DEFINED__ */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0022 */
/* [local] */ 

#if !defined(____x_ABI_CTelegram_CApi_CNative_CIUserConfiguration_INTERFACE_DEFINED__)
extern const __declspec(selectany) _Null_terminated_ WCHAR InterfaceName_Telegram_Api_Native_IUserConfiguration[] = L"Telegram.Api.Native.IUserConfiguration";
#endif /* !defined(____x_ABI_CTelegram_CApi_CNative_CIUserConfiguration_INTERFACE_DEFINED__) */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0022 */
/* [local] */ 



extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0022_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0022_v0_0_s_ifspec;

#ifndef ____x_ABI_CTelegram_CApi_CNative_CIUserConfiguration_INTERFACE_DEFINED__
#define ____x_ABI_CTelegram_CApi_CNative_CIUserConfiguration_INTERFACE_DEFINED__

/* interface __x_ABI_CTelegram_CApi_CNative_CIUserConfiguration */
/* [uuid][object] */ 



/* interface ABI::Telegram::Api::Native::IUserConfiguration */
/* [uuid][object] */ 


EXTERN_C const IID IID___x_ABI_CTelegram_CApi_CNative_CIUserConfiguration;

#if defined(__cplusplus) && !defined(CINTERFACE)
    } /* end extern "C" */
    namespace ABI {
        namespace Telegram {
            namespace Api {
                namespace Native {
                    
                    MIDL_INTERFACE("27960B7A-14F2-4F63-AA65-6201B9A190EC")
                    IUserConfiguration : public IInspectable
                    {
                    public:
                        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_DeviceModel( 
                            /* [out][retval] */ HSTRING *value) = 0;
                        
                        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_SystemVersion( 
                            /* [out][retval] */ HSTRING *value) = 0;
                        
                        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_AppVersion( 
                            /* [out][retval] */ HSTRING *value) = 0;
                        
                        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Language( 
                            /* [out][retval] */ HSTRING *value) = 0;
                        
                    };

                    extern const __declspec(selectany) IID & IID_IUserConfiguration = __uuidof(IUserConfiguration);

                    
                }  /* end namespace */
            }  /* end namespace */
        }  /* end namespace */
    }  /* end namespace */
    extern "C" { 
    
#else 	/* C style interface */

    typedef struct __x_ABI_CTelegram_CApi_CNative_CIUserConfigurationVtbl
    {
        BEGIN_INTERFACE
        
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __x_ABI_CTelegram_CApi_CNative_CIUserConfiguration * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __x_ABI_CTelegram_CApi_CNative_CIUserConfiguration * This);
        
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __x_ABI_CTelegram_CApi_CNative_CIUserConfiguration * This);
        
        HRESULT ( STDMETHODCALLTYPE *GetIids )( 
            __x_ABI_CTelegram_CApi_CNative_CIUserConfiguration * This,
            /* [out] */ ULONG *iidCount,
            /* [size_is][size_is][out] */ IID **iids);
        
        HRESULT ( STDMETHODCALLTYPE *GetRuntimeClassName )( 
            __x_ABI_CTelegram_CApi_CNative_CIUserConfiguration * This,
            /* [out] */ HSTRING *className);
        
        HRESULT ( STDMETHODCALLTYPE *GetTrustLevel )( 
            __x_ABI_CTelegram_CApi_CNative_CIUserConfiguration * This,
            /* [out] */ TrustLevel *trustLevel);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_DeviceModel )( 
            __x_ABI_CTelegram_CApi_CNative_CIUserConfiguration * This,
            /* [out][retval] */ HSTRING *value);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_SystemVersion )( 
            __x_ABI_CTelegram_CApi_CNative_CIUserConfiguration * This,
            /* [out][retval] */ HSTRING *value);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_AppVersion )( 
            __x_ABI_CTelegram_CApi_CNative_CIUserConfiguration * This,
            /* [out][retval] */ HSTRING *value);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Language )( 
            __x_ABI_CTelegram_CApi_CNative_CIUserConfiguration * This,
            /* [out][retval] */ HSTRING *value);
        
        END_INTERFACE
    } __x_ABI_CTelegram_CApi_CNative_CIUserConfigurationVtbl;

    interface __x_ABI_CTelegram_CApi_CNative_CIUserConfiguration
    {
        CONST_VTBL struct __x_ABI_CTelegram_CApi_CNative_CIUserConfigurationVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define __x_ABI_CTelegram_CApi_CNative_CIUserConfiguration_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIUserConfiguration_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIUserConfiguration_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define __x_ABI_CTelegram_CApi_CNative_CIUserConfiguration_GetIids(This,iidCount,iids)	\
    ( (This)->lpVtbl -> GetIids(This,iidCount,iids) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIUserConfiguration_GetRuntimeClassName(This,className)	\
    ( (This)->lpVtbl -> GetRuntimeClassName(This,className) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIUserConfiguration_GetTrustLevel(This,trustLevel)	\
    ( (This)->lpVtbl -> GetTrustLevel(This,trustLevel) ) 


#define __x_ABI_CTelegram_CApi_CNative_CIUserConfiguration_get_DeviceModel(This,value)	\
    ( (This)->lpVtbl -> get_DeviceModel(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIUserConfiguration_get_SystemVersion(This,value)	\
    ( (This)->lpVtbl -> get_SystemVersion(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIUserConfiguration_get_AppVersion(This,value)	\
    ( (This)->lpVtbl -> get_AppVersion(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIUserConfiguration_get_Language(This,value)	\
    ( (This)->lpVtbl -> get_Language(This,value) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* ____x_ABI_CTelegram_CApi_CNative_CIUserConfiguration_INTERFACE_DEFINED__ */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0023 */
/* [local] */ 

#if !defined(____x_ABI_CTelegram_CApi_CNative_CIDatacenter_INTERFACE_DEFINED__)
extern const __declspec(selectany) _Null_terminated_ WCHAR InterfaceName_Telegram_Api_Native_IDatacenter[] = L"Telegram.Api.Native.IDatacenter";
#endif /* !defined(____x_ABI_CTelegram_CApi_CNative_CIDatacenter_INTERFACE_DEFINED__) */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0023 */
/* [local] */ 



extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0023_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0023_v0_0_s_ifspec;

#ifndef ____x_ABI_CTelegram_CApi_CNative_CIDatacenter_INTERFACE_DEFINED__
#define ____x_ABI_CTelegram_CApi_CNative_CIDatacenter_INTERFACE_DEFINED__

/* interface __x_ABI_CTelegram_CApi_CNative_CIDatacenter */
/* [uuid][object] */ 



/* interface ABI::Telegram::Api::Native::IDatacenter */
/* [uuid][object] */ 


EXTERN_C const IID IID___x_ABI_CTelegram_CApi_CNative_CIDatacenter;

#if defined(__cplusplus) && !defined(CINTERFACE)
    } /* end extern "C" */
    namespace ABI {
        namespace Telegram {
            namespace Api {
                namespace Native {
                    
                    MIDL_INTERFACE("ACBC9624-7B96-417D-A9F9-A7F93C195C86")
                    IDatacenter : public IInspectable
                    {
                    public:
                        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Id( 
                            /* [out][retval] */ UINT32 *value) = 0;
                        
                        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_HandshakeState( 
                            /* [out][retval] */ ABI::Telegram::Api::Native::HandshakeState *value) = 0;
                        
                        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_ServerSalt( 
                            /* [out][retval] */ INT64 *value) = 0;
                        
                        virtual HRESULT STDMETHODCALLTYPE GetCurrentAddress( 
                            /* [in] */ ABI::Telegram::Api::Native::ConnectionType connectionType,
                            /* [in] */ boolean ipv6,
                            /* [out][retval] */ HSTRING *value) = 0;
                        
                        virtual HRESULT STDMETHODCALLTYPE GetCurrentPort( 
                            /* [in] */ ABI::Telegram::Api::Native::ConnectionType connectionType,
                            /* [in] */ boolean ipv6,
                            /* [out][retval] */ UINT32 *value) = 0;
                        
                    };

                    extern const __declspec(selectany) IID & IID_IDatacenter = __uuidof(IDatacenter);

                    
                }  /* end namespace */
            }  /* end namespace */
        }  /* end namespace */
    }  /* end namespace */
    extern "C" { 
    
#else 	/* C style interface */

    typedef struct __x_ABI_CTelegram_CApi_CNative_CIDatacenterVtbl
    {
        BEGIN_INTERFACE
        
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __x_ABI_CTelegram_CApi_CNative_CIDatacenter * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __x_ABI_CTelegram_CApi_CNative_CIDatacenter * This);
        
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __x_ABI_CTelegram_CApi_CNative_CIDatacenter * This);
        
        HRESULT ( STDMETHODCALLTYPE *GetIids )( 
            __x_ABI_CTelegram_CApi_CNative_CIDatacenter * This,
            /* [out] */ ULONG *iidCount,
            /* [size_is][size_is][out] */ IID **iids);
        
        HRESULT ( STDMETHODCALLTYPE *GetRuntimeClassName )( 
            __x_ABI_CTelegram_CApi_CNative_CIDatacenter * This,
            /* [out] */ HSTRING *className);
        
        HRESULT ( STDMETHODCALLTYPE *GetTrustLevel )( 
            __x_ABI_CTelegram_CApi_CNative_CIDatacenter * This,
            /* [out] */ TrustLevel *trustLevel);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Id )( 
            __x_ABI_CTelegram_CApi_CNative_CIDatacenter * This,
            /* [out][retval] */ UINT32 *value);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_HandshakeState )( 
            __x_ABI_CTelegram_CApi_CNative_CIDatacenter * This,
            /* [out][retval] */ __x_ABI_CTelegram_CApi_CNative_CHandshakeState *value);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_ServerSalt )( 
            __x_ABI_CTelegram_CApi_CNative_CIDatacenter * This,
            /* [out][retval] */ INT64 *value);
        
        HRESULT ( STDMETHODCALLTYPE *GetCurrentAddress )( 
            __x_ABI_CTelegram_CApi_CNative_CIDatacenter * This,
            /* [in] */ __x_ABI_CTelegram_CApi_CNative_CConnectionType connectionType,
            /* [in] */ boolean ipv6,
            /* [out][retval] */ HSTRING *value);
        
        HRESULT ( STDMETHODCALLTYPE *GetCurrentPort )( 
            __x_ABI_CTelegram_CApi_CNative_CIDatacenter * This,
            /* [in] */ __x_ABI_CTelegram_CApi_CNative_CConnectionType connectionType,
            /* [in] */ boolean ipv6,
            /* [out][retval] */ UINT32 *value);
        
        END_INTERFACE
    } __x_ABI_CTelegram_CApi_CNative_CIDatacenterVtbl;

    interface __x_ABI_CTelegram_CApi_CNative_CIDatacenter
    {
        CONST_VTBL struct __x_ABI_CTelegram_CApi_CNative_CIDatacenterVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define __x_ABI_CTelegram_CApi_CNative_CIDatacenter_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIDatacenter_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIDatacenter_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define __x_ABI_CTelegram_CApi_CNative_CIDatacenter_GetIids(This,iidCount,iids)	\
    ( (This)->lpVtbl -> GetIids(This,iidCount,iids) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIDatacenter_GetRuntimeClassName(This,className)	\
    ( (This)->lpVtbl -> GetRuntimeClassName(This,className) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIDatacenter_GetTrustLevel(This,trustLevel)	\
    ( (This)->lpVtbl -> GetTrustLevel(This,trustLevel) ) 


#define __x_ABI_CTelegram_CApi_CNative_CIDatacenter_get_Id(This,value)	\
    ( (This)->lpVtbl -> get_Id(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIDatacenter_get_HandshakeState(This,value)	\
    ( (This)->lpVtbl -> get_HandshakeState(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIDatacenter_get_ServerSalt(This,value)	\
    ( (This)->lpVtbl -> get_ServerSalt(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIDatacenter_GetCurrentAddress(This,connectionType,ipv6,value)	\
    ( (This)->lpVtbl -> GetCurrentAddress(This,connectionType,ipv6,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIDatacenter_GetCurrentPort(This,connectionType,ipv6,value)	\
    ( (This)->lpVtbl -> GetCurrentPort(This,connectionType,ipv6,value) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* ____x_ABI_CTelegram_CApi_CNative_CIDatacenter_INTERFACE_DEFINED__ */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0024 */
/* [local] */ 

#ifndef RUNTIMECLASS_Telegram_Api_Native_Datacenter_DEFINED
#define RUNTIMECLASS_Telegram_Api_Native_Datacenter_DEFINED
extern const __declspec(selectany) _Null_terminated_ WCHAR RuntimeClass_Telegram_Api_Native_Datacenter[] = L"Telegram.Api.Native.Datacenter";
#endif
#if !defined(____x_ABI_CTelegram_CApi_CNative_CIConnection_INTERFACE_DEFINED__)
extern const __declspec(selectany) _Null_terminated_ WCHAR InterfaceName_Telegram_Api_Native_IConnection[] = L"Telegram.Api.Native.IConnection";
#endif /* !defined(____x_ABI_CTelegram_CApi_CNative_CIConnection_INTERFACE_DEFINED__) */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0024 */
/* [local] */ 



extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0024_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0024_v0_0_s_ifspec;

#ifndef ____x_ABI_CTelegram_CApi_CNative_CIConnection_INTERFACE_DEFINED__
#define ____x_ABI_CTelegram_CApi_CNative_CIConnection_INTERFACE_DEFINED__

/* interface __x_ABI_CTelegram_CApi_CNative_CIConnection */
/* [uuid][object] */ 



/* interface ABI::Telegram::Api::Native::IConnection */
/* [uuid][object] */ 


EXTERN_C const IID IID___x_ABI_CTelegram_CApi_CNative_CIConnection;

#if defined(__cplusplus) && !defined(CINTERFACE)
    } /* end extern "C" */
    namespace ABI {
        namespace Telegram {
            namespace Api {
                namespace Native {
                    
                    MIDL_INTERFACE("108FB951-3940-4FF5-A8A1-ED449D305029")
                    IConnection : public IInspectable
                    {
                    public:
                        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Token( 
                            /* [out][retval] */ UINT32 *value) = 0;
                        
                        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Datacenter( 
                            /* [out][retval] */ ABI::Telegram::Api::Native::IDatacenter **value) = 0;
                        
                        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Type( 
                            /* [out][retval] */ ABI::Telegram::Api::Native::ConnectionType *value) = 0;
                        
                        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_CurrentNetworkType( 
                            /* [out][retval] */ ABI::Telegram::Api::Native::ConnectionNeworkType *value) = 0;
                        
                        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_SessionId( 
                            /* [out][retval] */ INT64 *value) = 0;
                        
                    };

                    extern const __declspec(selectany) IID & IID_IConnection = __uuidof(IConnection);

                    
                }  /* end namespace */
            }  /* end namespace */
        }  /* end namespace */
    }  /* end namespace */
    extern "C" { 
    
#else 	/* C style interface */

    typedef struct __x_ABI_CTelegram_CApi_CNative_CIConnectionVtbl
    {
        BEGIN_INTERFACE
        
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __x_ABI_CTelegram_CApi_CNative_CIConnection * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __x_ABI_CTelegram_CApi_CNative_CIConnection * This);
        
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __x_ABI_CTelegram_CApi_CNative_CIConnection * This);
        
        HRESULT ( STDMETHODCALLTYPE *GetIids )( 
            __x_ABI_CTelegram_CApi_CNative_CIConnection * This,
            /* [out] */ ULONG *iidCount,
            /* [size_is][size_is][out] */ IID **iids);
        
        HRESULT ( STDMETHODCALLTYPE *GetRuntimeClassName )( 
            __x_ABI_CTelegram_CApi_CNative_CIConnection * This,
            /* [out] */ HSTRING *className);
        
        HRESULT ( STDMETHODCALLTYPE *GetTrustLevel )( 
            __x_ABI_CTelegram_CApi_CNative_CIConnection * This,
            /* [out] */ TrustLevel *trustLevel);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Token )( 
            __x_ABI_CTelegram_CApi_CNative_CIConnection * This,
            /* [out][retval] */ UINT32 *value);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Datacenter )( 
            __x_ABI_CTelegram_CApi_CNative_CIConnection * This,
            /* [out][retval] */ __x_ABI_CTelegram_CApi_CNative_CIDatacenter **value);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Type )( 
            __x_ABI_CTelegram_CApi_CNative_CIConnection * This,
            /* [out][retval] */ __x_ABI_CTelegram_CApi_CNative_CConnectionType *value);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_CurrentNetworkType )( 
            __x_ABI_CTelegram_CApi_CNative_CIConnection * This,
            /* [out][retval] */ __x_ABI_CTelegram_CApi_CNative_CConnectionNeworkType *value);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_SessionId )( 
            __x_ABI_CTelegram_CApi_CNative_CIConnection * This,
            /* [out][retval] */ INT64 *value);
        
        END_INTERFACE
    } __x_ABI_CTelegram_CApi_CNative_CIConnectionVtbl;

    interface __x_ABI_CTelegram_CApi_CNative_CIConnection
    {
        CONST_VTBL struct __x_ABI_CTelegram_CApi_CNative_CIConnectionVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define __x_ABI_CTelegram_CApi_CNative_CIConnection_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIConnection_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIConnection_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define __x_ABI_CTelegram_CApi_CNative_CIConnection_GetIids(This,iidCount,iids)	\
    ( (This)->lpVtbl -> GetIids(This,iidCount,iids) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIConnection_GetRuntimeClassName(This,className)	\
    ( (This)->lpVtbl -> GetRuntimeClassName(This,className) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIConnection_GetTrustLevel(This,trustLevel)	\
    ( (This)->lpVtbl -> GetTrustLevel(This,trustLevel) ) 


#define __x_ABI_CTelegram_CApi_CNative_CIConnection_get_Token(This,value)	\
    ( (This)->lpVtbl -> get_Token(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIConnection_get_Datacenter(This,value)	\
    ( (This)->lpVtbl -> get_Datacenter(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIConnection_get_Type(This,value)	\
    ( (This)->lpVtbl -> get_Type(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIConnection_get_CurrentNetworkType(This,value)	\
    ( (This)->lpVtbl -> get_CurrentNetworkType(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIConnection_get_SessionId(This,value)	\
    ( (This)->lpVtbl -> get_SessionId(This,value) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* ____x_ABI_CTelegram_CApi_CNative_CIConnection_INTERFACE_DEFINED__ */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0025 */
/* [local] */ 

#ifndef RUNTIMECLASS_Telegram_Api_Native_Connection_DEFINED
#define RUNTIMECLASS_Telegram_Api_Native_Connection_DEFINED
extern const __declspec(selectany) _Null_terminated_ WCHAR RuntimeClass_Telegram_Api_Native_Connection[] = L"Telegram.Api.Native.Connection";
#endif
#if !defined(____x_ABI_CTelegram_CApi_CNative_CIConnectionManagerStatics_INTERFACE_DEFINED__)
extern const __declspec(selectany) _Null_terminated_ WCHAR InterfaceName_Telegram_Api_Native_IConnectionManagerStatics[] = L"Telegram.Api.Native.IConnectionManagerStatics";
#endif /* !defined(____x_ABI_CTelegram_CApi_CNative_CIConnectionManagerStatics_INTERFACE_DEFINED__) */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0025 */
/* [local] */ 



extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0025_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0025_v0_0_s_ifspec;

#ifndef ____x_ABI_CTelegram_CApi_CNative_CIConnectionManagerStatics_INTERFACE_DEFINED__
#define ____x_ABI_CTelegram_CApi_CNative_CIConnectionManagerStatics_INTERFACE_DEFINED__

/* interface __x_ABI_CTelegram_CApi_CNative_CIConnectionManagerStatics */
/* [uuid][object] */ 



/* interface ABI::Telegram::Api::Native::IConnectionManagerStatics */
/* [uuid][object] */ 


EXTERN_C const IID IID___x_ABI_CTelegram_CApi_CNative_CIConnectionManagerStatics;

#if defined(__cplusplus) && !defined(CINTERFACE)
    } /* end extern "C" */
    namespace ABI {
        namespace Telegram {
            namespace Api {
                namespace Native {
                    
                    MIDL_INTERFACE("6945B11D-9663-4E6E-B866-7A1AB6A98349")
                    IConnectionManagerStatics : public IInspectable
                    {
                    public:
                        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Instance( 
                            /* [out][retval] */ ABI::Telegram::Api::Native::IConnectionManager **value) = 0;
                        
                        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Version( 
                            /* [out][retval] */ ABI::Telegram::Api::Native::Version *value) = 0;
                        
                    };

                    extern const __declspec(selectany) IID & IID_IConnectionManagerStatics = __uuidof(IConnectionManagerStatics);

                    
                }  /* end namespace */
            }  /* end namespace */
        }  /* end namespace */
    }  /* end namespace */
    extern "C" { 
    
#else 	/* C style interface */

    typedef struct __x_ABI_CTelegram_CApi_CNative_CIConnectionManagerStaticsVtbl
    {
        BEGIN_INTERFACE
        
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __x_ABI_CTelegram_CApi_CNative_CIConnectionManagerStatics * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __x_ABI_CTelegram_CApi_CNative_CIConnectionManagerStatics * This);
        
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __x_ABI_CTelegram_CApi_CNative_CIConnectionManagerStatics * This);
        
        HRESULT ( STDMETHODCALLTYPE *GetIids )( 
            __x_ABI_CTelegram_CApi_CNative_CIConnectionManagerStatics * This,
            /* [out] */ ULONG *iidCount,
            /* [size_is][size_is][out] */ IID **iids);
        
        HRESULT ( STDMETHODCALLTYPE *GetRuntimeClassName )( 
            __x_ABI_CTelegram_CApi_CNative_CIConnectionManagerStatics * This,
            /* [out] */ HSTRING *className);
        
        HRESULT ( STDMETHODCALLTYPE *GetTrustLevel )( 
            __x_ABI_CTelegram_CApi_CNative_CIConnectionManagerStatics * This,
            /* [out] */ TrustLevel *trustLevel);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Instance )( 
            __x_ABI_CTelegram_CApi_CNative_CIConnectionManagerStatics * This,
            /* [out][retval] */ __x_ABI_CTelegram_CApi_CNative_CIConnectionManager **value);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Version )( 
            __x_ABI_CTelegram_CApi_CNative_CIConnectionManagerStatics * This,
            /* [out][retval] */ __x_ABI_CTelegram_CApi_CNative_CVersion *value);
        
        END_INTERFACE
    } __x_ABI_CTelegram_CApi_CNative_CIConnectionManagerStaticsVtbl;

    interface __x_ABI_CTelegram_CApi_CNative_CIConnectionManagerStatics
    {
        CONST_VTBL struct __x_ABI_CTelegram_CApi_CNative_CIConnectionManagerStaticsVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define __x_ABI_CTelegram_CApi_CNative_CIConnectionManagerStatics_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIConnectionManagerStatics_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIConnectionManagerStatics_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define __x_ABI_CTelegram_CApi_CNative_CIConnectionManagerStatics_GetIids(This,iidCount,iids)	\
    ( (This)->lpVtbl -> GetIids(This,iidCount,iids) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIConnectionManagerStatics_GetRuntimeClassName(This,className)	\
    ( (This)->lpVtbl -> GetRuntimeClassName(This,className) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIConnectionManagerStatics_GetTrustLevel(This,trustLevel)	\
    ( (This)->lpVtbl -> GetTrustLevel(This,trustLevel) ) 


#define __x_ABI_CTelegram_CApi_CNative_CIConnectionManagerStatics_get_Instance(This,value)	\
    ( (This)->lpVtbl -> get_Instance(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIConnectionManagerStatics_get_Version(This,value)	\
    ( (This)->lpVtbl -> get_Version(This,value) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* ____x_ABI_CTelegram_CApi_CNative_CIConnectionManagerStatics_INTERFACE_DEFINED__ */


/* interface __MIDL_itf_Telegram2EApi2ENative2Eidl_0000_4665 */




/* interface __MIDL_itf_Telegram2EApi2ENative2Eidl_0000_4665 */




extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative2Eidl_0000_4665_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative2Eidl_0000_4665_v0_0_s_ifspec;

/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0027 */
/* [local] */ 

#ifndef DEF___FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable
#define DEF___FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable
#if !defined(__cplusplus) || defined(RO_NO_TEMPLATE_NAME)


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0027 */
/* [local] */ 



extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0027_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0027_v0_0_s_ifspec;

#ifndef ____FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable_INTERFACE_DEFINED__
#define ____FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable_INTERFACE_DEFINED__

/* interface __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable */
/* [unique][uuid][object] */ 



/* interface __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID___FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("11ee97ee-6b16-52b7-a286-d68783596699")
    __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE Invoke( 
            /* [in] */ ABI::Telegram::Api::Native::IConnectionManager *sender,
            /* [in] */ IInspectable *e) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectableVtbl
    {
        BEGIN_INTERFACE
        
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable * This);
        
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable * This);
        
        HRESULT ( STDMETHODCALLTYPE *Invoke )( 
            __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable * This,
            /* [in] */ __x_ABI_CTelegram_CApi_CNative_CIConnectionManager *sender,
            /* [in] */ IInspectable *e);
        
        END_INTERFACE
    } __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectableVtbl;

    interface __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable
    {
        CONST_VTBL struct __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectableVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable_Invoke(This,sender,e)	\
    ( (This)->lpVtbl -> Invoke(This,sender,e) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* ____FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable_INTERFACE_DEFINED__ */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0028 */
/* [local] */ 

#endif /* pinterface */
#endif /* DEF___FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0028 */
/* [local] */ 



extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0028_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0028_v0_0_s_ifspec;

/* interface __MIDL_itf_Telegram2EApi2ENative2Eidl_0000_4666 */




/* interface __MIDL_itf_Telegram2EApi2ENative2Eidl_0000_4666 */




extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative2Eidl_0000_4666_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative2Eidl_0000_4666_v0_0_s_ifspec;

/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0029 */
/* [local] */ 

#ifndef DEF___FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CTLUnparsedMessage
#define DEF___FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CTLUnparsedMessage
#if !defined(__cplusplus) || defined(RO_NO_TEMPLATE_NAME)


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0029 */
/* [local] */ 



extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0029_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0029_v0_0_s_ifspec;

#ifndef ____FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CTLUnparsedMessage_INTERFACE_DEFINED__
#define ____FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CTLUnparsedMessage_INTERFACE_DEFINED__

/* interface __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CTLUnparsedMessage */
/* [unique][uuid][object] */ 



/* interface __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CTLUnparsedMessage */
/* [unique][uuid][object] */ 


EXTERN_C const IID IID___FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CTLUnparsedMessage;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("d21077de-9066-581f-aab4-244b0006f1fb")
    __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CTLUnparsedMessage : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE Invoke( 
            /* [in] */ ABI::Telegram::Api::Native::IConnectionManager *sender,
            /* [in] */ ABI::Telegram::Api::Native::ITLUnparsedMessage *e) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CTLUnparsedMessageVtbl
    {
        BEGIN_INTERFACE
        
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CTLUnparsedMessage * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CTLUnparsedMessage * This);
        
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CTLUnparsedMessage * This);
        
        HRESULT ( STDMETHODCALLTYPE *Invoke )( 
            __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CTLUnparsedMessage * This,
            /* [in] */ __x_ABI_CTelegram_CApi_CNative_CIConnectionManager *sender,
            /* [in] */ __x_ABI_CTelegram_CApi_CNative_CITLUnparsedMessage *e);
        
        END_INTERFACE
    } __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CTLUnparsedMessageVtbl;

    interface __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CTLUnparsedMessage
    {
        CONST_VTBL struct __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CTLUnparsedMessageVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CTLUnparsedMessage_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CTLUnparsedMessage_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CTLUnparsedMessage_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CTLUnparsedMessage_Invoke(This,sender,e)	\
    ( (This)->lpVtbl -> Invoke(This,sender,e) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* ____FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CTLUnparsedMessage_INTERFACE_DEFINED__ */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0030 */
/* [local] */ 

#endif /* pinterface */
#endif /* DEF___FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CTLUnparsedMessage */
#if !defined(____x_ABI_CTelegram_CApi_CNative_CIConnectionManager_INTERFACE_DEFINED__)
extern const __declspec(selectany) _Null_terminated_ WCHAR InterfaceName_Telegram_Api_Native_IConnectionManager[] = L"Telegram.Api.Native.IConnectionManager";
#endif /* !defined(____x_ABI_CTelegram_CApi_CNative_CIConnectionManager_INTERFACE_DEFINED__) */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0030 */
/* [local] */ 



extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0030_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0030_v0_0_s_ifspec;

#ifndef ____x_ABI_CTelegram_CApi_CNative_CIConnectionManager_INTERFACE_DEFINED__
#define ____x_ABI_CTelegram_CApi_CNative_CIConnectionManager_INTERFACE_DEFINED__

/* interface __x_ABI_CTelegram_CApi_CNative_CIConnectionManager */
/* [uuid][object] */ 



/* interface ABI::Telegram::Api::Native::IConnectionManager */
/* [uuid][object] */ 


EXTERN_C const IID IID___x_ABI_CTelegram_CApi_CNative_CIConnectionManager;

#if defined(__cplusplus) && !defined(CINTERFACE)
    } /* end extern "C" */
    namespace ABI {
        namespace Telegram {
            namespace Api {
                namespace Native {
                    
                    MIDL_INTERFACE("1C986C1D-56D3-4DA5-8027-5240F0CD2DFF")
                    IConnectionManager : public IInspectable
                    {
                    public:
                        virtual HRESULT STDMETHODCALLTYPE add_CurrentNetworkTypeChanged( 
                            /* [in] */ __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable *handler,
                            /* [out][retval] */ EventRegistrationToken *token) = 0;
                        
                        virtual HRESULT STDMETHODCALLTYPE remove_CurrentNetworkTypeChanged( 
                            /* [in] */ EventRegistrationToken token) = 0;
                        
                        virtual HRESULT STDMETHODCALLTYPE add_ConnectionStateChanged( 
                            /* [in] */ __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable *handler,
                            /* [out][retval] */ EventRegistrationToken *token) = 0;
                        
                        virtual HRESULT STDMETHODCALLTYPE remove_ConnectionStateChanged( 
                            /* [in] */ EventRegistrationToken token) = 0;
                        
                        virtual HRESULT STDMETHODCALLTYPE add_UnparsedMessageReceived( 
                            /* [in] */ __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CTLUnparsedMessage *handler,
                            /* [out][retval] */ EventRegistrationToken *token) = 0;
                        
                        virtual HRESULT STDMETHODCALLTYPE remove_UnparsedMessageReceived( 
                            /* [in] */ EventRegistrationToken token) = 0;
                        
                        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_ConnectionState( 
                            /* [out][retval] */ ABI::Telegram::Api::Native::ConnectionState *value) = 0;
                        
                        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_CurrentNetworkType( 
                            /* [out][retval] */ ABI::Telegram::Api::Native::ConnectionNeworkType *value) = 0;
                        
                        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_IsIpv6Enabled( 
                            /* [out][retval] */ boolean *value) = 0;
                        
                        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_IsNetworkAvailable( 
                            /* [out][retval] */ boolean *value) = 0;
                        
                        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_UserConfiguration( 
                            /* [out][retval] */ ABI::Telegram::Api::Native::IUserConfiguration **value) = 0;
                        
                        virtual /* [propput] */ HRESULT STDMETHODCALLTYPE put_UserConfiguration( 
                            /* [in] */ ABI::Telegram::Api::Native::IUserConfiguration *value) = 0;
                        
                        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_UserId( 
                            /* [out][retval] */ INT32 *value) = 0;
                        
                        virtual /* [propput] */ HRESULT STDMETHODCALLTYPE put_UserId( 
                            /* [in] */ INT32 value) = 0;
                        
                        virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Datacenters( 
                            /* [out][retval] */ __FIVectorView_1_Telegram__CApi__CNative__CDatacenter **value) = 0;
                        
                        virtual HRESULT STDMETHODCALLTYPE SendRequest( 
                            /* [in] */ ABI::Telegram::Api::Native::TL::ITLObject *object,
                            /* [in] */ ABI::Telegram::Api::Native::ISendRequestCompletedCallback *onCompleted,
                            /* [in] */ ABI::Telegram::Api::Native::IRequestQuickAckReceivedCallback *onQuickAckReceivedCallback,
                            /* [in] */ UINT32 datacenterId,
                            /* [in] */ ABI::Telegram::Api::Native::ConnectionType connectionType,
                            /* [in] */ boolean immediate,
                            /* [in] */ INT32 requestToken) = 0;
                        
                        virtual HRESULT STDMETHODCALLTYPE SendRequestWithFlags( 
                            /* [in] */ ABI::Telegram::Api::Native::TL::ITLObject *object,
                            /* [in] */ ABI::Telegram::Api::Native::ISendRequestCompletedCallback *onCompleted,
                            /* [in] */ ABI::Telegram::Api::Native::IRequestQuickAckReceivedCallback *onQuickAckReceivedCallback,
                            /* [in] */ UINT32 datacenterId,
                            /* [in] */ ABI::Telegram::Api::Native::ConnectionType connectionType,
                            /* [in] */ boolean immediate,
                            /* [in] */ INT32 requestToken,
                            /* [in] */ ABI::Telegram::Api::Native::RequestFlag flags) = 0;
                        
                        virtual HRESULT STDMETHODCALLTYPE CancelRequest( 
                            /* [in] */ INT32 requestToken,
                            /* [in] */ boolean notifyServer) = 0;
                        
                        virtual HRESULT STDMETHODCALLTYPE GetDatacenterById( 
                            /* [in] */ UINT32 id,
                            /* [out][retval] */ ABI::Telegram::Api::Native::IDatacenter **value) = 0;
                        
                        virtual HRESULT STDMETHODCALLTYPE BoomBaby( 
                            /* [in] */ ABI::Telegram::Api::Native::IUserConfiguration *userConfiguration,
                            /* [out] */ ABI::Telegram::Api::Native::TL::ITLObject **object,
                            /* [out][retval] */ ABI::Telegram::Api::Native::IConnection **value) = 0;
                        
                    };

                    extern const __declspec(selectany) IID & IID_IConnectionManager = __uuidof(IConnectionManager);

                    
                }  /* end namespace */
            }  /* end namespace */
        }  /* end namespace */
    }  /* end namespace */
    extern "C" { 
    
#else 	/* C style interface */

    typedef struct __x_ABI_CTelegram_CApi_CNative_CIConnectionManagerVtbl
    {
        BEGIN_INTERFACE
        
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __x_ABI_CTelegram_CApi_CNative_CIConnectionManager * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __x_ABI_CTelegram_CApi_CNative_CIConnectionManager * This);
        
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __x_ABI_CTelegram_CApi_CNative_CIConnectionManager * This);
        
        HRESULT ( STDMETHODCALLTYPE *GetIids )( 
            __x_ABI_CTelegram_CApi_CNative_CIConnectionManager * This,
            /* [out] */ ULONG *iidCount,
            /* [size_is][size_is][out] */ IID **iids);
        
        HRESULT ( STDMETHODCALLTYPE *GetRuntimeClassName )( 
            __x_ABI_CTelegram_CApi_CNative_CIConnectionManager * This,
            /* [out] */ HSTRING *className);
        
        HRESULT ( STDMETHODCALLTYPE *GetTrustLevel )( 
            __x_ABI_CTelegram_CApi_CNative_CIConnectionManager * This,
            /* [out] */ TrustLevel *trustLevel);
        
        HRESULT ( STDMETHODCALLTYPE *add_CurrentNetworkTypeChanged )( 
            __x_ABI_CTelegram_CApi_CNative_CIConnectionManager * This,
            /* [in] */ __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable *handler,
            /* [out][retval] */ EventRegistrationToken *token);
        
        HRESULT ( STDMETHODCALLTYPE *remove_CurrentNetworkTypeChanged )( 
            __x_ABI_CTelegram_CApi_CNative_CIConnectionManager * This,
            /* [in] */ EventRegistrationToken token);
        
        HRESULT ( STDMETHODCALLTYPE *add_ConnectionStateChanged )( 
            __x_ABI_CTelegram_CApi_CNative_CIConnectionManager * This,
            /* [in] */ __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable *handler,
            /* [out][retval] */ EventRegistrationToken *token);
        
        HRESULT ( STDMETHODCALLTYPE *remove_ConnectionStateChanged )( 
            __x_ABI_CTelegram_CApi_CNative_CIConnectionManager * This,
            /* [in] */ EventRegistrationToken token);
        
        HRESULT ( STDMETHODCALLTYPE *add_UnparsedMessageReceived )( 
            __x_ABI_CTelegram_CApi_CNative_CIConnectionManager * This,
            /* [in] */ __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CTLUnparsedMessage *handler,
            /* [out][retval] */ EventRegistrationToken *token);
        
        HRESULT ( STDMETHODCALLTYPE *remove_UnparsedMessageReceived )( 
            __x_ABI_CTelegram_CApi_CNative_CIConnectionManager * This,
            /* [in] */ EventRegistrationToken token);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_ConnectionState )( 
            __x_ABI_CTelegram_CApi_CNative_CIConnectionManager * This,
            /* [out][retval] */ __x_ABI_CTelegram_CApi_CNative_CConnectionState *value);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_CurrentNetworkType )( 
            __x_ABI_CTelegram_CApi_CNative_CIConnectionManager * This,
            /* [out][retval] */ __x_ABI_CTelegram_CApi_CNative_CConnectionNeworkType *value);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_IsIpv6Enabled )( 
            __x_ABI_CTelegram_CApi_CNative_CIConnectionManager * This,
            /* [out][retval] */ boolean *value);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_IsNetworkAvailable )( 
            __x_ABI_CTelegram_CApi_CNative_CIConnectionManager * This,
            /* [out][retval] */ boolean *value);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_UserConfiguration )( 
            __x_ABI_CTelegram_CApi_CNative_CIConnectionManager * This,
            /* [out][retval] */ __x_ABI_CTelegram_CApi_CNative_CIUserConfiguration **value);
        
        /* [propput] */ HRESULT ( STDMETHODCALLTYPE *put_UserConfiguration )( 
            __x_ABI_CTelegram_CApi_CNative_CIConnectionManager * This,
            /* [in] */ __x_ABI_CTelegram_CApi_CNative_CIUserConfiguration *value);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_UserId )( 
            __x_ABI_CTelegram_CApi_CNative_CIConnectionManager * This,
            /* [out][retval] */ INT32 *value);
        
        /* [propput] */ HRESULT ( STDMETHODCALLTYPE *put_UserId )( 
            __x_ABI_CTelegram_CApi_CNative_CIConnectionManager * This,
            /* [in] */ INT32 value);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Datacenters )( 
            __x_ABI_CTelegram_CApi_CNative_CIConnectionManager * This,
            /* [out][retval] */ __FIVectorView_1_Telegram__CApi__CNative__CDatacenter **value);
        
        HRESULT ( STDMETHODCALLTYPE *SendRequest )( 
            __x_ABI_CTelegram_CApi_CNative_CIConnectionManager * This,
            /* [in] */ __x_ABI_CTelegram_CApi_CNative_CTL_CITLObject *object,
            /* [in] */ __x_ABI_CTelegram_CApi_CNative_CISendRequestCompletedCallback *onCompleted,
            /* [in] */ __x_ABI_CTelegram_CApi_CNative_CIRequestQuickAckReceivedCallback *onQuickAckReceivedCallback,
            /* [in] */ UINT32 datacenterId,
            /* [in] */ __x_ABI_CTelegram_CApi_CNative_CConnectionType connectionType,
            /* [in] */ boolean immediate,
            /* [in] */ INT32 requestToken);
        
        HRESULT ( STDMETHODCALLTYPE *SendRequestWithFlags )( 
            __x_ABI_CTelegram_CApi_CNative_CIConnectionManager * This,
            /* [in] */ __x_ABI_CTelegram_CApi_CNative_CTL_CITLObject *object,
            /* [in] */ __x_ABI_CTelegram_CApi_CNative_CISendRequestCompletedCallback *onCompleted,
            /* [in] */ __x_ABI_CTelegram_CApi_CNative_CIRequestQuickAckReceivedCallback *onQuickAckReceivedCallback,
            /* [in] */ UINT32 datacenterId,
            /* [in] */ __x_ABI_CTelegram_CApi_CNative_CConnectionType connectionType,
            /* [in] */ boolean immediate,
            /* [in] */ INT32 requestToken,
            /* [in] */ __x_ABI_CTelegram_CApi_CNative_CRequestFlag flags);
        
        HRESULT ( STDMETHODCALLTYPE *CancelRequest )( 
            __x_ABI_CTelegram_CApi_CNative_CIConnectionManager * This,
            /* [in] */ INT32 requestToken,
            /* [in] */ boolean notifyServer);
        
        HRESULT ( STDMETHODCALLTYPE *GetDatacenterById )( 
            __x_ABI_CTelegram_CApi_CNative_CIConnectionManager * This,
            /* [in] */ UINT32 id,
            /* [out][retval] */ __x_ABI_CTelegram_CApi_CNative_CIDatacenter **value);
        
        HRESULT ( STDMETHODCALLTYPE *BoomBaby )( 
            __x_ABI_CTelegram_CApi_CNative_CIConnectionManager * This,
            /* [in] */ __x_ABI_CTelegram_CApi_CNative_CIUserConfiguration *userConfiguration,
            /* [out] */ __x_ABI_CTelegram_CApi_CNative_CTL_CITLObject **object,
            /* [out][retval] */ __x_ABI_CTelegram_CApi_CNative_CIConnection **value);
        
        END_INTERFACE
    } __x_ABI_CTelegram_CApi_CNative_CIConnectionManagerVtbl;

    interface __x_ABI_CTelegram_CApi_CNative_CIConnectionManager
    {
        CONST_VTBL struct __x_ABI_CTelegram_CApi_CNative_CIConnectionManagerVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define __x_ABI_CTelegram_CApi_CNative_CIConnectionManager_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIConnectionManager_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIConnectionManager_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define __x_ABI_CTelegram_CApi_CNative_CIConnectionManager_GetIids(This,iidCount,iids)	\
    ( (This)->lpVtbl -> GetIids(This,iidCount,iids) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIConnectionManager_GetRuntimeClassName(This,className)	\
    ( (This)->lpVtbl -> GetRuntimeClassName(This,className) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIConnectionManager_GetTrustLevel(This,trustLevel)	\
    ( (This)->lpVtbl -> GetTrustLevel(This,trustLevel) ) 


#define __x_ABI_CTelegram_CApi_CNative_CIConnectionManager_add_CurrentNetworkTypeChanged(This,handler,token)	\
    ( (This)->lpVtbl -> add_CurrentNetworkTypeChanged(This,handler,token) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIConnectionManager_remove_CurrentNetworkTypeChanged(This,token)	\
    ( (This)->lpVtbl -> remove_CurrentNetworkTypeChanged(This,token) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIConnectionManager_add_ConnectionStateChanged(This,handler,token)	\
    ( (This)->lpVtbl -> add_ConnectionStateChanged(This,handler,token) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIConnectionManager_remove_ConnectionStateChanged(This,token)	\
    ( (This)->lpVtbl -> remove_ConnectionStateChanged(This,token) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIConnectionManager_add_UnparsedMessageReceived(This,handler,token)	\
    ( (This)->lpVtbl -> add_UnparsedMessageReceived(This,handler,token) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIConnectionManager_remove_UnparsedMessageReceived(This,token)	\
    ( (This)->lpVtbl -> remove_UnparsedMessageReceived(This,token) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIConnectionManager_get_ConnectionState(This,value)	\
    ( (This)->lpVtbl -> get_ConnectionState(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIConnectionManager_get_CurrentNetworkType(This,value)	\
    ( (This)->lpVtbl -> get_CurrentNetworkType(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIConnectionManager_get_IsIpv6Enabled(This,value)	\
    ( (This)->lpVtbl -> get_IsIpv6Enabled(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIConnectionManager_get_IsNetworkAvailable(This,value)	\
    ( (This)->lpVtbl -> get_IsNetworkAvailable(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIConnectionManager_get_UserConfiguration(This,value)	\
    ( (This)->lpVtbl -> get_UserConfiguration(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIConnectionManager_put_UserConfiguration(This,value)	\
    ( (This)->lpVtbl -> put_UserConfiguration(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIConnectionManager_get_UserId(This,value)	\
    ( (This)->lpVtbl -> get_UserId(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIConnectionManager_put_UserId(This,value)	\
    ( (This)->lpVtbl -> put_UserId(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIConnectionManager_get_Datacenters(This,value)	\
    ( (This)->lpVtbl -> get_Datacenters(This,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIConnectionManager_SendRequest(This,object,onCompleted,onQuickAckReceivedCallback,datacenterId,connectionType,immediate,requestToken)	\
    ( (This)->lpVtbl -> SendRequest(This,object,onCompleted,onQuickAckReceivedCallback,datacenterId,connectionType,immediate,requestToken) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIConnectionManager_SendRequestWithFlags(This,object,onCompleted,onQuickAckReceivedCallback,datacenterId,connectionType,immediate,requestToken,flags)	\
    ( (This)->lpVtbl -> SendRequestWithFlags(This,object,onCompleted,onQuickAckReceivedCallback,datacenterId,connectionType,immediate,requestToken,flags) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIConnectionManager_CancelRequest(This,requestToken,notifyServer)	\
    ( (This)->lpVtbl -> CancelRequest(This,requestToken,notifyServer) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIConnectionManager_GetDatacenterById(This,id,value)	\
    ( (This)->lpVtbl -> GetDatacenterById(This,id,value) ) 

#define __x_ABI_CTelegram_CApi_CNative_CIConnectionManager_BoomBaby(This,userConfiguration,object,value)	\
    ( (This)->lpVtbl -> BoomBaby(This,userConfiguration,object,value) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* ____x_ABI_CTelegram_CApi_CNative_CIConnectionManager_INTERFACE_DEFINED__ */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0031 */
/* [local] */ 

#ifndef RUNTIMECLASS_Telegram_Api_Native_ConnectionManager_DEFINED
#define RUNTIMECLASS_Telegram_Api_Native_ConnectionManager_DEFINED
extern const __declspec(selectany) _Null_terminated_ WCHAR RuntimeClass_Telegram_Api_Native_ConnectionManager[] = L"Telegram.Api.Native.ConnectionManager";
#endif


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0031 */
/* [local] */ 



extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0031_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0031_v0_0_s_ifspec;

/* Additional Prototypes for ALL interfaces */

unsigned long             __RPC_USER  HSTRING_UserSize(     unsigned long *, unsigned long            , HSTRING * ); 
unsigned char * __RPC_USER  HSTRING_UserMarshal(  unsigned long *, unsigned char *, HSTRING * ); 
unsigned char * __RPC_USER  HSTRING_UserUnmarshal(unsigned long *, unsigned char *, HSTRING * ); 
void                      __RPC_USER  HSTRING_UserFree(     unsigned long *, HSTRING * ); 

/* end of Additional Prototypes */

#ifdef __cplusplus
}
#endif

#endif


