#pragma once
#include <wrl.h>

using namespace Platform;
using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;
using namespace Windows::Foundation::Metadata;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			[uuid("8662588C-DE66-44F7-AC07-EC65913760D0")]
			interface class IEventObject
			{
				void OnEvent(uint32 events);
			};

		}
	}
}

namespace ABI
{
	namespace Telegram
	{
		namespace Api
		{
			namespace Native
			{

				MIDL_INTERFACE("8662588C-DE66-44F7-AC07-EC65913760D0") IEventObject : public IInspectable
				{
				public:
					virtual HRESULT STDMETHODCALLTYPE OnEvent(uint32 events) = 0;
				};

			}
		}
	}
}
