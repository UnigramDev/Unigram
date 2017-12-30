#pragma once
#include "ThreadpoolManager.h"
#include "MultithreadObject.h"
#include "Helpers\COMHelper.h"

#define THREADPOOL_TIMER_WINDOW 0

using namespace Microsoft::WRL;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			inline void TimeoutToFileTime(INT64 timeoutMs, _Out_ FILETIME& filetime)
			{
				ULARGE_INTEGER timeout;
				timeout.QuadPart = timeoutMs * 10000LL;

				filetime.dwHighDateTime = timeout.HighPart;
				filetime.dwLowDateTime = timeout.LowPart;
			}


			namespace ThreadpoolTraits
			{

				struct TimerTraits;
				struct WaitTraits;
				struct WorkTraits;

			}


			class ThreadpoolObject abstract
			{
				friend class ThreadpoolManager;
				friend struct ThreadpoolTraits::TimerTraits;
				friend struct ThreadpoolTraits::WaitTraits;
				friend struct ThreadpoolTraits::WorkTraits;

			public:
				virtual HRESULT AttachToThreadpool(_In_ ThreadpoolManager* threadpoolManager) = 0;

			protected:
				virtual HRESULT OnCallback(_Inout_ PTP_CALLBACK_INSTANCE instance, _In_ ULONG_PTR parameter) = 0;
				virtual HRESULT OnGroupCleanupCallback() = 0;
			};


			namespace ThreadpoolTraits
			{

				struct TimerTraits
				{
				public:
					typedef PTP_TIMER Handle;

					inline static Handle Create(_In_ ThreadpoolObject* eventObject, _In_ PTP_CALLBACK_ENVIRON threadpoolEnvironment) throw()
					{
						return ::CreateThreadpoolTimer(TimerTraits::Callback, eventObject, threadpoolEnvironment);
					}

					inline static void Wait(_In_ Handle timer, BOOL cancelPendingCallbacks) throw()
					{
						::WaitForThreadpoolTimerCallbacks(timer, cancelPendingCallbacks);
					}

					inline static void Close(_In_ Handle timer) throw()
					{
						::CloseThreadpoolTimer(timer);
					}

					inline static void Cancel(_In_ Handle timer) throw()
					{
						::SetThreadpoolTimer(timer, nullptr, 0, 0);
					}

				private:
					static void NTAPI Callback(_Inout_ PTP_CALLBACK_INSTANCE instance, _Inout_opt_ PVOID context, _Inout_ Handle work)
					{
						reinterpret_cast<ThreadpoolObject*>(context)->OnCallback(instance, NULL);
					}
				};

				struct WaitTraits
				{
				public:
					typedef PTP_WAIT Handle;

					inline static Handle Create(_In_ ThreadpoolObject* eventObject, _In_ PTP_CALLBACK_ENVIRON threadpoolEnvironment) throw()
					{
						return ::CreateThreadpoolWait(WaitTraits::Callback, eventObject, threadpoolEnvironment);
					}

					inline static void Wait(_In_ Handle wait, BOOL cancelPendingCallbacks) throw()
					{
						::WaitForThreadpoolWaitCallbacks(wait, cancelPendingCallbacks);
					}

					inline static void Close(_In_ Handle wait) throw()
					{
						::CloseThreadpoolWait(wait);
					}

					inline static void Cancel(_In_ Handle wait) throw()
					{
						::SetThreadpoolWait(wait, nullptr, nullptr);
					}

				private:
					static void NTAPI Callback(_Inout_ PTP_CALLBACK_INSTANCE instance, _Inout_opt_ PVOID context, _Inout_ Handle wait, _In_ TP_WAIT_RESULT waitResult)
					{
						reinterpret_cast<ThreadpoolObject*>(context)->OnCallback(instance, waitResult);
					}
				};

				struct WorkTraits
				{
				public:
					typedef PTP_WORK Handle;

					inline static Handle Create(_In_ ThreadpoolObject* eventObject, _In_ PTP_CALLBACK_ENVIRON threadpoolEnvironment) throw()
					{
						return ::CreateThreadpoolWork(WorkTraits::Callback, eventObject, threadpoolEnvironment);
					}

					inline static void Wait(_In_ Handle work, BOOL cancelPendingCallbacks) throw()
					{
						::WaitForThreadpoolWorkCallbacks(work, cancelPendingCallbacks);
					}

					inline static void Close(_In_ Handle work) throw()
					{
						::CloseThreadpoolWork(work);
					}

					inline static void Cancel(_In_ Handle work) throw()
					{
						UNREFERENCED_PARAMETER(work);
					}

				private:
					static void NTAPI Callback(_Inout_ PTP_CALLBACK_INSTANCE instance, _Inout_opt_ PVOID context, _Inout_ Handle work)
					{
						reinterpret_cast<ThreadpoolObject*>(context)->OnCallback(instance, NULL);
					}
				};

			}


			namespace Details
			{

				template<typename ThreadpoolTraits, bool ownsHandle>
				class __declspec(novtable) ThreadpoolObjectT abstract : public ThreadpoolObject
				{
				};

				template<typename ThreadpoolTraits>
				class __declspec(novtable) ThreadpoolObjectT<ThreadpoolTraits, true> abstract : public ThreadpoolObject, public virtual MultiThreadObject
				{
				public:
					ThreadpoolObjectT() :
						m_handle(nullptr)
					{
					}

					~ThreadpoolObjectT()
					{
						if (m_handle != nullptr)
						{
							ThreadpoolTraits::Wait(m_handle, true);
							ThreadpoolTraits::Close(m_handle);
						}
					}

					virtual HRESULT AttachToThreadpool(_In_ ThreadpoolManager* threadpoolManager) override final
					{
						if (threadpoolManager == nullptr)
						{
							return E_INVALIDARG;
						}

						auto lock = LockCriticalSection();

						if (m_handle != nullptr)
						{
							return E_NOT_VALID_STATE;
						}

						if ((m_handle = ThreadpoolTraits::Create(this, threadpoolManager->GetEnvironment())) == nullptr)
						{
							return GetLastHRESULT();
						}

						return S_OK;
					}

					inline bool IsAttached()
					{
						auto lock = LockCriticalSection();
						return m_handle != nullptr;
					}

					inline HRESULT DetachFromThreadpool()
					{
						auto lock = LockCriticalSection();

						if (m_handle == nullptr)
						{
							return E_NOT_VALID_STATE;
						}

						ThreadpoolTraits::Close(m_handle);

						m_handle = nullptr;
						return S_OK;
					}

					inline HRESULT Cancel()
					{
						auto lock = LockCriticalSection();

						if (m_handle == nullptr)
						{
							return E_NOT_VALID_STATE;
						}

						ThreadpoolTraits::Cancel(m_handle);
						return S_OK;
					}

					inline HRESULT WaitForCallback(bool cancelPending)
					{
						HandleType handle;

						{
							auto lock = LockCriticalSection();

							if (m_handle == nullptr)
							{
								return E_NOT_VALID_STATE;
							}

							handle = m_handle;
						}

						ThreadpoolTraits::Wait(handle, cancelPending);
						return S_OK;
					}

				protected:
					typedef typename ThreadpoolTraits::Handle HandleType;

					inline HandleType GetHandle() const
					{
						return m_handle;
					}

				private:
					virtual HRESULT OnGroupCleanupCallback() override final
					{
						auto lock = LockCriticalSection();

						m_handle = nullptr;
						return S_OK;
					}

					HandleType m_handle;
				};

			}


			class ThreadpoolScheduledWork : public Details::ThreadpoolObjectT<ThreadpoolTraits::TimerTraits, true>
			{
			public:
				ThreadpoolScheduledWork(_In_ ThreadpoolManager::ThreadpoolWorkCallback const& workCallback) :
					m_workCallback(workCallback)
				{
				}

				~ThreadpoolScheduledWork()
				{
				}

				inline bool IsScheduled()
				{
					auto lock = LockCriticalSection();
					auto handle = ThreadpoolObjectT::GetHandle();
					return handle != nullptr && IsThreadpoolTimerSet(handle);
				}

				inline HRESULT Schedule(INT64 timeoutMs)
				{
					auto lock = LockCriticalSection();

					auto handle = ThreadpoolObjectT::GetHandle();
					if (handle == nullptr)
					{
						return E_NOT_VALID_STATE;
					}

					FILETIME timeout;
					TimeoutToFileTime(timeoutMs, timeout);
					SetThreadpoolTimer(handle, &timeout, 0, THREADPOOL_TIMER_WINDOW);
					return S_OK;
				}

				inline HRESULT TrySchedule(INT64 timeoutMs)
				{
					auto lock = LockCriticalSection();

					auto handle = ThreadpoolObjectT::GetHandle();
					if (handle == nullptr)
					{
						return E_NOT_VALID_STATE;
					}

					if (IsThreadpoolTimerSet(handle))
					{
						return S_FALSE;
					}

					FILETIME timeout;
					TimeoutToFileTime(timeoutMs, timeout);
					SetThreadpoolTimer(handle, &timeout, 0, THREADPOOL_TIMER_WINDOW);
					return S_OK;
				}

				inline HRESULT ExecuteNow()
				{
					auto lock = LockCriticalSection();

					auto handle = ThreadpoolObjectT::GetHandle();
					if (handle == nullptr)
					{
						return E_NOT_VALID_STATE;
					}

					FILETIME timeout = {};
					SetThreadpoolTimer(handle, &timeout, 0, THREADPOOL_TIMER_WINDOW);
					return S_OK;
				}

			private:
				virtual HRESULT OnCallback(_Inout_ PTP_CALLBACK_INSTANCE instance, _In_ ULONG_PTR parameter) override final
				{
					/*{
						auto lock = LockCriticalSection();

						SetThreadpoolTimer(ThreadpoolObjectT::GetHandle(), nullptr, 0, 0);
					}*/

					SetThreadpoolTimer(ThreadpoolObjectT::GetHandle(), nullptr, 0, 0);
					return m_workCallback();
				}

				const ThreadpoolManager::ThreadpoolWorkCallback m_workCallback;
			};

			class ThreadpoolPeriodicWork : public Details::ThreadpoolObjectT<ThreadpoolTraits::TimerTraits, true>
			{
			public:
				ThreadpoolPeriodicWork(_In_ ThreadpoolManager::ThreadpoolWorkCallback const& workCallback) :
					m_workCallback(workCallback)
				{
				}

				~ThreadpoolPeriodicWork()
				{
				}

				inline HRESULT SetPeriod(UINT32 periodMs)
				{
					auto lock = LockCriticalSection();

					auto handle = ThreadpoolObjectT::GetHandle();
					if (handle == nullptr)
					{
						return E_NOT_VALID_STATE;
					}

					FILETIME timeout;
					TimeoutToFileTime(-static_cast<INT64>(periodMs), timeout);
					SetThreadpoolTimer(handle, &timeout, periodMs, THREADPOOL_TIMER_WINDOW);
					return S_OK;
				}

			private:
				virtual HRESULT OnCallback(_Inout_ PTP_CALLBACK_INSTANCE instance, _In_ ULONG_PTR parameter) override final
				{
					return m_workCallback();
				}

				const ThreadpoolManager::ThreadpoolWorkCallback m_workCallback;
			};

			class ThreadpoolWork : public Details::ThreadpoolObjectT<ThreadpoolTraits::WorkTraits, false>
			{
				friend class ThreadpoolManager;

			public:
				~ThreadpoolWork()
				{
				}

			private:
				ThreadpoolWork(_In_ ThreadpoolManager::ThreadpoolWorkCallback const& workCallback) :
					m_workCallback(workCallback)
				{
				}

				virtual HRESULT AttachToThreadpool(_In_ ThreadpoolManager* threadpoolManager) override final
				{
					if (threadpoolManager == nullptr)
					{
						return E_INVALIDARG;
					}

					auto workHandle = ThreadpoolTraits::WorkTraits::Create(this, threadpoolManager->GetEnvironment());
					if (workHandle == nullptr)
					{
						return GetLastHRESULT();
					}

					SubmitThreadpoolWork(workHandle);
					CloseThreadpoolWork(workHandle);
					return S_OK;
				}

				virtual HRESULT OnCallback(_Inout_ PTP_CALLBACK_INSTANCE instance, _In_ ULONG_PTR parameter) override final
				{
					HRESULT result = m_workCallback();

					delete this;
					return result;
				}

				virtual HRESULT OnGroupCleanupCallback() override final
				{
					delete this;
					return S_OK;
				}

				inline HRESULT Execute()
				{
					return m_workCallback();
				}

				const ThreadpoolManager::ThreadpoolWorkCallback m_workCallback;
			};

		}
	}
}