#include "pch.h"
#include "PlaceholderImageHelper.h"
#if __has_include("PlaceholderImageHelper.g.cpp")
#include "PlaceholderImageHelper.g.cpp"
#endif

#include "Qr/QrCode.hpp"
#include "SVG/nanosvg.h"
#include "StringUtils.h"
#include "Helpers\COMHelper.h"

#include <webp\decode.h>
#include <webp\demux.h>

#include <shcore.h>

#include <winrt/Windows.ApplicationModel.h>
#include <winrt/Windows.Foundation.Collections.h>

using namespace D2D1;
using namespace winrt::Windows::ApplicationModel;
using namespace qrcodegen;

namespace winrt::Unigram::Native::implementation
{
	critical_section PlaceholderImageHelper::s_criticalSection;
	winrt::com_ptr<PlaceholderImageHelper> PlaceholderImageHelper::s_current{ nullptr };

	class CustomFontFileEnumerator
		: public winrt::implements<CustomFontFileEnumerator, IDWriteFontFileEnumerator>
	{
		winrt::com_ptr<IDWriteFactory> m_factory;
		std::wstring m_filename;
		winrt::com_ptr<IDWriteFontFile> m_theFile;

	public:
		CustomFontFileEnumerator(IDWriteFactory* factory, void const* collectionKey, uint32_t collectionKeySize)
			: m_factory()
			, m_filename(static_cast<wchar_t const*>(collectionKey), collectionKeySize / 2)
		{
			m_factory.copy_from(factory);
		}

		IFACEMETHODIMP MoveNext(BOOL* hasCurrentFile) override
		{
			if (m_theFile)
			{
				*hasCurrentFile = FALSE;
			}
			else if (SUCCEEDED(m_factory->CreateFontFileReference(m_filename.c_str(), nullptr, m_theFile.put())))
			{
				*hasCurrentFile = TRUE;
			}
			else
			{
				*hasCurrentFile = FALSE;
			}

			return S_OK;
		}

		IFACEMETHODIMP GetCurrentFontFile(IDWriteFontFile** fontFile) override
		{
			m_theFile.copy_to(fontFile);
			return S_OK;
		}
	};



	class CustomFontLoader
		: public winrt::implements<CustomFontLoader, IDWriteFontCollectionLoader>
	{
	public:
		IFACEMETHODIMP CreateEnumeratorFromKey(
			IDWriteFactory* factory,
			void const* collectionKey,
			uint32_t collectionKeySize,
			IDWriteFontFileEnumerator** fontFileEnumerator) override
		{
			return ExceptionBoundary(
				[=]
				{
					auto enumerator = winrt::make_self<CustomFontFileEnumerator>(factory, collectionKey, collectionKeySize);
					enumerator.as<IDWriteFontFileEnumerator>().copy_to(fontFileEnumerator);
				});
		}
	};

	void PlaceholderImageHelper::DrawWebP(hstring fileName, int32_t maxWidth, IRandomAccessStream randomAccessStream, Windows::Foundation::Size& size)
	{
		size = Windows::Foundation::Size{ 0,0 };

		FILE* file = _wfopen(fileName.data(), L"rb");
		if (file == NULL) {
			return;
		}

		fseek(file, 0, SEEK_END);
		size_t length = ftell(file);
		fseek(file, 0, SEEK_SET);
		char* buffer = (char*)malloc(length);
		fread(buffer, 1, length, file);
		fclose(file);

		WebPData webPData;
		webPData.bytes = (uint8_t*)buffer;
		webPData.size = length;

		auto spDemuxer = std::unique_ptr<WebPDemuxer, decltype(&WebPDemuxDelete)>
		{
			WebPDemux(&webPData),
			WebPDemuxDelete
		};
		if (!spDemuxer)
		{
			//throw ref new InvalidArgumentException(ref new String(L"Failed to create demuxer"));
			free(buffer);
			return;
		}

		WebPIterator iter;
		if (WebPDemuxGetFrame(spDemuxer.get(), 1, &iter))
		{
			WebPDecoderConfig config;
			int ret = WebPInitDecoderConfig(&config);
			if (!ret)
			{
				//throw ref new FailureException(ref new String(L"WebPInitDecoderConfig failed"));
				free(buffer);
				return;
			}

			ret = (WebPGetFeatures(iter.fragment.bytes, iter.fragment.size, &config.input) == VP8_STATUS_OK);
			if (!ret)
			{
				//throw ref new FailureException(ref new String(L"WebPGetFeatures failed"));
				free(buffer);
				return;
			}

			int width = iter.width;
			int height = iter.height;

			if (iter.width > maxWidth || iter.height > maxWidth)
			{
				auto ratioX = (double)maxWidth / iter.width;
				auto ratioY = (double)maxWidth / iter.height;
				auto ratio = std::min(ratioX, ratioY);

				width = (int)(iter.width * ratio);
				height = (int)(iter.height * ratio);
			}

			size.Width = width;
			size.Height = height;

			uint8_t* pixels = new uint8_t[(width * 4) * height];

			if (width != iter.width || height != iter.height) {
				config.options.scaled_width = width;
				config.options.scaled_height = height;
				config.options.use_scaling = 1;
				config.options.no_fancy_upsampling = 1;
			}

			config.output.colorspace = MODE_BGRA;
			config.output.is_external_memory = 1;
			config.output.u.RGBA.rgba = pixels;
			config.output.u.RGBA.stride = width * 4;
			config.output.u.RGBA.size = (width * 4) * height;

			ret = WebPDecode(iter.fragment.bytes, iter.fragment.size, &config);

			if (ret != VP8_STATUS_OK)
			{
				//throw ref new FailureException(ref new String(L"Failed to decode frame"));
				delete[] pixels;

				free(buffer);
				return;
			}

			winrt::com_ptr<IWICImagingFactory> piFactory;
			winrt::com_ptr<IWICBitmapEncoder> piEncoder;
			winrt::com_ptr<IStream> piStream;

			CoCreateInstance(CLSID_WICImagingFactory, NULL, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(&piFactory));

			HRESULT hr = CreateStreamOverRandomAccessStream(winrt::get_unknown(randomAccessStream), IID_PPV_ARGS(&piStream));

			piFactory->CreateEncoder(GUID_ContainerFormatPng, NULL, piEncoder.put());
			piEncoder->Initialize(piStream.get(), WICBitmapEncoderNoCache);

			winrt::com_ptr<IPropertyBag2> propertyBag;
			winrt::com_ptr<IWICBitmapFrameEncode> frame;
			piEncoder->CreateNewFrame(frame.put(), propertyBag.put());

			frame->Initialize(propertyBag.get());
			frame->SetSize(width, height);

			WICPixelFormatGUID format = GUID_WICPixelFormat32bppPBGRA;
			frame->SetPixelFormat(&format);
			frame->WritePixels(height, width * 4, (width * 4) * height, pixels);

			frame->Commit();
			piEncoder->Commit();
			piStream->Seek({ 0 }, STREAM_SEEK_SET, nullptr);

			delete[] pixels;
		}

		free(buffer);
	}

	void PlaceholderImageHelper::DrawSvg(hstring path, Color foreground, IRandomAccessStream randomAccessStream, Windows::Foundation::Size& size)
	{
		winrt::check_hresult(InternalDrawSvg(path, foreground, randomAccessStream, size));
	}

	void PlaceholderImageHelper::DrawQr(hstring data, Color foreground, Color background, IRandomAccessStream randomAccessStream)
	{
		winrt::check_hresult(InternalDrawQr(data, foreground, background, randomAccessStream));
	}

	void PlaceholderImageHelper::DrawIdenticon(IVector<uint8_t> hash, int side, IRandomAccessStream randomAccessStream)
	{
		winrt::check_hresult(InternalDrawIdenticon(hash, side, randomAccessStream));
	}

