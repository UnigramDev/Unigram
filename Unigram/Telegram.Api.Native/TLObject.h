#pragma once
#include <unordered_map>
#include <memory>
#include <wrl.h>
#include "Telegram.Api.Native.h"
#include "TLBinaryReader.h"
#include "TLBinaryWriter.h"
#include "Helpers\COMHelper.h"

#define MAKE_TLOBJECT_TRAITS(objectTypeName, constructor, isLayerNeeded) \
	struct objectTypeName##Traits \
	{ \
		typedef typename objectTypeName TLObjectType; \
		static constexpr UINT32 Constructor = constructor; \
		static constexpr boolean IsLayerNeeded = isLayerNeeded; \
		static constexpr WCHAR RuntimeClassName[] = _STRINGIFY_W("Telegram.Api.Native.TL." _STRINGIFY(objectTypeName)); \
		static HRESULT CreateInstance(_Out_ ITLObject** instance) \
		{ \
			auto object = Make<TLObjectType>(); \
			return object.CopyTo(instance); \
		} \
	} \


#define REGISTER_TLOBJECT_CONSTRUCTOR(objectTypeName) \
	template<> \
	Telegram::Api::Native::TL::Details::TLObjectInitializer<##objectTypeName##::Traits> TLObjectT<##objectTypeName##::Traits>::Initializer = Telegram::Api::Native::TL::Details::TLObjectInitializer<##objectTypeName##::Traits>()\


using namespace Microsoft::WRL;
using ABI::Telegram::Api::Native::IUserConfiguration;
using ABI::Telegram::Api::Native::TL::ITLObject;
using ABI::Telegram::Api::Native::TL::ITLUnparsedObject;
using ABI::Telegram::Api::Native::TL::ITLBinaryReader;
using ABI::Telegram::Api::Native::TL::ITLBinaryWriter;
using ABI::Telegram::Api::Native::TL::ITLBinaryReaderEx;
using ABI::Telegram::Api::Native::TL::ITLBinaryWriterEx;

namespace ABI
{
	namespace Telegram
	{
		namespace Api
		{
			namespace Native
			{
				namespace TL
				{

					MIDL_INTERFACE("CCCED6D5-978D-4719-81BA-A61E74EECE29") ITLObjectWithQuery : public IUnknown
					{
					public:
						virtual HRESULT STDMETHODCALLTYPE get_Query(_Out_ ITLObject** value) = 0;
					};

				}
			}
		}
	}
}


using ABI::Telegram::Api::Native::TL::ITLObjectWithQuery;
using ABI::Telegram::Api::Native::TL::ITLObjectConstructorDelegate;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{
			namespace TL
			{
				namespace Details
				{

					template<typename TLObjectTraits>
					struct TLObjectInitializer
					{
						TLObjectInitializer()
						{
							TLObject::RegisterTLObjecConstructor<TLObjectTraits>();
						}
					};

				}

				class TLObject abstract : public Implements<RuntimeClassFlags<WinRtClassicComMix>, ITLObject>
				{
					template<typename TLObjectTraits>
					friend struct Details::TLObjectInitializer;
					friend class TLObjectSerializerStatics;

				public:
					//COM exported methods
					IFACEMETHODIMP Read(_In_ ITLBinaryReader* reader);
					IFACEMETHODIMP Write(_In_ ITLBinaryWriter* writer);

					//Internal methods
					static HRESULT GetObjectConstructor(UINT32 constructor, _Out_ ComPtr<ITLObjectConstructorDelegate>& delegate);

				protected:
					virtual HRESULT ReadBody(_In_ ITLBinaryReaderEx* reader)
					{
						return E_NOTIMPL;
					}

					virtual HRESULT WriteBody(_In_ ITLBinaryWriterEx* writer)
					{
						return E_NOTIMPL;
					}

				private:
					typedef HRESULT(*TLObjectConstructor)(_Out_ ITLObject**);

					static HRESULT RegisterTLObjecConstructor(UINT32 constructor, _In_ ITLObjectConstructorDelegate* delegate);
					static std::unordered_map<UINT32, ComPtr<ITLObjectConstructorDelegate>>& GetObjectConstructors();
				
					template<typename TLObjectTraits>
					inline static void RegisterTLObjecConstructor()
					{
						GetObjectConstructors()[TLObjectTraits::Constructor] = Callback<ITLObjectConstructorDelegate>(&TLObjectTraits::CreateInstance);
					}
				};

				class TLObjectWithQuery abstract : public Implements<RuntimeClassFlags<WinRtClassicComMix>, CloakedIid<ITLObjectWithQuery>>
				{
				public:
					//COM exported methods
					IFACEMETHODIMP get_Query(_Out_ ITLObject** value);

				protected:
					HRESULT RuntimeClassInitialize(_In_ ITLObject* query);

					inline ITLObject* GetQuery() const
					{
						return m_query.Get();
					}

					inline HRESULT WriteQuery(_In_ ITLBinaryWriterEx* writer)
					{
						return writer->WriteObject(m_query.Get());
					}

				private:
					ComPtr<ITLObject> m_query;
				};

				template<typename TLObjectTraits>
				class TLObjectT abstract : public Implements<RuntimeClassFlags<WinRtClassicComMix>, TLObject>
				{
				public:
					typedef typename TLObjectTraits Traits;
					static constexpr UINT32 Constructor = TLObjectTraits::Constructor;

					//COM exported methods
					IFACEMETHODIMP get_Constructor(_Out_ UINT32* value)
					{
						if (value == nullptr)
						{
							return E_POINTER;
						}

						*value = TLObjectTraits::Constructor;
						return S_OK;
					}

					IFACEMETHODIMP get_IsLayerNeeded(_Out_ boolean* value)
					{
						if (value == nullptr)
						{
							return E_POINTER;
						}

						*value = TLObjectTraits::IsLayerNeeded;
						return S_OK;
					}

				private:
					static Details::TLObjectInitializer<TLObjectTraits> Initializer;
				};

				class TLUnparsedObject WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, ITLUnparsedObject, TLObject>
				{
					InspectableClass(RuntimeClass_Telegram_Api_Native_TL_TLUnparsedObject, BaseTrust);

				public:
					TLUnparsedObject(UINT32 constructor, _In_ ITLBinaryReader* reader);

					//COM exported methods
					STDMETHODIMP get_Constructor(_Out_ UINT32* value);
					STDMETHODIMP get_Reader(_Out_ ITLBinaryReader** value);
					STDMETHODIMP get_IsLayerNeeded(_Out_ boolean* value);

				private:
					UINT32 m_constructor;
					ComPtr<ITLBinaryReader> m_reader;
				};

			}
		}
	}
}