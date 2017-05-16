#pragma once
#include <unordered_map>
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

using namespace Microsoft::WRL;
using ABI::Telegram::Api::Native::IUserConfiguration;
using ABI::Telegram::Api::Native::TL::ITLObject;
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

					MIDL_INTERFACE("6BE8E0F6-9152-4420-AEFC-7DF53FB0238E") ITLObjectWithConstructor : public IUnknown
					{
					public:
						virtual HRESULT STDMETHODCALLTYPE get_Constructor(_Out_ UINT32* value) = 0;
					};

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


using ABI::Telegram::Api::Native::TL::ITLObjectWithConstructor;
using ABI::Telegram::Api::Native::TL::ITLObjectWithQuery;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{
			namespace TL
			{

				class TLObject abstract : public Implements<RuntimeClassFlags<WinRtClassicComMix>, ITLObject>
				{
					template<typename TLObjectTraits>
					friend class TLObjectT;

				public:
					//COM exported methods
					IFACEMETHODIMP Read(_In_ ITLBinaryReader* reader);
					IFACEMETHODIMP Write(_In_ ITLBinaryWriter* writer);

					//Internal methods
					virtual HRESULT Read(_In_ ITLBinaryReaderEx* reader) = 0;
					virtual HRESULT Write(_In_ ITLBinaryWriterEx* writer) = 0;

					static HRESULT Deserialize(_In_ ITLBinaryReaderEx* reader, UINT32 constructor, _Out_ ITLObject** object);

				private:
					typedef HRESULT(*TLObjectConstructor)(_Out_ ITLObject**);

					static std::unordered_map<UINT32, TLObjectConstructor> s_constructors;
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

					inline HRESULT Write(_In_ ITLBinaryWriterEx* writer)
					{
						return m_query->Write(writer);
					}

				private:
					ComPtr<ITLObject> m_query;
				};

				template<typename TLObjectTraits>
				class TLObjectT abstract : public Implements<RuntimeClassFlags<WinRtClassicComMix>, CloakedIid<ITLObjectWithConstructor>, TLObject>
				{
					typedef typename TLObjectTraits Traits;

				public:
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

					//Internal methods
					virtual HRESULT Read(_In_ ITLBinaryReaderEx* reader) override final
					{
						return ReadBody(reader);
					}

					virtual HRESULT Write(_In_ ITLBinaryWriterEx* writer) override final
					{
						HRESULT result;
						ReturnIfFailed(result, writer->WriteUInt32(TLObjectTraits::Constructor));

						return WriteBody(writer);
					}

				protected:
					virtual HRESULT ReadBody(_In_ ITLBinaryReaderEx* reader) = 0;
					virtual HRESULT WriteBody(_In_ ITLBinaryWriterEx* writer) = 0;

				private:
					struct Initializer
					{
						Initializer()
						{
							TLObject::s_objectConstructors[TLObjectTraits::Constructor] = &TLObjectTraits::CreateInstance;
						}
					};

					static Initializer s_initializer;
				};

			}
		}
	}
}