	void PlaceholderImageHelper::DrawGlyph(hstring text, Color top, Color bottom, IRandomAccessStream randomAccessStream)
	{
		winrt::check_hresult(InternalDrawGlyph(text, top, bottom, randomAccessStream));
	}

	void PlaceholderImageHelper::DrawSavedMessages(Color top, Color bottom, IRandomAccessStream randomAccessStream)
	{
		winrt::check_hresult(InternalDrawSavedMessages(top, bottom, randomAccessStream));
	}

	void PlaceholderImageHelper::DrawDeletedUser(Color top, Color bottom, IRandomAccessStream randomAccessStream)
	{
		winrt::check_hresult(InternalDrawDeletedUser(top, bottom, randomAccessStream));
	}

	void PlaceholderImageHelper::DrawProfilePlaceholder(hstring text, Color top, Color bottom, IRandomAccessStream randomAccessStream)
	{
		winrt::check_hresult(InternalDrawProfilePlaceholder(text, top, bottom, randomAccessStream));
	}

	void PlaceholderImageHelper::DrawThumbnailPlaceholder(hstring fileName, float blurAmount, IRandomAccessStream randomAccessStream)
	{
		winrt::check_hresult(InternalDrawThumbnailPlaceholder(fileName, blurAmount, randomAccessStream));
	}

	void PlaceholderImageHelper::DrawThumbnailPlaceholder(IVector<uint8_t> bytes, float blurAmount, IRandomAccessStream randomAccessStream)
	{
		winrt::check_hresult(InternalDrawThumbnailPlaceholder(bytes, blurAmount, randomAccessStream));
	}

	HRESULT PlaceholderImageHelper::InternalDrawSvg(hstring path, Color foreground, IRandomAccessStream randomAccessStream, Windows::Foundation::Size& size)
	{
		auto lock = critical_section::scoped_lock(m_criticalSection);

		HRESULT result;

		auto data = string_to_unmanaged(path);

		struct NSVGimage* image;
		image = nsvgParse((char*)data.c_str(), "px", 96);

		auto imageWidth = image->width / 2;
		auto imageHeight = image->height / 2;
		size = Windows::Foundation::Size(imageWidth, imageHeight);

		winrt::com_ptr<ID2D1Bitmap1> targetBitmap;
		D2D1_BITMAP_PROPERTIES1 properties = { { DXGI_FORMAT_R8G8B8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED }, 96, 96, D2D1_BITMAP_OPTIONS_TARGET, 0 };
		ReturnIfFailed(result, m_d2dContext->CreateBitmap(D2D1_SIZE_U{ (uint32_t)imageWidth, (uint32_t)imageHeight }, nullptr, 0, &properties, targetBitmap.put()));

		m_d2dContext->SetTarget(targetBitmap.get());
		m_d2dContext->BeginDraw();
		m_d2dContext->SetTransform(D2D1::Matrix3x2F::Scale(0.5f, 0.5f));

		winrt::com_ptr<ID2D1SolidColorBrush> blackBrush;
		ReturnIfFailed(result, m_d2dContext->CreateSolidColorBrush(
			D2D1::ColorF(foreground.R / 255.0f, foreground.G / 255.0f, foreground.B / 255.0f, foreground.A / 255.0f), blackBrush.put()));

		for (auto shape = image->shapes; shape != NULL; shape = shape->next)
		{
			if (!(shape->flags & NSVG_FLAGS_VISIBLE) || (shape->fill.type == NSVG_PAINT_NONE && shape->stroke.type == NSVG_PAINT_NONE))
			{
				continue;
			}

			blackBrush->SetOpacity(shape->opacity);

			winrt::com_ptr<ID2D1PathGeometry1> geometry;
			ReturnIfFailed(result, m_d2dFactory->CreatePathGeometry(geometry.put()));

			winrt::com_ptr<ID2D1GeometrySink> sink;
			ReturnIfFailed(result, geometry->Open(sink.put()));

			for (NSVGpath* path = shape->paths; path != NULL; path = path->next)
			{
				sink->BeginFigure({ path->pts[0], path->pts[1] }, D2D1_FIGURE_BEGIN_FILLED);

				for (int i = 0; i < path->npts - 1; i += 3)
				{
					float* p = &path->pts[i * 2];
					sink->AddBezier({ { p[2], p[3] }, { p[4], p[5] }, { p[6], p[7] } });
				}

				sink->EndFigure(path->closed ? D2D1_FIGURE_END_CLOSED : D2D1_FIGURE_END_OPEN);
			}

			ReturnIfFailed(result, sink->Close());

			if (shape->fill.type != NSVG_PAINT_NONE)
			{
				switch (shape->fillRule)
				{
				case NSVG_FILLRULE_EVENODD:
					sink->SetFillMode(D2D1_FILL_MODE_ALTERNATE);
					break;
				default:
					sink->SetFillMode(D2D1_FILL_MODE_WINDING);
					break;
				}

				m_d2dContext->FillGeometry(geometry.get(), blackBrush.get());
			}

			if (shape->stroke.type != NSVG_PAINT_NONE)
			{
				D2D1_STROKE_STYLE_PROPERTIES1 strokeProperties{};
				strokeProperties.miterLimit = shape->miterLimit;

				switch (shape->strokeLineCap)
				{
				case NSVG_CAP_BUTT:
					strokeProperties.startCap = strokeProperties.endCap = D2D1_CAP_STYLE_FLAT;
					break;
				case NSVG_CAP_ROUND:
					strokeProperties.startCap = strokeProperties.endCap = D2D1_CAP_STYLE_ROUND;
					break;
				case NSVG_CAP_SQUARE:
					strokeProperties.startCap = strokeProperties.endCap = D2D1_CAP_STYLE_SQUARE;
					break;
				default:
					break;
				}

				switch (shape->strokeLineJoin)
				{
				case NSVG_JOIN_BEVEL:
					strokeProperties.lineJoin = D2D1_LINE_JOIN_BEVEL;
					break;
				case NSVG_JOIN_MITER:
					strokeProperties.lineJoin = D2D1_LINE_JOIN_MITER;
					break;
				case NSVG_JOIN_ROUND:
					strokeProperties.lineJoin = D2D1_LINE_JOIN_ROUND;
					break;
				default:
					break;
				}

				winrt::com_ptr<ID2D1StrokeStyle1> strokeStyle;
				ReturnIfFailed(result, m_d2dFactory->CreateStrokeStyle(strokeProperties, NULL, 0, strokeStyle.put()));

				m_d2dContext->DrawGeometry(geometry.get(), blackBrush.get(), shape->strokeWidth, strokeStyle.get());
			}
		}

		nsvgDelete(image);

		m_d2dContext->SetTransform(D2D1::Matrix3x2F::Identity());

		if ((result = m_d2dContext->EndDraw()) == D2DERR_RECREATE_TARGET)
		{
			ReturnIfFailed(result, CreateDeviceResources());
			return InternalDrawSvg(path, foreground, randomAccessStream, size);
		}

		return SaveImageToStream(targetBitmap.get(), GUID_ContainerFormatPng, randomAccessStream);
	}

	constexpr auto kShareQrSize = 768;
	constexpr auto kShareQrPadding = 16;

	inline int ReplaceElements(const QrData& data) {
		const auto elements = (data.size / 4);
		const auto shift = (data.size - elements) % 2;
		return (elements - shift);
	}

	inline int ReplaceSize(const QrData& data, int pixel) {
		return ReplaceElements(data) * pixel;
	}

