#pragma once
#include <wrl.h>

using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;

namespace ABI
{
	namespace Telegram
	{
		namespace Api
		{
			namespace Native
			{

				MIDL_INTERFACE("F310910A-64AF-454C-9A39-E2785D0FD4C0") IRequest : public IUnknown
				{
				public:
				};

				class Request WrlSealed : public RuntimeClass<RuntimeClassFlags<ClassicCom>, IRequest>
				{
				public:
					Request();
					~Request();
				};

			}
		}
	}
}