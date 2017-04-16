#pragma once
#include <wrl.h>

using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			public ref class ConnectionManager sealed
			{
			public:
				property ConnectionManager^ Instance
				{
					ConnectionManager^ get();
				}

			private:
				ConnectionManager();

				static ConnectionManager^ s_instance;
			};

		}
	}
}