	HRESULT PlaceholderImageHelper::InternalDrawQr(hstring text, Color foreground, Color background, IRandomAccessStream randomAccessStream)
	{
		auto lock = critical_section::scoped_lock(m_criticalSection);

		HRESULT result;

		winrt::com_ptr<ID2D1Bitmap1> targetBitmap;
		D2D1_BITMAP_PROPERTIES1 properties = { { DXGI_FORMAT_R8G8B8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED }, 96, 96, D2D1_BITMAP_OPTIONS_TARGET, 0 };
		ReturnIfFailed(result, m_d2dContext->CreateBitmap(D2D1_SIZE_U{ kShareQrSize - 4 * kShareQrPadding, kShareQrSize - 4 * kShareQrPadding }, nullptr, 0, &properties, targetBitmap.put()));


		m_d2dContext->SetTarget(targetBitmap.get());
		m_d2dContext->BeginDraw();

		auto data = QrData();
		const auto utf8 = string_to_unmanaged(text);
		const auto qr = QrCode::encodeText(utf8.c_str(), QrCode::Ecc::MEDIUM);
		data.size = qr.getSize();

		data.values.reserve(data.size * data.size);
		for (auto row = 0; row != data.size; ++row) {
			for (auto column = 0; column != data.size; ++column) {
				data.values.push_back(qr.getModule(row, column));
			}
		}

		const auto size = (kShareQrSize - 2 * kShareQrPadding);
		const auto pixel = size / data.size;

		const auto replaceElements = ReplaceElements(data);
		const auto replaceFrom = (data.size - replaceElements) / 2;
		const auto replaceTill = (data.size - replaceFrom);
		//const auto black = GenerateSingle(pixel, Qt::transparent, Qt::black);
		//const auto white = GenerateSingle(pixel, Qt::black, Qt::transparent);
		const auto value = [&](int row, int column) {
			return (row >= 0)
				&& (row < data.size)
				&& (column >= 0)
				&& (column < data.size)
				&& (row < replaceFrom
					|| row >= replaceTill
					|| column < replaceFrom
					|| column >= replaceTill)
				&& data.values[row * data.size + column];
		};
		const auto blackFull = [&](int row, int column) {
			return (value(row - 1, column) && value(row + 1, column))
				|| (value(row, column - 1) && value(row, column + 1));
		};
		const auto whiteCorner = [&](int row, int column, int dx, int dy) {
			return !value(row + dy, column)
				|| !value(row, column + dx)
				|| !value(row + dy, column + dx);
		};
		const auto whiteFull = [&](int row, int column) {
			return whiteCorner(row, column, -1, -1)
				&& whiteCorner(row, column, 1, -1)
				&& whiteCorner(row, column, 1, 1)
				&& whiteCorner(row, column, -1, 1);
		};
		//auto result = QImage(
		//	data.size * pixel,
		//	data.size * pixel,
		//	QImage::Format_ARGB32_Premultiplied);
		//result.fill(Qt::transparent);
		{
			//auto p = QPainter(&result);
			//p.setCompositionMode(QPainter::CompositionMode_Source);
			auto context = m_d2dContext;

			winrt::com_ptr<ID2D1SolidColorBrush> blackBrush;
			ReturnIfFailed(result, m_d2dContext->CreateSolidColorBrush(
				D2D1::ColorF(foreground.R / 255.0f, foreground.G / 255.0f, foreground.B / 255.0f, foreground.A / 255.0f), blackBrush.put()));

			winrt::com_ptr<ID2D1SolidColorBrush> whiteBrush;
			ReturnIfFailed(result, m_d2dContext->CreateSolidColorBrush(
				D2D1::ColorF(background.R / 255.0f, background.G / 255.0f, background.B / 255.0f, background.A / 255.0f), whiteBrush.put()));

			const auto skip = pixel - pixel / 2;
			const auto brect = [&](float x, float y, float width, float height) {
				context->FillRectangle(D2D1_RECT_F{ x, y, x + width, y + height }, blackBrush.get());
			};
			const auto wrect = [&](float x, float y, float width, float height) {
				context->FillRectangle(D2D1_RECT_F{ x, y, x + width, y + height }, whiteBrush.get());
			};
			const auto large = [&](float x, float y) {
				context->FillRoundedRectangle(D2D1_ROUNDED_RECT{ D2D1_RECT_F{
					x,
					y,
					x + pixel * 7,
					y + pixel * 7 }, pixel * 2.0f, pixel * 2.0f }, blackBrush.get());
				context->FillRoundedRectangle(D2D1_ROUNDED_RECT{ D2D1_RECT_F{
					x + pixel,
					y + pixel,
					x + pixel + pixel * 5,
					y + pixel + pixel * 5 }, pixel * 1.5f, pixel * 1.5f }, whiteBrush.get());
				context->FillRoundedRectangle(D2D1_ROUNDED_RECT{ D2D1_RECT_F{
					x + pixel * 2,
					y + pixel * 2,
					x + pixel * 2 + pixel * 3,
					y + pixel * 2 + pixel * 3 }, (float)pixel, (float)pixel }, blackBrush.get());
			};
			const auto white = [&](float x, float y) {
				context->FillRectangle(D2D1_RECT_F{ x, y, x + pixel, y + pixel }, blackBrush.get());
				context->FillRoundedRectangle(D2D1_ROUNDED_RECT{ D2D1_RECT_F{ x, y, x + pixel, y + pixel }, pixel / 2.0f, pixel / 2.0f }, whiteBrush.get());
			};
			const auto black = [&](float x, float y) {
				context->FillRectangle(D2D1_RECT_F{ x, y, x + pixel, y + pixel }, whiteBrush.get());
				context->FillRoundedRectangle(D2D1_ROUNDED_RECT{ D2D1_RECT_F{ x, y, x + pixel, y + pixel }, pixel / 2.0f, pixel / 2.0f }, blackBrush.get());
			};
			for (auto row = 0; row != data.size; ++row) {
				for (auto column = 0; column != data.size; ++column) {
					if ((row < 7 && (column < 7 || column >= data.size - 7))
						|| (column < 7 && (row < 7 || row >= data.size - 7))) {
						continue;
					}
					const auto x = column * pixel;
					const auto y = row * pixel;
					const auto index = row * data.size + column;
					if (value(row, column)) {
						if (blackFull(row, column)) {
							brect(x, y, pixel, pixel);
						}
						else {
							black(x, y);
							if (value(row - 1, column)) {
								brect(x, y, pixel, pixel / 2);
							}
							else if (value(row + 1, column)) {
								brect(x, y + skip, pixel, pixel / 2);
							}
							if (value(row, column - 1)) {
								brect(x, y, pixel / 2, pixel);
							}
							else if (value(row, column + 1)) {
								brect(x + skip, y, pixel / 2, pixel);
							}
						}
					}
					else if (whiteFull(row, column)) {
						wrect(x, y, pixel, pixel);
					}
					else {
						white(x, y);
						if (whiteCorner(row, column, -1, -1)
							&& whiteCorner(row, column, 1, -1)) {
							wrect(x, y, pixel, pixel / 2);
						}
						else if (whiteCorner(row, column, -1, 1)
							&& whiteCorner(row, column, 1, 1)) {
							wrect(x, y + skip, pixel, pixel / 2);
						}
						if (whiteCorner(row, column, -1, -1)
							&& whiteCorner(row, column, -1, 1)) {
							wrect(x, y, pixel / 2, pixel);
						}
						else if (whiteCorner(row, column, 1, -1)
							&& whiteCorner(row, column, 1, 1)) {
							wrect(x + skip, y, pixel / 2, pixel);
						}
						if (whiteCorner(row, column, -1, -1)) {
							wrect(x, y, pixel / 2, pixel / 2);
						}
						if (whiteCorner(row, column, 1, -1)) {
							wrect(x + skip, y, pixel / 2, pixel / 2);
						}
						if (whiteCorner(row, column, 1, 1)) {
							wrect(x + skip, y + skip, pixel / 2, pixel / 2);
						}
						if (whiteCorner(row, column, -1, 1)) {
							wrect(x, y + skip, pixel / 2, pixel / 2);
						}
					}
				}
			}

			//PrepareForRound(p);
			large(0, 0);
			large((data.size - 7) * pixel, 0);
			large(0, (data.size - 7) * pixel);
		}

		float diamond = ReplaceSize(data, pixel);
		float x1 = (size - diamond) / 2.0f;
		x1 -= kShareQrPadding / 2.0f;
		//winrt::com_ptr<ID2D1SolidColorBrush> red;
		//ReturnIfFailed(result, m_d2dContext->CreateSolidColorBrush(D2D1::ColorF(D2D1::ColorF::Red), &red));
		//m_d2dContext->FillRectangle(D2D1_RECT_F{ x1, x1, x1 + diamond, x1 + diamond }, red.get());

		if ((result = m_d2dContext->EndDraw()) == D2DERR_RECREATE_TARGET)
		{
			ReturnIfFailed(result, CreateDeviceResources());
			return InternalDrawQr(text, foreground, background, randomAccessStream);
		}

		return SaveImageToStream(targetBitmap.get(), GUID_ContainerFormatPng, randomAccessStream);
	}

