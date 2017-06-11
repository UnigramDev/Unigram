#pragma once
#include <wrl.h>
#include <functional>
#include "MultiThreadObject.h"

using namespace Microsoft::WRL;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			class ThreadpoolManager abstract : public virtual MultiThreadObject
			{
				friend class EventObject;
				template<typename EventTraits>
				friend class EventObjectT;

			public:
				ThreadpoolManager();
				~ThreadpoolManager();

				HRESULT AttachEventObject(_In_ EventObject* object);
				HRESULT DetachEventObject(_In_ EventObject* object, boolean waitCallback);
				HRESULT SubmitWork(_In_ std::function<void()> work);

			protected:
				HRESULT RuntimeClassInitialize(UINT32 minimumThreadCount, UINT32 maximumThreadCount);

				inline PTP_CALLBACK_ENVIRON GetEnvironment()
				{
					return &m_threadpoolEnvironment;
				}

			private:
				static void NTAPI WorkCallback(_Inout_ PTP_CALLBACK_INSTANCE instance, _Inout_opt_ PVOID context, _Inout_ PTP_WORK work);

				TP_CALLBACK_ENVIRON m_threadpoolEnvironment;
				PTP_POOL m_threadpool;
				PTP_CLEANUP_GROUP m_threadpoolCleanupGroup;
			};

		}
	}
}