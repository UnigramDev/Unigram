// Copyright (c) 2017 Lorenzo Rossoni

#pragma once
#include "ByteStreamHandler.h"

namespace Unigram
{
	namespace Native
	{

		class GIFByteStreamHandler WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, ByteStreamHandler>
		{
			InspectableClass(L"Unigram.Native.GIFByteStreamHandler", TrustLevel::BaseTrust);

		protected:
			virtual QWORD GetMaxNumberOfBytesRequiredForResolution() noexcept override;
			virtual HRESULT ValidateURL(_In_ LPCWSTR url) override;
			virtual HRESULT ValidateByteStream(_In_ IMFByteStream* byteStream) override;
			virtual HRESULT CreateMediaSource(_In_  IMFByteStream* byteStream, _In_ IPropertyStore* properties,
				_Out_ IMFMediaSource** mediaSource) override;
		};

	}
}