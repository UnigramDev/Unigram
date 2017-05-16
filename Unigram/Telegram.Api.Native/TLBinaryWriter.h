#pragma once
#include <string>
#include <vector>
#include <wrl.h>
#include "Telegram.Api.Native.h"

using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;
using ABI::Telegram::Api::Native::TL::ITLBinaryWriter;
using ABI::Telegram::Api::Native::TL::ITLObject;
using ABI::Telegram::Api::Native::TL::ITLBinarySizeCalculator;
using ABI::Telegram::Api::Native::TL::ITLBinarySizeCalculatorStatics;

namespace ABI
{
	namespace Telegram
	{
		namespace Api
		{
			namespace Native
			{
				namespace TL
				{

					MIDL_INTERFACE("8A2AC333-54FD-4AF4-B7F3-9A049A3E73E8") ITLBinaryWriterEx : public ITLBinaryWriter
					{
					public:
						virtual HRESULT STDMETHODCALLTYPE WriteWString(_In_ std::wstring string) = 0;
						virtual HRESULT STDMETHODCALLTYPE WriteBuffer(_In_reads_(length) BYTE const* buffer, UINT32 length) = 0;
						virtual void STDMETHODCALLTYPE Skip(UINT32 length) = 0;
						virtual void STDMETHODCALLTYPE Reset() = 0;
					};

				}
			}
		}
	}
}


using ABI::Telegram::Api::Native::TL::ITLBinaryWriterEx;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{
			namespace TL
			{

				class TLBinaryWriter WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, CloakedIid<ITLBinaryWriterEx>, ITLBinaryWriter>
				{
					InspectableClass(RuntimeClass_Telegram_Api_Native_TL_TLBinaryWriter, BaseTrust);

				public:
					TLBinaryWriter(_In_ BYTE* buffer, UINT32 length);
					~TLBinaryWriter();

					//COM exported methods
					IFACEMETHODIMP get_UnstoredBufferLength(_Out_ UINT32* value);
					IFACEMETHODIMP WriteByte(BYTE value);
					IFACEMETHODIMP WriteInt16(INT16 value);
					IFACEMETHODIMP WriteUInt16(UINT16 value);
					IFACEMETHODIMP WriteInt32(INT32 value);
					IFACEMETHODIMP WriteUInt32(UINT32 value);
					IFACEMETHODIMP WriteInt64(INT64 value);
					IFACEMETHODIMP WriteUInt64(UINT64 value);
					IFACEMETHODIMP WriteBool(boolean value);
					IFACEMETHODIMP WriteString(HSTRING value);
					IFACEMETHODIMP WriteByteArray(UINT32 __valueSize, _In_reads_(__valueSize) BYTE* value);
					IFACEMETHODIMP WriteDouble(double value);
					IFACEMETHODIMP WriteFloat(float value);
					IFACEMETHODIMP WriteWString(_In_ std::wstring string);
					IFACEMETHODIMP WriteBuffer(_In_reads_(length) BYTE const* buffer, UINT32 length);
					IFACEMETHODIMP_(void) Skip(UINT32 length);
					IFACEMETHODIMP_(void) Reset();

				private:
					HRESULT WriteString(_In_ LPCWCHAR buffer, UINT32 length);

					BYTE* m_buffer;
					UINT32 m_position;
					UINT32 m_length;
				};

				class TLBinarySizeCalculator : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, ITLBinarySizeCalculator, CloakedIid<ITLBinaryWriterEx>>
				{
					InspectableClass(RuntimeClass_Telegram_Api_Native_TL_TLBinaryWriter, BaseTrust);

				public:
					TLBinarySizeCalculator();
					~TLBinarySizeCalculator();

					//COM exported methods
					IFACEMETHODIMP get_TotalLength(_Out_  UINT32* value);
					IFACEMETHODIMP get_UnstoredBufferLength(_Out_ UINT32* value);
					IFACEMETHODIMP WriteByte(BYTE value);
					IFACEMETHODIMP WriteInt16(INT16 value);
					IFACEMETHODIMP WriteUInt16(UINT16 value);
					IFACEMETHODIMP WriteInt32(INT32 value);
					IFACEMETHODIMP WriteUInt32(UINT32 value);
					IFACEMETHODIMP WriteInt64(INT64 value);
					IFACEMETHODIMP WriteUInt64(UINT64 value);
					IFACEMETHODIMP WriteBool(boolean value);
					IFACEMETHODIMP WriteString(HSTRING value);
					IFACEMETHODIMP WriteByteArray(UINT32 __valueSize, _In_reads_(__valueSize) BYTE* value);
					IFACEMETHODIMP WriteDouble(double value);
					IFACEMETHODIMP WriteFloat(float value);
					IFACEMETHODIMP WriteWString(_In_ std::wstring string);
					IFACEMETHODIMP WriteBuffer(_In_reads_(length) BYTE const* buffer, UINT32 length);
					IFACEMETHODIMP_(void) Reset();
					IFACEMETHODIMP_(void) Skip(UINT32 length);

					static HRESULT GetTLObjectSize(_In_ ITLObject* object, _Out_ UINT32* value);

				private:
					UINT32 m_length;
				};

				class TLBinarySizeCalculatorStatics WrlSealed : public ActivationFactory<ITLBinarySizeCalculatorStatics>
				{
					friend class TLBinarySizeCalculator;

					InspectableClassStatic(RuntimeClass_Telegram_Api_Native_TL_TLBinarySizeCalculator, BaseTrust);

				public:
					TLBinarySizeCalculatorStatics();
					~TLBinarySizeCalculatorStatics();

					IFACEMETHODIMP GetTLObjectSize(_In_ ITLObject* object, _Out_ UINT32* value);

				private:
					static thread_local ComPtr<TLBinarySizeCalculator> s_instance;
				};

			}
		}
	}
}