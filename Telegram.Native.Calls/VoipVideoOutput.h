#ifndef VOIP_VIDEO_OUTPUT_H
#define VOIP_VIDEO_OUTPUT_H

#include "pch.h"

#include <stddef.h>
#include <memory>
#include <mutex>

#include "api/video/i420_buffer.h"
#include "third_party/libyuv/include/libyuv.h"

#include "api/video/video_frame.h"
#include "api/video/video_source_interface.h"
#include "modules/video_coding/codecs/h264/win/d3d11_texture_buffer.h"
#include "media/base/video_adapter.h"
#include "media/base/video_broadcaster.h"
#include "rtc_base/logging.h"
#include "rtc_base/string_utils.h"

#include <winrt/Windows.Storage.h>
#include <winrt/Windows.System.h>

#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.UI.h>
#include <winrt/Windows.UI.Composition.h>
#include <winrt/Windows.UI.Core.h>
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
#include <d3d11_4.h>
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

    Compositor m_compositor{ nullptr };

    // Shared with the commit's completion, which can outlive the queue once a wait times out.
    std::shared_ptr<winrt::handle> m_committed = std::make_shared<winrt::handle>(CreateEventW(nullptr, FALSE, FALSE, nullptr));

    // Render thread only.
    bool m_awaiting_commit{ false };

    // Every RenderFrame is a BeginDraw/EndDraw, and DComp keeps each update until a commit takes
    // it. Commits stop behind a stalled UI thread and in a window nobody composes, while frames keep
    // arriving at the video's rate, so a batch waits for the previous one's commit. Frames that
    // arrive meanwhile replace each other in m_pending_commands.
    void request_commit()
    {
        if (m_compositor == nullptr || !*m_committed)
        {
            return;
        }

        try
        {
            m_compositor.RequestCommitAsync().Completed([committed = m_committed](auto&&, auto&&)
            {
                SetEvent(committed->get());
            });

            m_awaiting_commit = true;
        }
        catch (winrt::hresult_error const&)
        {
            // The compositor is going away with its window, and there is nothing left to wait for.
            m_awaiting_commit = false;
        }
    }

    // One slice at a time, so that the loop lets go of the queue in between and a queue whose
    // outputs are all gone is not kept alive by a commit that may never come.
    bool wait_for_commit()
    {
        if (WaitForSingleObject(m_committed->get(), 250) == WAIT_OBJECT_0)
        {
            m_awaiting_commit = false;
            return true;
        }

        request_commit();
        return false;
    }

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

            if (self->m_awaiting_commit && !self->wait_for_commit())
            {
                continue;
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

            if (!batch.empty())
            {
                self->request_commit();
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

    static std::shared_ptr<CompositionRenderQueue> Create(CompositionGraphicsDevice const& device)
    {
        auto instance = std::shared_ptr<CompositionRenderQueue>(new CompositionRenderQueue());
        instance->m_compositor = device.Compositor();
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

        auto queue = CompositionRenderQueue::Create(device);
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

    // What this output opened of a decoder's shared textures, under m_deviceMutex. Keyed by
    // ring, because a handle value can come back for another texture once a ring is gone.
    winrt::com_ptr<ID3D11Device> m_textureDevice;
    LUID m_textureAdapter{};
    uint64_t m_textureRing{ 0 };
    winrt::com_ptr<ID3D11Fence> m_textureFence;
    std::unordered_map<int, winrt::com_ptr<ID3D11Texture2D>> m_textures;
    bool m_textureFramesFailed{ false };

    // This output's own fence, which the decoder waits on before reusing a slot it drew.
    winrt::com_ptr<ID3D11DeviceContext4> m_textureContext;
    winrt::com_ptr<ID2D1Multithread> m_textureMultithread;
    winrt::com_ptr<ID3D11Fence> m_readFence;
    winrt::handle m_readFenceHandle;
    uint64_t m_readFenceValue{ 0 };
    uint64_t m_readerId{ 0 };

    winrt::com_ptr<winrt::Telegram::Native::Calls::implementation::FrameReceivedEventArgs> m_frameReceivedArgs;
    winrt::event<winrt::Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Calls::VoipVideoOutputSink,
        winrt::Telegram::Native::Calls::FrameReceivedEventArgs>> m_frameReceivedEventSource;

    winrt::event_token m_renderingDeviceReplaced;

    // The view this output draws into. Nothing is drawn while it is off screen, so its
    // visibility is watched here rather than plumbed down from the app. Null in the
    // flavours that have no CoreWindow, where the output simply always draws.
    winrt::Windows::UI::Core::CoreWindow m_coreWindow{ nullptr };
    winrt::event_token m_visibilityChanged{};
    std::atomic<bool> m_visible{ true };

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
    void OnVisibilityChanged(winrt::Windows::UI::Core::CoreWindow const&,
        winrt::Windows::UI::Core::VisibilityChangedEventArgs const& args)
    {
        m_visible.store(args.Visible(), std::memory_order_relaxed);
    }

    void OnRenderingDeviceReplaced(CompositionGraphicsDevice const&, RenderingDeviceReplacedEventArgs const&)
    {
        std::lock_guard<std::recursive_mutex> guard(m_deviceMutex);

        if (m_disposed.load())
        {
            return;
        }

        // The effect and the three bitmaps were realized on the device that's being replaced:
        // using them against a context from the new device fails with D2DERR_WRONG_RESOURCE_DOMAIN.
        ReleaseShader();

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

        // Drawing into a window that is off screen is not free and buys nothing: the
        // surface updates are never consumed, and they accumulate in graphics memory -
        // which a user hit as gigabytes of growth while a call window sat minimized, all
        // of it released the moment the window came back. Constructed on the view's own
        // thread, so this is that view's window.
        try
        {
            m_coreWindow = winrt::Windows::UI::Core::CoreWindow::GetForCurrentThread();
        }
        catch (...)
        {
            m_coreWindow = nullptr;
        }

        if (m_coreWindow)
        {
            m_visible = m_coreWindow.Visible();
            m_visibilityChanged = m_coreWindow.VisibilityChanged(
                { this, &VoipVideoOutput::OnVisibilityChanged });
        }
    }

    ~VoipVideoOutput()
    {
        m_disposed = true;
        m_compositionDevice.RenderingDeviceReplaced(m_renderingDeviceReplaced);

        if (m_coreWindow && m_visibilityChanged)
        {
            m_coreWindow.VisibilityChanged(m_visibilityChanged);
            m_visibilityChanged = {};
        }

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

        // A texture frame is drawn straight off the GPU. Anything else, or a texture this
        // output cannot open, goes through I420 and YUV420Effect as before.
        const webrtc::D3D11TextureBuffer* texture = m_textureFramesFailed
            ? nullptr
            : webrtc::D3D11TextureBuffer::Is(frame.video_frame_buffer().get());

        int32_t width = frame.width();
        int32_t height = frame.height();

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

        if (finalSize.cx != m_surfaceSize.cx || finalSize.cy != m_surfaceSize.cy)
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
        // Direct2DDevice will be handling this for us, raising RenderingDeviceReplaced.
        result = m_surface->BeginDraw(nullptr, __uuidof(ID2D1DeviceContext), d2dContext.put_void(), &offset);
        if (FAILED(result))
        {
            ReleaseShader();
            return result;
        }

        if (m_mirrored)
        {
            matrix = matrix * D2D1::Matrix3x2F::Scale(-1, 1, D2D1::Point2F(finalSize.cx / 2, finalSize.cy / 2));
        }

        matrix._31 += offset.x;
        matrix._32 += offset.y;

        bool drewTexture = false;

        if (texture != nullptr)
        {
            const HRESULT textureResult = DrawTexture(d2dContext.get(), *texture, matrix, x, y, width, height);
            drewTexture = SUCCEEDED(textureResult);

            if (!drewTexture)
            {
                // Not retried: what fails here - another adapter, a device without D3D11.4 -
                // fails the same way every frame.
                m_textureFramesFailed = true;
                RTC_LOG(LS_WARNING) << "Video output can't draw texture ring " << texture->ring_id()
                    << ": 0x" << rtc::ToHex(textureResult) << "; reading frames back to I420";
            }
        }

        if (!drewTexture)
        {
            rtc::scoped_refptr<webrtc::I420BufferInterface> buffer(frame.video_frame_buffer()->ToI420());
            if (buffer != nullptr)
            {
                DrawI420(d2dContext.get(), buffer, matrix, x, y, width, height);
            }
        }

        result = m_surface->EndDraw();

        if (drewTexture && SUCCEEDED(result))
        {
            SignalTextureRead(*texture);
        }

        return result;
    }

    // After EndDraw, which is when Direct2D hands the draw to the immediate context, so the
    // fence follows it there. Until the decoder holds this, its next copy into the slot could
    // overwrite the frame while the draw is still queued: releasing the buffer only means the
    // CPU is done with it.
    void SignalTextureRead(webrtc::D3D11TextureBuffer const& texture)
    {
        HRESULT result;

        if (m_textureMultithread)
        {
            m_textureMultithread->Enter();
        }

        result = m_textureContext->Signal(m_readFence.get(), m_readFenceValue + 1);

        // Composition stops flushing behind a minimized window, and the decoder would wait
        // on a signal still sitting in this context.
        m_textureContext->Flush();

        if (m_textureMultithread)
        {
            m_textureMultithread->Leave();
        }

        if (SUCCEEDED(result))
        {
            texture.AddReadFence(m_readerId, m_readFenceHandle.get(), ++m_readFenceValue);
        }
    }

    // The decoder's texture, opened on this output's device and drawn through Direct2D, which
    // converts NV12 itself. Fails without drawing whenever the texture cannot be used here.
    HRESULT DrawTexture(ID2D1DeviceContext* d2dContext, webrtc::D3D11TextureBuffer const& texture,
        D2D1::Matrix3x2F const& matrix, float x, float y, int32_t width, int32_t height)
    {
        HRESULT result;

        winrt::com_ptr<ID2D1DeviceContext2> d2dContext2;
        ReturnIfFailed(result, d2dContext->QueryInterface(IID_PPV_ARGS(d2dContext2.put())));

        // The device behind the surface being drawn, which is the one composition uses.
        winrt::com_ptr<ID2D1Image> target;
        d2dContext->GetTarget(target.put());

        auto targetBitmap = target.try_as<ID2D1Bitmap1>();
        if (targetBitmap == nullptr)
        {
            return E_NOINTERFACE;
        }

        winrt::com_ptr<IDXGISurface> targetSurface;
        ReturnIfFailed(result, targetBitmap->GetSurface(targetSurface.put()));

        winrt::com_ptr<ID3D11Device> device;
        ReturnIfFailed(result, targetSurface->GetDevice(IID_PPV_ARGS(device.put())));

        if (device != m_textureDevice)
        {
            m_textureDevice = nullptr;
            m_textureAdapter = {};
            m_textureRing = 0;
            m_textureFence = nullptr;
            m_textures.clear();
            m_textureContext = nullptr;
            m_readFence = nullptr;
            m_readFenceHandle.close();
            m_readFenceValue = 0;

            auto device5 = device.try_as<ID3D11Device5>();
            if (device5 == nullptr)
            {
                return E_NOINTERFACE;
            }

            winrt::com_ptr<ID3D11DeviceContext> immediate;
            device->GetImmediateContext(immediate.put());

            m_textureContext = immediate.try_as<ID3D11DeviceContext4>();
            if (m_textureContext == nullptr)
            {
                return E_NOINTERFACE;
            }

            // The immediate context is shared with composition and Direct2D, which use it from
            // other threads under Direct2D's lock.
            winrt::com_ptr<ID2D1Factory> factory;
            d2dContext->GetFactory(factory.put());
            m_textureMultithread = factory.try_as<ID2D1Multithread>();

            ReturnIfFailed(result, device5->CreateFence(0, D3D11_FENCE_FLAG_SHARED, IID_PPV_ARGS(m_readFence.put())));
            ReturnIfFailed(result, m_readFence->CreateSharedHandle(nullptr, GENERIC_ALL, nullptr, m_readFenceHandle.put()));

            // A new id with every fence: the decoder caches what it opened by id.
            m_readerId = webrtc::D3D11TextureBuffer::NewReaderId();
            m_textureDevice = device;

            winrt::com_ptr<IDXGIAdapter> adapter;
            DXGI_ADAPTER_DESC description{};
            if (auto dxgiDevice = device.try_as<IDXGIDevice>(); dxgiDevice &&
                SUCCEEDED(dxgiDevice->GetAdapter(adapter.put())) && SUCCEEDED(adapter->GetDesc(&description)))
            {
                m_textureAdapter = description.AdapterLuid;
            }
        }

        // A WARP renderer, or a hybrid laptop decoding on the other GPU.
        const LUID adapter = texture.adapter_luid();
        if (adapter.LowPart != m_textureAdapter.LowPart || adapter.HighPart != m_textureAdapter.HighPart)
        {
            return E_FAIL;
        }

        if (texture.ring_id() != m_textureRing)
        {
            m_textureRing = texture.ring_id();
            m_textureFence = nullptr;
            m_textures.clear();

            auto device5 = device.try_as<ID3D11Device5>();
            if (device5 == nullptr)
            {
                return E_NOINTERFACE;
            }

            ReturnIfFailed(result, device5->OpenSharedFence(texture.fence_handle(), IID_PPV_ARGS(m_textureFence.put())));
        }

        auto& opened = m_textures[texture.slot()];
        if (opened == nullptr)
        {
            auto device1 = device.try_as<ID3D11Device1>();
            if (device1 == nullptr)
            {
                return E_NOINTERFACE;
            }

            ReturnIfFailed(result, device1->OpenSharedResource1(texture.texture_handle(), IID_PPV_ARGS(opened.put())));
        }

        auto surface = opened.try_as<IDXGISurface>();
        if (surface == nullptr || m_textureFence == nullptr)
        {
            return E_FAIL;
        }

        if (m_textureMultithread)
        {
            m_textureMultithread->Enter();
        }

        // On the GPU: the draw below waits for the decoder's copy into this slot, and the
        // CPU does not.
        result = m_textureContext->Wait(m_textureFence.get(), texture.fence_value());

        if (m_textureMultithread)
        {
            m_textureMultithread->Leave();
        }

        if (FAILED(result))
        {
            return result;
        }

        const DXGI_COLOR_SPACE_TYPE colorSpace = texture.bt709()
            ? (texture.full_range() ? DXGI_COLOR_SPACE_YCBCR_FULL_G22_LEFT_P709 : DXGI_COLOR_SPACE_YCBCR_STUDIO_G22_LEFT_P709)
            : (texture.full_range() ? DXGI_COLOR_SPACE_YCBCR_FULL_G22_LEFT_P601 : DXGI_COLOR_SPACE_YCBCR_STUDIO_G22_LEFT_P601);

        IDXGISurface* surfaces[] = { surface.get() };
        winrt::com_ptr<ID2D1ImageSource> source;
        ReturnIfFailed(result, d2dContext2->CreateImageSourceFromDxgi(surfaces, 1, colorSpace, D2D1_IMAGE_SOURCE_FROM_DXGI_OPTIONS_NONE, source.put()));

        // The texture is rounded up to even sizes; only the visible area is drawn.
        const D2D1_RECT_F visible = D2D1::RectF(0, 0, static_cast<float>(width), static_cast<float>(height));
        const D2D1_POINT_2F offset = D2D1::Point2F(x, y);

        d2dContext->Clear();
        d2dContext->SetTransform(matrix);
        d2dContext->DrawImage(source.get(), &offset, &visible);
        return S_OK;
    }

    HRESULT DrawI420(ID2D1DeviceContext* d2dContext, rtc::scoped_refptr<webrtc::I420BufferInterface> const& buffer,
        D2D1::Matrix3x2F const& matrix, float x, float y, int32_t width, int32_t height)
    {
        HRESULT result = S_OK;

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
            d2dContext->Clear();
            d2dContext->SetTransform(matrix);
            d2dContext->DrawImage(m_shader.get(), D2D1::Point2F(x, y));
        }

    Cleanup:
        return result;
    }

    void OnFrame(const webrtc::VideoFrame& frame) override
    {
        // A frame dropped here costs nothing to recover: the next one is a whole picture,
        // and the brush keeps showing the last one that was drawn.
        if (m_disposed.load() || !m_visible.load(std::memory_order_relaxed))
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
