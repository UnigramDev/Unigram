#pragma once

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			ref class Datacenter sealed
			{
			public:
				property uint32 Id
				{
					uint32 get();
				}

			internal:
				Datacenter(uint32 id);

			private:
				uint32 m_id;
			};

		}
	}
}