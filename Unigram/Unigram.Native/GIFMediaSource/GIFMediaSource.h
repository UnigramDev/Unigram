#pragma once
#include <dxgi.h> 
#include <dxgi1_2.h> 
#include <d2d1_1.h> 
#include <d2d1_2.h>
#include <d3d11.h> 
#include <d3d11_2.h> 
#include <dwrite.h>
#include <windows.media.core.h>
#include <windows.media.h>
#include <wrl.h>
#include <wrl\wrappers\corewrappers.h>
#include <Wincodec.h>
#include "Helpers\MediaFoundationHelper.h"

using namespace Microsoft::WRL;

namespace Unigram
{
	namespace Native
	{

		class GIFMediaSource WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>,
			ABI::Windows::Media::Core::IMediaSource, IMFMediaSource, IMFMediaSourceEx, IMFMediaEventGenerator, IMFGetService>
		{
			friend class GIFMediaStream;

			InspectableClass(L"Unigram.Native.GIFMediaSource", TrustLevel::BaseTrust);

		public:
			GIFMediaSource();
			~GIFMediaSource();

			STDMETHODIMP RuntimeClassInitialize(_In_ IMFByteStream* byteStream);
			STDMETHODIMP BeginGetEvent(IMFAsyncCallback* pCallback, IUnknown* punkState);
			STDMETHODIMP EndGetEvent(IMFAsyncResult* pResult, IMFMediaEvent** ppEvent);
			STDMETHODIMP GetEvent(DWORD dwFlags, IMFMediaEvent** ppEvent);
			STDMETHODIMP QueueEvent(MediaEventType type, REFGUID guid, HRESULT status, PROPVARIANT const* val);
			STDMETHODIMP CreatePresentationDescriptor(IMFPresentationDescriptor** ppPresentationDescriptor);
			STDMETHODIMP GetCharacteristics(DWORD* pdwCharacteristics);
			STDMETHODIMP Pause();
			STDMETHODIMP Shutdown();
			STDMETHODIMP Start(IMFPresentationDescriptor* pPresentationDescriptor, GUID const* pguidTimeFormat, PROPVARIANT const* pvarStartPosition);
			STDMETHODIMP Stop();
			STDMETHODIMP GetService(REFGUID guidService, REFIID riid, LPVOID* ppvObject);
			STDMETHODIMP GetSourceAttributes(IMFAttributes** ppAttributes);
			STDMETHODIMP GetStreamAttributes(DWORD dwStreamIdentifier, IMFAttributes** ppAttributes);
			STDMETHODIMP SetD3DManager(IUnknown* pManager);

		private:
			enum class MediaSourceState
			{
				Shutdown = -1,
				None,
				Stopped,
				Paused,
				Started
			};

			HRESULT NotifyEndOfPresentation();

			MediaSourceState m_state;
			CriticalSection m_criticalSection;
			ComPtr<IMFPresentationDescriptor> m_presentationDescriptor;
			ComPtr<IMFMediaEventQueue> m_events;
			ComPtr<GIFMediaStream> m_mediaStream;
		};

		class GIFMediaStream WrlSealed : public RuntimeClass<RuntimeClassFlags<ClassicCom>, IMFMediaStream, IMFMediaEventGenerator>
		{
			friend class GIFMediaSource;

		public:
			GIFMediaStream();

			STDMETHODIMP RuntimeClassInitialize(_In_ GIFMediaSource* mediaSource, _In_ IMFByteStream* byteStream);
			STDMETHODIMP BeginGetEvent(IMFAsyncCallback* pCallback, IUnknown* punkState);
			STDMETHODIMP EndGetEvent(IMFAsyncResult* pResult, IMFMediaEvent** ppEvent);
			STDMETHODIMP GetEvent(DWORD dwFlags, IMFMediaEvent** ppEvent);
			STDMETHODIMP QueueEvent(MediaEventType met, REFGUID guidExtendedType, HRESULT hrStatus, PROPVARIANT const* pvValue);
			STDMETHODIMP GetMediaSource(IMFMediaSource** ppMediaSource);
			STDMETHODIMP GetStreamDescriptor(IMFStreamDescriptor** ppStreamDescriptor);
			STDMETHODIMP RequestSample(IUnknown* pToken);

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

			HRESULT Start(_In_ PROPVARIANT const* variant);
			HRESULT Pause();
			HRESULT Stop();
			HRESULT SetD3DManager(_In_ IMFDXGIDeviceManager* deviceManager);
			HRESULT Shutdown();
			HRESULT GetFrameDefinition(_In_ IWICBitmapFrameDecode* frame, _Out_ GIFFrameDefinition* frameDefinition);
			HRESULT CreateDeviceResources();
			HRESULT DrawFrame(_In_ GIFFrameDefinition const* frameDefinition, _Out_ IMFSample** ppSample);
			HRESULT InitializeMetadataResources(_In_ IWICMetadataQueryReader* metadataReader, _In_ IMFMediaType* mediaType);

			static HRESULT CreatePresentationDescriptor(_In_ IWICBitmapDecoder* wicGIFDecoder, _Out_ IMFMediaType** ppMediaType,
				_Out_ IMFPresentationDescriptor** ppPresentationDescriptor, _Out_ IMFStreamDescriptor** ppStreamDescriptor);
			static HRESULT GetBitmapPaletteColors(_In_ IWICImagingFactory* wicFactory, _In_ IUnknown* wicPaletteSource, _Out_ std::vector<WICColor>& colors);
			static HRESULT CreateFrameSample(_In_ ID2D1Bitmap1* frameBufferBitmap, D2D_SIZE_U frameSize, _Out_ IMFSample** ppSample);

			CriticalSection m_criticalSection;
			bool m_isShutdown;
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
			ComPtr<IMFMediaEventQueue> m_events;
			ComPtr<IMFStreamDescriptor> m_streamDescriptor;
			ComPtr<GIFMediaSource> m_mediaSource;
		};

	}
}