#pragma once
#include <wrl.h>
#include "Telegram.Api.Native.h"

using namespace Microsoft::WRL::Wrappers;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			class MultiThreadObject
			{
			protected:
				inline CriticalSection::SyncLock LockCriticalSection()
				{
					return m_criticalSection.Lock();
				}

			private:
				CriticalSection m_criticalSection;
			};

		}
	}
}