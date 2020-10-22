#include "pch.h"
#include <ShCore.h>
#include "PlaceholderImageHelper.h"
#include "Qr/QrCode.hpp"
#include "SVG/nanosvg.h"
#include "StringUtils.h"

using namespace D2D1;
using namespace Platform;
using namespace Windows::ApplicationModel;
using namespace Windows::Foundation::Collections;
using namespace Windows::UI::ViewManagement;
using namespace Windows::UI::Xaml;
using namespace Windows::Storage;
using namespace Unigram::Native;
using namespace qrcodegen;

std::map<int, WeakReference> PlaceholderImageHelper::s_windowContext;

CriticalSection PlaceholderImageHelper::s_criticalSection;
PlaceholderImageHelper^ PlaceholderImageHelper::s_current = nullptr;

class CustomFontFileEnumerator
	: public RuntimeClass<RuntimeClassFlags<ClassicCom>, IDWriteFontFileEnumerator>
{
	ComPtr<IDWriteFactory> m_factory;
	std::wstring m_filename;
	ComPtr<IDWriteFontFile> m_theFile;

public:
	CustomFontFileEnumerator(IDWriteFactory* factory, void const* collectionKey, uint32_t collectionKeySize)
		: m_factory(factory)
		, m_filename(static_cast<wchar_t const*>(collectionKey), collectionKeySize / 2)
	{
	}

	IFACEMETHODIMP MoveNext(BOOL* hasCurrentFile) override
	{
		if (m_theFile)
		{
			*hasCurrentFile = FALSE;
		}
		else if (SUCCEEDED(m_factory->CreateFontFileReference(m_filename.c_str(), nullptr, &m_theFile)))
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
		return m_theFile.CopyTo(fontFile);
	}
};



class CustomFontLoader
	: public RuntimeClass<RuntimeClassFlags<ClassicCom>, IDWriteFontCollectionLoader>
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
			auto enumerator = Make<CustomFontFileEnumerator>(factory, collectionKey, collectionKeySize);
			CheckMakeResult(enumerator);
			ThrowIfFailed(enumerator.CopyTo(fontFileEnumerator));
		});
	}
};

PlaceholderImageHelper^ PlaceholderImageHelper::GetForCurrentView()
{
	auto id = ApplicationView::GetApplicationViewIdForWindow(Window::Current->CoreWindow);
	auto reference = s_windowContext.find(id);

	if (reference != s_windowContext.end())
	{
		auto instance = reference->second.Resolve<PlaceholderImageHelper>();
		if (instance != nullptr)
		{
			return instance;
		}
	}

	auto instance = ref new PlaceholderImageHelper();
	WeakReference result(instance);
	s_windowContext[id] = result;

	return instance;
}

Windows::Foundation::Size PlaceholderImageHelper::DrawSvg(String^ path, _In_ Color foreground, IRandomAccessStream^ randomAccessStream)
{
	Windows::Foundation::Size size;
	ThrowIfFailed(InternalDrawSvg(path, foreground, randomAccessStream, size));
	return size;
}

void PlaceholderImageHelper::DrawQr(String^ data, _In_ Color foreground, _In_ Color background, IRandomAccessStream^ randomAccessStream)
{
	ThrowIfFailed(InternalDrawQr(data, foreground, background, randomAccessStream));
}

void PlaceholderImageHelper::DrawIdenticon(IVector<uint8>^ hash, int side, IRandomAccessStream^ randomAccessStream)
{
	ThrowIfFailed(InternalDrawIdenticon(hash, side, randomAccessStream));
}

void PlaceholderImageHelper::DrawGlyph(String^ text, Color clear, IRandomAccessStream^ randomAccessStream)
{
	ThrowIfFailed(InternalDrawGlyph(text, clear, randomAccessStream));
}

void PlaceholderImageHelper::DrawSavedMessages(Color clear, IRandomAccessStream^ randomAccessStream)
{
	ThrowIfFailed(InternalDrawSavedMessages(clear, randomAccessStream));
}

void PlaceholderImageHelper::DrawDeletedUser(Color clear, IRandomAccessStream^ randomAccessStream)
{
	ThrowIfFailed(InternalDrawDeletedUser(clear, randomAccessStream));
}

