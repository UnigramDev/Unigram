#pragma once
#include <wrl.h>
#include <functional>
#include "MultiThreadObject.h"
#include "LoggingProvider.h"

using namespace Microsoft::WRL;
using Telegram::Api::Native::Diagnostics::LoggingProvider;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{
			namespace Details
			{

				template<typename EventTraits, bool ownsHandle>
				class ThreadpoolObjectT;

			}

			
			class ThreadpoolManager abstract : public virtual MultiThreadObject, public virtual LoggingProvider
			{
				template<typename EventTraits, bool ownsHandle>
				friend class Details::ThreadpoolObjectT;
				friend class ThreadpoolWork;

			public:
				typedef std::function<HRESULT()> ThreadpoolWorkCallback;

				ThreadpoolManager();
				~ThreadpoolManager();

				HRESULT SubmitWork(_In_ ThreadpoolWorkCallback const& work);

			protected:
				HRESULT RuntimeClassInitialize(UINT32 minimumThreadCount, UINT32 maximumThreadCount);
				void CloseAllObjects(bool cancelPendingCallbacks);

				inline PTP_CALLBACK_ENVIRON GetEnvironment()
				{
					return &m_threadpoolEnvironment;
				}

			private:
				static void NTAPI WorkCallback(_Inout_ PTP_CALLBACK_INSTANCE instance, _Inout_opt_ PVOID context);
				static void NTAPI GroupCleanupCallback(_Inout_opt_ PVOID objectContext, _Inout_opt_ PVOID cleanupContext);

				TP_CALLBACK_ENVIRON m_threadpoolEnvironment;
				PTP_POOL m_threadpool;
				PTP_CLEANUP_GROUP m_threadpoolCleanupGroup;
			};

		}
	}
}