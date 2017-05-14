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

			class TLBinaryWriter WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, ABI::Telegram::Api::Native::ITLBinaryWriter>
			{
				InspectableClass(RuntimeClass_Telegram_Api_Native_TLBinaryWriter, BaseTrust);

			public:
				TLBinaryWriter();
				~TLBinaryWriter();

				//COM exported methods
				STDMETHODIMP RuntimeClassInitialize(_In_ BYTE* buffer, UINT32 length);
				STDMETHODIMP WriteByte(BYTE value);
				STDMETHODIMP WriteInt16(INT16 value);
				STDMETHODIMP WriteUInt16(UINT16 value);
				STDMETHODIMP WriteInt32(INT32 value);
				STDMETHODIMP WriteUInt32(UINT32 value);
				STDMETHODIMP WriteInt64(INT64 value);
				STDMETHODIMP WriteUInt64(UINT64 value);
				STDMETHODIMP WriteBool(boolean value);
				STDMETHODIMP WriteString(HSTRING value);
				STDMETHODIMP WriteByteArray(UINT32 __valueSize, _In_reads_(__valueSize) BYTE* value);
				STDMETHODIMP WriteDouble(double value);
				STDMETHODIMP WriteFloat(float value);

				//Internal methods
				void Reset();
				void Skip(UINT32 length);
				HRESULT WriteString(_In_ std::wstring string);
				HRESULT WriteBuffer(_In_reads_(length) BYTE const* buffer, UINT32 length);

			private:
				HRESULT WriteString(_In_ LPCWCHAR buffer, UINT32 length);

				BYTE* m_buffer;
				UINT32 m_position;
				UINT32 m_length;
			};

		}
	}
}