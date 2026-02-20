#ifndef VOIP_VIDEO_OUTPUT_H
#define VOIP_VIDEO_OUTPUT_H

#include "pch.h"

#include <stddef.h>
#include <memory>
#include <mutex>

#include "api/video/i420_buffer.h"
#include "libyuv.h"

#include "api/video/video_frame.h"
#include "api/video/video_source_interface.h"
#include "media/base/video_adapter.h"
#include "media/base/video_broadcaster.h"

#include <winrt/Windows.Storage.h>
#include <winrt/Windows.System.h>

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
#include <windows.ui.composition.interop.h>

#include <d3d11.h>
#include <d2d1_3.h>
#include <d2d1effectauthor.h>
#include <d2d1effecthelpers.h>
#include <vector>
#include <fstream>
#include <unordered_map>

#include "FrameReceivedEventArgs.h"
#include "../Telegram.Native/Helpers/COMHelper.h"

using namespace winrt::Windows::Foundation::Numerics;
using namespace winrt::Windows::Graphics::DirectX;
using namespace winrt::Windows::UI::Composition;

namespace abi
{
    using namespace ABI::Windows::Foundation;
    using namespace ABI::Windows::Graphics;
    using namespace ABI::Windows::Graphics::DirectX;
    using namespace ABI::Windows::Graphics::Effects;
    using namespace ABI::Windows::UI::Composition;
}

DEFINE_GUID(CLSID_YUV420Effect,
    0xABCD1234, 0x5678, 0x9ABC, 0xDE, 0xF0, 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC);

class YUV420Effect : public ID2D1EffectImpl, public ID2D1DrawTransform
{
private:
    LONG m_refCount;
    winrt::com_ptr<ID2D1EffectContext> m_effectContext;
    winrt::com_ptr<ID2D1DrawInfo> m_drawInfo;

public:
    YUV420Effect() : m_refCount(1) {}

    // IUnknown methods
    IFACEMETHODIMP_(ULONG) AddRef() { return InterlockedIncrement(&m_refCount); }

