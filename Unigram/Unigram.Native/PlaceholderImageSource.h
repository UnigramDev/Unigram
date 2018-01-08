#pragma once
#include <ppl.h>
#include <wincodec.h>
#include <Dwrite_1.h>
#include "Helpers\COMHelper.h"

using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;
using namespace Windows::UI;
using namespace Windows::Foundation;
using namespace Windows::Storage::Streams;

namespace Unigram
{
	namespace Native
	{

		public ref class PlaceholderImageSource sealed
		{
		public:
			static void Draw(Color clear, _In_ Platform::String^ text, _In_ IRandomAccessStream^ randomAccessStream);

		internal:
			PlaceholderImageSource(int x, int y);

		private:
			HRESULT InternalDraw(Color clear, _In_ Platform::String^ text, _In_ IRandomAccessStream^ randomAccessStream);
			HRESULT SaveBitmapToStream(_In_ REFGUID wicFormat, _In_ IStream* stream);
			HRESULT MeasureText(_In_ Platform::String^ text, _Out_ DWRITE_TEXT_METRICS* textMetrics);
			HRESULT SaveBitmapToFile(_In_ IRandomAccessStream^ randomAccessStream);
			HRESULT CreateDeviceIndependentResources();
			HRESULT CreateDeviceResources();

		private:
			ComPtr<ID2D1Factory1> m_d2dFactory;
			ComPtr<ID2D1Device> m_d2dDevice;
			ComPtr<ID2D1DeviceContext> m_d2dContext;
			D3D_FEATURE_LEVEL m_featureLevel;
			ComPtr<IWICImagingFactory2> m_wicFactory;
			ComPtr<IWICImageEncoder> m_imageEncoder;
			ComPtr<IDWriteFactory1> m_dwriteFactory;
			ComPtr<IDWriteTextFormat> m_textFormat;
			ComPtr<ID2D1SolidColorBrush> m_textBrush;
			ComPtr<ID2D1Bitmap1> m_targetBitmap;
			D2D1_SIZE_F m_renderTargetSize;
			CriticalSection m_criticalSection;
		};

	}
}