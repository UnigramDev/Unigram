#pragma once
#include <ppl.h>
#include <wincodec.h>
#include <Dwrite_1.h>
#include <D2d1_3.h>
#include <map>
#include "Helpers\COMHelper.h"

using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;
using namespace Windows::UI;
using namespace Windows::Foundation;
using namespace Windows::Foundation::Collections;
using namespace Windows::Storage::Streams;
using namespace Platform;

namespace Unigram
{
	namespace Native
	{

		public ref class PlaceholderImageHelper sealed
		{
		public:
			static PlaceholderImageHelper^ GetForCurrentView();

			void DrawIdenticon(_In_ IVector<uint8>^ hash, _In_ IRandomAccessStream^ randomAccessStream);
			void DrawProfilePlaceholder(Color clear, _In_ Platform::String^ text, _In_ IRandomAccessStream^ randomAccessStream);
			void DrawThumbnailPlaceholder(_In_ Platform::String^ fileName, float blurAmount, _In_ IRandomAccessStream^ randomAccessStream);

		internal:
			PlaceholderImageHelper();

		private:
			HRESULT InternalDrawIdenticon(_In_ IVector<uint8>^ hash, _In_ IRandomAccessStream^ randomAccessStream);
			HRESULT InternalDrawProfilePlaceholder(Color clear, _In_ Platform::String^ text, _In_ IRandomAccessStream^ randomAccessStream);
			HRESULT InternalDrawThumbnailPlaceholder(_In_ Platform::String^ fileName, float blurAmount, _In_ IRandomAccessStream^ randomAccessStream);
			HRESULT InternalDrawThumbnailPlaceholder(_In_ IWICBitmapSource* wicBitmapSource, float blurAmount, _In_ IRandomAccessStream^ randomAccessStream);
			HRESULT SaveImageToStream(_In_ ID2D1Image* image, _In_ REFGUID wicFormat, _In_ IRandomAccessStream^ randomAccessStream);
			HRESULT MeasureText(_In_ Platform::String^ text, _Out_ DWRITE_TEXT_METRICS* textMetrics);
			HRESULT CreateDeviceIndependentResources();
			HRESULT CreateDeviceResources();

		private:
			static std::map<int, WeakReference> s_windowContext;

			ComPtr<ID2D1Factory1> m_d2dFactory;
			ComPtr<ID2D1Device> m_d2dDevice;
			ComPtr<ID2D1DeviceContext2> m_d2dContext;
			D3D_FEATURE_LEVEL m_featureLevel;
			ComPtr<IWICImagingFactory2> m_wicFactory;
			ComPtr<IWICImageEncoder> m_imageEncoder;
			ComPtr<IDWriteFactory1> m_dwriteFactory;
			ComPtr<IDWriteTextFormat> m_textFormat;
			ComPtr<ID2D1SolidColorBrush> m_textBrush;
			std::vector<ComPtr<ID2D1SolidColorBrush>> m_identiconBrushes;
			ComPtr<ID2D1Effect> m_gaussianBlurEffect;
			ComPtr<ID2D1Bitmap1> m_targetBitmap;
			CriticalSection m_criticalSection;
		};

	}
}