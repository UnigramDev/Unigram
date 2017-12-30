#pragma once
#include <DbgHelp.h>
#include <string>
#include <wrl.h>
#include "DebugExtensions.h"
#include "Helpers\DebugHelper.h"
#include "Helpers\LibraryHelper.h"
#include "Helpers\StringHelper.h"

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
					Test();

					auto deadlockGuard = Make<CriticalSectionGuard>(L"", this);

					::EnterCriticalSection(&m_criticalSection);

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
						SetEvent(m_waitEvent.Get());
						WaitForSingleObject(m_waitThread.Get(), INFINITE);
					}

				private:
					static DWORD WINAPI WaitThreadWork(_In_opt_ LPVOID lpArgToCompletionRoutine)
					{
						ComPtr<CriticalSectionGuard> context(reinterpret_cast<CriticalSectionGuard*>(lpArgToCompletionRoutine));
						if (WaitForSingleObject(context->m_waitEvent.Get(), 10000) != WAIT_OBJECT_0)
						{
							return 1;
						}

						//::LeaveCriticalSection(const_cast<LPCRITICAL_SECTION>(&context->m_criticalSection->m_criticalSection));

						OutputDebugStringFormat(L"CriticalSection timeout at %s\n", context->m_methodName.data());
						return 0;
					}

					const Event m_waitEvent;
					const HandleT<HandleTraits::HANDLENullTraits> m_waitThread;
					const std::wstring m_methodName;
					const DebugCriticalSection* m_criticalSection;
				};

				static void Test()
				{
					PVOID stackFrames[100];
					USHORT frameCount = CaptureStackBackTrace(1, ARRAYSIZE(stackFrames), stackFrames, nullptr);

					HANDLE hProcess = GetCurrentProcess();
					HANDLE hThread = GetCurrentThread();

					const size_t symbolSize = 4096;
					/*auto imageHelpSymbol = reinterpret_cast<PSYMBOL_INFO>(GlobalAlloc(GMEM_FIXED, symbolSize));
					imageHelpSymbol->SizeOfStruct = symbolSize;
					imageHelpSymbol->MaxNameLen = symbolSize - sizeof(SYMBOL_INFO) - 1;*/

					auto imageHelpSymbol = reinterpret_cast<PIMAGEHLP_SYMBOL64>(GlobalAlloc(GMEM_FIXED, symbolSize));
					imageHelpSymbol->SizeOfStruct = symbolSize;
					imageHelpSymbol->MaxNameLength = symbolSize - sizeof(PIMAGEHLP_SYMBOL64) - 1;

					auto hDbghelp = GetModuleHandle(L"dbghelp.dll");
					auto pSymFunctionTableAccess64 = reinterpret_cast<PFUNCTION_TABLE_ACCESS_ROUTINE64>(GetProcAddress(hDbghelp, "SymFunctionTableAccess64"));
					auto pSymGetModuleBase64 = reinterpret_cast<PGET_MODULE_BASE_ROUTINE64>(GetProcAddress(hDbghelp, "SymGetModuleBase64"));

					for (USHORT i = 0; i < frameCount; i++)
					{
						DWORD64 displacement = 0;
						if (SymGetSymFromAddr64(hProcess, reinterpret_cast<DWORD64>(stackFrames[i]), &displacement, imageHelpSymbol))
						{
							std::wstring symbolName;
							MultiByteToWideChar(imageHelpSymbol->Name, static_cast<UINT32>(strlen(imageHelpSymbol->Name)), symbolName);

							OutputDebugStringFormat(L"%s at line %llu\n", symbolName.data(), displacement);
						}
						else
						{
							auto error = GetLastError();

							WCHAR* text;
							if ((FormatMessage(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS, nullptr,
								error, MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT), reinterpret_cast<LPWSTR>(&text), 0, nullptr)) > 0)
							{
								OutputDebugStringFormat(L"SymFromAddr returned error: 0x%08x\n", error);
							}
							else
							{
								OutputDebugStringFormat(L"SymFromAddr returned error: %s (0x%08x)", text, error);
								LocalFree(text);
							}
						}
					}

					GlobalFree(imageHelpSymbol);
				}

				CRITICAL_SECTION m_criticalSection;
			};

		}
	}
}