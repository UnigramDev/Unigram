#pragma once
#include <wrl.h>
#include "Telegram.Api.Native.h"

#define DEBUG_CRITICAL_SECTION 0

#if DEBUG_CRITICAL_SECTION
#include "DebugCriticalSection.h"
#endif

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

#if DEBUG_CRITICAL_SECTION
				typedef Microsoft::WRL::Wrappers::DebugCriticalSection CriticalSection;
#else
				typedef Microsoft::WRL::Wrappers::CriticalSection CriticalSection;
#endif

				inline CriticalSection::SyncLock LockCriticalSection()
				{
					return m_criticalSection.Lock();
				}

				inline CriticalSection::SyncLock TryLockCriticalSection()
				{
					return m_criticalSection.TryLock();
				}

			private:
				CriticalSection m_criticalSection;
			};

		}
	}
}