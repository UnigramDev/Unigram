#pragma once
#include <string>
#include <wrl\wrappers\corewrappers.h>
#include "Helpers\DebugHelper.h"

namespace Microsoft
{
	namespace WRL
	{
		namespace Wrappers
		{

			class DebugCriticalSection;


			namespace Details
			{
				class DebugCriticalSectionLock
				{
					friend class DebugCriticalSection;

				public:
					DebugCriticalSectionLock(_Inout_ DebugCriticalSectionLock&& other) throw() :
						m_criticalSection(other.m_criticalSection)
					{
						other.m_criticalSection = nullptr;
					}

					DebugCriticalSectionLock(const DebugCriticalSectionLock&) = delete;
					DebugCriticalSectionLock& operator=(const DebugCriticalSectionLock&) = delete;

					_Releases_lock_(*m_criticalSection) ~DebugCriticalSectionLock() throw()
					{
						InternalUnlock();
					}

					_Releases_lock_(*m_criticalSection) void Unlock() throw()
					{
						InternalUnlock();
					}

					bool IsLocked() const throw()
					{
						return m_criticalSection != nullptr;
					}

				private:
					explicit DebugCriticalSectionLock(CRITICAL_SECTION* criticalSection = nullptr) throw() :
						m_criticalSection(criticalSection)
					{
					}

					_Releases_lock_(*m_criticalSection) void InternalUnlock() throw()
					{
						if (IsLocked())
						{
							LeaveCriticalSection(m_criticalSection);

							m_criticalSection = nullptr;
						}
					}

					CRITICAL_SECTION* m_criticalSection;
				};

			}


			class DebugCriticalSection
			{
			public:
				typedef Details::DebugCriticalSectionLock SyncLock;

				explicit DebugCriticalSection(ULONG spincount = 0) throw()
				{
					::InitializeCriticalSectionEx(&m_criticalSection, spincount, 0);
				}

				DebugCriticalSection(const DebugCriticalSection&) = delete;
				DebugCriticalSection& operator=(const DebugCriticalSection&) = delete;

				~DebugCriticalSection() throw()
				{
					::DeleteCriticalSection(&m_criticalSection);
				}

				_Acquires_lock_(*return.m_criticalSection) _Post_same_lock_(*return.m_criticalSection, m_criticalSection)
					SyncLock Lock() throw()
				{
					auto deadlockGuard = Make<CriticalSectionGuard>(L"", this);

					::EnterCriticalSection(&m_criticalSection);

					deadlockGuard->Unlock();

					return SyncLock(&m_criticalSection);
				}

				_Acquires_lock_(*return.m_criticalSection) _Post_same_lock_(*return.m_criticalSection, m_criticalSection)
					SyncLock TryLock() throw()
				{
					bool acquired = !!::TryEnterCriticalSection(&m_criticalSection);

					_Analysis_assume_lock_held_(m_criticalSection);
					return SyncLock((acquired) ? &m_criticalSection : nullptr);
				}

				bool IsValid() const throw()
				{
					return true;
				}

			private:
				struct CriticalSectionGuard : public RuntimeClass<RuntimeClassFlags<ClassicCom>, IUnknown>
				{
					CriticalSectionGuard(const std::wstring methodName, const DebugCriticalSection* criticalSection) :
						m_methodName(methodName),
						m_criticalSection(criticalSection),
						m_waitEvent(CreateEvent(nullptr, TRUE, FALSE, nullptr)),
						m_waitThread(CreateThread(nullptr, 0, WaitThreadWork, this, 0, nullptr))
					{
					}

					~CriticalSectionGuard()
					{
					}

					void Unlock()
					{
						SetEvent(m_waitEvent.Get());
						WaitForSingleObject(m_waitThread.Get(), INFINITE);
					}

				private:
					static DWORD WINAPI WaitThreadWork(_In_opt_ LPVOID lpArgToCompletionRoutine)
					{
						ComPtr<CriticalSectionGuard> context(reinterpret_cast<CriticalSectionGuard*>(lpArgToCompletionRoutine));
						if (WaitForSingleObject(context->m_waitEvent.Get(), 10000) != WAIT_OBJECT_0)
						{
							__debugbreak();
							return 1;
						}

						return 0;
					}

					const Event m_waitEvent;
					const HandleT<HandleTraits::HANDLENullTraits> m_waitThread;
					const std::wstring m_methodName;
					const DebugCriticalSection* m_criticalSection;
				};

				CRITICAL_SECTION m_criticalSection;
			};

		}
	}
}