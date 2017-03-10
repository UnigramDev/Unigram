#pragma once

#include <ppl.h>
#include "Helpers\DirectXEx.h"

using Concurrency::critical_section;
using namespace Microsoft::WRL;
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
			static void Draw(Color clear, Platform::String^ text, IRandomAccessStream^ randomAccessStream);

		internal:
			PlaceholderImageSource(int x, int y);

			void BeginDraw(Color clear);
			void EndDraw();

			void DrawText(Platform::String^ text, int x, int y, Platform::String^ fontFamilyName,
				D2D1_COLOR_F textColor, float fontSize, DWRITE_FONT_STYLE fontStyle,
				DWRITE_FONT_WEIGHT fontWeight);

			void MeasureText(Platform::String^ text, Platform::String^ fontFamilyName,
				float fontSize, DWRITE_FONT_STYLE fontStyle,
				DWRITE_FONT_WEIGHT fontWeight, DWRITE_TEXT_METRICS* textMetrics);

			void SaveBitmapToFile(IRandomAccessStream^ randomAccessStream);


			void CreateDeviceIndependentResources();
			void CreateDeviceResources();

		private:
			void Initialize();
			void SaveBitmapToStream(
				_In_ ComPtr<ID2D1Bitmap1> d2dBitmap,
				_In_ ComPtr<IWICImagingFactory2> wicFactory2,
				_In_ ComPtr<ID2D1DeviceContext> d2dContext,
				_In_ REFGUID wicFormat,
				_In_ IStream* stream);

		private:
			ComPtr<ID2D1Factory1>					m_d2dFactory;
			ComPtr<ID2D1Device>						m_d2dDevice;
			ComPtr<ID2D1DeviceContext>				m_d2dContext;
			D3D_FEATURE_LEVEL						m_featureLevel;
			ComPtr<IWICImagingFactory2>				m_wicFactory;
			ComPtr<IDWriteFactory1>					m_dwriteFactory;

			ComPtr<ID2D1SolidColorBrush>			m_textBrush;
			ComPtr<IDWriteTextFormat>				m_textFormat;

			ComPtr<ID2D1Bitmap1>					m_targetBitmap;
			ComPtr<ID2D1Bitmap1>					m_watermarkBitmap;

			D2D1_SIZE_F								m_renderTargetSize;

			static critical_section					m_criticalSection;
			static PlaceholderImageSource^			m_instance;
		};
	}
}