#pragma once
#include <unordered_map>
#include <wrl.h>
#include "Telegram.Api.Native.h"

using namespace Microsoft::WRL;
using ABI::Telegram::Api::Native::ITLObject;
using ABI::Telegram::Api::Native::ITLBinaryReader;
using ABI::Telegram::Api::Native::ITLBinaryWriter;

namespace ABI
{
	namespace Telegram
	{
		namespace Api
		{
			namespace Native
			{

				interface ITLBinaryReaderEx;
				interface ITLBinaryWriterEx;

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

using ABI::Telegram::Api::Native::ITLBinaryReaderEx;
using ABI::Telegram::Api::Native::ITLBinaryWriterEx;
using ABI::Telegram::Api::Native::ITLObjectWithConstructor;
using ABI::Telegram::Api::Native::ITLObjectWithQuery;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			class TLInitConnectionObject;

			namespace TLObjectTraits
			{

				struct TLInitConnectionTraits
				{
					static constexpr UINT32 Constructor = 0x69796de9;
					static constexpr boolean IsLayerNeeded = false;

					static HRESULT CreateInstance(_Out_ ITLObjectWithConstructor** instance)
					{
						auto object = Make<TLInitConnectionObject>();
						return object.CopyTo(instance);
					}
				};

			}


			class TLObject abstract : public Implements<RuntimeClassFlags<WinRtClassicComMix>, ITLObject >
			{
				template<typename TLObjectTraits>
				friend class TLObjectT;

			public:
				TLObject();
				~TLObject();

				//COM exported methods
				IFACEMETHODIMP get_Size(_Out_ UINT32* value);
				IFACEMETHODIMP Read(_In_ ITLBinaryReader* reader);
				IFACEMETHODIMP Write(_In_ ITLBinaryWriter* writer);

				//Internal methods
				virtual HRESULT Read(_In_ ITLBinaryReaderEx* reader) = 0;
				virtual HRESULT Write(_In_ ITLBinaryWriterEx* writer) = 0;

			private:
				typedef HRESULT(*TLObjectConstructor)(_Out_ ITLObjectWithConstructor**);

				static std::unordered_map<UINT32, TLObjectConstructor> s_constructors;
			};

			class TLObjectWithQuery abstract : public Implements<RuntimeClassFlags<WinRtClassicComMix>, CloakedIid<ITLObjectWithQuery>>
			{
			public:
				TLObjectWithQuery();
				~TLObjectWithQuery();

				//COM exported methods
				IFACEMETHODIMP get_Query(_Out_ ITLObject** value);

			protected:
				HRESULT RuntimeClassInitialize(_In_ ITLObject* query);

				inline ITLObject* GetQuery() const
				{
					return m_query.Get();
				}

			private:
				ComPtr<ITLObject> m_query;
			};

			template<typename TLObjectTraits>
			class TLObjectT abstract : public Implements<RuntimeClassFlags<WinRtClassicComMix>, CloakedIid<ITLObjectWithConstructor>, TLObject>
			{
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

			class TLInitConnectionObject WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLInitConnectionTraits>, TLObjectWithQuery>
			{
				InspectableClass(L"Telegram.Api.Native.TLInitConnectionObject", BaseTrust);

			public:
				TLInitConnectionObject();
				~TLInitConnectionObject();

				//COM exported methods
				virtual HRESULT Read(_In_ ITLBinaryReaderEx* reader) override;
				virtual HRESULT Write(_In_ ITLBinaryWriterEx* writer) override;

				//Internal methods
				STDMETHODIMP RuntimeClassInitialize(_In_ ITLObject* query);

			private:
				INT32 apiId;
				std::wstring m_deviceModel;
				std::wstring m_systemVersion;
				std::wstring m_appVersion;
				std::wstring m_langCode;
			};

		}
	}
}