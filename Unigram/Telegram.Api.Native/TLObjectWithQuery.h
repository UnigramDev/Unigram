#pragma once
#include <wrl.h>
#include "Telegram.Api.Native.h"

using namespace Microsoft::WRL;
using ABI::Telegram::Api::Native::ITLObject;

namespace ABI
{
	namespace Telegram
	{
		namespace Api
		{
			namespace Native
			{

				MIDL_INTERFACE("CCCED6D5-978D-4719-81BA-A61E74EECE29") ITLObjectWithQuery : public ITLObject
				{
				public:
					virtual HRESULT STDMETHODCALLTYPE get_Query(_Out_ ITLObject** value) = 0;
				};

			}
		}
	}
}


using ABI::Telegram::Api::Native::ITLObjectWithQuery;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			class TLObjectWithQuery abstract : public Implements<RuntimeClassFlags<WinRtClassicComMix>, ITLObjectWithQuery>
			{
			public:
				TLObjectWithQuery(_In_ ITLObject* query);
				~TLObjectWithQuery();

				//COM exported methods
				STDMETHODIMP get_Query(_Out_ ITLObject** value);

			protected:
				inline ITLObject* GetQuery() const
				{
					return m_query.Get();
				}

			private:
				ComPtr<ITLObject> m_query;
			};

		}
	}
}