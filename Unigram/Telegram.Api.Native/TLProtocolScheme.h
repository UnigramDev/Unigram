#pragma once
#include "TLObject.h"
#include "Telegram.Api.Native.h"

using ABI::Telegram::Api::Native::TL::ITLError;
using ABI::Telegram::Api::Native::TL::ITLErrorFactory;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{
			namespace TL
			{

				class TLError;
				class TLFutureSalt;
				class TLInvokeWithLayer;
				class TLInitConnection;


				namespace TLObjectTraits
				{

					MAKE_TLOBJECT_TRAITS(TLError, 0xc4b9f9bb, false);
					MAKE_TLOBJECT_TRAITS(TLFutureSalt, 0x0949d9dc, false);
					MAKE_TLOBJECT_TRAITS(TLInvokeWithLayer, 0xda9b0d0d, false);
					MAKE_TLOBJECT_TRAITS(TLInitConnection, 0x69796de9, false);

				}


				class TLError WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, ITLError, TLObjectT<TLObjectTraits::TLErrorTraits>>
				{
					InspectableClass(RuntimeClass_Telegram_Api_Native_TL_TLError, BaseTrust);

				public:
					//COM exported methods
					STDMETHODIMP get_Code(_Out_ UINT32* value);
					STDMETHODIMP get_Text(_Out_ HSTRING* value);

					//Internal methods
					STDMETHODIMP RuntimeClassInitialize(INT32 code, _In_ HSTRING text);

					template<size_t sizeDest>
					STDMETHODIMP RuntimeClassInitialize(INT32 code, _In_ const WCHAR(&text)[sizeDest])
					{
						m_code = code;
						return m_text.Set(text);
					}

					inline INT32 GetCode() const
					{
						return m_code;
					}

					inline HSTRING GetText() const
					{
						return m_text.Get();
					}

				protected:
					virtual HRESULT ReadBody(_In_ ITLBinaryReaderEx* reader) override;
					virtual HRESULT WriteBody(_In_ ITLBinaryWriterEx* writer) override;

				private:
					INT32 m_code;
					HString m_text;
				};

				class TLFutureSalt WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLFutureSaltTraits>>
				{
					InspectableClass(Traits::RuntimeClassName, BaseTrust);

				public:
					TLFutureSalt();
					~TLFutureSalt();

					//Internal methods
					inline INT32 GetValidSince() const
					{
						return m_validSince;
					}

					inline INT32 GetValidUntil() const
					{
						return m_validUntil;
					}

					inline INT64 GetSalt() const
					{
						return m_salt;
					}

				protected:
					virtual HRESULT ReadBody(_In_ ITLBinaryReaderEx* reader) override;

				private:
					INT32 m_validSince;
					INT32 m_validUntil;
					INT64 m_salt;
				};

				class TLInvokeWithLayer WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLInvokeWithLayerTraits>, TLObjectWithQuery>
				{
					InspectableClass(Traits::RuntimeClassName, BaseTrust);

				public:
					//Internal methods
					STDMETHODIMP RuntimeClassInitialize(_In_ ITLObject* query);

				protected:
					virtual HRESULT WriteBody(_In_ ITLBinaryWriterEx* writer) override;
				};

				class TLInitConnection WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLInitConnectionTraits>, TLObjectWithQuery>
				{
					InspectableClass(Traits::RuntimeClassName, BaseTrust);

				public:
					//Internal methods
					STDMETHODIMP RuntimeClassInitialize(_In_ IUserConfiguration* userConfiguration, _In_ ITLObject* query);

				protected:
					virtual HRESULT WriteBody(_In_ ITLBinaryWriterEx* writer) override;

				private:
					ComPtr<IUserConfiguration> m_userConfiguration;
				};


				class TLErrorFactory WrlSealed : public AgileActivationFactory<ITLErrorFactory>
				{
					InspectableClassStatic(RuntimeClass_Telegram_Api_Native_TL_TLError, BaseTrust);

				public:
					IFACEMETHODIMP CreateTLError(UINT32 code, _In_  HSTRING text, _Out_ ITLError** instance);
				};

			}
		}
	}
}