	HRESULT PlaceholderImageHelper::InternalDrawIdenticon(IVector<uint8_t> hash, int side, IRandomAccessStream randomAccessStream)
	{
		auto lock = critical_section::scoped_lock(m_criticalSection);

		HRESULT result;

		m_d2dContext->SetTarget(m_targetBitmap.get());
		m_d2dContext->BeginDraw();

		auto width = side;
		auto height = side;

		if (16 == hash.Size())
		{
			int bitPointer = 0;
			float rectSize = (float)std::floor(std::min(width, height) / 8.0f);
			float xOffset = std::max(0.0f, (width - rectSize * 8) / 2);
			float yOffset = std::max(0.0f, (height - rectSize * 8) / 2);
			for (int iy = 0; iy < 8; iy++)
			{
				for (int ix = 0; ix < 8; ix++)
				{
					int byteValue = (hash.GetAt(bitPointer / 8) >> (bitPointer % 8)) & 0x3;
					bitPointer += 2;
					int colorIndex = std::abs(byteValue) % 4;
					D2D1_RECT_F layoutRect = { (int)(xOffset + ix * rectSize), (int)(iy * rectSize + yOffset), (int)(xOffset + ix * rectSize + rectSize), (int)(iy * rectSize + rectSize + yOffset) };
					m_d2dContext->FillRectangle(layoutRect, m_identiconBrushes[colorIndex].get());
				}
			}
		}
		else
		{
			int bitPointer = 0;
			float rectSize = (float)std::floor(std::min(width, height) / 12.0f);
			float xOffset = std::max(0.0f, (width - rectSize * 12) / 2);
			float yOffset = std::max(0.0f, (height - rectSize * 12) / 2);
			for (int iy = 0; iy < 12; iy++)
			{
				for (int ix = 0; ix < 12; ix++)
				{
					int byteValue = (hash.GetAt(bitPointer / 8) >> (bitPointer % 8)) & 0x3;
					int colorIndex = std::abs(byteValue) % 4;
					D2D1_RECT_F layoutRect = { (int)(xOffset + ix * rectSize), (int)(iy * rectSize + yOffset), (int)(xOffset + ix * rectSize + rectSize), (int)(iy * rectSize + rectSize + yOffset) };
					m_d2dContext->FillRectangle(layoutRect, m_identiconBrushes[colorIndex].get());
					bitPointer += 2;
				}
			}
		}

		if ((result = m_d2dContext->EndDraw()) == D2DERR_RECREATE_TARGET)
		{
			ReturnIfFailed(result, CreateDeviceResources());
			return InternalDrawIdenticon(hash, side, randomAccessStream);
		}

		return SaveImageToStream(m_targetBitmap.get(), GUID_ContainerFormatPng, randomAccessStream);
	}

	HRESULT PlaceholderImageHelper::InternalDrawGlyph(hstring glyph, Color top, Color bottom, IRandomAccessStream randomAccessStream)
	{
		auto lock = critical_section::scoped_lock(m_criticalSection);
		auto text = glyph.data();

		HRESULT result;
		DWRITE_TEXT_METRICS textMetrics;
		ReturnIfFailed(result, MeasureText(text, m_symbolFormat.get(), &textMetrics));

		m_d2dContext->SetTarget(m_targetBitmap.get());
		m_d2dContext->BeginDraw();
		//m_d2dContext->SetTransform(D2D1::Matrix3x2F::Identity());
		m_d2dContext->Clear(D2D1::ColorF(top.R / 255.0f, top.G / 255.0f, top.B / 255.0f, top.A / 255.0f));

		D2D1_LINEAR_GRADIENT_BRUSH_PROPERTIES properties;
		properties.startPoint = { 0, 0 };
		properties.endPoint = { 0, 192 };
		D2D1_GRADIENT_STOP* stops = new D2D1_GRADIENT_STOP[2];
		stops[0] = { 1, D2D1::ColorF(top.R / 255.0f, top.G / 255.0f, top.B / 255.0f, top.A / 255.0f) };
		stops[1] = { 0, D2D1::ColorF(bottom.R / 255.0f, bottom.G / 255.0f, bottom.B / 255.0f, bottom.A / 255.0f) };
		winrt::com_ptr<ID2D1GradientStopCollection> collection;
		ReturnIfFailed(result, m_d2dContext->CreateGradientStopCollection(stops, 2, collection.put()));
		winrt::com_ptr<ID2D1LinearGradientBrush> brush;
		ReturnIfFailed(result, m_d2dContext->CreateLinearGradientBrush(properties, collection.get(), brush.put()));
		m_d2dContext->FillRectangle({ 0, 0, 192, 192 }, brush.get());

		D2D1_RECT_F layoutRect = { (192.0f - textMetrics.width) / 2.0f, (192.0f - textMetrics.height) / 2.0f, 192.0f, 192.0f };
		m_d2dContext->DrawText(text, 1, m_symbolFormat.get(), &layoutRect, m_textBrush.get());

		if ((result = m_d2dContext->EndDraw()) == D2DERR_RECREATE_TARGET)
		{
			ReturnIfFailed(result, CreateDeviceResources());
			return InternalDrawSavedMessages(top, bottom, randomAccessStream);
		}

		return SaveImageToStream(m_targetBitmap.get(), GUID_ContainerFormatPng, randomAccessStream);
	}

	HRESULT PlaceholderImageHelper::InternalDrawSavedMessages(Color top, Color bottom, IRandomAccessStream randomAccessStream)
	{
		auto lock = critical_section::scoped_lock(m_criticalSection);
		auto text = L"\uE907";

		HRESULT result;
		DWRITE_TEXT_METRICS textMetrics;
		ReturnIfFailed(result, MeasureText(text, m_symbolFormat.get(), &textMetrics));

		m_d2dContext->SetTarget(m_targetBitmap.get());
		m_d2dContext->BeginDraw();
		//m_d2dContext->SetTransform(D2D1::Matrix3x2F::Identity());
		m_d2dContext->Clear(D2D1::ColorF(top.R / 255.0f, top.G / 255.0f, top.B / 255.0f, top.A / 255.0f));

		D2D1_LINEAR_GRADIENT_BRUSH_PROPERTIES properties;
		properties.startPoint = { 0, 0 };
		properties.endPoint = { 0, 192 };
		D2D1_GRADIENT_STOP* stops = new D2D1_GRADIENT_STOP[2];
		stops[0] = { 1, D2D1::ColorF(top.R / 255.0f, top.G / 255.0f, top.B / 255.0f, top.A / 255.0f) };
		stops[1] = { 0, D2D1::ColorF(bottom.R / 255.0f, bottom.G / 255.0f, bottom.B / 255.0f, bottom.A / 255.0f) };
		winrt::com_ptr<ID2D1GradientStopCollection> collection;
		ReturnIfFailed(result, m_d2dContext->CreateGradientStopCollection(stops, 2, collection.put()));
		winrt::com_ptr<ID2D1LinearGradientBrush> brush;
		ReturnIfFailed(result, m_d2dContext->CreateLinearGradientBrush(properties, collection.get(), brush.put()));
		m_d2dContext->FillRectangle({ 0, 0, 192, 192 }, brush.get());

		D2D1_RECT_F layoutRect = { (192.0f - textMetrics.width) / 2.0f, (192.0f - textMetrics.height) / 2.0f, 192.0f, 192.0f };
		m_d2dContext->DrawText(text, 1, m_symbolFormat.get(), &layoutRect, m_textBrush.get());

		if ((result = m_d2dContext->EndDraw()) == D2DERR_RECREATE_TARGET)
		{
			ReturnIfFailed(result, CreateDeviceResources());
			return InternalDrawSavedMessages(top, bottom, randomAccessStream);
		}

		return SaveImageToStream(m_targetBitmap.get(), GUID_ContainerFormatPng, randomAccessStream);
	}

