#pragma once
#include <unordered_map>
#include <memory>
#include <type_traits>
#include <wrl.h>
#include "Telegram.Api.Native.h"
#include "TLBinaryReader.h"
#include "TLBinaryWriter.h"
#include "Helpers\COMHelper.h"

#define MakeTLObjectTraits(objectTypeName, constructor, isLayerNeeded) \
	struct objectTypeName##Traits \
	{ \
		typedef typename objectTypeName TLObjectType; \
		static constexpr UINT32 Constructor = constructor; \
		static constexpr boolean IsLayerNeeded = isLayerNeeded; \
		static constexpr WCHAR RuntimeClassName[] = _STRINGIFY_W("Telegram.Api.Native.TL." _STRINGIFY(objectTypeName)); \
	} \


#define RegisterTLObjectConstructor(objectTypeName) \
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
							TLObject::RegisterObjectConstructor<TLObjectTraits>();
						}
					};

				}


				class TLBinaryReader;

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
					inline static void RegisterObjectConstructor()
					{
						GetObjectConstructors()[TLObjectTraits::Constructor] = Callback<ITLObjectConstructorDelegate>(CreateObjectInstance<TLObjectTraits>);
					}

					template<typename TLObjectTraits>
					inline static HRESULT CreateObjectInstance(_Out_ ITLObject** instance)
					{
						auto object = Make<typename TLObjectTraits::TLObjectType>();
						return object.CopyTo(instance);
					}
				};

				class TLObjectWithQuery abstract : public Implements<RuntimeClassFlags<WinRtClassicComMix>, CloakedIid<ITLObjectWithQuery>>
				{
				public:
					//COM exported methods
					IFACEMETHODIMP get_Query(_Out_ ITLObject** value);

				protected:
					HRESULT RuntimeClassInitialize(_In_ ITLObject* query);

					inline ComPtr<ITLObject>& GetQuery()
					{
						return m_query;
					}

					inline HRESULT WriteQuery(_In_ ITLBinaryWriterEx* writer)
					{
						return writer->WriteObject(m_query.Get());
					}

					inline HRESULT ReadQuery(_In_ ITLBinaryReader* reader)
					{
						return reader->ReadObject(&m_query);
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

				class TLUnparsedObject WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, ITLUnparsedObject, ITLObject>
				{
					InspectableClass(RuntimeClass_Telegram_Api_Native_TL_TLUnparsedObject, BaseTrust);

				public:
					//COM exported methods
					IFACEMETHODIMP get_Reader(_Out_ ITLBinaryReader** value);
					IFACEMETHODIMP get_Constructor(_Out_ UINT32* value);
					IFACEMETHODIMP get_IsLayerNeeded(_Out_ boolean* value);
					IFACEMETHODIMP Read(_In_ ITLBinaryReader* reader);
					IFACEMETHODIMP Write(_In_ ITLBinaryWriter* writer);


					//Internal methods
					STDMETHODIMP RuntimeClassInitialize(UINT32 constructor, _In_ TLBinaryReader* reader);
					STDMETHODIMP RuntimeClassInitialize(UINT32 constructor, UINT32 objectSizeWithoutConstructor, _In_ TLBinaryReader* reader);

				private:
					UINT32 m_constructor;
					ComPtr<ITLBinaryReader> m_reader;
				};


				template<typename TLObjectType>
				inline typename std::enable_if<std::is_base_of<ITLObject, TLObjectType>::value, TLObjectType>::type* CastTLObject(_In_ ITLObject* object)
				{
					if (object == nullptr)
					{
						return nullptr;
					}

					UINT32 constructor;
					if (SUCCEEDED(object->get_Constructor(&constructor)) && constructor == TLObjectType::Constructor)
					{
						return static_cast<typename TLObjectType*>(object);
					}

					return nullptr;
				}

				template<typename TLObjectType>
				inline HRESULT CastTLObject(_In_ ITLObject* object, _Out_ typename std::enable_if<std::is_base_of<ITLObject, TLObjectType>::value, TLObjectType>::type** value)
				{
					if (object == nullptr)
					{
						return E_INVALIDARG;
					}

					if (value == nullptr)
					{
						return E_POINTER;
					}

					UINT32 constructor;
					if (SUCCEEDED(object->get_Constructor(&constructor)) && constructor == TLObjectType::Constructor)
					{
						return static_cast<typename TLObjectType*>(object);
					}

					return E_NOINTERFACE;
				}

			}
		}
	}
}