    IFACEMETHODIMP_(ULONG) Release()
    {
        ULONG count = InterlockedDecrement(&m_refCount);
        if (count == 0) { delete this; }
        return count;
    }

    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppOutput)
    {
        if (riid == __uuidof(ID2D1EffectImpl))
        {
            *ppOutput = static_cast<ID2D1EffectImpl*>(this);
        }
        else if (riid == __uuidof(ID2D1DrawTransform))
        {
            *ppOutput = static_cast<ID2D1DrawTransform*>(this);
        }
        else if (riid == __uuidof(ID2D1Transform))
        {
            *ppOutput = static_cast<ID2D1Transform*>(this);
        }
        else if (riid == __uuidof(ID2D1TransformNode))
        {
            *ppOutput = static_cast<ID2D1TransformNode*>(this);
        }
        else if (riid == __uuidof(IUnknown))
        {
            *ppOutput = static_cast<IUnknown*>(static_cast<ID2D1EffectImpl*>(this));
        }
        else
        {
            *ppOutput = nullptr;
            return E_NOINTERFACE;
        }
        AddRef();
        return S_OK;
    }

    // ID2D1EffectImpl methods
    IFACEMETHODIMP Initialize(ID2D1EffectContext* effectContext, ID2D1TransformGraph* transformGraph)
    {
        m_effectContext.copy_from(effectContext);

        // Load the compiled shader
        std::vector<BYTE> shaderCode = LoadShaderFile(L"Assets\\i420.bin");

        HRESULT result;
        ReturnIfFailed(result, effectContext->LoadPixelShader(CLSID_YUV420Effect, shaderCode.data(), static_cast<UINT32>(shaderCode.size())));

        // Set the transform graph
        ReturnIfFailed(result, transformGraph->SetSingleTransformNode(this));

        return S_OK;
    }

    IFACEMETHODIMP PrepareForRender(D2D1_CHANGE_TYPE changeType)
    {
        return S_OK;
    }

    IFACEMETHODIMP SetGraph(ID2D1TransformGraph* transformGraph)
    {
        return E_NOTIMPL;
    }

    // ID2D1Transform methods
    IFACEMETHODIMP MapOutputRectToInputRects(
        const D2D1_RECT_L* outputRect,
        D2D1_RECT_L* inputRects,
        UINT32 inputRectsCount) const
    {

        if (inputRectsCount != 4) return E_INVALIDARG;

        // Input 0 (Y plane) - same size as output
        inputRects[0] = *outputRect;

        // Inputs 1 & 2 (U and V planes) - half size (YUV420 format)
        inputRects[1].left = outputRect->left / 2;
        inputRects[1].top = outputRect->top / 2;
        inputRects[1].right = (outputRect->right + 1) / 2;
        inputRects[1].bottom = (outputRect->bottom + 1) / 2;

        inputRects[2] = inputRects[1];

        // Input 3 (Alpha plane) - same size as output
        inputRects[3] = *outputRect;

        return S_OK;
    }

    IFACEMETHODIMP MapInputRectsToOutputRect(
        const D2D1_RECT_L* inputRects,
        const D2D1_RECT_L* inputOpaqueSubRects,
        UINT32 inputRectCount,
        D2D1_RECT_L* outputRect,
        D2D1_RECT_L* outputOpaqueSubRect)
    {

        if (inputRectCount != 4) return E_INVALIDARG;

        // Output size is determined by the Y plane (input 0)
        *outputRect = inputRects[0];
        *outputOpaqueSubRect = inputOpaqueSubRects[0];

        // No opaque regions if alpha channel is present
        //*outputOpaqueSubRect = D2D1::RectL(0, 0, 0, 0);
        return S_OK;
    }

    IFACEMETHODIMP MapInvalidRect(
        UINT32 inputIndex,
        D2D1_RECT_L invalidInputRect,
        D2D1_RECT_L* invalidOutputRect) const
    {

        // For U and V planes (inputs 1 and 2), scale up by 2x
        if (inputIndex == 1 || inputIndex == 2)
        {
            invalidOutputRect->left = invalidInputRect.left * 2;
            invalidOutputRect->top = invalidInputRect.top * 2;
            invalidOutputRect->right = invalidInputRect.right * 2;
            invalidOutputRect->bottom = invalidInputRect.bottom * 2;
        }
        else
        {
            // For Y and Alpha planes, 1:1 mapping
            *invalidOutputRect = invalidInputRect;
        }

        return S_OK;
    }

    // ID2D1TransformNode methods
    IFACEMETHODIMP_(UINT32) GetInputCount() const { return 4; }

    // ID2D1DrawTransform methods
    IFACEMETHODIMP SetDrawInfo(ID2D1DrawInfo* drawInfo)
    {
        m_drawInfo.copy_from(drawInfo);

        HRESULT result;

        // Set input descriptions to match shader requirements
        // Input 0: SIMPLE (Y plane)
        ReturnIfFailed(result, drawInfo->SetInputDescription(0, D2D1_INPUT_DESCRIPTION{ D2D1_FILTER_MIN_MAG_MIP_POINT, 0 }));

        // Input 1: COMPLEX (U plane)
        ReturnIfFailed(result, drawInfo->SetInputDescription(1, D2D1_INPUT_DESCRIPTION{ D2D1_FILTER_MIN_MAG_MIP_LINEAR, 0 }));

        // Input 2: COMPLEX (V plane)
        ReturnIfFailed(result, drawInfo->SetInputDescription(2, D2D1_INPUT_DESCRIPTION{ D2D1_FILTER_MIN_MAG_MIP_LINEAR, 0 }));

        // Input 3: SIMPLE (Alpha plane)
        ReturnIfFailed(result, drawInfo->SetInputDescription(3, D2D1_INPUT_DESCRIPTION{ D2D1_FILTER_MIN_MAG_MIP_POINT, 0 }));

        ReturnIfFailed(result, drawInfo->SetOutputBuffer(D2D1_BUFFER_PRECISION_8BPC_UNORM, D2D1_CHANNEL_DEPTH_4));

        return drawInfo->SetPixelShader(CLSID_YUV420Effect);
    }

    // Factory method
    static HRESULT __stdcall CreateEffect(IUnknown** effect)
    {
        *effect = static_cast<ID2D1EffectImpl*>(new YUV420Effect());
        return S_OK;
    }

