#pragma once
#include "TLObject.h"

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{
			namespace TL
			{

				class TLInitConnectionObject;
				class TLInvokeWithLayerObject;

				namespace TLObjectTraits
				{

					struct TLInvokeWithLayerTraits
					{
						static constexpr UINT32 Constructor = 0xda9b0d0d;
						static constexpr boolean IsLayerNeeded = false;

						static HRESULT CreateInstance(_Out_ ITLObject** instance)
						{
							auto object = Make<TLInvokeWithLayerObject>();
							return object.CopyTo(instance);
						}
					};

					struct TLInitConnectionTraits
					{
						static constexpr UINT32 Constructor = 0x69796de9;
						static constexpr boolean IsLayerNeeded = false;

						static HRESULT CreateInstance(_Out_ ITLObject** instance)
						{
							auto object = Make<TLInitConnectionObject>();
							return object.CopyTo(instance);
						}
					};

				}


				class TLInvokeWithLayerObject WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLInvokeWithLayerTraits>, TLObjectWithQuery>
				{
					InspectableClass(L"Telegram.Api.Native.TL.TLInvokeWithLayerObject", BaseTrust);

				public:
					//COM exported methods
					virtual HRESULT ReadBody(_In_ ITLBinaryReaderEx* reader) override;
					virtual HRESULT WriteBody(_In_ ITLBinaryWriterEx* writer) override;

					//Internal methods
					STDMETHODIMP RuntimeClassInitialize(_In_ ITLObject* query);
				};

				class TLInitConnectionObject WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLInitConnectionTraits>, TLObjectWithQuery>
				{
					InspectableClass(L"Telegram.Api.Native.TL.TLInitConnectionObject", BaseTrust);

				public:
					//COM exported methods
					virtual HRESULT ReadBody(_In_ ITLBinaryReaderEx* reader) override;
					virtual HRESULT WriteBody(_In_ ITLBinaryWriterEx* writer) override;

					//Internal methods
					STDMETHODIMP RuntimeClassInitialize(_In_ IUserConfiguration* userConfiguration, _In_ ITLObject* query);

				private:
					ComPtr<IUserConfiguration> m_userConfiguration;
				};

			}
		}
	}
}