	HRESULT PlaceholderImageHelper::InternalDrawDeletedUser(Color top, Color bottom, IRandomAccessStream randomAccessStream)
	{
		auto lock = critical_section::scoped_lock(m_criticalSection);
		auto text = L"\uE91A";

		HRESULT result;
		DWRITE_TEXT_METRICS textMetrics;
		ReturnIfFailed(result, MeasureText(text, m_symbolFormat.get(), &textMetrics));

		m_d2dContext->SetTarget(m_targetBitmap.get());
		m_d2dContext->BeginDraw();
		//m_d2dContext->SetTransform(D2D1::Matrix3x2F::Identity());
		m_d2dContext->Clear(D2D1::ColorF(top.R / 255.0f, top.G / 255.0f, top.B / 255.0f, top.A / 255.0f));

		D2D1_LINEAR_GRADIENT_BRUSH_PROPERTIES properties;
		properties.startPoint = { 0, 0 };
		properties.endPoint = { 0, 192 };
		D2D1_GRADIENT_STOP* stops = new D2D1_GRADIENT_STOP[2];
		stops[0] = { 1, D2D1::ColorF(top.R / 255.0f, top.G / 255.0f, top.B / 255.0f, top.A / 255.0f) };
		stops[1] = { 0, D2D1::ColorF(bottom.R / 255.0f, bottom.G / 255.0f, bottom.B / 255.0f, bottom.A / 255.0f) };
		winrt::com_ptr<ID2D1GradientStopCollection> collection;
		ReturnIfFailed(result, m_d2dContext->CreateGradientStopCollection(stops, 2, collection.put()));
		winrt::com_ptr<ID2D1LinearGradientBrush> brush;
		ReturnIfFailed(result, m_d2dContext->CreateLinearGradientBrush(properties, collection.get(), brush.put()));
		m_d2dContext->FillRectangle({ 0, 0, 192, 192 }, brush.get());

		D2D1_RECT_F layoutRect = { (192.0f - textMetrics.width) / 2.0f, (184.0f - textMetrics.height) / 2.0f, 192.0f, 192.0f };
		m_d2dContext->DrawText(text, 1, m_symbolFormat.get(), &layoutRect, m_textBrush.get());

		if ((result = m_d2dContext->EndDraw()) == D2DERR_RECREATE_TARGET)
		{
			ReturnIfFailed(result, CreateDeviceResources());
			return InternalDrawDeletedUser(top, bottom, randomAccessStream);
		}

		return SaveImageToStream(m_targetBitmap.get(), GUID_ContainerFormatPng, randomAccessStream);
	}

	HRESULT PlaceholderImageHelper::InternalDrawProfilePlaceholder(hstring text, Color top, Color bottom, IRandomAccessStream randomAccessStream)
	{
		auto lock = critical_section::scoped_lock(m_criticalSection);

		HRESULT result;
		DWRITE_TEXT_METRICS textMetrics;
		ReturnIfFailed(result, MeasureText(text.data(), m_textFormat.get(), &textMetrics));

		m_d2dContext->SetTarget(m_targetBitmap.get());
		m_d2dContext->BeginDraw();
		//m_d2dContext->SetTransform(D2D1::Matrix3x2F::Identity());
		//m_d2dContext->Clear(D2D1::ColorF(top.R / 255.0f, top.G / 255.0f, top.B / 255.0f, top.A / 255.0f));

		D2D1_LINEAR_GRADIENT_BRUSH_PROPERTIES properties;
		properties.startPoint = { 0, 0 };
		properties.endPoint = { 0, 192 };
		D2D1_GRADIENT_STOP* stops = new D2D1_GRADIENT_STOP[2];
		stops[0] = { 1, D2D1::ColorF(top.R / 255.0f, top.G / 255.0f, top.B / 255.0f, top.A / 255.0f) };
		stops[1] = { 0, D2D1::ColorF(bottom.R / 255.0f, bottom.G / 255.0f, bottom.B / 255.0f, bottom.A / 255.0f) };
		winrt::com_ptr<ID2D1GradientStopCollection> collection;
		ReturnIfFailed(result, m_d2dContext->CreateGradientStopCollection(stops, 2, collection.put()));
		winrt::com_ptr<ID2D1LinearGradientBrush> brush;
		ReturnIfFailed(result, m_d2dContext->CreateLinearGradientBrush(properties, collection.get(), brush.put()));
		m_d2dContext->FillRectangle({ 0, 0, 192, 192 }, brush.get());

		D2D1_RECT_F layoutRect = { (192.0f - textMetrics.width) / 2.0f, (180.0f - textMetrics.height) / 2.0f, 192.0f, 192.0f };
		m_d2dContext->DrawText(text.data(), text.size(), m_textFormat.get(), &layoutRect, m_textBrush.get());

		if ((result = m_d2dContext->EndDraw()) == D2DERR_RECREATE_TARGET)
		{
			ReturnIfFailed(result, CreateDeviceResources());
			return InternalDrawProfilePlaceholder(text, top, bottom, randomAccessStream);
		}

		return SaveImageToStream(m_targetBitmap.get(), GUID_ContainerFormatPng, randomAccessStream);
	}

	HRESULT PlaceholderImageHelper::InternalDrawThumbnailPlaceholder(hstring fileName, float blurAmount, IRandomAccessStream randomAccessStream)
	{
		auto lock = critical_section::scoped_lock(m_criticalSection);

		HANDLE file = CreateFile2FromAppW(fileName.data(), GENERIC_READ, FILE_SHARE_READ, OPEN_EXISTING, nullptr);

		if (file == INVALID_HANDLE_VALUE)
		{
			return ERROR_FILE_NOT_FOUND;
		}

		HRESULT result;
		winrt::com_ptr<IWICBitmapDecoder> wicBitmapDecoder;
		//ReturnIfFailed(result, m_wicFactory->CreateDecoderFromFilename(fileName->Data(), nullptr, GENERIC_READ, WICDecodeMetadataCacheOnLoad, &wicBitmapDecoder));
		ReturnIfFailed(result, m_wicFactory->CreateDecoderFromFileHandle(reinterpret_cast<ULONG_PTR>(file), nullptr, WICDecodeMetadataCacheOnLoad, wicBitmapDecoder.put()));

		winrt::com_ptr<IWICBitmapFrameDecode> wicFrameDecode;
		ReturnIfFailed(result, wicBitmapDecoder->GetFrame(0, wicFrameDecode.put()));

		winrt::com_ptr<IWICFormatConverter> wicFormatConverter;
		ReturnIfFailed(result, m_wicFactory->CreateFormatConverter(wicFormatConverter.put()));
		ReturnIfFailed(result, wicFormatConverter->Initialize(wicFrameDecode.get(), GUID_WICPixelFormat32bppPBGRA, WICBitmapDitherTypeNone, nullptr, 0.f, WICBitmapPaletteTypeCustom));

		ReturnIfFailed(result, InternalDrawThumbnailPlaceholder(wicFormatConverter.get(), blurAmount, randomAccessStream, false));

		CloseHandle(file);

		return result;
	}

