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

			class TLBinaryWriter WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, ITLBinaryWriter>
			{
				InspectableClass(RuntimeClass_Telegram_Api_Native_TLBinaryWriter, BaseTrust);

			public:
				TLBinaryWriter();
				~TLBinaryWriter();

				STDMETHODIMP RuntimeClassInitialize();
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

			private:
			};

		}
	}
}