private:
    static std::vector<BYTE> LoadShaderFile(const wchar_t* filename)
    {
        std::ifstream file(filename, std::ios::binary | std::ios::ate);
        winrt::check_bool(file.is_open());

        std::streamsize size = file.tellg();
        file.seekg(0, std::ios::beg);

        std::vector<BYTE> buffer(size);
        winrt::check_bool(file.read(reinterpret_cast<char*>(buffer.data()), size).good());

        return buffer;
    }
};

class CompositionRenderQueue : public std::enable_shared_from_this<CompositionRenderQueue>
{
private:
    struct RenderCommand
    {
        std::function<void()> execute;
        uint64_t source_id;

        RenderCommand() = default;
        RenderCommand(std::function<void()>&& f, uint64_t id)
            : execute(std::move(f)), source_id(id)
        {
        }
    };

    std::unordered_map<uint64_t, RenderCommand> m_pending_commands;
    std::vector<uint64_t> m_render_order;

    std::mutex m_mutex;
    std::condition_variable m_cv;
    std::atomic<bool> m_running{ false };

    static void render_loop(std::weak_ptr<CompositionRenderQueue> weak_self)
    {
        SetThreadDescription(GetCurrentThread(), L"CompositionRenderThread");

        std::vector<RenderCommand> batch;
        batch.reserve(32);

        while (auto self = weak_self.lock())
        {
            if (!self->m_running.load(std::memory_order_acquire))
            {
                break;
            }

            {
                std::unique_lock<std::mutex> lock(self->m_mutex);

                // Wait for commands with timeout to check m_running flag
                self->m_cv.wait_for(lock, std::chrono::milliseconds(10),
                    [&] { return !self->m_pending_commands.empty() || !self->m_running.load(std::memory_order_acquire); });

                if (!self->m_running.load(std::memory_order_acquire))
                {
                    break;
                }

                for (uint64_t source_id : self->m_render_order)
                {
                    auto it = self->m_pending_commands.find(source_id);
                    if (it != self->m_pending_commands.end())
                    {
                        batch.push_back(std::move(it->second));
                        self->m_pending_commands.erase(it);
                    }
                }

                self->m_render_order.clear();
            }

            for (auto& cmd : batch)
            {
                if (cmd.execute)
                {
                    try
                    {
                        cmd.execute();
                    }
                    catch (...)
                    {
                        // Meh
                    }
                }
            }

            batch.clear();
        }
    }

    CompositionRenderQueue() = default;

public:
    ~CompositionRenderQueue()
    {
        shutdown();
    }

    void start_thread()
    {
        m_running.store(true, std::memory_order_release);
        std::thread([weak_self{ weak_from_this() }]()
            {
                render_loop(weak_self);
            }).detach();
    }

    static std::shared_ptr<CompositionRenderQueue> Create()
    {
        auto instance = std::shared_ptr<CompositionRenderQueue>(new CompositionRenderQueue());
        instance->start_thread();
        return instance;
    }

    template<typename F>
    void enqueue(F&& func, uint64_t source_id)
    {
        {
            std::lock_guard<std::mutex> lock(m_mutex);

            auto it = m_pending_commands.find(source_id);
            if (it != m_pending_commands.end())
            {
                it->second = RenderCommand(std::forward<F>(func), source_id);
            }
            else
            {
                m_pending_commands.emplace(source_id, RenderCommand(std::forward<F>(func), source_id));
                m_render_order.push_back(source_id);
            }
        }
        m_cv.notify_one();
    }

