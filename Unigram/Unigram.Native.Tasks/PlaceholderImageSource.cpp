#include "pch.h"
#include "PlaceholderImageSource.h"
using namespace concurrency;
using namespace D2D1;
using namespace Windows::Storage;
using namespace Unigram::Native::Tasks;

PlaceholderImageSource^ PlaceholderImageSource::m_instance;
critical_section PlaceholderImageSource::m_criticalSection;

void PlaceholderImageSource::Draw(Color clear, Platform::String^ text, IRandomAccessStream^ randomAccessStream)
{
	critical_section::scoped_lock scopedLock(m_criticalSection);

	if (m_instance == nullptr)
		m_instance = ref new PlaceholderImageSource(192, 192);

	m_instance->BeginDraw(clear);

	//ID2D1LinearGradientBrush *m_pLinearGradientBrush;
	//ID2D1GradientStopCollection *pGradientStops = NULL;

	//D2D1_GRADIENT_STOP gradientStops[2];
	//gradientStops[0].color = D2D1::ColorF(clear.R / 255.0f, clear.G / 255.0f, clear.B / 255.0f, clear.A / 255.0f);
	//gradientStops[0].position = 0.0f;
	//gradientStops[1].color = D2D1::ColorF(end.R / 255.0f, end.G / 255.0f, end.B / 255.0f, end.A / 255.0f);
	//gradientStops[1].position = 1.0f;
	//DX::ThrowIfFailed(m_instance->m_d2dContext->CreateGradientStopCollection(
	//	gradientStops,
	//	2,
	//	D2D1_GAMMA_2_2,
	//	D2D1_EXTEND_MODE_CLAMP,
	//	&pGradientStops)
	//);

	//DX::ThrowIfFailed(m_instance->m_d2dContext->CreateLinearGradientBrush(
	//	D2D1::LinearGradientBrushProperties(
	//		D2D1::Point2F(0, 0),
	//		D2D1::Point2F(0, 192)),
	//	pGradientStops,
	//	&m_pLinearGradientBrush)
	//);

	//m_instance->m_d2dContext->FillRectangle(D2D1::RectF(0, 0, 192, 192), m_pLinearGradientBrush);

	DWRITE_TEXT_METRICS textMetrics = { 0 };
	m_instance->MeasureText(text, L"Segoe UI", 82, DWRITE_FONT_STYLE_NORMAL, DWRITE_FONT_WEIGHT_NORMAL, &textMetrics);
	m_instance->DrawText(text, (192 - textMetrics.width) / 2, (180 - textMetrics.height) / 2, L"Segoe UI", D2D1::ColorF(D2D1::ColorF::White), 82, DWRITE_FONT_STYLE_NORMAL, DWRITE_FONT_WEIGHT_NORMAL);
	m_instance->EndDraw();
	m_instance->SaveBitmapToFile(randomAccessStream);
}

PlaceholderImageSource::PlaceholderImageSource(int width, int height)
{
	CreateDeviceIndependentResources();
	CreateDeviceResources();

	D2D1_SIZE_U size = { width, height };
	D2D1_BITMAP_PROPERTIES1 properties = { { DXGI_FORMAT_R8G8B8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED }, 96, 96, D2D1_BITMAP_OPTIONS_TARGET, 0 };

	DX::ThrowIfFailed(
		m_d2dContext->CreateBitmap(
			size, (const void *)0, 0,
			&properties,
			&m_targetBitmap
		)
	);

	m_d2dContext->SetTarget(m_targetBitmap.Get());

	m_renderTargetSize = m_d2dContext->GetSize();

	//BeginDraw();
}

void PlaceholderImageSource::Initialize()
{
	CreateDeviceIndependentResources();
	CreateDeviceResources();
}


void PlaceholderImageSource::CreateDeviceIndependentResources()
{
	D2D1_FACTORY_OPTIONS options;
	ZeroMemory(&options, sizeof(D2D1_FACTORY_OPTIONS));


	DX::ThrowIfFailed(
		D2D1CreateFactory(
			D2D1_FACTORY_TYPE_SINGLE_THREADED,
			__uuidof(ID2D1Factory1),
			&options,
			&m_d2dFactory)
	);

	DX::ThrowIfFailed(
		CoCreateInstance(
			CLSID_WICImagingFactory,
			nullptr,
			CLSCTX_INPROC_SERVER,
			IID_PPV_ARGS(&m_wicFactory))
	);

	DX::ThrowIfFailed(
		DWriteCreateFactory(
			DWRITE_FACTORY_TYPE_SHARED,
			__uuidof(IDWriteFactory),
			&m_dwriteFactory)
	);
}

