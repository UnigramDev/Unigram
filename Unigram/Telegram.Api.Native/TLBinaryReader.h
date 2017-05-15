#pragma once
#include <string>
#include <vector>
#include <wrl.h>
#include "Telegram.Api.Native.h"

using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;
using ABI::Telegram::Api::Native::ITLBinaryReader;

namespace ABI
{
	namespace Telegram
	{
		namespace Api
		{
			namespace Native
			{

				MIDL_INTERFACE("AAFAB0A9-17F1-42D0-87DF-E188B2BE5BC7") ITLBinaryReaderEx : public ITLBinaryReader
				{
				public:
					virtual HRESULT STDMETHODCALLTYPE ReadBigEndianInt32(_Out_ INT32* value) = 0;
					virtual HRESULT STDMETHODCALLTYPE ReadString(_Out_ std::wstring& string) = 0;
					virtual HRESULT STDMETHODCALLTYPE ReadBuffer(_Out_writes_(length) BYTE* buffer, UINT32 length) = 0;
					virtual void Skip(UINT32 length) = 0;
					virtual void Reset() = 0;
				};

			}
		}
	}
}


using ABI::Telegram::Api::Native::ITLBinaryReaderEx;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			class TLBinaryReader WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, CloakedIid<ITLBinaryReaderEx>, ITLBinaryReader>
			{
				InspectableClass(RuntimeClass_Telegram_Api_Native_TLBinaryReader, BaseTrust);

			public:
				TLBinaryReader(_In_ BYTE const* buffer, UINT32 length);
				~TLBinaryReader();

				//COM exported methods
				IFACEMETHODIMP get_UnconsumedBufferLength(_Out_ UINT32* value);
				IFACEMETHODIMP ReadByte(_Out_ BYTE* value);
				IFACEMETHODIMP ReadInt16(_Out_ INT16* value);
				IFACEMETHODIMP ReadUInt16(_Out_ UINT16* value);
				IFACEMETHODIMP ReadInt32(_Out_ INT32* value);
				IFACEMETHODIMP ReadUInt32(_Out_ UINT32* value);
				IFACEMETHODIMP ReadInt64(_Out_ INT64* value);
				IFACEMETHODIMP ReadUInt64(_Out_ UINT64* value);
				IFACEMETHODIMP ReadBool(_Out_ boolean* value);
				IFACEMETHODIMP ReadString(_Out_ HSTRING* value);
				IFACEMETHODIMP ReadByteArray(_Out_ UINT32* __valueSize, _Out_writes_(*__valueSize) BYTE** value);
				IFACEMETHODIMP ReadDouble(_Out_ double* value);
				IFACEMETHODIMP ReadFloat(_Out_ float* value);
				IFACEMETHODIMP ReadBigEndianInt32(_Out_ INT32* value);
				IFACEMETHODIMP ReadString(_Out_ std::wstring& string);
				IFACEMETHODIMP ReadBuffer(_Out_writes_(length) BYTE* buffer, UINT32 length);
				IFACEMETHODIMP_(void) Skip(UINT32 length);
				IFACEMETHODIMP_(void) Reset();

			private:
				HRESULT ReadBuffer(_Out_ BYTE const** buffer, _Out_ UINT32* length);

				BYTE const* m_buffer;
				UINT32 m_position;
				UINT32 m_length;
			};

		}
	}
}