void PlaceholderImageHelper::DrawProfilePlaceholder(Color clear, Platform::String^ text, IRandomAccessStream^ randomAccessStream)
{
	ThrowIfFailed(InternalDrawProfilePlaceholder(clear, text, randomAccessStream));
}

void PlaceholderImageHelper::DrawThumbnailPlaceholder(Platform::String^ fileName, float blurAmount, IRandomAccessStream^ randomAccessStream)
{
	ThrowIfFailed(InternalDrawThumbnailPlaceholder(fileName, blurAmount, randomAccessStream));
}

HRESULT PlaceholderImageHelper::InternalDrawSvg(String^ path, _In_ Color foreground, IRandomAccessStream^ randomAccessStream, Windows::Foundation::Size& size)
{
	auto lock = m_criticalSection.Lock();

	HRESULT result;

	auto data = string_to_unmanaged(path);

	struct NSVGimage* image;
	image = nsvgParse((char*)data.c_str(), "px", 96);

	size = Windows::Foundation::Size(image->width, image->height);

	ComPtr<ID2D1Bitmap1> targetBitmap;
	D2D1_BITMAP_PROPERTIES1 properties = { { DXGI_FORMAT_R8G8B8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED }, 96, 96, D2D1_BITMAP_OPTIONS_TARGET, 0 };
	ReturnIfFailed(result, m_d2dContext->CreateBitmap(D2D1_SIZE_U{ (uint32_t)image->width, (uint32_t)image->height }, nullptr, 0, &properties, &targetBitmap));

	m_d2dContext->SetTarget(targetBitmap.Get());
	m_d2dContext->BeginDraw();

	ComPtr<ID2D1SolidColorBrush> blackBrush;
	ReturnIfFailed(result, m_d2dContext->CreateSolidColorBrush(
		D2D1::ColorF(foreground.R / 255.0f, foreground.G / 255.0f, foreground.B / 255.0f, foreground.A / 255.0f), &blackBrush));

	for (auto shape = image->shapes; shape != NULL; shape = shape->next)
	{
		if (!(shape->flags & NSVG_FLAGS_VISIBLE) || (shape->fill.type == NSVG_PAINT_NONE && shape->stroke.type == NSVG_PAINT_NONE))
		{
			continue;
		}

		blackBrush->SetOpacity(shape->opacity);

		ComPtr<ID2D1PathGeometry1> geometry;
		ReturnIfFailed(result, m_d2dFactory->CreatePathGeometry(&geometry));

		ComPtr<ID2D1GeometrySink> sink;
		ReturnIfFailed(result, geometry->Open(&sink));

		for (NSVGpath* path = shape->paths; path != NULL; path = path->next)
		{
			sink->BeginFigure({ path->pts[0], path->pts[1] }, D2D1_FIGURE_BEGIN_FILLED);

			for (int i = 0; i < path->npts - 1; i += 3)
			{
				float* p = &path->pts[i * 2];
				sink->AddBezier({ { p[2], p[3] }, { p[4], p[5] }, { p[6], p[7] }});
			}

			sink->EndFigure(path->closed ? D2D1_FIGURE_END_CLOSED : D2D1_FIGURE_END_OPEN);
		}

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

			ReturnIfFailed(result, sink->Close());
			m_d2dContext->FillGeometry(geometry.Get(), blackBrush.Get());
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

			ComPtr<ID2D1StrokeStyle1> strokeStyle;
			ReturnIfFailed(result, m_d2dFactory->CreateStrokeStyle(strokeProperties, NULL, 0, &strokeStyle));


			ReturnIfFailed(result, sink->Close());
			m_d2dContext->DrawGeometry(geometry.Get(), blackBrush.Get(), shape->strokeWidth, strokeStyle.Get());
		}
	}

	nsvgDelete(image);

	if ((result = m_d2dContext->EndDraw()) == D2DERR_RECREATE_TARGET)
	{
		ReturnIfFailed(result, CreateDeviceResources());
		return InternalDrawSvg(path, foreground, randomAccessStream, size);
	}

	return SaveImageToStream(targetBitmap.Get(), GUID_ContainerFormatPng, randomAccessStream);
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