void PlaceholderImageSource::CreateDeviceResources()
{
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

	DX::ThrowIfFailed(
		D3D11CreateDevice(
			nullptr,                    // specify null to use the default adapter
			D3D_DRIVER_TYPE_HARDWARE,
			0,
			creationFlags,              // optionally set debug and Direct2D compatibility flags
			featureLevels,              // list of feature levels this app can support
			ARRAYSIZE(featureLevels),   // number of possible feature levels
			D3D11_SDK_VERSION,
			&device,                    // returns the Direct3D device created
			&m_featureLevel,            // returns feature level of device created
			&context                    // returns the device immediate context
		)
	);

	ComPtr<IDXGIDevice> dxgiDevice;

	DX::ThrowIfFailed(
		device.As(&dxgiDevice)
	);

	DX::ThrowIfFailed(
		m_d2dFactory->CreateDevice(dxgiDevice.Get(), &m_d2dDevice)
	);

	DX::ThrowIfFailed(
		m_d2dDevice->CreateDeviceContext(
			D2D1_DEVICE_CONTEXT_OPTIONS_NONE,
			&m_d2dContext)
	);
}

void PlaceholderImageSource::BeginDraw(Color clear)
{
	m_d2dContext->BeginDraw();
	m_d2dContext->SetTransform(D2D1::Matrix3x2F::Identity());
	m_d2dContext->Clear(D2D1::ColorF(clear.R / 255.0f, clear.G / 255.0f, clear.B / 255.0f, clear.A / 255.0f));
}

void PlaceholderImageSource::EndDraw()
{
	DX::ThrowIfFailed(
		m_d2dContext->EndDraw()
	);

	//if (needPreview)
	//{
	//	GUID wicFormat = GUID_ContainerFormatBmp;
	//	ComPtr<IStream> stream;
	//	ComPtr<ISequentialStream> ss;
	//	auto inMemStream = ref new InMemoryRandomAccessStream();
	//	DX::ThrowIfFailed(
	//		CreateStreamOverRandomAccessStream(inMemStream, IID_PPV_ARGS(&stream))
	//		);

	//	SaveBitmapToStream(m_targetBitmap, m_wicFactory, m_d2dContext, wicFormat, stream.Get());

	//	return inMemStream;
	//}

	//return nullptr;
}

void PlaceholderImageSource::DrawText(Platform::String^ text, int x, int y, Platform::String^ fontFamilyName,
	D2D1_COLOR_F textColor, float fontSize, DWRITE_FONT_STYLE fontStyle,
	DWRITE_FONT_WEIGHT fontWeight)
{
	DX::ThrowIfFailed(
		m_dwriteFactory->CreateTextFormat(
			fontFamilyName->Data(),				 // font family name
			nullptr,							 // system font collection
			fontWeight,							 // font weight 
			fontStyle,							 // font style
			DWRITE_FONT_STRETCH_NORMAL,			 // default font stretch
			fontSize,						     // font size
			L"",								 // locale name
			&m_textFormat
		)
	);

	// Set text alignment.
	DX::ThrowIfFailed(
		m_textFormat->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_LEADING)
	);

	// Set paragraph alignment.
	DX::ThrowIfFailed(
		m_textFormat->SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_NEAR)
	);

	D2D1_RECT_F layoutRect = { x, y, m_renderTargetSize.width, m_renderTargetSize.height };

	DX::ThrowIfFailed(
		m_d2dContext->CreateSolidColorBrush(
			textColor,
			&m_textBrush
		)
	);

	m_d2dContext->DrawText(text->Data(), text->Length(), m_textFormat.Get(), &layoutRect, m_textBrush.Get());
}

void PlaceholderImageSource::MeasureText(Platform::String^ text, Platform::String^ fontFamilyName, float fontSize, DWRITE_FONT_STYLE fontStyle, DWRITE_FONT_WEIGHT fontWeight, DWRITE_TEXT_METRICS* textMetrics)
{
	DX::ThrowIfFailed(
		m_dwriteFactory->CreateTextFormat(
			fontFamilyName->Data(),				 // font family name
			nullptr,							 // system font collection
			fontWeight,							 // font weight 
			fontStyle,							 // font style
			DWRITE_FONT_STRETCH_NORMAL,			 // default font stretch
			fontSize,						     // font size
			L"",								 // locale name
			&m_textFormat
		)
	);

	DX::ThrowIfFailed(
		m_textFormat->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_LEADING)
	);

	DX::ThrowIfFailed(
		m_textFormat->SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_NEAR)
	);

	ComPtr<IDWriteTextLayout> pTextLayout_;
	m_dwriteFactory->CreateTextLayout(
		text->Data(),      // The string to be laid out and formatted.
		text->Length(),  // The length of the string.
		m_textFormat.Get(),  // The text format to apply to the string (contains font information, etc).
		m_renderTargetSize.width,         // The width of the layout box.
		m_renderTargetSize.height,        // The height of the layout box.
		&pTextLayout_  // The IDWriteTextLayout interface pointer.
	);

	DX::ThrowIfFailed(
		pTextLayout_->GetMetrics(textMetrics)
	);
}

