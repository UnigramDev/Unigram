#pragma once
#include <Windows.h>
#include <wrl\wrappers\corewrappers.h>

namespace Microsoft
{
	namespace WRL
	{
		namespace Wrappers
		{

			class Thread : public HandleT<HandleTraits::HANDLENullTraits>
			{
			public:
				explicit Thread() :
					HandleT()
				{
				}

				Thread(_Inout_ Thread&& h) throw() : HandleT(::Microsoft::WRL::Details::Move(h))
				{
				}

				Thread& operator=(_Inout_ Thread&& h) throw()
				{
					*static_cast<HandleT*>(this) = ::Microsoft::WRL::Details::Move(h);
					return *this;
				}

				bool Join(DWORD timeout = INFINITE)
				{
					return WaitForSingleObject(handle_, timeout) == WAIT_OBJECT_0;
				}

				bool Join(_Out_ LPDWORD exitCode, DWORD timeout = INFINITE)
				{
					if (WaitForSingleObject(handle_, timeout) != WAIT_OBJECT_0)
						return false;

					return GetExitCodeThread(handle_, exitCode) == TRUE;
				}

			private:
				void Close();
				HANDLE Detach();
				HANDLE* GetAddressOf();
				HANDLE* ReleaseAndGetAddressOf();
			};

		}
	}
}
