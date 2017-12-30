// Copyright (c) 2017 Lorenzo Rossoni

#pragma once
#include <dxgi.h> 
#include <dxgi1_2.h> 
#include <d2d1_1.h> 
#include <d2d1_2.h>
#include <d3d11.h> 
#include <d3d11_2.h> 
#include <dwrite.h>
#include <Wincodec.h>
#include "MediaSource.h"

namespace Unigram
{
	namespace Native
	{

		class GIFMediaSource WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, MediaSource>
		{
			friend class GIFMediaStream;

			InspectableClass(L"Unigram.Native.GIFMediaSource", TrustLevel::BaseTrust);

		public:
			STDMETHODIMP RuntimeClassInitialize(IMFByteStream* byteStream);
			IFACEMETHODIMP SetD3DManager(IUnknown* pManager);

		protected:
			virtual DWORD GetCharacteristics() noexcept override;
			virtual DWORD GetMediaStreamCount() noexcept override;
			virtual MediaStream* GetMediaStreamByIndex(DWORD streamIndex) noexcept override;
			virtual MediaStream* GetMediaStreamById(DWORD streamId) noexcept override;
			virtual HRESULT OnStart(MFTIME position) override;
			virtual HRESULT OnSeek(MFTIME position) override;
			virtual HRESULT OnPause() override;
			virtual HRESULT OnStop() override;
			virtual HRESULT OnShutdown() override;

		private:
			ComPtr<GIFMediaStream> m_mediaStream;
		};

		class GIFMediaStream WrlSealed : public RuntimeClass<RuntimeClassFlags<ClassicCom>, MediaStream>
		{
			friend class GIFMediaSource;

		public:
			GIFMediaStream();

			STDMETHODIMP RuntimeClassInitialize(_In_ GIFMediaSource* mediaSource, _In_ IMFByteStream* byteStream,
				_Out_ IMFPresentationDescriptor** ppPresentationDescriptor);

		protected:
			virtual bool IsEndOfStream() noexcept override;
			virtual HRESULT OnSampleRequested(_In_ IUnknown* pToken) override;
			virtual HRESULT OnStart(MFTIME position) override;
			virtual HRESULT OnSeek(MFTIME position) override;
			virtual HRESULT OnPause() override;
			virtual HRESULT OnStop() override;
			virtual HRESULT OnShutdown() override;

		private:
			enum class GIFFrameDisposalMethod : byte
			{
				None = 0,
				DontDispose = 1,
				RestoreBackgroundColor = 2,
				RestorePrevious = 3
			};

			struct GIFFrameDefinition
			{
				GIFFrameDefinition(D2D_SIZE_U size) :
					DisposalMethod(GIFFrameDisposalMethod::None),
					Delay(1000000),
					Bounds({ 0.0f, 0.0f, static_cast<float>(size.width),  static_cast<float>(size.height) })
				{
				}

				GIFFrameDisposalMethod DisposalMethod;
				DWORD Delay;
				D2D1_RECT_F Bounds;
			};

			HRESULT SetD3DManager(_In_ IMFDXGIDeviceManager* deviceManager);
			HRESULT GetFrameDefinition(_In_ IWICBitmapFrameDecode* frame, _Out_ GIFFrameDefinition* frameDefinition);
			HRESULT CreateDeviceResources();
			HRESULT DrawFrame(_In_ GIFFrameDefinition const* frameDefinition, _Out_ IMFSample** ppSample);
			HRESULT InitializeMetadataResources(_In_ IWICMetadataQueryReader* metadataReader, _In_ IMFMediaType* mediaType,
				_In_ IMFPresentationDescriptor* presentationDescriptor);

			static HRESULT CreatePresentationDescriptor(_In_ IWICBitmapDecoder* wicGIFDecoder, _Out_ IMFMediaType** ppMediaType,
				_Out_ IMFPresentationDescriptor** ppPresentationDescriptor, _Out_ IMFStreamDescriptor** ppStreamDescriptor);
			static HRESULT GetBitmapPaletteColors(_In_ IWICImagingFactory* wicFactory, _In_ IUnknown* wicPaletteSource, _Out_ std::vector<WICColor>& colors);
			static HRESULT CreateFrameSample(_In_ ID2D1Bitmap1* frameBufferBitmap, D2D_SIZE_U frameSize, _Out_ IMFSample** ppSample);

			DWORD m_frameIndex;
			LONGLONG m_frameTime;
			D2D1_SIZE_U m_frameSize;
			D2D1_COLOR_F m_backgroundColor;
			ComPtr<IMFDXGIDeviceManager> m_dxgiDeviceManager;
			ComPtr<ID2D1Device> m_d2dDevice;
			ComPtr<ID2D1DeviceContext> m_d2dDeviceContext;
			ComPtr<IWICImagingFactory> m_wicFactory;
			ComPtr<ID2D1Bitmap1> m_frameTargetBitmap;
			ComPtr<ID2D1Bitmap1> m_frameBufferBitmap;
			std::vector<GIFFrameDefinition> m_frameDefinitions;
			ComPtr<IWICBitmapDecoder> m_wicGIFDecoder;
		};

	}
}