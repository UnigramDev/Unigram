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

			class ThreadpoolObject abstract
			{
				friend class ThreadpoolManager;

			protected:
				virtual void OnGroupCancel() = 0;
			};

			class ThreadpoolManager abstract : public virtual MultiThreadObject
			{
				friend class EventObject;
				template<typename EventTraits>
				friend class EventObjectT;

			public:
				ThreadpoolManager();
				~ThreadpoolManager();

				HRESULT AttachEventObject(_In_ EventObject* object);
				HRESULT DetachEventObject(_In_ EventObject* object, bool waitCallback);
				HRESULT SubmitWork(_In_ std::function<void()> const& work);

			protected:
				HRESULT RuntimeClassInitialize(UINT32 minimumThreadCount, UINT32 maximumThreadCount);
				void CloseAllObjects(bool wait);

				inline PTP_CALLBACK_ENVIRON GetEnvironment()
				{
					return &m_threadpoolEnvironment;
				}

			private:
				class WorkContext : public ThreadpoolObject
				{
				public:
					WorkContext(std::function<void()> const& work) :
						m_work(work)
					{
					}

					inline void Invoke()
					{
						m_work();
					}

				protected:
					virtual void OnGroupCancel() override
					{
						delete this;
					}

				private:
					const std::function<void()> m_work;
				};

				static void NTAPI WorkCallback(_Inout_ PTP_CALLBACK_INSTANCE instance, _Inout_opt_ PVOID context, _Inout_ PTP_WORK work);
				static void NTAPI GroupCancelCallback(_Inout_opt_ PVOID objectContext, _Inout_opt_ PVOID cleanupContext);

				TP_CALLBACK_ENVIRON m_threadpoolEnvironment;
				PTP_POOL m_threadpool;
				PTP_CLEANUP_GROUP m_threadpoolCleanupGroup;
			};

		}
	}
}