    void shutdown()
    {
        if (m_running.exchange(false, std::memory_order_acq_rel))
        {
            m_cv.notify_all();
        }
    }
};

class RenderQueueManager
{
private:
    struct DeviceHash
    {
        size_t operator()(CompositionGraphicsDevice const& device) const
        {
            return std::hash<void*>{}(winrt::get_abi(device));
        }
    };

    struct DeviceEqual
    {
        bool operator()(CompositionGraphicsDevice const& a,
            CompositionGraphicsDevice const& b) const
        {
            return winrt::get_abi(a) == winrt::get_abi(b);
        }
    };

    std::unordered_map<
        CompositionGraphicsDevice,
        std::weak_ptr<CompositionRenderQueue>,
        DeviceHash,
        DeviceEqual> m_queues;
    std::mutex m_mutex;

public:
    std::shared_ptr<CompositionRenderQueue> get_queue(CompositionGraphicsDevice const& device)
    {
        std::lock_guard<std::mutex> lock(m_mutex);

        auto it = m_queues.find(device);
        if (it != m_queues.end())
        {
            // Try to lock weak_ptr
            if (auto queue = it->second.lock())
            {
                return queue;
            }

            // Expired, remove it
            m_queues.erase(it);
        }

        auto queue = CompositionRenderQueue::Create();
        m_queues[device] = queue;

        return queue;
    }

    void cleanup()
    {
        std::lock_guard<std::mutex> lock(m_mutex);

        for (auto it = m_queues.begin(); it != m_queues.end(); )
        {
            if (it->second.expired())
            {
                it = m_queues.erase(it);
            }
            else
            {
                ++it;
            }
        }
    }

    void shutdown_all()
    {
        std::lock_guard<std::mutex> lock(m_mutex);

        for (auto& [device, weak_queue] : m_queues)
        {
            if (auto queue = weak_queue.lock())
            {
                queue->shutdown();
            }
        }

        m_queues.clear();
    }

    static RenderQueueManager& instance()
    {
        static RenderQueueManager instance;
        return instance;
    }
};

struct VoipVideoOutput : public std::enable_shared_from_this<VoipVideoOutput>, rtc::VideoSinkInterface<webrtc::VideoFrame>
{
    std::atomic<bool> m_disposed{ false };
    std::atomic<bool> m_deviceLost{ false };
    std::atomic<bool> m_mirrored{ false };

    mutable std::recursive_mutex m_deviceMutex;
    mutable winrt::slim_mutex m_frameMutex;

    std::atomic<bool> m_resourcesValid{ false };

    SIZE m_surfaceSize{ 0,0 };
    D2D1_SIZE_U m_bitmapSize{ 0,0 };

    CompositionGraphicsDevice m_compositionDevice;
    CompositionSurfaceBrush m_brush{ nullptr };
    winrt::com_ptr<abi::ICompositionDrawingSurfaceInterop> m_surface;
    winrt::com_ptr<ID2D1Device> m_d2dDevice;
    winrt::com_ptr<ID2D1Effect> m_shader{ nullptr };
    winrt::com_ptr<ID2D1Bitmap1> m_bitmapY{ nullptr };
    winrt::com_ptr<ID2D1Bitmap1> m_bitmapU{ nullptr };
    winrt::com_ptr<ID2D1Bitmap1> m_bitmapV{ nullptr };
    std::shared_ptr<CompositionRenderQueue> m_queue;

    winrt::com_ptr<winrt::Telegram::Native::Calls::implementation::FrameReceivedEventArgs> m_frameReceivedArgs;
    winrt::event<winrt::Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Calls::VoipVideoOutputSink,
        winrt::Telegram::Native::Calls::FrameReceivedEventArgs>> m_frameReceivedEventSource;

