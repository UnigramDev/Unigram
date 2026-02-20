#pragma once

#include "PlaceholderImageHelper.g.h"

#include <ppl.h>
#include <wincodec.h>
#include <Dwrite_1.h>
#include <D2d1_3.h>
#include <D3d11_4.h>
#include <map>

#include <SurfaceImage.h>
#include <TextFormat.h>
#include "FreeformGradientSurface.h"
#include "MessageBubbleNineGrid.h";

#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.UI.h>
#include <winrt/Windows.UI.Composition.h>
#include <winrt/Windows.UI.Xaml.h>
#include <winrt/Windows.Storage.Streams.h>
#include <winrt/Windows.Graphics.h>
#include <winrt/Windows.Graphics.DirectX.h>
#include <winrt/Windows.Graphics.DirectX.Direct3D11.h>
#include <winrt/Windows.Graphics.Effects.h>
#include <winrt/Windows.Graphics.Imaging.h>
#include <windows.graphics.interop.h>
#include <windows.graphics.effects.interop.h>

using namespace concurrency;
using namespace winrt::Windows::Graphics;
using namespace winrt::Windows::Graphics::DirectX;
using namespace winrt::Windows::Graphics::DirectX::Direct3D11;
using namespace winrt::Windows::Graphics::Effects;
using namespace winrt::Windows::Graphics::Imaging;
using namespace winrt::Windows::UI;
using namespace winrt::Windows::UI::Composition;
using namespace winrt::Windows::UI::Xaml;
using namespace winrt::Windows::Foundation::Collections;
using namespace winrt::Windows::Foundation::Numerics;
using namespace winrt::Windows::Storage::Streams;

namespace abi
{
    using namespace ABI::Windows::Foundation;
    using namespace ABI::Windows::Graphics;
    using namespace ABI::Windows::Graphics::DirectX;
    using namespace ABI::Windows::Graphics::Effects;
    using namespace ABI::Windows::UI::Composition;
}

#define IFACEMETHODIMP2        __override COM_DECLSPEC_NOTHROW HRESULT STDMETHODCALLTYPE

