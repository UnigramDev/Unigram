#pragma once

#include "IVirtualImageSourceRenderer.h"

#pragma comment(lib, "d2d1.lib")
#pragma comment(lib, "d3d11.lib")
#pragma comment(lib, "dxgi.lib")
#pragma comment(lib, "dxguid.lib")

using namespace Platform;
using namespace Windows::Storage;
using namespace Windows::UI::Xaml::Media::Imaging;
using namespace Windows::Storage::Streams;
using namespace Windows::Foundation;
using namespace Microsoft::WRL;

namespace Unigram
{
	namespace Native 
	{
		[Windows::Foundation::Metadata::WebHostHidden]
		public ref class VideoImageSourceRenderer sealed : public IVirtualImageSourceRenderer
		{
		public:
			VideoImageSourceRenderer(int width, int height);

			void Initialize(String^ path);

			virtual event PropertyChangedEventHandler^ PropertyChanged;

			virtual property VirtualSurfaceImageSource^ ImageSource
			{
				VirtualSurfaceImageSource^ get();
			}

			virtual void NotifyUpdatesNeeded();
		private:
			~VideoImageSourceRenderer();


			void OnSuspending(_In_ Object^ sender, _In_ Windows::ApplicationModel::SuspendingEventArgs^ args);
			void CreateDeviceResources();
			void CreateDeviceIndependentResources();
			void Draw(_In_ RECT const& drawingBounds);
			void BeginDraw(_In_ RECT const& drawingBounds);
			void EndDraw();
			void Invalidate(Boolean resize);

			DispatcherTimer^ m_timer;

			VirtualSurfaceImageSource^ m_imageSource;
			ComPtr<VirtualImageSourceRendererCallback> m_updatesCallback;
			ComPtr<IVirtualSurfaceImageSourceNative> m_imageSourceNative;
			ComPtr<ID3D11Device> m_d3dDevice;
			ComPtr<ID2D1Device> m_d2dDevice;
			ComPtr<ID2D1DeviceContext> m_d2dDeviceContext;

			ComPtr<IMFSourceReader> ppSourceReader;

			int m_width;
			int m_height;

			Windows::Foundation::EventRegistrationToken m_eventTokens[2];




			static HRESULT ConvertVideoTypeToUncompressedType(IMFMediaType *pType, const GUID& subtype, IMFMediaType **ppType);

			void OnTick(Platform::Object ^sender, Platform::Object ^args);
		};
	}
}