	HRESULT PlaceholderImageHelper::InternalDrawThumbnailPlaceholder(IVector<uint8_t> bytes, float blurAmount, IRandomAccessStream randomAccessStream)
	{
		auto lock = critical_section::scoped_lock(m_criticalSection);

		HRESULT result;
		winrt::com_ptr<IStream> stream;
		ReturnIfFailed(result, CreateStreamOverRandomAccessStream(winrt::get_unknown(randomAccessStream), IID_PPV_ARGS(&stream)));

		auto yolo = std::vector<byte>(bytes.begin(), bytes.end());

		ReturnIfFailed(result, stream->Write(yolo.data(), bytes.Size(), nullptr));
		ReturnIfFailed(result, stream->Seek({ 0 }, STREAM_SEEK_SET, nullptr));

		winrt::com_ptr<IWICBitmapDecoder> wicBitmapDecoder;
		ReturnIfFailed(result, m_wicFactory->CreateDecoderFromStream(stream.get(), nullptr, WICDecodeMetadataCacheOnLoad, wicBitmapDecoder.put()));

		winrt::com_ptr<IWICBitmapFrameDecode> wicFrameDecode;
		ReturnIfFailed(result, wicBitmapDecoder->GetFrame(0, wicFrameDecode.put()));

		winrt::com_ptr<IWICFormatConverter> wicFormatConverter;
		ReturnIfFailed(result, m_wicFactory->CreateFormatConverter(wicFormatConverter.put()));
		ReturnIfFailed(result, wicFormatConverter->Initialize(wicFrameDecode.get(), GUID_WICPixelFormat32bppPBGRA, WICBitmapDitherTypeNone, nullptr, 0.f, WICBitmapPaletteTypeCustom));

		ReturnIfFailed(result, InternalDrawThumbnailPlaceholder(wicFormatConverter.get(), blurAmount, randomAccessStream, true));

		return result;
	}

	HRESULT PlaceholderImageHelper::InternalDrawThumbnailPlaceholder(IWICBitmapSource* wicBitmapSource, float blurAmount, IRandomAccessStream randomAccessStream, bool minithumbnail)
	{
		HRESULT result;
		winrt::com_ptr<ID2D1ImageSourceFromWic> imageSource;
		ReturnIfFailed(result, m_d2dContext->CreateImageSourceFromWic(wicBitmapSource, imageSource.put()));

		D2D1_SIZE_U size;
		ReturnIfFailed(result, wicBitmapSource->GetSize(&size.width, &size.height));

		//if (minithumbnail) {
		//	size.width *= 2;
		//	size.height *= 2;
		//}

		winrt::com_ptr<ID2D1Bitmap1> targetBitmap;
		D2D1_BITMAP_PROPERTIES1 properties = { { DXGI_FORMAT_R8G8B8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED }, 96, 96, D2D1_BITMAP_OPTIONS_TARGET, 0 };
		ReturnIfFailed(result, m_d2dContext->CreateBitmap(size, nullptr, 0, &properties, targetBitmap.put()));

		//winrt::com_ptr<ID2D1Effect> scaleEffect;
		//ReturnIfFailed(result, m_d2dContext->CreateEffect(CLSID_D2D1Scale, scaleEffect.put()));
		//ReturnIfFailed(result, scaleEffect->SetValue(D2D1_SCALE_PROP_SCALE, D2D1_VECTOR_2F({ 2, 2 })));
		//ReturnIfFailed(result, scaleEffect->SetValue(D2D1_SCALE_PROP_INTERPOLATION_MODE, D2D1_SCALE_INTERPOLATION_MODE_NEAREST_NEIGHBOR));
		//scaleEffect->SetInput(0, imageSource.get());

		//winrt::com_ptr<ID2D1Image> test;
		//scaleEffect->SetInput(0, imageSource.get());
		//scaleEffect->GetOutput(test.put());

		ReturnIfFailed(result, m_gaussianBlurEffect->SetValue(D2D1_GAUSSIANBLUR_PROP_STANDARD_DEVIATION, blurAmount));

		//m_gaussianBlurEffect->SetInput(0, test.get());
		m_gaussianBlurEffect->SetInput(0, imageSource.get());

		m_d2dContext->SetTarget(targetBitmap.get());
		m_d2dContext->BeginDraw();
		//m_d2dContext->SetTransform(D2D1::Matrix3x2F::Identity());
		m_d2dContext->Clear(D2D1::ColorF(ColorF::Black, 0.0f));
		m_d2dContext->DrawImage(m_gaussianBlurEffect.get());

		if ((result = m_d2dContext->EndDraw()) == D2DERR_RECREATE_TARGET)
		{
			ReturnIfFailed(result, CreateDeviceResources());
			return InternalDrawThumbnailPlaceholder(wicBitmapSource, blurAmount, randomAccessStream, minithumbnail);
		}

		return SaveImageToStream(targetBitmap.get(), GUID_ContainerFormatPng, randomAccessStream);
	}

	PlaceholderImageHelper::PlaceholderImageHelper()
	{
		winrt::check_hresult(CreateDeviceIndependentResources());
		winrt::check_hresult(CreateDeviceResources());
	}