    winrt::event_token m_renderingDeviceReplaced;

private:
    static inline std::atomic<uint64_t> s_nextInstanceId{ 1 };
    const uint64_t m_instanceId;

public:
    void ReleaseShader()
    {
        // Should be called under m_deviceMutex
        m_shader = nullptr;
        m_bitmapY = nullptr;
        m_bitmapU = nullptr;
        m_bitmapV = nullptr;

        m_resourcesValid = false;
    }

private:
    void OnRenderingDeviceReplaced(CompositionGraphicsDevice const&, RenderingDeviceReplacedEventArgs const&)
    {
        std::lock_guard<std::recursive_mutex> guard(m_deviceMutex);

        if (m_disposed.load())
        {
            return;
        }

        if (m_surface)
        {
            m_resourcesValid = true;
        }
        else
        {
            try
            {
                auto surface = m_compositionDevice.CreateDrawingSurface2({ 0, 0 }, DirectXPixelFormat::B8G8R8A8UIntNormalized, DirectXAlphaMode::Premultiplied);
                m_surface = surface.as<abi::ICompositionDrawingSurfaceInterop>();
                m_resourcesValid = true;
            }
            catch (...)
            {
                m_resourcesValid = false;
            }
        }
    }

public:
    VoipVideoOutput(CompositionGraphicsDevice const& device, SpriteVisual const& visual, bool mirrored, bool uniformToFill)
        : m_compositionDevice(device)
        , m_queue(RenderQueueManager::instance().get_queue(device))
        , m_instanceId(s_nextInstanceId.fetch_add(1, std::memory_order_relaxed))
    {
        m_frameReceivedArgs = winrt::make_self<winrt::Telegram::Native::Calls::implementation::FrameReceivedEventArgs>(0, 0);
        m_mirrored = mirrored;
        m_renderingDeviceReplaced = m_compositionDevice.RenderingDeviceReplaced({ this, &VoipVideoOutput::OnRenderingDeviceReplaced });

        CompositionDrawingSurface surface{ nullptr };
        try
        {
            surface = m_compositionDevice.CreateDrawingSurface2({ 0, 0 }, DirectXPixelFormat::B8G8R8A8UIntNormalized, DirectXAlphaMode::Premultiplied);
            m_surface = surface.as<abi::ICompositionDrawingSurfaceInterop>();
            m_resourcesValid = true;
        }
        catch (...)
        {
            m_resourcesValid = false;
        }
        m_brush = visual.Compositor().CreateSurfaceBrush(surface);
        m_brush.HorizontalAlignmentRatio(.5);
        m_brush.VerticalAlignmentRatio(.5);
        m_brush.Stretch(uniformToFill ? CompositionStretch::UniformToFill : CompositionStretch::Uniform);

        visual.Brush(m_brush);
    }

    ~VoipVideoOutput()
    {
        m_disposed = true;
        m_compositionDevice.RenderingDeviceReplaced(m_renderingDeviceReplaced);

        std::lock_guard<std::recursive_mutex> deviceGuard(m_deviceMutex);
        winrt::slim_lock_guard const frameGuard(m_frameMutex);

        try
        {
            ReleaseShader();
        }
        catch (...) {}

        //try
        //{
        //    if (m_surface)
        //    {
        //        m_surface.Close();
        //        m_surface = nullptr;
        //    }
        //}
        //catch (...) {}

        try
        {
            if (m_brush)
            {
                m_brush.Close();
                m_brush = nullptr;
            }
        }
        catch (...) {}
    }

    std::atomic<int32_t> m_pixelWidth{ 0 };
    std::atomic<int32_t> m_pixelHeight{ 0 };

    HRESULT IsEffectRegistered(winrt::com_ptr<ID2D1Factory1> factory, IID const& effectId)
    {
        // Size query.
        UINT32 returnedCount, registeredCount;

        HRESULT result;
        ReturnIfFailed(result, factory->GetRegisteredEffects(nullptr, 0, &returnedCount, &registeredCount));

        // Read the data.
        std::vector<IID> ids(registeredCount);

        ReturnIfFailed(result, factory->GetRegisteredEffects(ids.data(), registeredCount, &returnedCount, &registeredCount));

        if (std::find(ids.begin(), ids.end(), effectId) != ids.end())
        {
            return S_OK;
        }

        return S_FALSE;
    }

