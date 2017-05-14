#pragma once
#include <string>
#include <vector>
#include <wrl.h>
#include "Telegram.Api.Native.h"

using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			class TLBinaryReader WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, ABI::Telegram::Api::Native::ITLBinaryReader>
			{
				InspectableClass(RuntimeClass_Telegram_Api_Native_TLBinaryReader, BaseTrust);

			public:
				TLBinaryReader();
				~TLBinaryReader();

				//COM exported methods
				STDMETHODIMP RuntimeClassInitialize(_In_ BYTE const* buffer, UINT32 length);
				STDMETHODIMP ReadByte(_Out_ BYTE* value);
				STDMETHODIMP ReadInt16(_Out_ INT16* value);
				STDMETHODIMP ReadUInt16(_Out_ UINT16* value);
				STDMETHODIMP ReadInt32(_Out_ INT32* value);
				STDMETHODIMP ReadUInt32(_Out_ UINT32* value);
				STDMETHODIMP ReadInt64(_Out_ INT64* value);
				STDMETHODIMP ReadUInt64(_Out_ UINT64* value);
				STDMETHODIMP ReadBool(_Out_ boolean* value);
				STDMETHODIMP ReadString(_Out_ HSTRING* value);
				STDMETHODIMP ReadByteArray(_Out_ UINT32* __valueSize, _Out_writes_(*__valueSize) BYTE** value);
				STDMETHODIMP ReadDouble(_Out_ double* value);
				STDMETHODIMP ReadFloat(_Out_ float* value);

				//Internal methods
				void Reset();
				void Skip(UINT32 length);
				HRESULT ReadBigEndianInt32(_Out_ INT32* value);
				HRESULT ReadString(_Out_ std::wstring& string);
				HRESULT ReadBuffer(_Out_writes_(length) BYTE* buffer, UINT32 length);

			private:
				HRESULT ReadBuffer(_Out_ BYTE const** padding, _Out_ UINT32* length);

				BYTE const* m_buffer;
				UINT32 m_position;
				UINT32 m_length;
			};

		}
	}
}