HRESULT PlaceholderImageHelper::InternalDrawQr(String^ text, _In_ Color foreground, _In_ Color background, IRandomAccessStream^ randomAccessStream)
{
	auto lock = m_criticalSection.Lock();

	HRESULT result;

	ComPtr<ID2D1Bitmap1> targetBitmap;
	D2D1_BITMAP_PROPERTIES1 properties = { { DXGI_FORMAT_R8G8B8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED }, 96, 96, D2D1_BITMAP_OPTIONS_TARGET, 0 };
	ReturnIfFailed(result, m_d2dContext->CreateBitmap(D2D1_SIZE_U{ kShareQrSize - 4 * kShareQrPadding, kShareQrSize - 4 * kShareQrPadding }, nullptr, 0, &properties, &targetBitmap));


	m_d2dContext->SetTarget(targetBitmap.Get());
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

		ComPtr<ID2D1SolidColorBrush> blackBrush;
		ReturnIfFailed(result, m_d2dContext->CreateSolidColorBrush(
			D2D1::ColorF(foreground.R / 255.0f, foreground.G / 255.0f, foreground.B / 255.0f, foreground.A / 255.0f), &blackBrush));

		ComPtr<ID2D1SolidColorBrush> whiteBrush;
		ReturnIfFailed(result, m_d2dContext->CreateSolidColorBrush(
			D2D1::ColorF(background.R / 255.0f, background.G / 255.0f, background.B / 255.0f, background.A / 255.0f), &whiteBrush));

		const auto skip = pixel - pixel / 2;
		const auto brect = [&](float x, float y, float width, float height) {
			context->FillRectangle(D2D1_RECT_F{ x, y, x + width, y + height }, blackBrush.Get());
		};
		const auto wrect = [&](float x, float y, float width, float height) {
			context->FillRectangle(D2D1_RECT_F{ x, y, x + width, y + height }, whiteBrush.Get());
		};
		const auto large = [&](float x, float y) {
			context->FillRoundedRectangle(D2D1_ROUNDED_RECT{ D2D1_RECT_F{
				x,
				y,
				x + pixel * 7,
				y + pixel * 7 }, pixel * 2.0f, pixel * 2.0f }, blackBrush.Get());
			context->FillRoundedRectangle(D2D1_ROUNDED_RECT{ D2D1_RECT_F{
				x + pixel,
				y + pixel,
				x + pixel + pixel * 5,
				y + pixel + pixel * 5 }, pixel * 1.5f, pixel * 1.5f }, whiteBrush.Get());
			context->FillRoundedRectangle(D2D1_ROUNDED_RECT{ D2D1_RECT_F{
				x + pixel * 2,
				y + pixel * 2,
				x + pixel * 2 + pixel * 3,
				y + pixel * 2 + pixel * 3 }, (float)pixel, (float)pixel }, blackBrush.Get());
		};
		const auto white = [&](float x, float y) {
			context->FillRectangle(D2D1_RECT_F{ x, y, x + pixel, y + pixel }, blackBrush.Get());
			context->FillRoundedRectangle(D2D1_ROUNDED_RECT{ D2D1_RECT_F{ x, y, x + pixel, y + pixel }, pixel / 2.0f, pixel / 2.0f }, whiteBrush.Get());
		};
		const auto black = [&](float x, float y) {
			context->FillRectangle(D2D1_RECT_F{ x, y, x + pixel, y + pixel }, whiteBrush.Get());
			context->FillRoundedRectangle(D2D1_ROUNDED_RECT{ D2D1_RECT_F{ x, y, x + pixel, y + pixel }, pixel / 2.0f, pixel / 2.0f }, blackBrush.Get());
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
	//ComPtr<ID2D1SolidColorBrush> red;
	//ReturnIfFailed(result, m_d2dContext->CreateSolidColorBrush(D2D1::ColorF(D2D1::ColorF::Red), &red));
	//m_d2dContext->FillRectangle(D2D1_RECT_F{ x1, x1, x1 + diamond, x1 + diamond }, red.Get());

	if ((result = m_d2dContext->EndDraw()) == D2DERR_RECREATE_TARGET)
	{
		ReturnIfFailed(result, CreateDeviceResources());
		return InternalDrawQr(text, foreground, background, randomAccessStream);
	}

	return SaveImageToStream(targetBitmap.Get(), GUID_ContainerFormatPng, randomAccessStream);
}

HRESULT PlaceholderImageHelper::InternalDrawIdenticon(IVector<uint8>^ hash, int side, IRandomAccessStream^ randomAccessStream)
{
	auto lock = m_criticalSection.Lock();

	HRESULT result;

	m_d2dContext->SetTarget(m_targetBitmap.Get());
	m_d2dContext->BeginDraw();

	auto width = side;
	auto height = side;

	if (hash->Size == 16)
	{
		int bitPointer = 0;
		float rectSize = (float)std::floor(std::min(width, height) / 8.0f);
		float xOffset = std::max(0.0f, (width - rectSize * 8) / 2);
		float yOffset = std::max(0.0f, (height - rectSize * 8) / 2);
		for (int iy = 0; iy < 8; iy++)
		{
			for (int ix = 0; ix < 8; ix++)
			{
				int byteValue = (hash->GetAt(bitPointer / 8) >> (bitPointer % 8)) & 0x3;
				bitPointer += 2;
				int colorIndex = std::abs(byteValue) % 4;
				D2D1_RECT_F layoutRect = { (int)(xOffset + ix * rectSize), (int)(iy * rectSize + yOffset), (int)(xOffset + ix * rectSize + rectSize), (int)(iy * rectSize + rectSize + yOffset) };
				m_d2dContext->FillRectangle(layoutRect, m_identiconBrushes[colorIndex].Get());
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
				int byteValue = (hash->GetAt(bitPointer / 8) >> (bitPointer % 8)) & 0x3;
				int colorIndex = std::abs(byteValue) % 4;
				D2D1_RECT_F layoutRect = { (int)(xOffset + ix * rectSize), (int)(iy * rectSize + yOffset), (int)(xOffset + ix * rectSize + rectSize), (int)(iy * rectSize + rectSize + yOffset) };
				m_d2dContext->FillRectangle(layoutRect, m_identiconBrushes[colorIndex].Get());
				bitPointer += 2;
			}
		}
	}

	if ((result = m_d2dContext->EndDraw()) == D2DERR_RECREATE_TARGET)
	{
		ReturnIfFailed(result, CreateDeviceResources());
		return InternalDrawIdenticon(hash, side, randomAccessStream);
	}

	return SaveImageToStream(m_targetBitmap.Get(), GUID_ContainerFormatPng, randomAccessStream);
}

HRESULT PlaceholderImageHelper::InternalDrawGlyph(String^ glyph, Color clear, IRandomAccessStream^ randomAccessStream)
{
	auto lock = m_criticalSection.Lock();
	auto text = glyph->Data();

	HRESULT result;
	DWRITE_TEXT_METRICS textMetrics;
	ReturnIfFailed(result, MeasureText(text, m_mdl2Format.Get(), &textMetrics));

	m_d2dContext->SetTarget(m_targetBitmap.Get());
	m_d2dContext->BeginDraw();
	//m_d2dContext->SetTransform(D2D1::Matrix3x2F::Identity());
	m_d2dContext->Clear(D2D1::ColorF(clear.R / 255.0f, clear.G / 255.0f, clear.B / 255.0f, clear.A / 255.0f));

	D2D1_RECT_F layoutRect = { (192.0f - textMetrics.width) / 2.0f, (192.0f - textMetrics.height) / 2.0f, 192.0f, 192.0f };
	m_d2dContext->DrawText(text, 1, m_mdl2Format.Get(), &layoutRect, m_textBrush.Get());

	if ((result = m_d2dContext->EndDraw()) == D2DERR_RECREATE_TARGET)
	{
		ReturnIfFailed(result, CreateDeviceResources());
		return InternalDrawSavedMessages(clear, randomAccessStream);
	}

	return SaveImageToStream(m_targetBitmap.Get(), GUID_ContainerFormatPng, randomAccessStream);
}

HRESULT PlaceholderImageHelper::InternalDrawSavedMessages(Color clear, IRandomAccessStream^ randomAccessStream)
{
	auto lock = m_criticalSection.Lock();
	auto text = L"\uE907";

	HRESULT result;
	DWRITE_TEXT_METRICS textMetrics;
	ReturnIfFailed(result, MeasureText(text, m_symbolFormat.Get(), &textMetrics));

	m_d2dContext->SetTarget(m_targetBitmap.Get());
	m_d2dContext->BeginDraw();
	//m_d2dContext->SetTransform(D2D1::Matrix3x2F::Identity());
	m_d2dContext->Clear(D2D1::ColorF(clear.R / 255.0f, clear.G / 255.0f, clear.B / 255.0f, clear.A / 255.0f));

	D2D1_RECT_F layoutRect = { (192.0f - textMetrics.width) / 2.0f, (192.0f - textMetrics.height) / 2.0f, 192.0f, 192.0f };
	m_d2dContext->DrawText(text, 1, m_symbolFormat.Get(), &layoutRect, m_textBrush.Get());

	if ((result = m_d2dContext->EndDraw()) == D2DERR_RECREATE_TARGET)
	{
		ReturnIfFailed(result, CreateDeviceResources());
		return InternalDrawSavedMessages(clear, randomAccessStream);
	}

	return SaveImageToStream(m_targetBitmap.Get(), GUID_ContainerFormatPng, randomAccessStream);
}

HRESULT PlaceholderImageHelper::InternalDrawDeletedUser(Color clear, IRandomAccessStream^ randomAccessStream)
{
	auto lock = m_criticalSection.Lock();
	auto text = L"\uE91A";

	HRESULT result;
	DWRITE_TEXT_METRICS textMetrics;
	ReturnIfFailed(result, MeasureText(text, m_symbolFormat.Get(), &textMetrics));

	m_d2dContext->SetTarget(m_targetBitmap.Get());
	m_d2dContext->BeginDraw();
	//m_d2dContext->SetTransform(D2D1::Matrix3x2F::Identity());
	m_d2dContext->Clear(D2D1::ColorF(clear.R / 255.0f, clear.G / 255.0f, clear.B / 255.0f, clear.A / 255.0f));

	D2D1_RECT_F layoutRect = { (192.0f - textMetrics.width) / 2.0f, (184.0f - textMetrics.height) / 2.0f, 192.0f, 192.0f };
	m_d2dContext->DrawText(text, 1, m_symbolFormat.Get(), &layoutRect, m_textBrush.Get());

	if ((result = m_d2dContext->EndDraw()) == D2DERR_RECREATE_TARGET)
	{
		ReturnIfFailed(result, CreateDeviceResources());
		return InternalDrawDeletedUser(clear, randomAccessStream);
	}

	return SaveImageToStream(m_targetBitmap.Get(), GUID_ContainerFormatPng, randomAccessStream);
}

HRESULT PlaceholderImageHelper::InternalDrawProfilePlaceholder(Color clear, Platform::String^ text, IRandomAccessStream^ randomAccessStream)
{
	auto lock = m_criticalSection.Lock();

	HRESULT result;
	DWRITE_TEXT_METRICS textMetrics;
	ReturnIfFailed(result, MeasureText(text->Data(), m_textFormat.Get(), &textMetrics));

	m_d2dContext->SetTarget(m_targetBitmap.Get());
	m_d2dContext->BeginDraw();
	//m_d2dContext->SetTransform(D2D1::Matrix3x2F::Identity());
	m_d2dContext->Clear(D2D1::ColorF(clear.R / 255.0f, clear.G / 255.0f, clear.B / 255.0f, clear.A / 255.0f));

	D2D1_RECT_F layoutRect = { (192.0f - textMetrics.width) / 2.0f, (180.0f - textMetrics.height) / 2.0f, 192.0f, 192.0f };
	m_d2dContext->DrawText(text->Data(), text->Length(), m_textFormat.Get(), &layoutRect, m_textBrush.Get());

	if ((result = m_d2dContext->EndDraw()) == D2DERR_RECREATE_TARGET)
	{
		ReturnIfFailed(result, CreateDeviceResources());
		return InternalDrawProfilePlaceholder(clear, text, randomAccessStream);
	}

	return SaveImageToStream(m_targetBitmap.Get(), GUID_ContainerFormatPng, randomAccessStream);
}

HRESULT PlaceholderImageHelper::InternalDrawThumbnailPlaceholder(Platform::String^ fileName, float blurAmount, IRandomAccessStream^ randomAccessStream)
{
	auto lock = m_criticalSection.Lock();

	HANDLE file = CreateFile2(fileName->Data(), GENERIC_READ, FILE_SHARE_READ, OPEN_EXISTING, nullptr);

	if (file == INVALID_HANDLE_VALUE)
	{
		return ERROR_FILE_NOT_FOUND;
	}

	HRESULT result;
	ComPtr<IWICBitmapDecoder> wicBitmapDecoder;
	//ReturnIfFailed(result, m_wicFactory->CreateDecoderFromFilename(fileName->Data(), nullptr, GENERIC_READ, WICDecodeMetadataCacheOnLoad, &wicBitmapDecoder));
	ReturnIfFailed(result, m_wicFactory->CreateDecoderFromFileHandle(reinterpret_cast<ULONG_PTR>(file), nullptr, WICDecodeMetadataCacheOnLoad, &wicBitmapDecoder));

	ComPtr<IWICBitmapFrameDecode> wicFrameDecode;
	ReturnIfFailed(result, wicBitmapDecoder->GetFrame(0, &wicFrameDecode));

	ComPtr<IWICFormatConverter> wicFormatConverter;
	ReturnIfFailed(result, m_wicFactory->CreateFormatConverter(&wicFormatConverter));
	ReturnIfFailed(result, wicFormatConverter->Initialize(wicFrameDecode.Get(), GUID_WICPixelFormat32bppPBGRA, WICBitmapDitherTypeNone, nullptr, 0.f, WICBitmapPaletteTypeCustom));

	ReturnIfFailed(result, InternalDrawThumbnailPlaceholder(wicFormatConverter.Get(), blurAmount, randomAccessStream));

	CloseHandle(file);

	return result;
}

HRESULT PlaceholderImageHelper::InternalDrawThumbnailPlaceholder(IWICBitmapSource* wicBitmapSource, float blurAmount, IRandomAccessStream^ randomAccessStream)
{
	HRESULT result;
	ComPtr<ID2D1ImageSourceFromWic> imageSource;
	ReturnIfFailed(result, m_d2dContext->CreateImageSourceFromWic(wicBitmapSource, &imageSource));

	D2D1_SIZE_U size;
	ReturnIfFailed(result, wicBitmapSource->GetSize(&size.width, &size.height));

	ComPtr<ID2D1Bitmap1> targetBitmap;
	D2D1_BITMAP_PROPERTIES1 properties = { { DXGI_FORMAT_R8G8B8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED }, 96, 96, D2D1_BITMAP_OPTIONS_TARGET, 0 };
	ReturnIfFailed(result, m_d2dContext->CreateBitmap(size, nullptr, 0, &properties, &targetBitmap));

	ReturnIfFailed(result, m_gaussianBlurEffect->SetValue(D2D1_GAUSSIANBLUR_PROP_STANDARD_DEVIATION, blurAmount));

	m_gaussianBlurEffect->SetInput(0, imageSource.Get());

	m_d2dContext->SetTarget(targetBitmap.Get());
	m_d2dContext->BeginDraw();
	//m_d2dContext->SetTransform(D2D1::Matrix3x2F::Identity());
	m_d2dContext->Clear(D2D1::ColorF(ColorF::Black, 0.0f));
	m_d2dContext->DrawImage(m_gaussianBlurEffect.Get());

	if ((result = m_d2dContext->EndDraw()) == D2DERR_RECREATE_TARGET)
	{
		ReturnIfFailed(result, CreateDeviceResources());
		return InternalDrawThumbnailPlaceholder(wicBitmapSource, blurAmount, randomAccessStream);
	}

	return SaveImageToStream(targetBitmap.Get(), GUID_ContainerFormatPng, randomAccessStream);
}

PlaceholderImageHelper::PlaceholderImageHelper()
{
	ThrowIfFailed(CreateDeviceIndependentResources());
	ThrowIfFailed(CreateDeviceResources());
}

HRESULT PlaceholderImageHelper::CreateDeviceIndependentResources()
{
	HRESULT result;
	D2D1_FACTORY_OPTIONS options = {};
	ReturnIfFailed(result, D2D1CreateFactory(D2D1_FACTORY_TYPE_SINGLE_THREADED, __uuidof(ID2D1Factory1), &options, &m_d2dFactory));
	ReturnIfFailed(result, CoCreateInstance(CLSID_WICImagingFactory, nullptr, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(&m_wicFactory)));
	ReturnIfFailed(result, DWriteCreateFactory(DWRITE_FACTORY_TYPE_SHARED, __uuidof(IDWriteFactory), &m_dwriteFactory));



	String^ path = String::Concat(Package::Current->InstalledLocation->Path, L"\\Assets\\Fonts\\Telegram.ttf");
	auto pathBegin = begin(path);
	auto pathEnd = end(path);

	//assert(pathBegin && pathEnd);

	void const* key = pathBegin;
	uint32_t keySize = static_cast<uint32_t>(std::distance(pathBegin, pathEnd) * sizeof(wchar_t));

	m_customLoader = Make<CustomFontLoader>();

	ReturnIfFailed(result, m_dwriteFactory->RegisterFontCollectionLoader(m_customLoader.Get()));
	ReturnIfFailed(result, m_dwriteFactory->CreateCustomFontCollection(m_customLoader.Get(), key, keySize, &m_fontCollection));
	ReturnIfFailed(result, m_dwriteFactory->CreateTextFormat(
		L"Telegram",							// font family name
		m_fontCollection.Get(),					// system font collection
		DWRITE_FONT_WEIGHT_NORMAL,				// font weight 
		DWRITE_FONT_STYLE_NORMAL,				// font style
		DWRITE_FONT_STRETCH_NORMAL,				// default font stretch
		82.0f,									// font size
		L"",									// locale name
		&m_symbolFormat
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
		&m_mdl2Format
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
		&m_textFormat
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
		D3D_FEATURE_LEVEL_11_1,
		D3D_FEATURE_LEVEL_11_0,
		D3D_FEATURE_LEVEL_10_1,
		D3D_FEATURE_LEVEL_10_0,
		D3D_FEATURE_LEVEL_9_3,
		D3D_FEATURE_LEVEL_9_2,
		D3D_FEATURE_LEVEL_9_1
	};

	ComPtr<ID3D11Device> device;
	ComPtr<ID3D11DeviceContext> context;
	ReturnIfFailed(result, D3D11CreateDevice(nullptr,	// specify null to use the default adapter
		D3D_DRIVER_TYPE_HARDWARE, 0,
		creationFlags,									// optionally set debug and Direct2D compatibility flags
		featureLevels,									// list of feature levels this app can support
		ARRAYSIZE(featureLevels),						// number of possible feature levels
		D3D11_SDK_VERSION,
		&device,										// returns the Direct3D device created
		&m_featureLevel,								// returns feature level of device created
		&context										// returns the device immediate context
	));

	ComPtr<IDXGIDevice> dxgiDevice;
	ReturnIfFailed(result, device.As(&dxgiDevice));
	ReturnIfFailed(result, m_d2dFactory->CreateDevice(dxgiDevice.Get(), &m_d2dDevice));

	ComPtr<ID2D1DeviceContext> d2dContext;
	ReturnIfFailed(result, m_d2dDevice->CreateDeviceContext(D2D1_DEVICE_CONTEXT_OPTIONS_NONE, &d2dContext));
	ReturnIfFailed(result, d2dContext.As(&m_d2dContext));

	ReturnIfFailed(result, m_d2dContext->CreateSolidColorBrush(D2D1::ColorF(D2D1::ColorF::White), &m_textBrush));
	ReturnIfFailed(result, m_d2dContext->CreateSolidColorBrush(D2D1::ColorF(D2D1::ColorF::Black), &m_black));
	ReturnIfFailed(result, m_d2dContext->CreateSolidColorBrush(D2D1::ColorF(D2D1::ColorF::White), &m_transparent));
	ReturnIfFailed(result, m_d2dContext->CreateEffect(CLSID_D2D1GaussianBlur, &m_gaussianBlurEffect));
	ReturnIfFailed(result, m_gaussianBlurEffect->SetValue(D2D1_GAUSSIANBLUR_PROP_BORDER_MODE, D2D1_BORDER_MODE_HARD));

	/*            Color.FromArgb(0xff, 0xff, 0xff, 0xff),
            Color.FromArgb(0xff, 0xd5, 0xe6, 0xf3),
            Color.FromArgb(0xff, 0x2d, 0x57, 0x75),
            Color.FromArgb(0xff, 0x2f, 0x99, 0xc9)
*/

	ComPtr<ID2D1SolidColorBrush> color1;
	ComPtr<ID2D1SolidColorBrush> color2;
	ComPtr<ID2D1SolidColorBrush> color3;
	ComPtr<ID2D1SolidColorBrush> color4;
	ReturnIfFailed(result, m_d2dContext->CreateSolidColorBrush(D2D1::ColorF(D2D1::ColorF::White), &color1));
	ReturnIfFailed(result, m_d2dContext->CreateSolidColorBrush(D2D1::ColorF(0xd5 / 255.0f, 0xe6 / 255.0f, 0xf3 / 255.0f, 1.0f), &color2));
	ReturnIfFailed(result, m_d2dContext->CreateSolidColorBrush(D2D1::ColorF(0x2d / 255.0f, 0x57 / 255.0f, 0x75 / 255.0f, 1.0f), &color3));
	ReturnIfFailed(result, m_d2dContext->CreateSolidColorBrush(D2D1::ColorF(0x2f / 255.0f, 0x99 / 255.0f, 0xc9 / 255.0f, 1.0f), &color4));

	m_identiconBrushes.push_back(color1);
	m_identiconBrushes.push_back(color2);
	m_identiconBrushes.push_back(color3);
	m_identiconBrushes.push_back(color4);

	D2D1_SIZE_U size = { 192, 192 };
	D2D1_BITMAP_PROPERTIES1 properties = { { DXGI_FORMAT_R8G8B8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED }, 96, 96, D2D1_BITMAP_OPTIONS_TARGET, 0 };
	ReturnIfFailed(result, m_d2dContext->CreateBitmap(size, nullptr, 0, &properties, &m_targetBitmap));

	m_d2dContext->SetAntialiasMode(D2D1_ANTIALIAS_MODE_PER_PRIMITIVE);

	return m_wicFactory->CreateImageEncoder(m_d2dDevice.Get(), &m_imageEncoder);
}

HRESULT PlaceholderImageHelper::MeasureText(const wchar_t* text, IDWriteTextFormat* format, DWRITE_TEXT_METRICS* textMetrics)
{
	HRESULT result;
	ComPtr<IDWriteTextLayout> textLayout;
	ReturnIfFailed(result, m_dwriteFactory->CreateTextLayout(
		text,							// The string to be laid out and formatted.
		wcslen(text),					// The length of the string.
		format,							// The text format to apply to the string (contains font information, etc).
		192.0f,							// The width of the layout box.
		192.0f,							// The height of the layout box.
		&textLayout						// The IDWriteTextLayout interface pointer.
	));

	return textLayout->GetMetrics(textMetrics);
}

HRESULT PlaceholderImageHelper::SaveImageToStream(ID2D1Image* image, REFGUID wicFormat, IRandomAccessStream^ randomAccessStream)
{
	HRESULT result;
	ComPtr<IStream> stream;
	ReturnIfFailed(result, CreateStreamOverRandomAccessStream(randomAccessStream, IID_PPV_ARGS(&stream)));

	ComPtr<IWICBitmapEncoder> wicBitmapEncoder;
	ReturnIfFailed(result, m_wicFactory->CreateEncoder(wicFormat, nullptr, &wicBitmapEncoder));
	ReturnIfFailed(result, wicBitmapEncoder->Initialize(stream.Get(), WICBitmapEncoderNoCache));

	ComPtr<IWICBitmapFrameEncode> wicFrameEncode;
	ReturnIfFailed(result, wicBitmapEncoder->CreateNewFrame(&wicFrameEncode, nullptr));
	ReturnIfFailed(result, wicFrameEncode->Initialize(nullptr));

	ReturnIfFailed(result, m_imageEncoder->WriteFrame(image, wicFrameEncode.Get(), nullptr));
	ReturnIfFailed(result, wicFrameEncode->Commit());
	ReturnIfFailed(result, wicBitmapEncoder->Commit());

	ReturnIfFailed(result, stream->Commit(STGC_DEFAULT));

	return stream->Seek({ 0 }, STREAM_SEEK_SET, nullptr);
}