    HRESULT RegisterEffect(winrt::com_ptr<ID2D1Device> const& device)
    {
        winrt::com_ptr<ID2D1Factory> factory;
        device->GetFactory(factory.put());

        winrt::com_ptr<ID2D1Factory1> factory1;
        factory1 = factory.as<ID2D1Factory1>();

        HRESULT result;
        ReturnIfFailed(result, IsEffectRegistered(factory1, CLSID_YUV420Effect));

        if (result == S_OK)
        {
            return S_OK;
        }

        return factory1->RegisterEffectFromString(
            CLSID_YUV420Effect,
            L"<?xml version='1.0'?>"
            L"<Effect>"
            L"  <Property name='DisplayName' type='string' value='YUV420 to RGB Effect'/>"
            L"  <Property name='Author'      type='string' value='Telegram'/>"
            L"  <Property name='Category'    type='string' value='Video'/>"
            L"  <Property name='Description' type='string' value='YUV420 to RGB Effect'/>"
            L"  <Inputs>"
            L"    <Input name='Y'/>"
            L"    <Input name='U'/>"
            L"    <Input name='V'/>"
            L"    <Input name='Alpha'/>"
            L"  </Inputs>"
            L"</Effect>",
            nullptr,
            0,
            YUV420Effect::CreateEffect
        );
    }