void PlaceholderImageSource::SaveBitmapToFile(Streams::IRandomAccessStream^ randomAccessStream)
{
	//Pickers::FileSavePicker^ savePicker = ref new Pickers::FileSavePicker();
	//auto pngExtensions = ref new Platform::Collections::Vector<Platform::String^>();
	//pngExtensions->Append(".png");
	//savePicker->FileTypeChoices->Insert("PNG file", pngExtensions);
	//auto jpgExtensions = ref new Platform::Collections::Vector<Platform::String^>();
	//jpgExtensions->Append(".jpg");
	//savePicker->FileTypeChoices->Insert("JPEG file", jpgExtensions);
	//auto bmpExtensions = ref new Platform::Collections::Vector<Platform::String^>();
	//bmpExtensions->Append(".bmp");
	//savePicker->FileTypeChoices->Insert("BMP file", bmpExtensions);
	//savePicker->DefaultFileExtension = ".png";
	//savePicker->SuggestedFileName = "watermark";
	//savePicker->SuggestedStartLocation = Pickers::PickerLocationId::PicturesLibrary;

	std::shared_ptr<GUID> wicFormat = std::make_shared<GUID>(GUID_ContainerFormatPng);

	//create_task(savePicker->PickSaveFileAsync()).then([=](StorageFile^ file)
	//{
	//	if (file == nullptr)
	//	{
	//		// If user clicks "Cancel", reset the saving state, then cancel the current task.
	//		//m_screenSavingState = ScreenSavingState::NotSaved;
	//		cancel_current_task();
	//	}

	//	if (file->FileType == ".bmp")
	//	{
	//		*wicFormat = GUID_ContainerFormatBmp;
	//	}
	//	else if (file->FileType == ".jpg")
	//	{
	//		*wicFormat = GUID_ContainerFormatJpeg;
	//	}
	//	return file->OpenAsync(FileAccessMode::ReadWrite);

	//}).then([=](Streams::IRandomAccessStream^ randomAccessStream)
	//{
		// Convert the RandomAccessStream to an IStream.
	ComPtr<IStream> stream;
	DX::ThrowIfFailed(
		CreateStreamOverRandomAccessStream(randomAccessStream, IID_PPV_ARGS(&stream))
	);

	SaveBitmapToStream(m_targetBitmap, m_wicFactory, m_d2dContext, GUID_ContainerFormatPng, stream.Get());
	//});
}

void PlaceholderImageSource::SaveBitmapToStream(
	_In_ ComPtr<ID2D1Bitmap1> d2dBitmap,
	_In_ ComPtr<IWICImagingFactory2> wicFactory2,
	_In_ ComPtr<ID2D1DeviceContext> d2dContext,
	_In_ REFGUID wicFormat,
	_In_ IStream* stream
)
{
	ComPtr<IWICBitmapEncoder> wicBitmapEncoder;
	DX::ThrowIfFailed(
		wicFactory2->CreateEncoder(
			wicFormat,
			nullptr,    // No preferred codec vendor.
			&wicBitmapEncoder
		)
	);

	DX::ThrowIfFailed(
		wicBitmapEncoder->Initialize(
			stream,
			WICBitmapEncoderNoCache
		)
	);

	ComPtr<IWICBitmapFrameEncode> wicFrameEncode;
	DX::ThrowIfFailed(
		wicBitmapEncoder->CreateNewFrame(
			&wicFrameEncode,
			nullptr     // No encoder options.
		)
	);

	DX::ThrowIfFailed(
		wicFrameEncode->Initialize(nullptr)
	);

	ComPtr<ID2D1Device> d2dDevice;
	d2dContext->GetDevice(&d2dDevice);

	ComPtr<IWICImageEncoder> imageEncoder;
	DX::ThrowIfFailed(
		wicFactory2->CreateImageEncoder(
			d2dDevice.Get(),
			&imageEncoder
		)
	);

	D2D1_SIZE_F imageSize = d2dBitmap->GetSize();
	WICImageParameters *parames = new WICImageParameters();
	parames->DpiX = 96.0;
	parames->DpiY = 96.0;
	parames->Left = 0;
	parames->PixelFormat = d2dBitmap->GetPixelFormat();
	parames->PixelHeight = imageSize.height;
	parames->PixelWidth = imageSize.width;
	parames->Top = 0;

	DX::ThrowIfFailed(
		imageEncoder->WriteFrame(
			d2dBitmap.Get(),
			wicFrameEncode.Get(),
			parames
			//nullptr     // Use default WICImageParameter options.
		)
	);

	DX::ThrowIfFailed(
		wicFrameEncode->Commit()
	);

	DX::ThrowIfFailed(
		wicBitmapEncoder->Commit()
	);

	DX::ThrowIfFailed(
		stream->Commit(STGC_DEFAULT)
	);
}
