

/* this ALWAYS GENERATED file contains the definitions for the interfaces */


 /* File created by MIDL compiler version 8.01.0622 */
/* at Tue Jan 19 04:14:07 2038
 */
/* Compiler settings for C:\Users\loren\AppData\Local\Temp\Telegram.Api.Native.idl-c068c241:
    Oicf, W1, Zp8, env=Win64 (32b run), target_arch=AMD64 8.01.0622 
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

#ifndef ____x_Telegram_CApi_CNative_CITLObject_FWD_DEFINED__
#define ____x_Telegram_CApi_CNative_CITLObject_FWD_DEFINED__
typedef interface __x_Telegram_CApi_CNative_CITLObject __x_Telegram_CApi_CNative_CITLObject;

#ifdef __cplusplus
namespace Telegram {
    namespace Api {
        namespace Native {
            interface ITLObject;
        } /* end namespace */
    } /* end namespace */
} /* end namespace */

#endif /* __cplusplus */

#endif 	/* ____x_Telegram_CApi_CNative_CITLObject_FWD_DEFINED__ */


#ifndef ____x_Telegram_CApi_CNative_CITLBinaryReader_FWD_DEFINED__
#define ____x_Telegram_CApi_CNative_CITLBinaryReader_FWD_DEFINED__
typedef interface __x_Telegram_CApi_CNative_CITLBinaryReader __x_Telegram_CApi_CNative_CITLBinaryReader;

#ifdef __cplusplus
namespace Telegram {
    namespace Api {
        namespace Native {
            interface ITLBinaryReader;
        } /* end namespace */
    } /* end namespace */
} /* end namespace */

#endif /* __cplusplus */

#endif 	/* ____x_Telegram_CApi_CNative_CITLBinaryReader_FWD_DEFINED__ */


#ifndef ____x_Telegram_CApi_CNative_CITLBinaryWriter_FWD_DEFINED__
#define ____x_Telegram_CApi_CNative_CITLBinaryWriter_FWD_DEFINED__
typedef interface __x_Telegram_CApi_CNative_CITLBinaryWriter __x_Telegram_CApi_CNative_CITLBinaryWriter;

#ifdef __cplusplus
namespace Telegram {
    namespace Api {
        namespace Native {
            interface ITLBinaryWriter;
        } /* end namespace */
    } /* end namespace */
} /* end namespace */

#endif /* __cplusplus */

#endif 	/* ____x_Telegram_CApi_CNative_CITLBinaryWriter_FWD_DEFINED__ */


#ifndef ____x_Telegram_CApi_CNative_CIDatacenter_FWD_DEFINED__
#define ____x_Telegram_CApi_CNative_CIDatacenter_FWD_DEFINED__
typedef interface __x_Telegram_CApi_CNative_CIDatacenter __x_Telegram_CApi_CNative_CIDatacenter;

#ifdef __cplusplus
namespace Telegram {
    namespace Api {
        namespace Native {
            interface IDatacenter;
        } /* end namespace */
    } /* end namespace */
} /* end namespace */

#endif /* __cplusplus */

#endif 	/* ____x_Telegram_CApi_CNative_CIDatacenter_FWD_DEFINED__ */


#ifndef ____x_Telegram_CApi_CNative_CIConnection_FWD_DEFINED__
#define ____x_Telegram_CApi_CNative_CIConnection_FWD_DEFINED__
typedef interface __x_Telegram_CApi_CNative_CIConnection __x_Telegram_CApi_CNative_CIConnection;

#ifdef __cplusplus
namespace Telegram {
    namespace Api {
        namespace Native {
            interface IConnection;
        } /* end namespace */
    } /* end namespace */
} /* end namespace */

#endif /* __cplusplus */

#endif 	/* ____x_Telegram_CApi_CNative_CIConnection_FWD_DEFINED__ */


#ifndef ____x_Telegram_CApi_CNative_CIConnectionManagerStatics_FWD_DEFINED__
#define ____x_Telegram_CApi_CNative_CIConnectionManagerStatics_FWD_DEFINED__
typedef interface __x_Telegram_CApi_CNative_CIConnectionManagerStatics __x_Telegram_CApi_CNative_CIConnectionManagerStatics;

#ifdef __cplusplus
namespace Telegram {
    namespace Api {
        namespace Native {
            interface IConnectionManagerStatics;
        } /* end namespace */
    } /* end namespace */
} /* end namespace */

#endif /* __cplusplus */

#endif 	/* ____x_Telegram_CApi_CNative_CIConnectionManagerStatics_FWD_DEFINED__ */


#ifndef ____x_Telegram_CApi_CNative_CIConnectionManager_FWD_DEFINED__
#define ____x_Telegram_CApi_CNative_CIConnectionManager_FWD_DEFINED__
typedef interface __x_Telegram_CApi_CNative_CIConnectionManager __x_Telegram_CApi_CNative_CIConnectionManager;

#ifdef __cplusplus
namespace Telegram {
    namespace Api {
        namespace Native {
            interface IConnectionManager;
        } /* end namespace */
    } /* end namespace */
} /* end namespace */

#endif /* __cplusplus */

#endif 	/* ____x_Telegram_CApi_CNative_CIConnectionManager_FWD_DEFINED__ */


/* header files for imported files */
#include "inspectable.h"

#ifdef __cplusplus
extern "C"{
#endif 


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0000 */
/* [local] */ 

#if !defined(__cplusplus)
#if !defined(__cplusplus)

typedef enum __x_Telegram_CApi_CNative_CConnectionType __x_Telegram_CApi_CNative_CConnectionType;


#endif /* end if !defined(__cplusplus) */


#endif
#if !defined(__cplusplus)
#if !defined(__cplusplus)

typedef enum __x_Telegram_CApi_CNative_CConnectionNeworkType __x_Telegram_CApi_CNative_CConnectionNeworkType;


#endif /* end if !defined(__cplusplus) */


#endif
#if !defined(__cplusplus)
#if !defined(__cplusplus)

typedef enum __x_Telegram_CApi_CNative_CConnectionState __x_Telegram_CApi_CNative_CConnectionState;


#endif /* end if !defined(__cplusplus) */


#endif

#ifdef __cplusplus
namespace Telegram {
namespace Api {
namespace Native {
class Datacenter;
} /*Native*/
} /*Api*/
} /*Telegram*/
#endif

#ifdef __cplusplus
namespace Telegram {
namespace Api {
namespace Native {
class Connection;
} /*Native*/
} /*Api*/
} /*Telegram*/
#endif

#ifdef __cplusplus
namespace Telegram {
namespace Api {
namespace Native {
class ConnectionManager;
} /*Native*/
} /*Api*/
} /*Telegram*/
#endif

#ifdef __cplusplus
namespace Telegram {
namespace Api {
namespace Native {
class TLBinaryReader;
} /*Native*/
} /*Api*/
} /*Telegram*/
#endif

#ifdef __cplusplus
namespace Telegram {
namespace Api {
namespace Native {
class TLBinaryWriter;
} /*Native*/
} /*Api*/
} /*Telegram*/
#endif

#if !defined(____x_Telegram_CApi_CNative_CITLObject_INTERFACE_DEFINED__)
extern const __declspec(selectany) _Null_terminated_ WCHAR InterfaceName_Telegram_Api_Native_ITLObject[] = L"Telegram.Api.Native.ITLObject";
#endif /* !defined(____x_Telegram_CApi_CNative_CITLObject_INTERFACE_DEFINED__) */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0000 */
/* [local] */ 

#ifdef __cplusplus

} /* end extern "C" */
namespace Telegram {
    namespace Api {
        namespace Native {
            