	HRESULT PlaceholderImageHelper::CreateDeviceIndependentResources()
	{
		HRESULT result;
		D2D1_FACTORY_OPTIONS options = {};
		ReturnIfFailed(result, D2D1CreateFactory(D2D1_FACTORY_TYPE_SINGLE_THREADED, __uuidof(ID2D1Factory1), &options, m_d2dFactory.put_void()));
		ReturnIfFailed(result, CoCreateInstance(CLSID_WICImagingFactory, nullptr, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(&m_wicFactory)));
		ReturnIfFailed(result, DWriteCreateFactory(DWRITE_FACTORY_TYPE_SHARED, __uuidof(IDWriteFactory), (IUnknown**)m_dwriteFactory.put()));



		hstring path = Package::Current().InstalledLocation().Path() + L"\\Assets\\Fonts\\Telegram.ttf";
		void const* key = path.begin();
		uint32_t keySize = static_cast<uint32_t>(std::distance(path.begin(), path.end()) * sizeof(wchar_t));

		m_customLoader = winrt::make_self<CustomFontLoader>();

		ReturnIfFailed(result, m_dwriteFactory->RegisterFontCollectionLoader(m_customLoader.get()));
		ReturnIfFailed(result, m_dwriteFactory->CreateCustomFontCollection(m_customLoader.get(), key, keySize, m_fontCollection.put()));

		path = Package::Current().InstalledLocation().Path() + L"\\Assets\\Emoji\\apple.ttf";
		key = path.begin();
		keySize = static_cast<uint32_t>(std::distance(path.begin(), path.end()) * sizeof(wchar_t));
		ReturnIfFailed(result, m_dwriteFactory->CreateCustomFontCollection(m_customLoader.get(), key, keySize, m_appleCollection.put()));

		ReturnIfFailed(result, m_dwriteFactory->CreateTextFormat(
			L"Telegram",							// font family name
			m_fontCollection.get(),					// system font collection
			DWRITE_FONT_WEIGHT_NORMAL,				// font weight 
			DWRITE_FONT_STYLE_NORMAL,				// font style
			DWRITE_FONT_STRETCH_NORMAL,				// default font stretch
			92.0f,									// font size
			L"",									// locale name
			m_symbolFormat.put()
		));
		ReturnIfFailed(result, m_symbolFormat->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_LEADING));
		ReturnIfFailed(result, m_symbolFormat->SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_NEAR));

		ReturnIfFailed(result, m_dwriteFactory->CreateTextFormat(
			L"Segoe MDL2 Assets",					// font family name
			nullptr,								// system font collection
			DWRITE_FONT_WEIGHT_NORMAL,				// font weight 
			DWRITE_FONT_STYLE_NORMAL,				// font style
			DWRITE_FONT_STRETCH_NORMAL,				// default font stretch
			82.0f,									// font size
			L"",									// locale name
			m_mdl2Format.put()
		));
		ReturnIfFailed(result, m_mdl2Format->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_LEADING));
		ReturnIfFailed(result, m_mdl2Format->SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_NEAR));

		ReturnIfFailed(result, m_dwriteFactory->CreateTextFormat(
			L"Segoe UI",							// font family name
			nullptr,								// system font collection
			DWRITE_FONT_WEIGHT_NORMAL,				// font weight 
			DWRITE_FONT_STYLE_NORMAL,				// font style
			DWRITE_FONT_STRETCH_NORMAL,				// default font stretch
			82.0f,									// font size
			L"",									// locale name
			m_textFormat.put()
		));
		ReturnIfFailed(result, m_textFormat->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_LEADING));
		return m_textFormat->SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_NEAR);
	}

	HRESULT PlaceholderImageHelper::CreateDeviceResources()
	{
		HRESULT result;
		UINT creationFlags = D3D11_CREATE_DEVICE_BGRA_SUPPORT;

		D3D_FEATURE_LEVEL featureLevels[] =
		{
			//D3D_FEATURE_LEVEL_12_2,
			//D3D_FEATURE_LEVEL_12_1,
			//D3D_FEATURE_LEVEL_12_0,
			D3D_FEATURE_LEVEL_11_1,
			D3D_FEATURE_LEVEL_11_0,
			D3D_FEATURE_LEVEL_10_1,
			D3D_FEATURE_LEVEL_10_0,
			D3D_FEATURE_LEVEL_9_3,
			D3D_FEATURE_LEVEL_9_2,
			D3D_FEATURE_LEVEL_9_1
		};

		winrt::com_ptr<ID3D11DeviceContext> context;
		ReturnIfFailed(result, D3D11CreateDevice(nullptr,	// specify null to use the default adapter
			D3D_DRIVER_TYPE_HARDWARE, 0,
			creationFlags,									// optionally set debug and Direct2D compatibility flags
			featureLevels,									// list of feature levels this app can support
			ARRAYSIZE(featureLevels),						// number of possible feature levels
			D3D11_SDK_VERSION,
			m_d3dDevice.put(),								// returns the Direct3D device created
			&m_featureLevel,								// returns feature level of device created
			context.put()									// returns the device immediate context
		));

		winrt::com_ptr<IDXGIDevice> dxgiDevice = m_d3dDevice.as<IDXGIDevice>();
		ReturnIfFailed(result, m_d2dFactory->CreateDevice(dxgiDevice.get(), m_d2dDevice.put()));

		winrt::com_ptr<ID2D1DeviceContext> d2dContext;
		ReturnIfFailed(result, m_d2dDevice->CreateDeviceContext(D2D1_DEVICE_CONTEXT_OPTIONS_NONE, d2dContext.put()));
		m_d2dContext = d2dContext.as<ID2D1DeviceContext2>();

		ReturnIfFailed(result, m_d2dContext->CreateSolidColorBrush(D2D1::ColorF(D2D1::ColorF::White), m_textBrush.put()));
		ReturnIfFailed(result, m_d2dContext->CreateSolidColorBrush(D2D1::ColorF(D2D1::ColorF::Black), m_black.put()));
		ReturnIfFailed(result, m_d2dContext->CreateSolidColorBrush(D2D1::ColorF(D2D1::ColorF::White), m_transparent.put()));
		ReturnIfFailed(result, m_d2dContext->CreateEffect(CLSID_D2D1GaussianBlur, m_gaussianBlurEffect.put()));
		ReturnIfFailed(result, m_gaussianBlurEffect->SetValue(D2D1_GAUSSIANBLUR_PROP_BORDER_MODE, D2D1_BORDER_MODE_HARD));

		/*            Color.FromArgb(0xff, 0xff, 0xff, 0xff),
				Color.FromArgb(0xff, 0xd5, 0xe6, 0xf3),
				Color.FromArgb(0xff, 0x2d, 0x57, 0x75),
				Color.FromArgb(0xff, 0x2f, 0x99, 0xc9)
	*/

		winrt::com_ptr<ID2D1SolidColorBrush> color1;
		winrt::com_ptr<ID2D1SolidColorBrush> color2;
		winrt::com_ptr<ID2D1SolidColorBrush> color3;
		winrt::com_ptr<ID2D1SolidColorBrush> color4;
		ReturnIfFailed(result, m_d2dContext->CreateSolidColorBrush(D2D1::ColorF(D2D1::ColorF::White), color1.put()));
		ReturnIfFailed(result, m_d2dContext->CreateSolidColorBrush(D2D1::ColorF(0xd5 / 255.0f, 0xe6 / 255.0f, 0xf3 / 255.0f, 1.0f), color2.put()));
		ReturnIfFailed(result, m_d2dContext->CreateSolidColorBrush(D2D1::ColorF(0x2d / 255.0f, 0x57 / 255.0f, 0x75 / 255.0f, 1.0f), color3.put()));
		ReturnIfFailed(result, m_d2dContext->CreateSolidColorBrush(D2D1::ColorF(0x2f / 255.0f, 0x99 / 255.0f, 0xc9 / 255.0f, 1.0f), color4.put()));

		m_identiconBrushes.push_back(color1);
		m_identiconBrushes.push_back(color2);
		m_identiconBrushes.push_back(color3);
		m_identiconBrushes.push_back(color4);

		D2D1_SIZE_U size = { 192, 192 };
		D2D1_BITMAP_PROPERTIES1 properties = { { DXGI_FORMAT_R8G8B8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED }, 96, 96, D2D1_BITMAP_OPTIONS_TARGET, 0 };
		ReturnIfFailed(result, m_d2dContext->CreateBitmap(size, nullptr, 0, &properties, m_targetBitmap.put()));

		m_d2dContext->SetAntialiasMode(D2D1_ANTIALIAS_MODE_PER_PRIMITIVE);

		return m_wicFactory->CreateImageEncoder(m_d2dDevice.get(), m_imageEncoder.put());
	}

	HRESULT PlaceholderImageHelper::MeasureText(const wchar_t* text, IDWriteTextFormat* format, DWRITE_TEXT_METRICS* textMetrics)
	{
		HRESULT result;
		winrt::com_ptr<IDWriteTextLayout> textLayout;
		ReturnIfFailed(result, m_dwriteFactory->CreateTextLayout(
			text,							// The string to be laid out and formatted.
			wcslen(text),					// The length of the string.
			format,							// The text format to apply to the string (contains font information, etc).
			192.0f,							// The width of the layout box.
			192.0f,							// The height of the layout box.
			textLayout.put()				// The IDWriteTextLayout interface pointer.
		));

		return textLayout->GetMetrics(textMetrics);
	}

	float2 PlaceholderImageHelper::ContentEnd(hstring text, double fontSize, double width)
	{
		winrt::check_hresult(m_dwriteFactory->CreateTextFormat(
			L"Segoe UI Emoji",						// font family name
			m_appleCollection.get(),				// system font collection
			DWRITE_FONT_WEIGHT_NORMAL,				// font weight 
			DWRITE_FONT_STYLE_NORMAL,				// font style
			DWRITE_FONT_STRETCH_NORMAL,				// default font stretch
			fontSize,								// font size
			L"",									// locale name
			m_appleFormat.put()
		));
		winrt::check_hresult(m_appleFormat->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_LEADING));
		winrt::check_hresult(m_appleFormat->SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_NEAR));

		winrt::com_ptr<IDWriteTextLayout> textLayout;
		winrt::check_hresult(m_dwriteFactory->CreateTextLayout(
			text.data(),					// The string to be laid out and formatted.
			wcslen(text.data()),			// The length of the string.
			m_appleFormat.get(),			// The text format to apply to the string (contains font information, etc).
			width,							// The width of the layout box.
			INFINITY,						// The height of the layout box.
			textLayout.put()				// The IDWriteTextLayout interface pointer.
		));

		FLOAT x;
		FLOAT y;
		DWRITE_HIT_TEST_METRICS metrics;
		textLayout->HitTestTextPosition(text.size() - 1, false, &x, &y, &metrics);

		return float2(metrics.left + metrics.width, metrics.top + metrics.height);
	}

	IVector<Windows::Foundation::Rect> PlaceholderImageHelper::LineMetrics(hstring text, double fontSize, double width, bool rtl)
	{
		winrt::check_hresult(m_dwriteFactory->CreateTextFormat(
			L"Segoe UI Emoji",						// font family name
			m_appleCollection.get(),				// system font collection
			DWRITE_FONT_WEIGHT_NORMAL,				// font weight 
			DWRITE_FONT_STYLE_NORMAL,				// font style
			DWRITE_FONT_STRETCH_NORMAL,				// default font stretch
			fontSize,								// font size
			L"",									// locale name
			m_appleFormat.put()
		));
		winrt::check_hresult(m_appleFormat->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_LEADING));
		winrt::check_hresult(m_appleFormat->SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_NEAR));
		winrt::check_hresult(m_appleFormat->SetReadingDirection(rtl ? DWRITE_READING_DIRECTION_RIGHT_TO_LEFT : DWRITE_READING_DIRECTION_LEFT_TO_RIGHT));

		winrt::com_ptr<IDWriteTextLayout> textLayout;
		winrt::check_hresult(m_dwriteFactory->CreateTextLayout(
			text.data(),					// The string to be laid out and formatted.
			wcslen(text.data()),			// The length of the string.
			m_appleFormat.get(),			// The text format to apply to the string (contains font information, etc).
			width,							// The width of the layout box.
			INFINITY,						// The height of the layout box.
			textLayout.put()				// The IDWriteTextLayout interface pointer.
		));

		DWRITE_TEXT_METRICS metrics;
		winrt::check_hresult(textLayout->GetMetrics(&metrics));

		UINT32 maxHitTestMetricsCount = metrics.lineCount * metrics.maxBidiReorderingDepth;
		UINT32 actualTestsCount;
		DWRITE_HIT_TEST_METRICS* ranges = new DWRITE_HIT_TEST_METRICS[maxHitTestMetricsCount];
		winrt::check_hresult(textLayout->HitTestTextRange(0, text.size(), 0, 0, ranges, maxHitTestMetricsCount, &actualTestsCount));

		std::vector<Windows::Foundation::Rect> rects;

		for (int i = 0; i < actualTestsCount; i++) {
			float left = ranges[i].left;
			float top = ranges[i].top;
			float right = ranges[i].left + ranges[i].width;
			float bottom = ranges[i].top + ranges[i].height;

			rects.push_back({ left, top, right - left, bottom - top });
		}

		return winrt::single_threaded_vector<Windows::Foundation::Rect>(std::move(rects));
	}

	//IVector<Windows::Foundation::Rect> PlaceholderImageHelper::EntityMetrics(hstring text, IVector<TextEntity> entities, double fontSize, double width, bool rtl)
	//{
	//	winrt::check_hresult(m_dwriteFactory->CreateTextFormat(
	//		L"Segoe UI Emoji",						// font family name
	//		m_appleCollection.get(),				// system font collection
	//		DWRITE_FONT_WEIGHT_NORMAL,				// font weight 
	//		DWRITE_FONT_STYLE_NORMAL,				// font style
	//		DWRITE_FONT_STRETCH_NORMAL,				// default font stretch
	//		fontSize,								// font size
	//		L"",									// locale name
	//		m_appleFormat.put()
	//	));
	//	winrt::check_hresult(m_appleFormat->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_LEADING));
	//	winrt::check_hresult(m_appleFormat->SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_NEAR));
	//	winrt::check_hresult(m_appleFormat->SetReadingDirection(rtl ? DWRITE_READING_DIRECTION_RIGHT_TO_LEFT : DWRITE_READING_DIRECTION_LEFT_TO_RIGHT));

	//	winrt::com_ptr<IDWriteTextLayout> textLayout;
	//	winrt::check_hresult(m_dwriteFactory->CreateTextLayout(
	//		text.data(),					// The string to be laid out and formatted.
	//		wcslen(text.data()),			// The length of the string.
	//		m_appleFormat.get(),			// The text format to apply to the string (contains font information, etc).
	//		width,							// The width of the layout box.
	//		INFINITY,						// The height of the layout box.
	//		textLayout.put()				// The IDWriteTextLayout interface pointer.
	//	));

	//	DWRITE_TEXT_METRICS metrics;
	//	winrt::check_hresult(textLayout->GetMetrics(&metrics));

	//	UINT32 maxHitTestMetricsCount = metrics.lineCount * metrics.maxBidiReorderingDepth;
	//	DWRITE_HIT_TEST_METRICS* ranges = new DWRITE_HIT_TEST_METRICS[maxHitTestMetricsCount];

	//	std::vector<Windows::Foundation::Rect> rects;

	//	for (const TextEntity& entity : entities) {
	//		auto spoiler = entity.Type().try_as<TextEntityTypeSpoiler>();
	//		if (spoiler != nullptr) {
	//			UINT32 actualTestsCount;
	//			winrt::check_hresult(textLayout->HitTestTextRange(entity.Offset(), entity.Length(), 0, 0, ranges, maxHitTestMetricsCount, &actualTestsCount));

	//			for (int i = 0; i < actualTestsCount; i++) {
	//				float left = ranges[i].left;
	//				float top = ranges[i].top;
	//				float right = ranges[i].left + ranges[i].width;
	//				float bottom = ranges[i].top + ranges[i].height;

	//				rects.push_back({ left, top, right - left, bottom - top });
	//			}
	//		}
	//	}

	//	return winrt::single_threaded_vector<Windows::Foundation::Rect>(std::move(rects));
	//}

	void PlaceholderImageHelper::WriteBytes(IVector<byte> hash, IRandomAccessStream randomAccessStream)
	{
		winrt::com_ptr<IStream> stream;
		winrt::check_hresult(CreateStreamOverRandomAccessStream(winrt::get_unknown(randomAccessStream), IID_PPV_ARGS(&stream)));

		auto yolo = std::vector<byte>(hash.begin(), hash.end());

		winrt::check_hresult(stream->Write(yolo.data(), hash.Size(), nullptr));
		winrt::check_hresult(stream->Seek({ 0 }, STREAM_SEEK_SET, nullptr));
	}

	HRESULT PlaceholderImageHelper::SaveImageToStream(ID2D1Image* image, REFGUID wicFormat, IRandomAccessStream randomAccessStream)
	{
		HRESULT result;
		winrt::com_ptr<IStream> stream;
		ReturnIfFailed(result, CreateStreamOverRandomAccessStream(winrt::get_unknown(randomAccessStream), IID_PPV_ARGS(&stream)));

		if (randomAccessStream.Size()) {
			stream->SetSize({ 0 });
		}

		winrt::com_ptr<IWICBitmapEncoder> wicBitmapEncoder;
		ReturnIfFailed(result, m_wicFactory->CreateEncoder(wicFormat, nullptr, wicBitmapEncoder.put()));
		ReturnIfFailed(result, wicBitmapEncoder->Initialize(stream.get(), WICBitmapEncoderNoCache));

		winrt::com_ptr<IWICBitmapFrameEncode> wicFrameEncode;
		ReturnIfFailed(result, wicBitmapEncoder->CreateNewFrame(wicFrameEncode.put(), nullptr));
		ReturnIfFailed(result, wicFrameEncode->Initialize(nullptr));

		ReturnIfFailed(result, m_imageEncoder->WriteFrame(image, wicFrameEncode.get(), nullptr));
		ReturnIfFailed(result, wicFrameEncode->Commit());
		ReturnIfFailed(result, wicBitmapEncoder->Commit());

		ReturnIfFailed(result, stream->Commit(STGC_DEFAULT));

		return stream->Seek({ 0 }, STREAM_SEEK_SET, nullptr);
	}
} // namespace winrt::Unigram::Native::implementation