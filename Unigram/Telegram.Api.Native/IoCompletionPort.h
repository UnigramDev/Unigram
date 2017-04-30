#pragma once
#include <Windows.h>
#include <wrl\wrappers\corewrappers.h>

namespace Microsoft
{
	namespace WRL
	{
		namespace Wrappers
		{

			class IoCompletionPort : public HandleT<HandleTraits::EventTraits>
			{
			public:
				explicit IoCompletionPort() :
					HandleT()
				{
				}

				IoCompletionPort(_Inout_ IoCompletionPort&& h) throw() : HandleT(::Microsoft::WRL::Details::Move(h))
				{
				}

				IoCompletionPort& operator=(_Inout_ IoCompletionPort&& h) throw()
				{
					*static_cast<HandleT*>(this) = ::Microsoft::WRL::Details::Move(h);
					return *this;
				}

				bool AttachToHandle(typename HandleTraits::EventTraits::Type handle, _In_ ULONG_PTR completionKey)
				{
					return CreateIoCompletionPort(handle, handle_, completionKey, 0) == handle_;
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