            typedef MIDL_ENUM ConnectionType ConnectionType;
            
        } /* end namespace */
    } /* end namespace */
} /* end namespace */

extern "C" { 
#endif

#ifdef __cplusplus

} /* end extern "C" */
namespace Telegram {
    namespace Api {
        namespace Native {
            
            typedef MIDL_ENUM ConnectionNeworkType ConnectionNeworkType;
            
        } /* end namespace */
    } /* end namespace */
} /* end namespace */

extern "C" { 
#endif

#ifdef __cplusplus

} /* end extern "C" */
namespace Telegram {
    namespace Api {
        namespace Native {
            
            typedef MIDL_ENUM ConnectionState ConnectionState;
            
        } /* end namespace */
    } /* end namespace */
} /* end namespace */

extern "C" { 
#endif









extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0000_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0000_v0_0_s_ifspec;

#ifndef ____x_Telegram_CApi_CNative_CITLObject_INTERFACE_DEFINED__
#define ____x_Telegram_CApi_CNative_CITLObject_INTERFACE_DEFINED__

/* interface __x_Telegram_CApi_CNative_CITLObject */
/* [uuid][object] */ 



/* interface Telegram::Api::Native::ITLObject */
/* [uuid][object] */ 


EXTERN_C const IID IID___x_Telegram_CApi_CNative_CITLObject;

#if defined(__cplusplus) && !defined(CINTERFACE)
    } /* end extern "C" */
    namespace Telegram {
        namespace Api {
            namespace Native {
                
                MIDL_INTERFACE("B93C4F8A-0308-4598-8C0A-52ACC91E45E3")
                ITLObject : public IInspectable
                {
                public:
                    virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Size( 
                        /* [out][retval] */ UINT32 *value) = 0;
                    
                    virtual HRESULT STDMETHODCALLTYPE Read( 
                        /* [in] */ Telegram::Api::Native::ITLBinaryReader *reader) = 0;
                    
                    virtual HRESULT STDMETHODCALLTYPE Write( 
                        /* [in] */ Telegram::Api::Native::ITLBinaryWriter *writer) = 0;
                    
                };

                extern const __declspec(selectany) IID & IID_ITLObject = __uuidof(ITLObject);

                
            }  /* end namespace */
        }  /* end namespace */
    }  /* end namespace */
    extern "C" { 
    
#else 	/* C style interface */

    typedef struct __x_Telegram_CApi_CNative_CITLObjectVtbl
    {
        BEGIN_INTERFACE
        
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __x_Telegram_CApi_CNative_CITLObject * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __x_Telegram_CApi_CNative_CITLObject * This);
        
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __x_Telegram_CApi_CNative_CITLObject * This);
        
        HRESULT ( STDMETHODCALLTYPE *GetIids )( 
            __x_Telegram_CApi_CNative_CITLObject * This,
            /* [out] */ ULONG *iidCount,
            /* [size_is][size_is][out] */ IID **iids);
        
        HRESULT ( STDMETHODCALLTYPE *GetRuntimeClassName )( 
            __x_Telegram_CApi_CNative_CITLObject * This,
            /* [out] */ HSTRING *className);
        
        HRESULT ( STDMETHODCALLTYPE *GetTrustLevel )( 
            __x_Telegram_CApi_CNative_CITLObject * This,
            /* [out] */ TrustLevel *trustLevel);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Size )( 
            __x_Telegram_CApi_CNative_CITLObject * This,
            /* [out][retval] */ UINT32 *value);
        
        HRESULT ( STDMETHODCALLTYPE *Read )( 
            __x_Telegram_CApi_CNative_CITLObject * This,
            /* [in] */ __x_Telegram_CApi_CNative_CITLBinaryReader *reader);
        
        HRESULT ( STDMETHODCALLTYPE *Write )( 
            __x_Telegram_CApi_CNative_CITLObject * This,
            /* [in] */ __x_Telegram_CApi_CNative_CITLBinaryWriter *writer);
        
        END_INTERFACE
    } __x_Telegram_CApi_CNative_CITLObjectVtbl;

    interface __x_Telegram_CApi_CNative_CITLObject
    {
        CONST_VTBL struct __x_Telegram_CApi_CNative_CITLObjectVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define __x_Telegram_CApi_CNative_CITLObject_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define __x_Telegram_CApi_CNative_CITLObject_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define __x_Telegram_CApi_CNative_CITLObject_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define __x_Telegram_CApi_CNative_CITLObject_GetIids(This,iidCount,iids)	\
    ( (This)->lpVtbl -> GetIids(This,iidCount,iids) ) 

#define __x_Telegram_CApi_CNative_CITLObject_GetRuntimeClassName(This,className)	\
    ( (This)->lpVtbl -> GetRuntimeClassName(This,className) ) 

#define __x_Telegram_CApi_CNative_CITLObject_GetTrustLevel(This,trustLevel)	\
    ( (This)->lpVtbl -> GetTrustLevel(This,trustLevel) ) 


#define __x_Telegram_CApi_CNative_CITLObject_get_Size(This,value)	\
    ( (This)->lpVtbl -> get_Size(This,value) ) 

#define __x_Telegram_CApi_CNative_CITLObject_Read(This,reader)	\
    ( (This)->lpVtbl -> Read(This,reader) ) 

#define __x_Telegram_CApi_CNative_CITLObject_Write(This,writer)	\
    ( (This)->lpVtbl -> Write(This,writer) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* ____x_Telegram_CApi_CNative_CITLObject_INTERFACE_DEFINED__ */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0001 */
/* [local] */ 

#if !defined(____x_Telegram_CApi_CNative_CITLBinaryReader_INTERFACE_DEFINED__)
extern const __declspec(selectany) _Null_terminated_ WCHAR InterfaceName_Telegram_Api_Native_ITLBinaryReader[] = L"Telegram.Api.Native.ITLBinaryReader";
#endif /* !defined(____x_Telegram_CApi_CNative_CITLBinaryReader_INTERFACE_DEFINED__) */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0001 */
/* [local] */ 



extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0001_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0001_v0_0_s_ifspec;

#ifndef ____x_Telegram_CApi_CNative_CITLBinaryReader_INTERFACE_DEFINED__
#define ____x_Telegram_CApi_CNative_CITLBinaryReader_INTERFACE_DEFINED__

/* interface __x_Telegram_CApi_CNative_CITLBinaryReader */
/* [uuid][object] */ 



/* interface Telegram::Api::Native::ITLBinaryReader */
/* [uuid][object] */ 


EXTERN_C const IID IID___x_Telegram_CApi_CNative_CITLBinaryReader;

#if defined(__cplusplus) && !defined(CINTERFACE)
    } /* end extern "C" */
    namespace Telegram {
        namespace Api {
            namespace Native {
                
                MIDL_INTERFACE("7F865F69-62F1-4BC9-AD8C-717D0D8DD7F8")
                ITLBinaryReader : public IInspectable
                {
                public:
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
                        /* [out][size_is][size_is] */ BYTE **value) = 0;
                    
                    virtual HRESULT STDMETHODCALLTYPE ReadDouble( 
                        /* [out][retval] */ double *value) = 0;
                    
                };

                extern const __declspec(selectany) IID & IID_ITLBinaryReader = __uuidof(ITLBinaryReader);

                
            }  /* end namespace */
        }  /* end namespace */
    }  /* end namespace */
    extern "C" { 
    
#else 	/* C style interface */

    typedef struct __x_Telegram_CApi_CNative_CITLBinaryReaderVtbl
    {
        BEGIN_INTERFACE
        
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __x_Telegram_CApi_CNative_CITLBinaryReader * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __x_Telegram_CApi_CNative_CITLBinaryReader * This);
        
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __x_Telegram_CApi_CNative_CITLBinaryReader * This);
        
        HRESULT ( STDMETHODCALLTYPE *GetIids )( 
            __x_Telegram_CApi_CNative_CITLBinaryReader * This,
            /* [out] */ ULONG *iidCount,
            /* [size_is][size_is][out] */ IID **iids);
        
        HRESULT ( STDMETHODCALLTYPE *GetRuntimeClassName )( 
            __x_Telegram_CApi_CNative_CITLBinaryReader * This,
            /* [out] */ HSTRING *className);
        
        HRESULT ( STDMETHODCALLTYPE *GetTrustLevel )( 
            __x_Telegram_CApi_CNative_CITLBinaryReader * This,
            /* [out] */ TrustLevel *trustLevel);
        
        HRESULT ( STDMETHODCALLTYPE *ReadByte )( 
            __x_Telegram_CApi_CNative_CITLBinaryReader * This,
            /* [out][retval] */ BYTE *value);
        
        HRESULT ( STDMETHODCALLTYPE *ReadInt16 )( 
            __x_Telegram_CApi_CNative_CITLBinaryReader * This,
            /* [out][retval] */ INT16 *value);
        
        HRESULT ( STDMETHODCALLTYPE *ReadUInt16 )( 
            __x_Telegram_CApi_CNative_CITLBinaryReader * This,
            /* [out][retval] */ UINT16 *value);
        
        HRESULT ( STDMETHODCALLTYPE *ReadInt32 )( 
            __x_Telegram_CApi_CNative_CITLBinaryReader * This,
            /* [out][retval] */ INT32 *value);
        
        HRESULT ( STDMETHODCALLTYPE *ReadUInt32 )( 
            __x_Telegram_CApi_CNative_CITLBinaryReader * This,
            /* [out][retval] */ UINT32 *value);
        
        HRESULT ( STDMETHODCALLTYPE *ReadInt64 )( 
            __x_Telegram_CApi_CNative_CITLBinaryReader * This,
            /* [out][retval] */ INT64 *value);
        
        HRESULT ( STDMETHODCALLTYPE *ReadUInt64 )( 
            __x_Telegram_CApi_CNative_CITLBinaryReader * This,
            /* [out][retval] */ UINT64 *value);
        
        HRESULT ( STDMETHODCALLTYPE *ReadBool )( 
            __x_Telegram_CApi_CNative_CITLBinaryReader * This,
            /* [out][retval] */ boolean *value);
        
        HRESULT ( STDMETHODCALLTYPE *ReadString )( 
            __x_Telegram_CApi_CNative_CITLBinaryReader * This,
            /* [out][retval] */ HSTRING *value);
        
        HRESULT ( STDMETHODCALLTYPE *ReadByteArray )( 
            __x_Telegram_CApi_CNative_CITLBinaryReader * This,
            /* [out] */ UINT32 *__valueSize,
            /* [out][size_is][size_is] */ BYTE **value);
        
        HRESULT ( STDMETHODCALLTYPE *ReadDouble )( 
            __x_Telegram_CApi_CNative_CITLBinaryReader * This,
            /* [out][retval] */ double *value);
        
        END_INTERFACE
    } __x_Telegram_CApi_CNative_CITLBinaryReaderVtbl;

    interface __x_Telegram_CApi_CNative_CITLBinaryReader
    {
        CONST_VTBL struct __x_Telegram_CApi_CNative_CITLBinaryReaderVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define __x_Telegram_CApi_CNative_CITLBinaryReader_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define __x_Telegram_CApi_CNative_CITLBinaryReader_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define __x_Telegram_CApi_CNative_CITLBinaryReader_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define __x_Telegram_CApi_CNative_CITLBinaryReader_GetIids(This,iidCount,iids)	\
    ( (This)->lpVtbl -> GetIids(This,iidCount,iids) ) 

#define __x_Telegram_CApi_CNative_CITLBinaryReader_GetRuntimeClassName(This,className)	\
    ( (This)->lpVtbl -> GetRuntimeClassName(This,className) ) 

#define __x_Telegram_CApi_CNative_CITLBinaryReader_GetTrustLevel(This,trustLevel)	\
    ( (This)->lpVtbl -> GetTrustLevel(This,trustLevel) ) 


#define __x_Telegram_CApi_CNative_CITLBinaryReader_ReadByte(This,value)	\
    ( (This)->lpVtbl -> ReadByte(This,value) ) 

#define __x_Telegram_CApi_CNative_CITLBinaryReader_ReadInt16(This,value)	\
    ( (This)->lpVtbl -> ReadInt16(This,value) ) 

#define __x_Telegram_CApi_CNative_CITLBinaryReader_ReadUInt16(This,value)	\
    ( (This)->lpVtbl -> ReadUInt16(This,value) ) 

#define __x_Telegram_CApi_CNative_CITLBinaryReader_ReadInt32(This,value)	\
    ( (This)->lpVtbl -> ReadInt32(This,value) ) 

#define __x_Telegram_CApi_CNative_CITLBinaryReader_ReadUInt32(This,value)	\
    ( (This)->lpVtbl -> ReadUInt32(This,value) ) 

#define __x_Telegram_CApi_CNative_CITLBinaryReader_ReadInt64(This,value)	\
    ( (This)->lpVtbl -> ReadInt64(This,value) ) 

#define __x_Telegram_CApi_CNative_CITLBinaryReader_ReadUInt64(This,value)	\
    ( (This)->lpVtbl -> ReadUInt64(This,value) ) 

#define __x_Telegram_CApi_CNative_CITLBinaryReader_ReadBool(This,value)	\
    ( (This)->lpVtbl -> ReadBool(This,value) ) 

#define __x_Telegram_CApi_CNative_CITLBinaryReader_ReadString(This,value)	\
    ( (This)->lpVtbl -> ReadString(This,value) ) 

#define __x_Telegram_CApi_CNative_CITLBinaryReader_ReadByteArray(This,__valueSize,value)	\
    ( (This)->lpVtbl -> ReadByteArray(This,__valueSize,value) ) 

#define __x_Telegram_CApi_CNative_CITLBinaryReader_ReadDouble(This,value)	\
    ( (This)->lpVtbl -> ReadDouble(This,value) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* ____x_Telegram_CApi_CNative_CITLBinaryReader_INTERFACE_DEFINED__ */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0002 */
/* [local] */ 

#ifndef RUNTIMECLASS_Telegram_Api_Native_TLBinaryReader_DEFINED
#define RUNTIMECLASS_Telegram_Api_Native_TLBinaryReader_DEFINED
extern const __declspec(selectany) _Null_terminated_ WCHAR RuntimeClass_Telegram_Api_Native_TLBinaryReader[] = L"Telegram.Api.Native.TLBinaryReader";
#endif
#if !defined(____x_Telegram_CApi_CNative_CITLBinaryWriter_INTERFACE_DEFINED__)
extern const __declspec(selectany) _Null_terminated_ WCHAR InterfaceName_Telegram_Api_Native_ITLBinaryWriter[] = L"Telegram.Api.Native.ITLBinaryWriter";
#endif /* !defined(____x_Telegram_CApi_CNative_CITLBinaryWriter_INTERFACE_DEFINED__) */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0002 */
/* [local] */ 



extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0002_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0002_v0_0_s_ifspec;

#ifndef ____x_Telegram_CApi_CNative_CITLBinaryWriter_INTERFACE_DEFINED__
#define ____x_Telegram_CApi_CNative_CITLBinaryWriter_INTERFACE_DEFINED__

/* interface __x_Telegram_CApi_CNative_CITLBinaryWriter */
/* [uuid][object] */ 



/* interface Telegram::Api::Native::ITLBinaryWriter */
/* [uuid][object] */ 


EXTERN_C const IID IID___x_Telegram_CApi_CNative_CITLBinaryWriter;

#if defined(__cplusplus) && !defined(CINTERFACE)
    } /* end extern "C" */
    namespace Telegram {
        namespace Api {
            namespace Native {
                
                MIDL_INTERFACE("732B4B01-0603-4ADB-8F29-24096FCDF7C6")
                ITLBinaryWriter : public IInspectable
                {
                public:
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
                    
                    virtual HRESULT STDMETHODCALLTYPE WriteDouble( 
                        /* [in] */ double value) = 0;
                    
                };

                extern const __declspec(selectany) IID & IID_ITLBinaryWriter = __uuidof(ITLBinaryWriter);

                
            }  /* end namespace */
        }  /* end namespace */
    }  /* end namespace */
    extern "C" { 
    
#else 	/* C style interface */

    typedef struct __x_Telegram_CApi_CNative_CITLBinaryWriterVtbl
    {
        BEGIN_INTERFACE
        
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __x_Telegram_CApi_CNative_CITLBinaryWriter * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __x_Telegram_CApi_CNative_CITLBinaryWriter * This);
        
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __x_Telegram_CApi_CNative_CITLBinaryWriter * This);
        
        HRESULT ( STDMETHODCALLTYPE *GetIids )( 
            __x_Telegram_CApi_CNative_CITLBinaryWriter * This,
            /* [out] */ ULONG *iidCount,
            /* [size_is][size_is][out] */ IID **iids);
        
        HRESULT ( STDMETHODCALLTYPE *GetRuntimeClassName )( 
            __x_Telegram_CApi_CNative_CITLBinaryWriter * This,
            /* [out] */ HSTRING *className);
        
        HRESULT ( STDMETHODCALLTYPE *GetTrustLevel )( 
            __x_Telegram_CApi_CNative_CITLBinaryWriter * This,
            /* [out] */ TrustLevel *trustLevel);
        
        HRESULT ( STDMETHODCALLTYPE *WriteByte )( 
            __x_Telegram_CApi_CNative_CITLBinaryWriter * This,
            /* [in] */ BYTE value);
        
        HRESULT ( STDMETHODCALLTYPE *WriteInt16 )( 
            __x_Telegram_CApi_CNative_CITLBinaryWriter * This,
            /* [in] */ INT16 value);
        
        HRESULT ( STDMETHODCALLTYPE *WriteUInt16 )( 
            __x_Telegram_CApi_CNative_CITLBinaryWriter * This,
            /* [in] */ UINT16 value);
        
        HRESULT ( STDMETHODCALLTYPE *WriteInt32 )( 
            __x_Telegram_CApi_CNative_CITLBinaryWriter * This,
            /* [in] */ INT32 value);
        
        HRESULT ( STDMETHODCALLTYPE *WriteUInt32 )( 
            __x_Telegram_CApi_CNative_CITLBinaryWriter * This,
            /* [in] */ UINT32 value);
        
        HRESULT ( STDMETHODCALLTYPE *WriteInt64 )( 
            __x_Telegram_CApi_CNative_CITLBinaryWriter * This,
            /* [in] */ INT64 value);
        
        HRESULT ( STDMETHODCALLTYPE *WriteUInt64 )( 
            __x_Telegram_CApi_CNative_CITLBinaryWriter * This,
            /* [in] */ UINT64 value);
        
        HRESULT ( STDMETHODCALLTYPE *WriteBool )( 
            __x_Telegram_CApi_CNative_CITLBinaryWriter * This,
            /* [in] */ boolean value);
        
        HRESULT ( STDMETHODCALLTYPE *WriteString )( 
            __x_Telegram_CApi_CNative_CITLBinaryWriter * This,
            /* [in] */ HSTRING value);
        
        HRESULT ( STDMETHODCALLTYPE *WriteByteArray )( 
            __x_Telegram_CApi_CNative_CITLBinaryWriter * This,
            /* [in] */ UINT32 __valueSize,
            /* [in][size_is] */ BYTE *value);
        
        HRESULT ( STDMETHODCALLTYPE *WriteDouble )( 
            __x_Telegram_CApi_CNative_CITLBinaryWriter * This,
            /* [in] */ double value);
        
        END_INTERFACE
    } __x_Telegram_CApi_CNative_CITLBinaryWriterVtbl;

    interface __x_Telegram_CApi_CNative_CITLBinaryWriter
    {
        CONST_VTBL struct __x_Telegram_CApi_CNative_CITLBinaryWriterVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define __x_Telegram_CApi_CNative_CITLBinaryWriter_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define __x_Telegram_CApi_CNative_CITLBinaryWriter_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define __x_Telegram_CApi_CNative_CITLBinaryWriter_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define __x_Telegram_CApi_CNative_CITLBinaryWriter_GetIids(This,iidCount,iids)	\
    ( (This)->lpVtbl -> GetIids(This,iidCount,iids) ) 

#define __x_Telegram_CApi_CNative_CITLBinaryWriter_GetRuntimeClassName(This,className)	\
    ( (This)->lpVtbl -> GetRuntimeClassName(This,className) ) 

#define __x_Telegram_CApi_CNative_CITLBinaryWriter_GetTrustLevel(This,trustLevel)	\
    ( (This)->lpVtbl -> GetTrustLevel(This,trustLevel) ) 


#define __x_Telegram_CApi_CNative_CITLBinaryWriter_WriteByte(This,value)	\
    ( (This)->lpVtbl -> WriteByte(This,value) ) 

#define __x_Telegram_CApi_CNative_CITLBinaryWriter_WriteInt16(This,value)	\
    ( (This)->lpVtbl -> WriteInt16(This,value) ) 

#define __x_Telegram_CApi_CNative_CITLBinaryWriter_WriteUInt16(This,value)	\
    ( (This)->lpVtbl -> WriteUInt16(This,value) ) 

#define __x_Telegram_CApi_CNative_CITLBinaryWriter_WriteInt32(This,value)	\
    ( (This)->lpVtbl -> WriteInt32(This,value) ) 

#define __x_Telegram_CApi_CNative_CITLBinaryWriter_WriteUInt32(This,value)	\
    ( (This)->lpVtbl -> WriteUInt32(This,value) ) 

#define __x_Telegram_CApi_CNative_CITLBinaryWriter_WriteInt64(This,value)	\
    ( (This)->lpVtbl -> WriteInt64(This,value) ) 

#define __x_Telegram_CApi_CNative_CITLBinaryWriter_WriteUInt64(This,value)	\
    ( (This)->lpVtbl -> WriteUInt64(This,value) ) 

#define __x_Telegram_CApi_CNative_CITLBinaryWriter_WriteBool(This,value)	\
    ( (This)->lpVtbl -> WriteBool(This,value) ) 

#define __x_Telegram_CApi_CNative_CITLBinaryWriter_WriteString(This,value)	\
    ( (This)->lpVtbl -> WriteString(This,value) ) 

#define __x_Telegram_CApi_CNative_CITLBinaryWriter_WriteByteArray(This,__valueSize,value)	\
    ( (This)->lpVtbl -> WriteByteArray(This,__valueSize,value) ) 

#define __x_Telegram_CApi_CNative_CITLBinaryWriter_WriteDouble(This,value)	\
    ( (This)->lpVtbl -> WriteDouble(This,value) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* ____x_Telegram_CApi_CNative_CITLBinaryWriter_INTERFACE_DEFINED__ */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0003 */
/* [local] */ 

#ifndef RUNTIMECLASS_Telegram_Api_Native_TLBinaryWriter_DEFINED
#define RUNTIMECLASS_Telegram_Api_Native_TLBinaryWriter_DEFINED
extern const __declspec(selectany) _Null_terminated_ WCHAR RuntimeClass_Telegram_Api_Native_TLBinaryWriter[] = L"Telegram.Api.Native.TLBinaryWriter";
#endif
#if !defined(__cplusplus)

#if !defined(__cplusplus)
/* [v1_enum] */ 
enum __x_Telegram_CApi_CNative_CConnectionType
    {
        ConnectionType_Generic	= 1,
        ConnectionType_Download	= 2,
        ConnectionType_Upload	= 4
    } ;
#endif /* end if !defined(__cplusplus) */

#endif
#if !defined(____x_Telegram_CApi_CNative_CIDatacenter_INTERFACE_DEFINED__)
extern const __declspec(selectany) _Null_terminated_ WCHAR InterfaceName_Telegram_Api_Native_IDatacenter[] = L"Telegram.Api.Native.IDatacenter";
#endif /* !defined(____x_Telegram_CApi_CNative_CIDatacenter_INTERFACE_DEFINED__) */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0003 */
/* [local] */ 

#ifdef __cplusplus
} /* end extern "C" */
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

extern "C" { 
#endif



extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0003_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0003_v0_0_s_ifspec;

#ifndef ____x_Telegram_CApi_CNative_CIDatacenter_INTERFACE_DEFINED__
#define ____x_Telegram_CApi_CNative_CIDatacenter_INTERFACE_DEFINED__

/* interface __x_Telegram_CApi_CNative_CIDatacenter */
/* [uuid][object] */ 



/* interface Telegram::Api::Native::IDatacenter */
/* [uuid][object] */ 


EXTERN_C const IID IID___x_Telegram_CApi_CNative_CIDatacenter;

#if defined(__cplusplus) && !defined(CINTERFACE)
    } /* end extern "C" */
    namespace Telegram {
        namespace Api {
            namespace Native {
                
                MIDL_INTERFACE("ACBC9624-7B96-417D-A9F9-A7F93C195C86")
                IDatacenter : public IInspectable
                {
                public:
                    virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Id( 
                        /* [out][retval] */ UINT32 *value) = 0;
                    
                    virtual HRESULT STDMETHODCALLTYPE GetCurrentAddress( 
                        /* [in] */ Telegram::Api::Native::ConnectionType connectionType,
                        /* [in] */ boolean ipv6,
                        /* [out][retval] */ HSTRING *value) = 0;
                    
                    virtual HRESULT STDMETHODCALLTYPE GetCurrentPort( 
                        /* [in] */ Telegram::Api::Native::ConnectionType connectionType,
                        /* [in] */ boolean ipv6,
                        /* [out][retval] */ UINT32 *value) = 0;
                    
                };

                extern const __declspec(selectany) IID & IID_IDatacenter = __uuidof(IDatacenter);

                
            }  /* end namespace */
        }  /* end namespace */
    }  /* end namespace */
    extern "C" { 
    
#else 	/* C style interface */

    typedef struct __x_Telegram_CApi_CNative_CIDatacenterVtbl
    {
        BEGIN_INTERFACE
        
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __x_Telegram_CApi_CNative_CIDatacenter * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __x_Telegram_CApi_CNative_CIDatacenter * This);
        
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __x_Telegram_CApi_CNative_CIDatacenter * This);
        
        HRESULT ( STDMETHODCALLTYPE *GetIids )( 
            __x_Telegram_CApi_CNative_CIDatacenter * This,
            /* [out] */ ULONG *iidCount,
            /* [size_is][size_is][out] */ IID **iids);
        
        HRESULT ( STDMETHODCALLTYPE *GetRuntimeClassName )( 
            __x_Telegram_CApi_CNative_CIDatacenter * This,
            /* [out] */ HSTRING *className);
        
        HRESULT ( STDMETHODCALLTYPE *GetTrustLevel )( 
            __x_Telegram_CApi_CNative_CIDatacenter * This,
            /* [out] */ TrustLevel *trustLevel);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Id )( 
            __x_Telegram_CApi_CNative_CIDatacenter * This,
            /* [out][retval] */ UINT32 *value);
        
        HRESULT ( STDMETHODCALLTYPE *GetCurrentAddress )( 
            __x_Telegram_CApi_CNative_CIDatacenter * This,
            /* [in] */ __x_Telegram_CApi_CNative_CConnectionType connectionType,
            /* [in] */ boolean ipv6,
            /* [out][retval] */ HSTRING *value);
        
        HRESULT ( STDMETHODCALLTYPE *GetCurrentPort )( 
            __x_Telegram_CApi_CNative_CIDatacenter * This,
            /* [in] */ __x_Telegram_CApi_CNative_CConnectionType connectionType,
            /* [in] */ boolean ipv6,
            /* [out][retval] */ UINT32 *value);
        
        END_INTERFACE
    } __x_Telegram_CApi_CNative_CIDatacenterVtbl;

    interface __x_Telegram_CApi_CNative_CIDatacenter
    {
        CONST_VTBL struct __x_Telegram_CApi_CNative_CIDatacenterVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define __x_Telegram_CApi_CNative_CIDatacenter_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define __x_Telegram_CApi_CNative_CIDatacenter_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define __x_Telegram_CApi_CNative_CIDatacenter_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define __x_Telegram_CApi_CNative_CIDatacenter_GetIids(This,iidCount,iids)	\
    ( (This)->lpVtbl -> GetIids(This,iidCount,iids) ) 

#define __x_Telegram_CApi_CNative_CIDatacenter_GetRuntimeClassName(This,className)	\
    ( (This)->lpVtbl -> GetRuntimeClassName(This,className) ) 

#define __x_Telegram_CApi_CNative_CIDatacenter_GetTrustLevel(This,trustLevel)	\
    ( (This)->lpVtbl -> GetTrustLevel(This,trustLevel) ) 


#define __x_Telegram_CApi_CNative_CIDatacenter_get_Id(This,value)	\
    ( (This)->lpVtbl -> get_Id(This,value) ) 

#define __x_Telegram_CApi_CNative_CIDatacenter_GetCurrentAddress(This,connectionType,ipv6,value)	\
    ( (This)->lpVtbl -> GetCurrentAddress(This,connectionType,ipv6,value) ) 

#define __x_Telegram_CApi_CNative_CIDatacenter_GetCurrentPort(This,connectionType,ipv6,value)	\
    ( (This)->lpVtbl -> GetCurrentPort(This,connectionType,ipv6,value) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* ____x_Telegram_CApi_CNative_CIDatacenter_INTERFACE_DEFINED__ */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0004 */
/* [local] */ 

#ifndef RUNTIMECLASS_Telegram_Api_Native_Datacenter_DEFINED
#define RUNTIMECLASS_Telegram_Api_Native_Datacenter_DEFINED
extern const __declspec(selectany) _Null_terminated_ WCHAR RuntimeClass_Telegram_Api_Native_Datacenter[] = L"Telegram.Api.Native.Datacenter";
#endif
#if !defined(__cplusplus)

#if !defined(__cplusplus)
/* [v1_enum] */ 
enum __x_Telegram_CApi_CNative_CConnectionNeworkType
    {
        ConnectionNeworkType_Mobile	= 0,
        ConnectionNeworkType_WiFi	= 1,
        ConnectionNeworkType_Roaming	= 2
    } ;
#endif /* end if !defined(__cplusplus) */

#endif
#if !defined(____x_Telegram_CApi_CNative_CIConnection_INTERFACE_DEFINED__)
extern const __declspec(selectany) _Null_terminated_ WCHAR InterfaceName_Telegram_Api_Native_IConnection[] = L"Telegram.Api.Native.IConnection";
#endif /* !defined(____x_Telegram_CApi_CNative_CIConnection_INTERFACE_DEFINED__) */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0004 */
/* [local] */ 

#ifdef __cplusplus
} /* end extern "C" */
namespace Telegram {
    namespace Api {
        namespace Native {
            
            /* [v1_enum] */ 
            MIDL_ENUM ConnectionNeworkType
                {
                    Mobile	= 0,
                    WiFi	= 1,
                    Roaming	= 2
                } ;

            const MIDL_ENUM ConnectionNeworkType ConnectionNeworkType_Mobile = ConnectionNeworkType::Mobile;
            const MIDL_ENUM ConnectionNeworkType ConnectionNeworkType_WiFi = ConnectionNeworkType::WiFi;
            const MIDL_ENUM ConnectionNeworkType ConnectionNeworkType_Roaming = ConnectionNeworkType::Roaming;
            
        } /* end namespace */
    } /* end namespace */
} /* end namespace */

extern "C" { 
#endif



extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0004_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0004_v0_0_s_ifspec;

#ifndef ____x_Telegram_CApi_CNative_CIConnection_INTERFACE_DEFINED__
#define ____x_Telegram_CApi_CNative_CIConnection_INTERFACE_DEFINED__

/* interface __x_Telegram_CApi_CNative_CIConnection */
/* [uuid][object] */ 



/* interface Telegram::Api::Native::IConnection */
/* [uuid][object] */ 


EXTERN_C const IID IID___x_Telegram_CApi_CNative_CIConnection;

#if defined(__cplusplus) && !defined(CINTERFACE)
    } /* end extern "C" */
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
                        /* [out][retval] */ Telegram::Api::Native::IDatacenter **value) = 0;
                    
                    virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Type( 
                        /* [out][retval] */ Telegram::Api::Native::ConnectionType *value) = 0;
                    
                    virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_CurrentNetworkType( 
                        /* [out][retval] */ Telegram::Api::Native::ConnectionNeworkType *value) = 0;
                    
                    virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_SessionId( 
                        /* [out][retval] */ INT64 *value) = 0;
                    
                };

                extern const __declspec(selectany) IID & IID_IConnection = __uuidof(IConnection);

                
            }  /* end namespace */
        }  /* end namespace */
    }  /* end namespace */
    extern "C" { 
    
#else 	/* C style interface */

    typedef struct __x_Telegram_CApi_CNative_CIConnectionVtbl
    {
        BEGIN_INTERFACE
        
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __x_Telegram_CApi_CNative_CIConnection * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __x_Telegram_CApi_CNative_CIConnection * This);
        
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __x_Telegram_CApi_CNative_CIConnection * This);
        
        HRESULT ( STDMETHODCALLTYPE *GetIids )( 
            __x_Telegram_CApi_CNative_CIConnection * This,
            /* [out] */ ULONG *iidCount,
            /* [size_is][size_is][out] */ IID **iids);
        
        HRESULT ( STDMETHODCALLTYPE *GetRuntimeClassName )( 
            __x_Telegram_CApi_CNative_CIConnection * This,
            /* [out] */ HSTRING *className);
        
        HRESULT ( STDMETHODCALLTYPE *GetTrustLevel )( 
            __x_Telegram_CApi_CNative_CIConnection * This,
            /* [out] */ TrustLevel *trustLevel);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Token )( 
            __x_Telegram_CApi_CNative_CIConnection * This,
            /* [out][retval] */ UINT32 *value);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Datacenter )( 
            __x_Telegram_CApi_CNative_CIConnection * This,
            /* [out][retval] */ __x_Telegram_CApi_CNative_CIDatacenter **value);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Type )( 
            __x_Telegram_CApi_CNative_CIConnection * This,
            /* [out][retval] */ __x_Telegram_CApi_CNative_CConnectionType *value);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_CurrentNetworkType )( 
            __x_Telegram_CApi_CNative_CIConnection * This,
            /* [out][retval] */ __x_Telegram_CApi_CNative_CConnectionNeworkType *value);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_SessionId )( 
            __x_Telegram_CApi_CNative_CIConnection * This,
            /* [out][retval] */ INT64 *value);
        
        END_INTERFACE
    } __x_Telegram_CApi_CNative_CIConnectionVtbl;

    interface __x_Telegram_CApi_CNative_CIConnection
    {
        CONST_VTBL struct __x_Telegram_CApi_CNative_CIConnectionVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define __x_Telegram_CApi_CNative_CIConnection_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define __x_Telegram_CApi_CNative_CIConnection_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define __x_Telegram_CApi_CNative_CIConnection_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define __x_Telegram_CApi_CNative_CIConnection_GetIids(This,iidCount,iids)	\
    ( (This)->lpVtbl -> GetIids(This,iidCount,iids) ) 

#define __x_Telegram_CApi_CNative_CIConnection_GetRuntimeClassName(This,className)	\
    ( (This)->lpVtbl -> GetRuntimeClassName(This,className) ) 

#define __x_Telegram_CApi_CNative_CIConnection_GetTrustLevel(This,trustLevel)	\
    ( (This)->lpVtbl -> GetTrustLevel(This,trustLevel) ) 


#define __x_Telegram_CApi_CNative_CIConnection_get_Token(This,value)	\
    ( (This)->lpVtbl -> get_Token(This,value) ) 

#define __x_Telegram_CApi_CNative_CIConnection_get_Datacenter(This,value)	\
    ( (This)->lpVtbl -> get_Datacenter(This,value) ) 

#define __x_Telegram_CApi_CNative_CIConnection_get_Type(This,value)	\
    ( (This)->lpVtbl -> get_Type(This,value) ) 

#define __x_Telegram_CApi_CNative_CIConnection_get_CurrentNetworkType(This,value)	\
    ( (This)->lpVtbl -> get_CurrentNetworkType(This,value) ) 

#define __x_Telegram_CApi_CNative_CIConnection_get_SessionId(This,value)	\
    ( (This)->lpVtbl -> get_SessionId(This,value) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* ____x_Telegram_CApi_CNative_CIConnection_INTERFACE_DEFINED__ */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0005 */
/* [local] */ 

#ifndef RUNTIMECLASS_Telegram_Api_Native_Connection_DEFINED
#define RUNTIMECLASS_Telegram_Api_Native_Connection_DEFINED
extern const __declspec(selectany) _Null_terminated_ WCHAR RuntimeClass_Telegram_Api_Native_Connection[] = L"Telegram.Api.Native.Connection";
#endif
#if !defined(__cplusplus)

#if !defined(__cplusplus)
/* [v1_enum] */ 
enum __x_Telegram_CApi_CNative_CConnectionState
    {
        ConnectionState_Connecting	= 1,
        ConnectionState_WaitingForNetwork	= 2,
        ConnectionState_Connected	= 3
    } ;
#endif /* end if !defined(__cplusplus) */

#endif
#if !defined(____x_Telegram_CApi_CNative_CIConnectionManagerStatics_INTERFACE_DEFINED__)
extern const __declspec(selectany) _Null_terminated_ WCHAR InterfaceName_Telegram_Api_Native_IConnectionManagerStatics[] = L"Telegram.Api.Native.IConnectionManagerStatics";
#endif /* !defined(____x_Telegram_CApi_CNative_CIConnectionManagerStatics_INTERFACE_DEFINED__) */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0005 */
/* [local] */ 

#ifdef __cplusplus
} /* end extern "C" */
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

extern "C" { 
#endif



extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0005_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0005_v0_0_s_ifspec;

#ifndef ____x_Telegram_CApi_CNative_CIConnectionManagerStatics_INTERFACE_DEFINED__
#define ____x_Telegram_CApi_CNative_CIConnectionManagerStatics_INTERFACE_DEFINED__

/* interface __x_Telegram_CApi_CNative_CIConnectionManagerStatics */
/* [uuid][object] */ 



/* interface Telegram::Api::Native::IConnectionManagerStatics */
/* [uuid][object] */ 


EXTERN_C const IID IID___x_Telegram_CApi_CNative_CIConnectionManagerStatics;

#if defined(__cplusplus) && !defined(CINTERFACE)
    } /* end extern "C" */
    namespace Telegram {
        namespace Api {
            namespace Native {
                
                MIDL_INTERFACE("6945B11D-9663-4E6E-B866-7A1AB6A98349")
                IConnectionManagerStatics : public IInspectable
                {
                public:
                    virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_Instance( 
                        /* [out][retval] */ Telegram::Api::Native::IConnectionManager **value) = 0;
                    
                };

                extern const __declspec(selectany) IID & IID_IConnectionManagerStatics = __uuidof(IConnectionManagerStatics);

                
            }  /* end namespace */
        }  /* end namespace */
    }  /* end namespace */
    extern "C" { 
    
#else 	/* C style interface */

    typedef struct __x_Telegram_CApi_CNative_CIConnectionManagerStaticsVtbl
    {
        BEGIN_INTERFACE
        
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __x_Telegram_CApi_CNative_CIConnectionManagerStatics * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __x_Telegram_CApi_CNative_CIConnectionManagerStatics * This);
        
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __x_Telegram_CApi_CNative_CIConnectionManagerStatics * This);
        
        HRESULT ( STDMETHODCALLTYPE *GetIids )( 
            __x_Telegram_CApi_CNative_CIConnectionManagerStatics * This,
            /* [out] */ ULONG *iidCount,
            /* [size_is][size_is][out] */ IID **iids);
        
        HRESULT ( STDMETHODCALLTYPE *GetRuntimeClassName )( 
            __x_Telegram_CApi_CNative_CIConnectionManagerStatics * This,
            /* [out] */ HSTRING *className);
        
        HRESULT ( STDMETHODCALLTYPE *GetTrustLevel )( 
            __x_Telegram_CApi_CNative_CIConnectionManagerStatics * This,
            /* [out] */ TrustLevel *trustLevel);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_Instance )( 
            __x_Telegram_CApi_CNative_CIConnectionManagerStatics * This,
            /* [out][retval] */ __x_Telegram_CApi_CNative_CIConnectionManager **value);
        
        END_INTERFACE
    } __x_Telegram_CApi_CNative_CIConnectionManagerStaticsVtbl;

    interface __x_Telegram_CApi_CNative_CIConnectionManagerStatics
    {
        CONST_VTBL struct __x_Telegram_CApi_CNative_CIConnectionManagerStaticsVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define __x_Telegram_CApi_CNative_CIConnectionManagerStatics_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define __x_Telegram_CApi_CNative_CIConnectionManagerStatics_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define __x_Telegram_CApi_CNative_CIConnectionManagerStatics_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define __x_Telegram_CApi_CNative_CIConnectionManagerStatics_GetIids(This,iidCount,iids)	\
    ( (This)->lpVtbl -> GetIids(This,iidCount,iids) ) 

#define __x_Telegram_CApi_CNative_CIConnectionManagerStatics_GetRuntimeClassName(This,className)	\
    ( (This)->lpVtbl -> GetRuntimeClassName(This,className) ) 

#define __x_Telegram_CApi_CNative_CIConnectionManagerStatics_GetTrustLevel(This,trustLevel)	\
    ( (This)->lpVtbl -> GetTrustLevel(This,trustLevel) ) 


#define __x_Telegram_CApi_CNative_CIConnectionManagerStatics_get_Instance(This,value)	\
    ( (This)->lpVtbl -> get_Instance(This,value) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* ____x_Telegram_CApi_CNative_CIConnectionManagerStatics_INTERFACE_DEFINED__ */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0006 */
/* [local] */ 

#if !defined(____x_Telegram_CApi_CNative_CIConnectionManager_INTERFACE_DEFINED__)
extern const __declspec(selectany) _Null_terminated_ WCHAR InterfaceName_Telegram_Api_Native_IConnectionManager[] = L"Telegram.Api.Native.IConnectionManager";
#endif /* !defined(____x_Telegram_CApi_CNative_CIConnectionManager_INTERFACE_DEFINED__) */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0006 */
/* [local] */ 



extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0006_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0006_v0_0_s_ifspec;

#ifndef ____x_Telegram_CApi_CNative_CIConnectionManager_INTERFACE_DEFINED__
#define ____x_Telegram_CApi_CNative_CIConnectionManager_INTERFACE_DEFINED__

/* interface __x_Telegram_CApi_CNative_CIConnectionManager */
/* [uuid][object] */ 



/* interface Telegram::Api::Native::IConnectionManager */
/* [uuid][object] */ 


EXTERN_C const IID IID___x_Telegram_CApi_CNative_CIConnectionManager;

#if defined(__cplusplus) && !defined(CINTERFACE)
    } /* end extern "C" */
    namespace Telegram {
        namespace Api {
            namespace Native {
                
                MIDL_INTERFACE("1C986C1D-56D3-4DA5-8027-5240F0CD2DFF")
                IConnectionManager : public IInspectable
                {
                public:
                    virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_ConnectionState( 
                        /* [out][retval] */ Telegram::Api::Native::ConnectionState *value) = 0;
                    
                    virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_CurrentNetworkType( 
                        /* [out][retval] */ Telegram::Api::Native::ConnectionNeworkType *value) = 0;
                    
                    virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_IsIpv6Enabled( 
                        /* [out][retval] */ boolean *value) = 0;
                    
                    virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_IsNetworkAvailable( 
                        /* [out][retval] */ boolean *value) = 0;
                    
                    virtual HRESULT STDMETHODCALLTYPE SendRequest( 
                        /* [in] */ Telegram::Api::Native::ITLObject *object,
                        /* [in] */ UINT32 datacenterId,
                        /* [in] */ Telegram::Api::Native::ConnectionType connetionType,
                        /* [in] */ boolean immediate,
                        /* [out] */ INT32 *requestToken) = 0;
                    
                    virtual HRESULT STDMETHODCALLTYPE CancelRequest( 
                        INT32 requestToken,
                        boolean notifyServer) = 0;
                    
                    virtual HRESULT STDMETHODCALLTYPE GetDatacenterById( 
                        UINT32 id,
                        /* [out][retval] */ Telegram::Api::Native::IDatacenter **value) = 0;
                    
                    virtual HRESULT STDMETHODCALLTYPE BoomBaby( 
                        /* [out][retval] */ Telegram::Api::Native::IConnection **value) = 0;
                    
                };

                extern const __declspec(selectany) IID & IID_IConnectionManager = __uuidof(IConnectionManager);

                
            }  /* end namespace */
        }  /* end namespace */
    }  /* end namespace */
    extern "C" { 
    
#else 	/* C style interface */

    typedef struct __x_Telegram_CApi_CNative_CIConnectionManagerVtbl
    {
        BEGIN_INTERFACE
        
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            __x_Telegram_CApi_CNative_CIConnectionManager * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            __x_Telegram_CApi_CNative_CIConnectionManager * This);
        
        ULONG ( STDMETHODCALLTYPE *Release )( 
            __x_Telegram_CApi_CNative_CIConnectionManager * This);
        
        HRESULT ( STDMETHODCALLTYPE *GetIids )( 
            __x_Telegram_CApi_CNative_CIConnectionManager * This,
            /* [out] */ ULONG *iidCount,
            /* [size_is][size_is][out] */ IID **iids);
        
        HRESULT ( STDMETHODCALLTYPE *GetRuntimeClassName )( 
            __x_Telegram_CApi_CNative_CIConnectionManager * This,
            /* [out] */ HSTRING *className);
        
        HRESULT ( STDMETHODCALLTYPE *GetTrustLevel )( 
            __x_Telegram_CApi_CNative_CIConnectionManager * This,
            /* [out] */ TrustLevel *trustLevel);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_ConnectionState )( 
            __x_Telegram_CApi_CNative_CIConnectionManager * This,
            /* [out][retval] */ __x_Telegram_CApi_CNative_CConnectionState *value);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_CurrentNetworkType )( 
            __x_Telegram_CApi_CNative_CIConnectionManager * This,
            /* [out][retval] */ __x_Telegram_CApi_CNative_CConnectionNeworkType *value);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_IsIpv6Enabled )( 
            __x_Telegram_CApi_CNative_CIConnectionManager * This,
            /* [out][retval] */ boolean *value);
        
        /* [propget] */ HRESULT ( STDMETHODCALLTYPE *get_IsNetworkAvailable )( 
            __x_Telegram_CApi_CNative_CIConnectionManager * This,
            /* [out][retval] */ boolean *value);
        
        HRESULT ( STDMETHODCALLTYPE *SendRequest )( 
            __x_Telegram_CApi_CNative_CIConnectionManager * This,
            /* [in] */ __x_Telegram_CApi_CNative_CITLObject *object,
            /* [in] */ UINT32 datacenterId,
            /* [in] */ __x_Telegram_CApi_CNative_CConnectionType connetionType,
            /* [in] */ boolean immediate,
            /* [out] */ INT32 *requestToken);
        
        HRESULT ( STDMETHODCALLTYPE *CancelRequest )( 
            __x_Telegram_CApi_CNative_CIConnectionManager * This,
            INT32 requestToken,
            boolean notifyServer);
        
        HRESULT ( STDMETHODCALLTYPE *GetDatacenterById )( 
            __x_Telegram_CApi_CNative_CIConnectionManager * This,
            UINT32 id,
            /* [out][retval] */ __x_Telegram_CApi_CNative_CIDatacenter **value);
        
        HRESULT ( STDMETHODCALLTYPE *BoomBaby )( 
            __x_Telegram_CApi_CNative_CIConnectionManager * This,
            /* [out][retval] */ __x_Telegram_CApi_CNative_CIConnection **value);
        
        END_INTERFACE
    } __x_Telegram_CApi_CNative_CIConnectionManagerVtbl;

    interface __x_Telegram_CApi_CNative_CIConnectionManager
    {
        CONST_VTBL struct __x_Telegram_CApi_CNative_CIConnectionManagerVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define __x_Telegram_CApi_CNative_CIConnectionManager_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define __x_Telegram_CApi_CNative_CIConnectionManager_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define __x_Telegram_CApi_CNative_CIConnectionManager_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define __x_Telegram_CApi_CNative_CIConnectionManager_GetIids(This,iidCount,iids)	\
    ( (This)->lpVtbl -> GetIids(This,iidCount,iids) ) 

#define __x_Telegram_CApi_CNative_CIConnectionManager_GetRuntimeClassName(This,className)	\
    ( (This)->lpVtbl -> GetRuntimeClassName(This,className) ) 

#define __x_Telegram_CApi_CNative_CIConnectionManager_GetTrustLevel(This,trustLevel)	\
    ( (This)->lpVtbl -> GetTrustLevel(This,trustLevel) ) 


#define __x_Telegram_CApi_CNative_CIConnectionManager_get_ConnectionState(This,value)	\
    ( (This)->lpVtbl -> get_ConnectionState(This,value) ) 

#define __x_Telegram_CApi_CNative_CIConnectionManager_get_CurrentNetworkType(This,value)	\
    ( (This)->lpVtbl -> get_CurrentNetworkType(This,value) ) 

#define __x_Telegram_CApi_CNative_CIConnectionManager_get_IsIpv6Enabled(This,value)	\
    ( (This)->lpVtbl -> get_IsIpv6Enabled(This,value) ) 

#define __x_Telegram_CApi_CNative_CIConnectionManager_get_IsNetworkAvailable(This,value)	\
    ( (This)->lpVtbl -> get_IsNetworkAvailable(This,value) ) 

#define __x_Telegram_CApi_CNative_CIConnectionManager_SendRequest(This,object,datacenterId,connetionType,immediate,requestToken)	\
    ( (This)->lpVtbl -> SendRequest(This,object,datacenterId,connetionType,immediate,requestToken) ) 

#define __x_Telegram_CApi_CNative_CIConnectionManager_CancelRequest(This,requestToken,notifyServer)	\
    ( (This)->lpVtbl -> CancelRequest(This,requestToken,notifyServer) ) 

#define __x_Telegram_CApi_CNative_CIConnectionManager_GetDatacenterById(This,id,value)	\
    ( (This)->lpVtbl -> GetDatacenterById(This,id,value) ) 

#define __x_Telegram_CApi_CNative_CIConnectionManager_BoomBaby(This,value)	\
    ( (This)->lpVtbl -> BoomBaby(This,value) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* ____x_Telegram_CApi_CNative_CIConnectionManager_INTERFACE_DEFINED__ */


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0007 */
/* [local] */ 

#ifndef RUNTIMECLASS_Telegram_Api_Native_ConnectionManager_DEFINED
#define RUNTIMECLASS_Telegram_Api_Native_ConnectionManager_DEFINED
extern const __declspec(selectany) _Null_terminated_ WCHAR RuntimeClass_Telegram_Api_Native_ConnectionManager[] = L"Telegram.Api.Native.ConnectionManager";
#endif


/* interface __MIDL_itf_Telegram2EApi2ENative_0000_0007 */
/* [local] */ 



extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0007_v0_0_c_ifspec;
extern RPC_IF_HANDLE __MIDL_itf_Telegram2EApi2ENative_0000_0007_v0_0_s_ifspec;

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