#define CATCH_RETURN \
        return S_OK; \
    } catch (...) { \
        auto hr = winrt::to_hresult(); \
        __analysis_assume(FAILED(hr)); \
        return hr; 

namespace winrt::Telegram::Native::implementation
{
    class CompositionPathSource
        : public winrt::implements<CompositionPathSource, IGeometrySource2D, abi::IGeometrySource2DInterop>
    {
    public:
        CompositionPathSource(winrt::com_ptr<ID2D1Geometry> geometry)
            : m_geometry(geometry)
        {
        }

        IFACEMETHODIMP2 GetGeometry(
            _COM_Outptr_ ID2D1Geometry** value
        ) override
        {
            *value = nullptr;
            m_geometry.copy_to(value);
            return S_OK;
        }

        IFACEMETHODIMP2 TryGetGeometryUsingFactory(
            _In_ ID2D1Factory* factory,
            _COM_Outptr_result_maybenull_ ID2D1Geometry** value
        ) override
        {
            *value = nullptr;
            return S_OK;
        }

    private:
        winrt::com_ptr<ID2D1Geometry> m_geometry;
    };

    // Supply our own implementation not to depend on Win2D.uwp
    class CompositionAlphaMaskEffect
        : public winrt::implements<CompositionAlphaMaskEffect, IGraphicsEffect, abi::IGraphicsEffectD2D1Interop>
    {
    public:
        CompositionAlphaMaskEffect()
        {
        }

        inline IGraphicsEffectSource& to_winrt(abi::IGraphicsEffectSource*& instance)
        {
            return reinterpret_cast<IGraphicsEffectSource&>(instance);
        }

        IGraphicsEffectSource Source() { return m_source; }
        void Source(IGraphicsEffectSource value) { m_source = value; }

        IGraphicsEffectSource AlphaMask() { return m_alphaMask; }
        void AlphaMask(IGraphicsEffectSource value) { m_alphaMask = value; }

        // IGraphicsEffect
        winrt::hstring Name() { return m_name; }
        void Name(winrt::hstring const& value) { m_name = value; }

        // IGraphicsEffectD2D1Interop
        IFACEMETHODIMP GetEffectId(_Out_ GUID* id) override
        {
            *id = CLSID_D2D1AlphaMask;
            return S_OK;
        }

        IFACEMETHODIMP GetSourceCount(_Out_ UINT* count) override
        {
            *count = 2;
            return S_OK;
        }

        IFACEMETHODIMP GetPropertyCount(_Out_ UINT* count) override
        {
            *count = 0;
            return S_OK;
        }

        IFACEMETHODIMP GetSource(UINT index, _Outptr_ abi::IGraphicsEffectSource** source) override try
        {
            if (index == 0) to_winrt(*source) = m_source;
            else if (index == 1) to_winrt(*source) = m_alphaMask;
            else throw winrt::hresult_invalid_argument();
                CATCH_RETURN;
        }

        IFACEMETHODIMP GetProperty(UINT, _Outptr_ abi::IPropertyValue**) override
        {
            return E_INVALIDARG;
        }

        IFACEMETHODIMP GetNamedPropertyMapping(LPCWSTR, _Out_ UINT*,
            _Out_ abi::GRAPHICS_EFFECT_PROPERTY_MAPPING*) override
        {
            return E_INVALIDARG;
        }

    private:
        hstring m_name;
        IGraphicsEffectSource m_source;
        IGraphicsEffectSource m_alphaMask;
    };

    struct DeviceLostEventArgs
    {
        DeviceLostEventArgs(IDirect3DDevice const& device) : m_device(device) {}
        IDirect3DDevice Device() { return m_device; }
        static DeviceLostEventArgs Create(IDirect3DDevice const& device) { return DeviceLostEventArgs{ device }; }

    private:
        IDirect3DDevice m_device;
    };

    // From MSDN sample: https://learn.microsoft.com/en-us/windows/uwp/composition/composition-native-interop
    struct DeviceLostHelper
    {
        DeviceLostHelper() = default;

        ~DeviceLostHelper()
        {
            StopWatchingCurrentDevice();
            m_onDeviceLostHandler = nullptr;
        }

        IDirect3DDevice CurrentlyWatchedDevice() { return m_device; }

        void WatchDevice(winrt::com_ptr<::IDXGIDevice> const& dxgiDevice)
        {
            // If we're currently listening to a device, then stop.
            StopWatchingCurrentDevice();

            // Set the current device to the new device.
            m_device = nullptr;
            winrt::check_hresult(::CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.get(), reinterpret_cast<::IInspectable**>(winrt::put_abi(m_device))));

            // Get the DXGI Device.
            m_dxgiDevice = dxgiDevice;

            // QI For the ID3D11Device4 interface.
            winrt::com_ptr<::ID3D11Device4> d3dDevice{ m_dxgiDevice.as<::ID3D11Device4>() };

            // Create a wait struct.
            m_onDeviceLostHandler = nullptr;
            m_onDeviceLostHandler = ::CreateThreadpoolWait(DeviceLostHelper::OnDeviceLost, (PVOID)this, nullptr);

            // Create a handle and a cookie.
            m_eventHandle.attach(::CreateEvent(nullptr, false, false, nullptr));
            winrt::check_bool(bool{ m_eventHandle });
            m_cookie = 0;

            // Register for device lost.
            ::SetThreadpoolWait(m_onDeviceLostHandler, m_eventHandle.get(), nullptr);
            winrt::check_hresult(d3dDevice->RegisterDeviceRemovedEvent(m_eventHandle.get(), &m_cookie));
        }

        void StopWatchingCurrentDevice()
        {
            if (m_dxgiDevice && m_onDeviceLostHandler)
            {
                // QI For the ID3D11Device4 interface.
                auto d3dDevice{ m_dxgiDevice.as<::ID3D11Device4>() };

                // Unregister from the device lost event.
                ::CloseThreadpoolWait(m_onDeviceLostHandler);
                d3dDevice->UnregisterDeviceRemoved(m_cookie);

                // Clear member variables.
                m_onDeviceLostHandler = nullptr;
                m_eventHandle.close();
                m_cookie = 0;
                m_device = nullptr;
            }
        }

        void DeviceLost(winrt::delegate<DeviceLostHelper const*, DeviceLostEventArgs const&> const& handler)
        {
            m_deviceLost = handler;
        }

        winrt::delegate<DeviceLostHelper const*, DeviceLostEventArgs const&> m_deviceLost;

    private:
        void RaiseDeviceLostEvent(IDirect3DDevice const& oldDevice)
        {
            m_deviceLost(this, DeviceLostEventArgs::Create(oldDevice));
        }

        static void CALLBACK OnDeviceLost(PTP_CALLBACK_INSTANCE /* instance */, PVOID context, PTP_WAIT /* wait */, TP_WAIT_RESULT /* waitResult */)
        {
            auto deviceLostHelper = reinterpret_cast<DeviceLostHelper*>(context);
            auto oldDevice = deviceLostHelper->m_device;
            deviceLostHelper->StopWatchingCurrentDevice();
            deviceLostHelper->RaiseDeviceLostEvent(oldDevice);
        }

    private:
        IDirect3DDevice m_device;
        winrt::com_ptr<::IDXGIDevice> m_dxgiDevice;
        PTP_WAIT m_onDeviceLostHandler{ nullptr };
        winrt::handle m_eventHandle;
        DWORD m_cookie{ 0 };
    };

    struct MessageBubbleNineGrid;

    struct PlaceholderImageHelper : PlaceholderImageHelperT<PlaceholderImageHelper>
    {
    public:
        PlaceholderImageHelper(Window window);
        ~PlaceholderImageHelper()
        {
            Close();
        }

        // Explicit dispose is needed because otherwise XamlRoot may get deleted before deconstructor is invoked
        void Close()
        {
            m_nineGridCache.clear();
            m_deviceLostHelper.StopWatchingCurrentDevice();
        }

        HRESULT HandleDeviceLost()
        {
            std::lock_guard const guard(m_criticalSection);

            if (FAILED(m_d3dDevice->GetDeviceRemovedReason()))
            {
                return CreateDeviceResources();
            }

            return S_OK;
        }

        CompositionGraphicsDevice Device()
        {
            return m_compositionDevice;
        }

        //static winrt::Telegram::Native::PlaceholderImageHelper Background()
        //{
        //    std::lock_guard const guard(s_criticalSection);

        //    if (s_background == nullptr)
        //    {
        //        s_background = winrt::make_self<PlaceholderImageHelper>();
        //    }

        //    s_background->HandleDeviceLost();
        //    return s_background.as<winrt::Telegram::Native::PlaceholderImageHelper>();
        //}

        //static winrt::Telegram::Native::PlaceholderImageHelper Foreground()
        //{
        //    std::lock_guard const guard(s_criticalSection);

        //    if (s_foreground == nullptr)
        //    {
        //        s_foreground = winrt::make_self<PlaceholderImageHelper>();
        //    }

        //    s_foreground->HandleDeviceLost();
        //    return s_foreground.as<winrt::Telegram::Native::PlaceholderImageHelper>();
        //}

        static HRESULT WriteBytes(IVector<byte> hash, IRandomAccessStream randomAccessStream) noexcept;
        static IBuffer DrawWebP(hstring fileName, int32_t maxWidth, int32_t& pixelWidth, int32_t& pixelHeight) noexcept;
        static bool IsWebP(hstring fileName, int32_t& pixelWidth, int32_t& pixelHeight) noexcept;

        IVector<hstring> GetSystemFontFamilies(IVector<hstring> localeNames);

        winrt::Telegram::Native::FreeformGradientSurface CreateFreeformGradient(IVector<Color> colors);

        CompositionEffectBrush GetTail(int topLeftRadius, int topRightRadius, int bottomRightRadius, int bottomLeftRadius);
        //CompositionPath GetOutline(IVector<ClosedVectorPath> contours);
        CompositionPath GetEllipticalClip(float width, float height, float radius, float x, float y);
        CompositionPath GetReplyMarkupClip(IVector<IVector<Windows::Foundation::Rect>> rows, float bottomRightRadius, float bottomLeftRadius);
        CompositionPath GetVoiceNoteClip(IVector<byte> waveform, double waveformWidth);
        CompositionPath GetRoundedPolygon(IVector<IVector<Windows::Foundation::Rect>> shapes);

        HRESULT Encode(IBuffer source, IRandomAccessStream destination, int32_t width, int32_t height, int32_t rotation);

        winrt::Windows::Foundation::IAsyncOperation<ChatBackgroundPattern> DrawSvgAsync(Compositor compositor, hstring path, float intensity, bool negative, double rasterizationScale);
        ChatBackgroundPattern DrawSvg(Compositor compositor, hstring path, float intensity, bool negative, double rasterizationScale);

        SoftwareBitmap DrawBlurred(hstring fileName, float blurAmount);
        SoftwareBitmap DrawBlurred(IVector<uint8_t> bytes, float blurAmount);

        winrt::Telegram::Native::SurfaceImage Create(int32_t pixelWidth, int32_t pixelHeight);
        HRESULT Invalidate(winrt::Telegram::Native::SurfaceImage imageSource, IBuffer buffer);

        winrt::Telegram::Native::TextFormat CreateTextFormat2(hstring text, IVector<TextStylePart> entities, double fontSize, double width);

        float2 ContentEnd(hstring text, IVector<TextStylePart> entities, double fontSize, double width);
        IVector<Windows::Foundation::Rect> LineMetrics(hstring text, IVector<TextStylePart> entities, double fontSize, double width, bool rtl);
        IVector<Windows::Foundation::Rect> RangeMetrics(hstring text, int32_t offset, int32_t length, IVector<TextStylePart> entities, double fontSize, double width, bool rtl, bool wrap);
        Windows::Foundation::Rect LayoutMetrics(hstring text, int32_t offset, int32_t length, IVector<TextStylePart> entities, double fontSize, double width, bool rtl);
        MaxLinesMetrics MaxLines(hstring text, int32_t offset, int32_t length, IVector<TextStylePart> entities, double fontSize, double width, bool rtl, int32_t maxLines);
        //IVector<Windows::Foundation::Rect> EntityMetrics(hstring text, IVector<TextStylePart> entities, double fontSize, double width, bool rtl);

    private:
        HRESULT CreateDeviceIndependentResources();
        HRESULT CreateDeviceResources();
        HRESULT CreateTextFormat(double fontSize);

        void OnDirect3DDeviceLost(DeviceLostHelper const* /* sender */, DeviceLostEventArgs const& /* args */);

        CompositionDrawingSurface CreateDrawingSurface(SizeInt32 size);

        HRESULT DrawBlurredImpl(IWICBitmapSource* wicBitmapSource, float blurAmount, SoftwareBitmap& bitmap, bool minithumbnail);
        HRESULT SaveImageToStream(ID2D1Image* image, REFGUID wicFormat, IRandomAccessStream randomAccessStream);

        HRESULT CreateTextFormatImpl(hstring text, IVector<TextStylePart> entities, double fontSize, double width, winrt::com_ptr<TextFormat>& textFormat);

    public:
        Window m_window;
        Compositor m_compositor;
        CompositionEffectFactory m_alphaMaskFactory;
        CompositionGraphicsDevice m_compositionDevice;
        DeviceLostHelper m_deviceLostHelper;
        winrt::com_ptr<ID2D1Factory1> m_d2dFactory;
        winrt::com_ptr<ID2D1Device> m_d2dDevice;
        winrt::com_ptr<ID3D11Device> m_d3dDevice;
        winrt::com_ptr<ID2D1DeviceContext2> m_d2dContext;
        D3D_FEATURE_LEVEL m_featureLevel;
        winrt::com_ptr<IWICImagingFactory2> m_wicFactory;
        winrt::com_ptr<IWICImageEncoder> m_imageEncoder;
        winrt::com_ptr<IDWriteFactory1> m_dwriteFactory;
        winrt::com_ptr<IDWriteFontCollectionLoader> m_customLoader;
        winrt::com_ptr<IDWriteFontCollection> m_fontCollection;
        winrt::com_ptr<IDWriteFontCollection> m_systemCollection;
        winrt::com_ptr<IDWriteInlineObject> m_customEmoji;
        winrt::com_ptr<IDWriteTextFormat> m_appleFormat;
        winrt::com_ptr<ID2D1Effect> m_gaussianBlurEffect;
        std::mutex m_criticalSection;

        std::unordered_map<int, winrt::com_ptr<winrt::Telegram::Native::implementation::MessageBubbleNineGrid>> m_nineGridCache;
    };
} // namespace winrt::Telegram::Native::implementation

namespace winrt::Telegram::Native::factory_implementation
{
    struct PlaceholderImageHelper : PlaceholderImageHelperT<PlaceholderImageHelper, implementation::PlaceholderImageHelper>
    {
    };
} // namespace winrt::Telegram::Native::factory_implementation