    inline HRESULT RenderFrame(const webrtc::VideoFrame& frame)
    {
        HRESULT result;
        rtc::scoped_refptr<webrtc::I420BufferInterface> buffer(frame.video_frame_buffer()->ToI420());

        int32_t width = buffer->width();
        int32_t height = buffer->height();

        D2D1::Matrix3x2F matrix;
        SIZE finalSize{ width, height };

        switch (frame.rotation())
        {
        case webrtc::kVideoRotation_0:
            matrix = D2D1::Matrix3x2F::Identity();
            break;
        case webrtc::kVideoRotation_180:
            matrix = D2D1::Matrix3x2F::Rotation(180, D2D1::Point2F(width / 2, height / 2));
            break;
        case webrtc::kVideoRotation_90:
            finalSize = SIZE(height, width);
            matrix = D2D1::Matrix3x2F::Rotation(90, D2D1::Point2F(height / 2, width / 2));
            break;
        case webrtc::kVideoRotation_270:
            finalSize = SIZE(height, width);
            matrix = D2D1::Matrix3x2F::Rotation(270, D2D1::Point2F(height / 2, width / 2));
            break;
        }

        float x = (finalSize.cx - width) / 2;
        float y = (finalSize.cy - height) / 2;

        m_pixelWidth = m_frameReceivedArgs->m_pixelWidth = finalSize.cx;
        m_pixelHeight = m_frameReceivedArgs->m_pixelHeight = finalSize.cy;
        m_frameReceivedEventSource(nullptr, *m_frameReceivedArgs);

        std::lock_guard<std::recursive_mutex> deviceGuard(m_deviceMutex);

        if (m_disposed.load()) return S_FALSE;

        if (finalSize.cx != m_surfaceSize.cx || finalSize.cy != finalSize.cy)
        {
            result = m_surface->Resize(finalSize);
            if (FAILED(result))
            {
                ReleaseShader();
                return result;
            }

            m_surfaceSize = finalSize;
        }

        winrt::com_ptr<ID2D1DeviceContext> d2dContext;
        POINT offset;

        // BeginDraw can return DXGI_ERROR_DEVICE_REMOVED, if it happens we just return.
        // PlaceholderImageHelper will be handling this for us, raising RenderingDeviceReplaced.
        result = m_surface->BeginDraw(nullptr, __uuidof(ID2D1DeviceContext), d2dContext.put_void(), &offset);
        if (FAILED(result))
        {
            ReleaseShader();
            return result;
        }

        if (m_shader == nullptr ||
            m_bitmapSize.width != width ||
            m_bitmapSize.height != height)
        {
            ReleaseShader();

            winrt::com_ptr<ID2D1Device> d2dDevice;
            d2dContext->GetDevice(d2dDevice.put());
            ReturnIfFailed(result, RegisterEffect(d2dDevice));

            D2D1_SIZE_U sizeY{ width, height };
            D2D1_SIZE_U sizeUV{ width / 2, height / 2 };

            D2D1_BITMAP_PROPERTIES1 properties = { { DXGI_FORMAT_R8_UNORM, D2D1_ALPHA_MODE_IGNORE }, 96, 96, D2D1_BITMAP_OPTIONS_NONE, 0 };
            CleanupIfFailed(result, d2dContext->CreateBitmap(sizeY, buffer->DataY(), buffer->StrideY(), &properties, m_bitmapY.put()));
            CleanupIfFailed(result, d2dContext->CreateBitmap(sizeUV, buffer->DataU(), buffer->StrideU(), &properties, m_bitmapU.put()));
            CleanupIfFailed(result, d2dContext->CreateBitmap(sizeUV, buffer->DataV(), buffer->StrideV(), &properties, m_bitmapV.put()));

            if (m_shader == nullptr)
            {
                CleanupIfFailed(result, d2dContext->CreateEffect(CLSID_YUV420Effect, m_shader.put()));
            }

            if (m_shader)
            {
                std::unique_ptr<uint8_t[]> fill(new uint8_t[width * height * 4]);
                std::fill_n(fill.get(), width * height * 4, 0xFF);
                winrt::com_ptr<ID2D1Bitmap1> bgra;
                properties = { { DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED }, 96, 96, D2D1_BITMAP_OPTIONS_NONE, 0 };
                CleanupIfFailed(result, d2dContext->CreateBitmap(sizeY, fill.get(), width * 4, &properties, bgra.put()));

                m_shader->SetInput(0, m_bitmapY.get());
                m_shader->SetInput(1, m_bitmapU.get());
                m_shader->SetInput(2, m_bitmapV.get());
                m_shader->SetInput(3, bgra.get());
            }

            m_bitmapSize = D2D1_SIZE_U(width, height);
            m_resourcesValid = true;
        }
        else
        {
            D2D1_RECT_U rectY{ 0, 0, width, height };
            D2D1_RECT_U rectUV{ 0, 0, width / 2, height / 2 };

            CleanupIfFailed(result, m_bitmapY->CopyFromMemory(&rectY, buffer->DataY(), buffer->StrideY()));
            CleanupIfFailed(result, m_bitmapU->CopyFromMemory(&rectUV, buffer->DataU(), buffer->StrideU()));
            CleanupIfFailed(result, m_bitmapV->CopyFromMemory(&rectUV, buffer->DataV(), buffer->StrideV()));
        }

        if (m_shader && m_surface)
        {
            if (m_mirrored)
            {
                matrix = matrix * D2D1::Matrix3x2F::Scale(m_mirrored ? -1 : 1, 1, D2D1::Point2F(finalSize.cx / 2, finalSize.cy / 2));
            }

            matrix._31 += offset.x;
            matrix._32 += offset.y;

            d2dContext->Clear();
            d2dContext->SetTransform(matrix);
            d2dContext->DrawImage(m_shader.get(), D2D1::Point2F(x, y));
        }

    Cleanup:
        return m_surface->EndDraw();
    }

    void OnFrame(const webrtc::VideoFrame& frame) override
    {
        if (m_disposed.load())
        {
            return;
        }

        winrt::slim_lock_guard const frameGuard(m_frameMutex);

        if (m_disposed.load() || !m_surface || !m_resourcesValid.load())
        {
            return;
        }

        const auto weak = weak_from_this();
        m_queue->enqueue([weak, frame]() {
            if (auto strong = weak.lock())
            {
                strong->RenderFrame(frame);
            }
            }, m_instanceId);
    }
};

#endif // VOIP_VIDEO_OUTPUT_H
