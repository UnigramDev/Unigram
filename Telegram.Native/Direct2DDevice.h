#pragma once

#include "Direct2DDevice.g.h"

#include <ppl.h>
#include <wincodec.h>
#include <Dwrite_1.h>
#include <D2d1_3.h>
#include <D3d11_4.h>
#include <map>
#include <list>
#include <string>
#include <unordered_map>

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
            Shutdown();
        }

        // Cancels any pending wait and drains in-flight callbacks, so a device-lost callback can't
        // run RaiseDeviceLostEvent on an object that is going away. CloseThreadpoolWait on its own
        // does not wait for a callback that is already running, which is why StopWatchingCurrentDevice
        // is not enough on a teardown path.
        //
        // Must not be called from the callback itself -- that is what StopWatchingCurrentDevice is
        // for, since waiting there would be waiting on ourselves.
        void Shutdown()
        {
            if (m_onDeviceLostHandler)
            {
                ::SetThreadpoolWait(m_onDeviceLostHandler, nullptr, nullptr);
                ::WaitForThreadpoolWaitCallbacks(m_onDeviceLostHandler, TRUE);
            }

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

                // Unregister before closing the wait: the other order leaves a window where the
                // event can still be signalled against a wait object we have already closed.
                d3dDevice->UnregisterDeviceRemoved(m_cookie);
                ::CloseThreadpoolWait(m_onDeviceLostHandler);

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

    struct Direct2DDevice : Direct2DDeviceT<Direct2DDevice>
    {
    public:
        Direct2DDevice(Compositor compositor);
        ~Direct2DDevice()
        {
            Close();
        }

        // Explicit dispose is needed because otherwise XamlRoot may get deleted before deconstructor is invoked
        void Close()
        {
            // Shutdown, not StopWatchingCurrentDevice: a device-lost callback that is already
            // running holds a raw pointer to this object and would outlive it otherwise.
            m_deviceLostHelper.Shutdown();

            m_nineGridCache.clear();
            m_svgCacheList.clear();
            m_svgCacheIndex.clear();

            // The DWrite factory is shared process-wide, so a loader that is never unregistered
            // stays on it for the rest of the session, one per window thread. The collection goes
            // first: the loader has to stay registered for as long as anything built from it lives.
            if (m_customLoader && m_dwriteFactory)
            {
                m_fontCollection = nullptr;
                m_dwriteFactory->UnregisterFontCollectionLoader(m_customLoader.get());
                m_customLoader = nullptr;
            }
        }

        HRESULT HandleDeviceLost()
        {
            std::lock_guard const guard(m_criticalSection);

            // The device is null when a previous attempt to create one failed, which happens while
            // the display driver is still resetting.
            if (m_d3dDevice == nullptr || FAILED(m_d3dDevice->GetDeviceRemovedReason()))
            {
                return CreateDeviceResources();
            }

            return S_OK;
        }

        CompositionGraphicsDevice Device()
        {
            return m_compositionDevice;
        }

        //static winrt::Telegram::Native::Direct2DDevice Background()
        //{
        //    std::lock_guard const guard(s_criticalSection);

        //    if (s_background == nullptr)
        //    {
        //        s_background = winrt::make_self<Direct2DDevice>();
        //    }

        //    s_background->HandleDeviceLost();
        //    return s_background.as<winrt::Telegram::Native::Direct2DDevice>();
        //}

        //static winrt::Telegram::Native::Direct2DDevice Foreground()
        //{
        //    std::lock_guard const guard(s_criticalSection);

        //    if (s_foreground == nullptr)
        //    {
        //        s_foreground = winrt::make_self<Direct2DDevice>();
        //    }

        //    s_foreground->HandleDeviceLost();
        //    return s_foreground.as<winrt::Telegram::Native::Direct2DDevice>();
        //}

        static HRESULT WriteBytes(array_view<uint8_t const> hash, IRandomAccessStream randomAccessStream) noexcept;
        static IBuffer DrawWebP(hstring fileName, int32_t maxWidth, int32_t& pixelWidth, int32_t& pixelHeight) noexcept;
        static bool IsWebP(hstring fileName, int32_t& pixelWidth, int32_t& pixelHeight) noexcept;

        IVector<hstring> GetSystemFontFamilies(IVector<hstring> localeNames);

        winrt::Telegram::Native::FreeformGradientSurface CreateFreeformGradient(IVector<int32_t> colors);

        CompositionEffectBrush GetTail(XamlRoot xamlRoot, int topLeftRadius, int topRightRadius, int bottomRightRadius, int bottomLeftRadius);
        CompositionNineGridBrush GetTailMask(XamlRoot xamlRoot, int topLeftRadius, int topRightRadius, int bottomRightRadius, int bottomLeftRadius);
        //CompositionPath GetOutline(IVector<ClosedVectorPath> contours);
        CompositionPath GetEllipticalClip(float width, float height, float radius, float x, float y);
        CompositionPath GetReplyMarkupClip(IVector<IVector<Windows::Foundation::Rect>> rows, float bottomRightRadius, float bottomLeftRadius);
        CompositionPath GetVoiceNoteClip(array_view<uint8_t const> waveform, double waveformWidth);
        CompositionPath GetRoundedPolygon(IVector<IVector<Windows::Foundation::Rect>> shapes);

        HRESULT Encode(IBuffer source, IRandomAccessStream destination, int32_t width, int32_t height, int32_t rotation);

        winrt::Windows::Foundation::IAsyncOperation<ChatBackgroundPattern> DrawSvgAsync(Compositor compositor, hstring path, float intensity, bool negative, double rasterizationScale);
        ChatBackgroundPattern DrawSvg(Compositor compositor, hstring path, float intensity, bool negative, double rasterizationScale);

        SoftwareBitmap DrawBlurred(hstring fileName, float blurAmount);
        SoftwareBitmap DrawBlurred(array_view<uint8_t const> bytes, float blurAmount);

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

        winrt::com_ptr<winrt::Telegram::Native::implementation::MessageBubbleNineGrid> GetNineGrid(XamlRoot const& xamlRoot, int topLeftRadius, int topRightRadius, int bottomRightRadius, int bottomLeftRadius);
        void PruneNineGridCache();

        HRESULT DrawBlurredImpl(IWICBitmapSource* wicBitmapSource, float blurAmount, SoftwareBitmap& bitmap, bool minithumbnail);
        HRESULT SaveImageToStream(ID2D1Image* image, REFGUID wicFormat, IRandomAccessStream randomAccessStream);

        HRESULT CreateTextFormatImpl(hstring text, IVector<TextStylePart> entities, double fontSize, double width, winrt::com_ptr<TextFormat>& textFormat);

        // Returns decompressed SVG bytes, caching them in a small LRU. Must be called while holding
        // m_criticalSection. nsvgParse mutates its input in place, so callers must parse a *copy*.
        const std::string& GetDecompressedSvg(hstring const& path);

    public:
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
        const wchar_t* m_monospaceFamily = L"Consolas";
        winrt::com_ptr<IDWriteInlineObject> m_customEmoji;
        winrt::com_ptr<IDWriteTextFormat> m_appleFormat;
        winrt::com_ptr<ID2D1Effect> m_gaussianBlurEffect;
        std::mutex m_criticalSection;

        // Nine grids are rasterized at their window's scale, so they cannot be shared between
        // windows on one thread. Buckets hold the XamlRoot weakly and are keyed by its identity;
        // MessageBubbleNineGrid holds it weakly too, since this cache is its only owner and a
        // strong reference on either side would keep a closed window's tree alive.
        struct NineGridBucket
        {
            winrt::weak_ref<XamlRoot> Root;
            std::unordered_map<int, winrt::com_ptr<winrt::Telegram::Native::implementation::MessageBubbleNineGrid>> Grids;
        };

        std::unordered_map<void*, NineGridBucket> m_nineGridCache;

        // Bounded LRU cache of decompressed SVG bytes keyed by file path. Switching among a few chats
        // re-renders their (different) pattern backgrounds repeatedly; without this, each switch
        // re-reads + gunzips the same file, and those large variable-size temporaries fragment the
        // segment heap. Capped so at most a handful of patterns stay resident.
        static constexpr size_t kSvgCacheCapacity = 6;
        std::list<std::pair<std::wstring, std::string>> m_svgCacheList; // front = most recently used
        std::unordered_map<std::wstring, std::list<std::pair<std::wstring, std::string>>::iterator> m_svgCacheIndex;
    };
} // namespace winrt::Telegram::Native::implementation

namespace winrt::Telegram::Native::factory_implementation
{
    struct Direct2DDevice : Direct2DDeviceT<Direct2DDevice, implementation::Direct2DDevice>
    {
    };
} // namespace winrt::Telegram::Native::factory_implementation
