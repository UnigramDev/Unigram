#include "pch.h"
#include "MediaVideoRenderer.h"
#include "AsyncMediaPlayerSwapChain.h"

#include <winrt/Telegram.Native.h>

#include <chrono>
#include <format>
#include <thread>

#include <libyuv.h>

extern "C"
{
#include <libavutil/hwcontext.h>
#include <libavutil/hwcontext_d3d11va.h>
#include <libavutil/pixfmt.h>
}

namespace winrt::Telegram::Native::Media::implementation
{
    namespace
    {
        struct ContextLock
        {
            explicit ContextLock(HANDLE mutex) noexcept
                : m_mutex(mutex)
            {
                if (m_mutex != nullptr)
                {
                    WaitForSingleObjectEx(m_mutex, INFINITE, FALSE);
                }
            }

            ~ContextLock() noexcept
            {
                if (m_mutex != nullptr)
                {
                    ReleaseMutex(m_mutex);
                }
            }

            ContextLock(const ContextLock&) = delete;
            ContextLock& operator=(const ContextLock&) = delete;

        private:
            HANDLE m_mutex;
        };

        // A second loss within this long turns decoding on the GPU off for the player.
        constexpr double kRepeatedLoss = 60;

        // While a driver resets, creating a device fails for a while. Tried this many times, with
        // the wait doubling from the first, before giving up.
        constexpr int kRecoveryAttempts = 8;
        constexpr int kRecoveryWait = 250;

        constexpr UINT Even(int32_t value) noexcept
        {
            return static_cast<UINT>((value + 1) & ~1);
        }

        bool IsFullRange(AVFrame const* frame) noexcept
        {
            return frame->color_range == AVCOL_RANGE_JPEG || frame->format == AV_PIX_FMT_YUVJ420P;
        }

        enum class Matrix
        {
            BT601,
            BT709,
            BT2020
        };

        // An untagged stream is BT.709 from HD up and BT.601 below it, which is what encoders
        // assume when they leave the tag out.
        Matrix MatrixOf(AVFrame const* frame) noexcept
        {
            switch (frame->colorspace)
            {
            case AVCOL_SPC_BT709:
                return Matrix::BT709;
            case AVCOL_SPC_BT2020_NCL:
            case AVCOL_SPC_BT2020_CL:
                return Matrix::BT2020;
            case AVCOL_SPC_BT470BG:
            case AVCOL_SPC_SMPTE170M:
            case AVCOL_SPC_SMPTE240M:
            case AVCOL_SPC_FCC:
                return Matrix::BT601;
            default:
                return frame->height >= 720 ? Matrix::BT709 : Matrix::BT601;
            }
        }

        DXGI_COLOR_SPACE_TYPE ColorSpaceOf(AVFrame const* frame) noexcept
        {
            // HDR, as phones record it. Told what it is, the video processor maps it down to the
            // SDR output; told nothing, it would show the PQ or HLG signal as is, washed out.
            if (frame->color_trc == AVCOL_TRC_SMPTE2084)
            {
                return DXGI_COLOR_SPACE_YCBCR_STUDIO_G2084_LEFT_P2020;
            }
            else if (frame->color_trc == AVCOL_TRC_ARIB_STD_B67)
            {
                return DXGI_COLOR_SPACE_YCBCR_STUDIO_GHLG_TOPLEFT_P2020;
            }

            const bool full = IsFullRange(frame);

            switch (MatrixOf(frame))
            {
            case Matrix::BT2020:
                return full ? DXGI_COLOR_SPACE_YCBCR_FULL_G22_LEFT_P2020 : DXGI_COLOR_SPACE_YCBCR_STUDIO_G22_LEFT_P2020;
            case Matrix::BT709:
                return full ? DXGI_COLOR_SPACE_YCBCR_FULL_G22_LEFT_P709 : DXGI_COLOR_SPACE_YCBCR_STUDIO_G22_LEFT_P709;
            default:
                return full ? DXGI_COLOR_SPACE_YCBCR_FULL_G22_LEFT_P601 : DXGI_COLOR_SPACE_YCBCR_STUDIO_G22_LEFT_P601;
            }
        }

        libyuv::YuvConstants const* YuvConstantsOf(AVFrame const* frame) noexcept
        {
            const bool full = IsFullRange(frame);

            switch (MatrixOf(frame))
            {
            case Matrix::BT601:
                return full ? &libyuv::kYuvJPEGConstants : &libyuv::kYuvI601Constants;
            default:
                return full ? &libyuv::kYuvF709Constants : &libyuv::kYuvH709Constants;
            }
        }

        bool IsHdr(DXGI_COLOR_SPACE_TYPE space) noexcept
        {
            return space == DXGI_COLOR_SPACE_YCBCR_STUDIO_G2084_LEFT_P2020 || space == DXGI_COLOR_SPACE_YCBCR_STUDIO_GHLG_TOPLEFT_P2020;
        }

        AsyncMediaPlayerSwapChain* Owner(winrt::Telegram::Native::Media::AsyncMediaPlayerSwapChain const& owner) noexcept
        {
            return winrt::get_self<AsyncMediaPlayerSwapChain>(owner);
        }

        // A surface decoded on a device the renderer has since left cannot be drawn on the new one.
        bool IsOnDevice(AVFrame const* frame, ID3D11Device* device) noexcept
        {
            if (frame->hw_frames_ctx == nullptr)
            {
                return false;
            }

            auto const* frames = reinterpret_cast<AVHWFramesContext const*>(frame->hw_frames_ctx->data);
            auto const* context = static_cast<AVD3D11VADeviceContext const*>(frames->device_ctx->hwctx);

            return context->device == device;
        }

        void FreeDevice(AVHWDeviceContext* context) noexcept
        {
            CloseHandle(static_cast<HANDLE>(context->user_opaque));
        }

        D3D11_VIDEO_PROCESSOR_ROTATION RotationOf(int32_t degrees) noexcept
        {
            switch (degrees)
            {
            case 90:
                return D3D11_VIDEO_PROCESSOR_ROTATION_90;
            case 180:
                return D3D11_VIDEO_PROCESSOR_ROTATION_180;
            case 270:
                return D3D11_VIDEO_PROCESSOR_ROTATION_270;
            default:
                return D3D11_VIDEO_PROCESSOR_ROTATION_IDENTITY;
            }
        }
    }

    void MediaVideoRenderer::I420::Resize(int32_t width, int32_t height)
    {
        const int32_t chromaWidth = (width + 1) / 2;
        const int32_t chromaHeight = (height + 1) / 2;

        y.stride = width;
        u.stride = chromaWidth;
        v.stride = chromaWidth;

        // Grown, never shrunk: these are reused for every frame.
        if (y.data.size() < static_cast<size_t>(width) * height)
        {
            y.data.resize(static_cast<size_t>(width) * height);
        }

        if (u.data.size() < static_cast<size_t>(chromaWidth) * chromaHeight)
        {
            u.data.resize(static_cast<size_t>(chromaWidth) * chromaHeight);
            v.data.resize(static_cast<size_t>(chromaWidth) * chromaHeight);
        }
    }

    MediaVideoRenderer::MediaVideoRenderer(winrt::Telegram::Native::Media::AsyncMediaPlayerSwapChain const& swapChain)
        : m_owner(swapChain)
    {
        auto* owner = Owner(swapChain);

        m_device = owner->Device();
        m_context = owner->DeviceContext();
        m_swapChain = owner->SwapChain();

        if (HANDLE mutex = owner->ContextMutex())
        {
            DuplicateHandle(GetCurrentProcess(), mutex, GetCurrentProcess(), &m_contextMutex, 0, FALSE, DUPLICATE_SAME_ACCESS);
        }

        if (m_device && m_context)
        {
            m_videoDevice = m_device.try_as<ID3D11VideoDevice>();
            m_videoContext = m_context.try_as<ID3D11VideoContext>();
            m_videoContext1 = m_context.try_as<ID3D11VideoContext1>();
        }

        m_software = !m_videoDevice || !m_videoContext;

        if (m_software)
        {
            LOGGER_WARNING(L"The adapter has no video processor, converting video on the CPU");
        }

        m_frame = av_frame_alloc();
        m_transfer = av_frame_alloc();
    }

    MediaVideoRenderer::~MediaVideoRenderer()
    {
        Detach();

        {
            std::lock_guard<std::mutex> lock(m_lock);

            ReleaseTargets();
            ReleaseProcessor();

            av_frame_free(&m_frame);
            av_frame_free(&m_transfer);
            av_buffer_unref(&m_hardware);
        }

        if (m_contextMutex != nullptr)
        {
            CloseHandle(m_contextMutex);
        }
    }

    void MediaVideoRenderer::Attach(std::function<void()> sizeChanged, std::function<void()> reset)
    {
        {
            std::lock_guard<std::mutex> lock(m_lock);
            m_reset = std::move(reset);
        }

        Owner(m_owner)->SizeChanged([weak = weak_from_this(), sizeChanged = std::move(sizeChanged)]()
            {
                if (auto strong = weak.lock())
                {
                    {
                        std::lock_guard<std::mutex> lock(strong->m_lock);
                        strong->m_resized = true;
                    }

                    if (sizeChanged)
                    {
                        sizeChanged();
                    }
                }
            });
    }

    void MediaVideoRenderer::Detach()
    {
        if (m_owner)
        {
            Owner(m_owner)->SizeChanged(nullptr);
        }
    }

    void MediaVideoRenderer::Activate(uint64_t session)
    {
        std::lock_guard<std::mutex> lock(m_lock);
        m_session = session;

        // A decoder's surfaces are one texture array, which the last frame and the views on it
        // would otherwise keep alive, whole, after the decoder is gone.
        av_frame_unref(m_frame);
        av_frame_unref(m_transfer);
        m_decoderViews.clear();
        m_decoderTexture = nullptr;
    }

    bool MediaVideoRenderer::Render(uint64_t session, AVFrame const* frame, MediaVideoFormat const& format)
    {
        std::lock_guard<std::mutex> lock(m_lock);

        if (session != m_session || m_lost || !m_swapChain || m_frame == nullptr)
        {
            return false;
        }

        // Decoded before the device was replaced, and dropped by the seek that follows. Drawn, it
        // would fail, and the failure be taken for the processor's.
        if (frame->format == AV_PIX_FMT_D3D11 && !IsOnDevice(frame, m_device.get()))
        {
            return false;
        }

        av_frame_unref(m_frame);

        if (av_frame_ref(m_frame, frame) < 0)
        {
            return false;
        }

        m_format = format;

        return Draw(true);
    }

    void MediaVideoRenderer::Refresh(uint64_t session)
    {
        std::lock_guard<std::mutex> lock(m_lock);

        if (session != m_session || m_lost || !m_resized || !m_swapChain || m_frame == nullptr || m_frame->data[0] == nullptr)
        {
            return;
        }

        Draw(false);
    }

    bool MediaVideoRenderer::Draw(bool upload)
    {
        bool drawn = false;

        try
        {
            // Not held across the Present below: the decoders take the same lock to submit their
            // work, and a Present can wait for a vblank.
            ContextLock lock(m_contextMutex);

            m_resized = false;
            UpdateSize();

            if (!m_software)
            {
                drawn = DrawProcessor(upload);

                // A lost device fails everything, which is not the processor's fault.
                if (const HRESULT reason = m_device->GetDeviceRemovedReason(); !drawn && !m_lost && FAILED(reason))
                {
                    DeviceLost(L"drawing", reason);
                }

                if (!drawn && !m_lost)
                {
                    // Some drivers refuse a size or a format the enumerator was asked about.
                    // Converting on the CPU is slower but still a picture.
                    LOGGER_WARNING(L"The video processor failed for {}x{}, converting on the CPU", m_frame->width, m_frame->height);
                    m_software = true;
                }
            }

            if (m_software && !m_lost)
            {
                drawn = DrawSoftware();
            }
        }
        catch (winrt::hresult_error const& ex)
        {
            LOGGER_ERROR(L"Drawing failed: 0x{:08X}", static_cast<uint32_t>(ex.code()));

            if (m_device && FAILED(m_device->GetDeviceRemovedReason()))
            {
                DeviceLost(L"drawing", ex.code());
            }

            return false;
        }

        if (drawn)
        {
            Present();
        }

        return drawn;
    }

    AVBufferRef* MediaVideoRenderer::HardwareDevice()
    {
        std::lock_guard<std::mutex> lock(m_lock);

        // Not while the device is lost: building on it would fail, and be remembered as failing.
        if (m_lost || m_hardwareDisabled)
        {
            return nullptr;
        }

        if (m_hardware == nullptr && !m_hardwareFailed && !m_software && m_videoDevice && m_videoContext)
        {
            AVBufferRef* device = av_hwdevice_ctx_alloc(AV_HWDEVICE_TYPE_D3D11VA);

            if (device != nullptr)
            {
                auto* context = reinterpret_cast<AVHWDeviceContext*>(device->data);
                auto* d3d11 = static_cast<AVD3D11VADeviceContext*>(context->hwctx);

                // Released by ffmpeg along with the device context.
                d3d11->device = m_device.get();
                d3d11->device->AddRef();

                // The same lock this renderer draws under, which ffmpeg requires to be recursive.
                // A Win32 mutex is. Through a handle of the device's own: decoders hold on to the
                // device until they are rebuilt, after the renderer has moved to a new one and
                // closed its handle, and waiting on a closed handle is fatal in a UWP process.
                HANDLE mutex = nullptr;

                if (m_contextMutex != nullptr && DuplicateHandle(GetCurrentProcess(), m_contextMutex, GetCurrentProcess(), &mutex, 0, FALSE, DUPLICATE_SAME_ACCESS))
                {
                    d3d11->lock = &LockContext;
                    d3d11->unlock = &UnlockContext;
                    d3d11->lock_ctx = mutex;

                    context->user_opaque = mutex;
                    context->free = &FreeDevice;
                }

                // Without the shared lock ffmpeg would make one of its own, and decode on the
                // context while this draws on it.
                if (mutex != nullptr && av_hwdevice_ctx_init(device) >= 0)
                {
                    m_hardware = device;
                }
                else
                {
                    av_buffer_unref(&device);
                }
            }

            if (m_hardware == nullptr)
            {
                LOGGER_WARNING(L"No hardware decoding device, decoding video on the CPU");
                m_hardwareFailed = true;
            }
        }

        return m_hardware ? av_buffer_ref(m_hardware) : nullptr;
    }

    void MediaVideoRenderer::LockContext(void* context) noexcept
    {
        WaitForSingleObjectEx(static_cast<HANDLE>(context), INFINITE, FALSE);
    }

    void MediaVideoRenderer::UnlockContext(void* context) noexcept
    {
        ReleaseMutex(static_cast<HANDLE>(context));
    }

    void MediaVideoRenderer::Present()
    {
        const HRESULT result = m_swapChain->Present(1, 0);

        if (result == DXGI_ERROR_DEVICE_REMOVED || result == DXGI_ERROR_DEVICE_RESET)
        {
            DeviceLost(L"presenting", result);
        }
    }

    void MediaVideoRenderer::CheckDevice()
    {
        std::lock_guard<std::mutex> lock(m_lock);

        if (!m_lost && m_device)
        {
            if (const HRESULT reason = m_device->GetDeviceRemovedReason(); FAILED(reason))
            {
                DeviceLost(L"decoding", reason);
            }
        }
    }

    void MediaVideoRenderer::DeviceLost(const wchar_t* stage, HRESULT result)
    {
        if (m_lost)
        {
            return;
        }

        m_lost = true;

        LOGGER_ERROR(L"Device lost while {}: 0x{:08X}, reason 0x{:08X}", stage, static_cast<uint32_t>(result), static_cast<uint32_t>(m_device->GetDeviceRemovedReason()));

        const double now = MediaNow();

        if (now - m_lastLoss < kRepeatedLoss && !m_hardwareDisabled)
        {
            LOGGER_WARNING(L"Second device loss in {:.0f}s, decoding on the CPU from now on", now - m_lastLoss);
            m_hardwareDisabled = true;
        }

        m_lastLoss = now;
        StartRecovery();
    }

    void MediaVideoRenderer::StartRecovery()
    {
        RunOnThreadPool([weak = weak_from_this()]()
            {
                for (int attempt = 0; attempt < kRecoveryAttempts; attempt++)
                {
                    {
                        auto strong = weak.lock();

                        if (!strong || strong->Recover())
                        {
                            return;
                        }
                    }

                    std::this_thread::sleep_for(std::chrono::milliseconds(kRecoveryWait << attempt));
                }

                LOGGER_ERROR(L"No new device after {} attempts, video stays off", kRecoveryAttempts);
            });
    }

    bool MediaVideoRenderer::Recover()
    {
        return Owner(m_owner)->Recreate([weak = weak_from_this()](bool applied)
            {
                if (!applied)
                {
                    return;
                }

                if (auto strong = weak.lock())
                {
                    strong->Rebind();
                }
            });
    }

    void MediaVideoRenderer::Rebind()
    {
        std::function<void()> reset;

        {
            std::lock_guard<std::mutex> lock(m_lock);

            ReleaseTargets();
            ReleaseProcessor();

            // Both may hold a surface of the lost device.
            av_frame_unref(m_frame);
            av_frame_unref(m_transfer);

            // Decoders keep their own references to the old one until they rebuild.
            av_buffer_unref(&m_hardware);
            m_hardwareFailed = false;

            auto* owner = Owner(m_owner);

            m_device = owner->Device();
            m_context = owner->DeviceContext();
            m_swapChain = owner->SwapChain();

            if (m_contextMutex != nullptr)
            {
                CloseHandle(m_contextMutex);
                m_contextMutex = nullptr;
            }

            if (HANDLE mutex = owner->ContextMutex())
            {
                DuplicateHandle(GetCurrentProcess(), mutex, GetCurrentProcess(), &m_contextMutex, 0, FALSE, DUPLICATE_SAME_ACCESS);
            }

            m_videoDevice = m_device ? m_device.try_as<ID3D11VideoDevice>() : nullptr;
            m_videoContext = m_context ? m_context.try_as<ID3D11VideoContext>() : nullptr;
            m_videoContext1 = m_context ? m_context.try_as<ID3D11VideoContext1>() : nullptr;
            m_software = !m_videoDevice || !m_videoContext;

            // The new swap chain starts at its placeholder size; the next draw sizes it.
            m_width = 0;
            m_height = 0;
            m_resized = false;
            m_lost = false;

            m_generation++;
            reset = m_reset;
        }

        LOGGER_INFO(L"Video device recovered");

        if (reset)
        {
            reset();
        }
    }

    void MediaVideoRenderer::UpdateSize()
    {
        int32_t width;
        int32_t height;
        Owner(m_owner)->TargetSize(width, height);

        width = std::min(width, D3D11_REQ_TEXTURE2D_U_OR_V_DIMENSION);
        height = std::min(height, D3D11_REQ_TEXTURE2D_U_OR_V_DIMENSION);

        if (width <= 0 || height <= 0)
        {
            if (m_width == 0 || m_height == 0)
            {
                DXGI_SWAP_CHAIN_DESC1 desc{};
                m_swapChain->GetDesc1(&desc);

                m_width = desc.Width;
                m_height = desc.Height;
            }

            return;
        }

        if (static_cast<UINT>(width) == m_width && static_cast<UINT>(height) == m_height)
        {
            return;
        }

        // ResizeBuffers fails while anything still holds one of the buffers.
        ReleaseTargets();

        const HRESULT result = m_swapChain->ResizeBuffers(0, width, height, DXGI_FORMAT_UNKNOWN, 0);

        if (result == DXGI_ERROR_DEVICE_REMOVED || result == DXGI_ERROR_DEVICE_RESET)
        {
            DeviceLost(L"resizing", result);
            return;
        }

        if (FAILED(result))
        {
            LOGGER_WARNING(L"ResizeBuffers to {}x{} failed: 0x{:08X}", width, height, static_cast<uint32_t>(result));

            DXGI_SWAP_CHAIN_DESC1 desc{};
            m_swapChain->GetDesc1(&desc);

            m_width = desc.Width;
            m_height = desc.Height;
            return;
        }

        m_width = width;
        m_height = height;
    }

    bool MediaVideoRenderer::EnsureProcessor(UINT width, UINT height, DXGI_FORMAT format)
    {
        if (m_processor && m_inputWidth == width && m_inputHeight == height && m_inputFormat == format
            && m_processorWidth == m_width && m_processorHeight == m_height)
        {
            return true;
        }

        // Input and output views are created against the enumerator, so they go with it.
        for (auto& target : m_targets)
        {
            target.output = nullptr;
        }

        m_inputView = nullptr;
        m_decoderViews.clear();
        m_decoderTexture = nullptr;
        m_processor = nullptr;
        m_enumerator = nullptr;
        m_checkedSpace = DXGI_COLOR_SPACE_CUSTOM;

        D3D11_VIDEO_PROCESSOR_CONTENT_DESC desc{};
        desc.InputFrameFormat = D3D11_VIDEO_FRAME_FORMAT_PROGRESSIVE;
        desc.InputFrameRate = { 30, 1 };
        desc.InputWidth = width;
        desc.InputHeight = height;
        desc.OutputFrameRate = { 30, 1 };
        desc.OutputWidth = m_width;
        desc.OutputHeight = m_height;
        desc.Usage = D3D11_VIDEO_USAGE_PLAYBACK_NORMAL;

        if (FAILED(m_videoDevice->CreateVideoProcessorEnumerator(&desc, m_enumerator.put())))
        {
            return false;
        }

        UINT support = 0;

        if (FAILED(m_enumerator->CheckVideoProcessorFormat(format, &support)) || (support & D3D11_VIDEO_PROCESSOR_FORMAT_SUPPORT_INPUT) == 0)
        {
            return false;
        }

        if (FAILED(m_enumerator->CheckVideoProcessorFormat(DXGI_FORMAT_B8G8R8A8_UNORM, &support)) || (support & D3D11_VIDEO_PROCESSOR_FORMAT_SUPPORT_OUTPUT) == 0)
        {
            return false;
        }

        if (FAILED(m_videoDevice->CreateVideoProcessor(m_enumerator.get(), 0, m_processor.put())))
        {
            return false;
        }

        m_inputWidth = width;
        m_inputHeight = height;
        m_inputFormat = format;
        m_processorWidth = m_width;
        m_processorHeight = m_height;

        m_videoContext->VideoProcessorSetStreamAutoProcessingMode(m_processor.get(), 0, FALSE);
        m_videoContext->VideoProcessorSetStreamFrameFormat(m_processor.get(), 0, D3D11_VIDEO_FRAME_FORMAT_PROGRESSIVE);
        m_videoContext->VideoProcessorSetOutputAlphaFillMode(m_processor.get(), D3D11_VIDEO_PROCESSOR_ALPHA_FILL_MODE_OPAQUE, 0);

        return true;
    }

    bool MediaVideoRenderer::EnsureUpload(UINT width, UINT height)
    {
        if (!m_input || m_uploadWidth != width || m_uploadHeight != height)
        {
            m_inputView = nullptr;
            m_input = nullptr;
            m_staging[0] = nullptr;
            m_staging[1] = nullptr;

            D3D11_TEXTURE2D_DESC texture{};
            texture.Width = width;
            texture.Height = height;
            texture.MipLevels = 1;
            texture.ArraySize = 1;
            texture.Format = DXGI_FORMAT_NV12;
            texture.SampleDesc.Count = 1;
            texture.Usage = D3D11_USAGE_DEFAULT;

            // No bind flags: a processor input view only accepts a texture bound as a decoder,
            // render target or unordered access view, or bound as nothing at all.
            texture.BindFlags = 0;

            if (FAILED(m_device->CreateTexture2D(&texture, nullptr, m_input.put())))
            {
                return false;
            }

            texture.Usage = D3D11_USAGE_STAGING;
            texture.CPUAccessFlags = D3D11_CPU_ACCESS_WRITE;

            // Two, written in turns, so that writing one frame does not wait for the GPU to finish
            // copying out the one before it.
            for (auto& staging : m_staging)
            {
                if (FAILED(m_device->CreateTexture2D(&texture, nullptr, staging.put())))
                {
                    m_input = nullptr;
                    return false;
                }
            }

            m_uploadWidth = width;
            m_uploadHeight = height;
        }

        if (!m_inputView)
        {
            D3D11_VIDEO_PROCESSOR_INPUT_VIEW_DESC view{};
            view.FourCC = 0;
            view.ViewDimension = D3D11_VPIV_DIMENSION_TEXTURE2D;
            view.Texture2D.MipSlice = 0;
            view.Texture2D.ArraySlice = 0;

            if (FAILED(m_videoDevice->CreateVideoProcessorInputView(m_input.get(), m_enumerator.get(), &view, m_inputView.put())))
            {
                return false;
            }
        }

        return true;
    }

    ID3D11VideoProcessorInputView* MediaVideoRenderer::DecoderView(ID3D11Texture2D* texture, UINT slice)
    {
        if (texture != m_decoderTexture)
        {
            m_decoderViews.clear();
            m_decoderTexture = texture;
        }

        if (slice >= m_decoderViews.size())
        {
            m_decoderViews.resize(slice + 1);
        }

        auto& view = m_decoderViews[slice];

        if (!view)
        {
            D3D11_VIDEO_PROCESSOR_INPUT_VIEW_DESC desc{};
            desc.FourCC = 0;
            desc.ViewDimension = D3D11_VPIV_DIMENSION_TEXTURE2D;
            desc.Texture2D.MipSlice = 0;
            desc.Texture2D.ArraySlice = slice;

            if (FAILED(m_videoDevice->CreateVideoProcessorInputView(texture, m_enumerator.get(), &desc, view.put())))
            {
                return nullptr;
            }
        }

        return view.get();
    }

    DXGI_COLOR_SPACE_TYPE MediaVideoRenderer::SupportedSpace(DXGI_COLOR_SPACE_TYPE space)
    {
        if (space == m_checkedSpace)
        {
            return m_usedSpace;
        }

        m_checkedSpace = space;
        m_usedSpace = space;

        // A processor that cannot map HDR down may refuse the blit, which would send every frame
        // after it through the CPU. Told it is SDR instead, it shows it washed out, but on the GPU.
        if (IsHdr(space))
        {
            BOOL supported = TRUE;

            if (auto enumerator = m_enumerator.try_as<ID3D11VideoProcessorEnumerator1>(); enumerator
                && SUCCEEDED(enumerator->CheckVideoProcessorFormatConversion(m_inputFormat, space, DXGI_FORMAT_B8G8R8A8_UNORM, DXGI_COLOR_SPACE_RGB_FULL_G22_NONE_P709, &supported))
                && !supported)
            {
                LOGGER_WARNING(L"The video processor does not take color space {}, drawing it as SDR", static_cast<int>(space));
                m_usedSpace = DXGI_COLOR_SPACE_YCBCR_STUDIO_G22_LEFT_P2020;
            }
        }

        return m_usedSpace;
    }

    void MediaVideoRenderer::ReleaseProcessor()
    {
        m_checkedSpace = DXGI_COLOR_SPACE_CUSTOM;
        m_inputView = nullptr;
        m_decoderViews.clear();
        m_decoderTexture = nullptr;
        m_processor = nullptr;
        m_enumerator = nullptr;
        m_input = nullptr;
        m_staging[0] = nullptr;
        m_staging[1] = nullptr;
        m_inputWidth = 0;
        m_inputHeight = 0;
        m_inputFormat = DXGI_FORMAT_UNKNOWN;
        m_uploadWidth = 0;
        m_uploadHeight = 0;
        m_softwareTexture = nullptr;
    }

    void MediaVideoRenderer::ReleaseTargets()
    {
        m_targets.clear();
    }

    bool MediaVideoRenderer::Upload(AVFrame const* frame)
    {
        auto& staging = m_staging[m_stagingIndex];
        m_stagingIndex ^= 1;

        D3D11_MAPPED_SUBRESOURCE mapped{};
        if (FAILED(m_context->Map(staging.get(), 0, D3D11_MAP_WRITE, 0, &mapped)))
        {
            return false;
        }

        // The chroma plane of a mapped NV12 texture follows the luma plane, at the texture's
        // height rather than the picture's.
        uint8_t* y = static_cast<uint8_t*>(mapped.pData);
        uint8_t* uv = y + static_cast<size_t>(mapped.RowPitch) * m_uploadHeight;
        const int pitch = static_cast<int>(mapped.RowPitch);

        const int width = frame->width;
        const int height = frame->height;

        bool copied = true;

        switch (frame->format)
        {
        case AV_PIX_FMT_NV12:
            libyuv::CopyPlane(frame->data[0], frame->linesize[0], y, pitch, width, height);
            libyuv::CopyPlane(frame->data[1], frame->linesize[1], uv, pitch, Even(width), (height + 1) / 2);
            break;
        case AV_PIX_FMT_YUV420P:
        case AV_PIX_FMT_YUVJ420P:
            libyuv::I420ToNV12(
                frame->data[0], frame->linesize[0],
                frame->data[1], frame->linesize[1],
                frame->data[2], frame->linesize[2],
                y, pitch, uv, pitch, width, height);
            break;
        default:
            copied = false;
            break;
        }

        m_context->Unmap(staging.get(), 0);

        if (copied)
        {
            m_context->CopyResource(m_input.get(), staging.get());
        }

        return copied;
    }

    winrt::com_ptr<ID3D11Texture2D> MediaVideoRenderer::BackBuffer()
    {
        winrt::com_ptr<ID3D11Texture2D> buffer;
        winrt::check_hresult(m_swapChain->GetBuffer(0, IID_PPV_ARGS(buffer.put())));

        return buffer;
    }

    ID3D11RenderTargetView* MediaVideoRenderer::TargetView(ID3D11Texture2D* backBuffer)
    {
        for (auto& target : m_targets)
        {
            if (target.buffer.get() == backBuffer)
            {
                if (!target.renderTarget)
                {
                    winrt::check_hresult(m_device->CreateRenderTargetView(backBuffer, nullptr, target.renderTarget.put()));
                }

                return target.renderTarget.get();
            }
        }

        Target target;
        target.buffer.copy_from(backBuffer);
        winrt::check_hresult(m_device->CreateRenderTargetView(backBuffer, nullptr, target.renderTarget.put()));

        m_targets.push_back(std::move(target));
        return m_targets.back().renderTarget.get();
    }

    ID3D11VideoProcessorOutputView* MediaVideoRenderer::OutputView(ID3D11Texture2D* backBuffer)
    {
        TargetView(backBuffer);

        for (auto& target : m_targets)
        {
            if (target.buffer.get() == backBuffer)
            {
                if (!target.output)
                {
                    D3D11_VIDEO_PROCESSOR_OUTPUT_VIEW_DESC desc{};
                    desc.ViewDimension = D3D11_VPOV_DIMENSION_TEXTURE2D;
                    desc.Texture2D.MipSlice = 0;

                    winrt::check_hresult(m_videoDevice->CreateVideoProcessorOutputView(backBuffer, m_enumerator.get(), &desc, target.output.put()));
                }

                return target.output.get();
            }
        }

        return nullptr;
    }

    RECT MediaVideoRenderer::Fit(int32_t width, int32_t height) const
    {
        double displayWidth = width;
        double displayHeight = height;

        if (m_format.aspect.num > 0 && m_format.aspect.den > 0)
        {
            displayWidth = displayWidth * m_format.aspect.num / m_format.aspect.den;
        }

        if (m_format.rotation == 90 || m_format.rotation == 270)
        {
            std::swap(displayWidth, displayHeight);
        }

        const double scale = std::min(m_width / displayWidth, m_height / displayHeight);
        const LONG fitWidth = std::max<LONG>(1, std::lround(displayWidth * scale));
        const LONG fitHeight = std::max<LONG>(1, std::lround(displayHeight * scale));

        RECT rect;
        rect.left = (static_cast<LONG>(m_width) - fitWidth) / 2;
        rect.top = (static_cast<LONG>(m_height) - fitHeight) / 2;
        rect.right = rect.left + fitWidth;
        rect.bottom = rect.top + fitHeight;

        return rect;
    }

    bool MediaVideoRenderer::DrawProcessor(bool upload)
    {
        AVFrame const* frame = m_frame;
        ID3D11VideoProcessorInputView* input = nullptr;

        if (frame->format == AV_PIX_FMT_D3D11)
        {
            // A surface the decoder wrote on this same device: one slice of its texture array,
            // drawn from where it is.
            auto* texture = reinterpret_cast<ID3D11Texture2D*>(frame->data[0]);
            const UINT slice = static_cast<UINT>(reinterpret_cast<intptr_t>(frame->data[1]));

            D3D11_TEXTURE2D_DESC desc{};
            texture->GetDesc(&desc);

            if (!EnsureProcessor(Even(frame->width), Even(frame->height), desc.Format))
            {
                return false;
            }

            input = DecoderView(texture, slice);
        }
        else if (frame->format == AV_PIX_FMT_NV12 || frame->format == AV_PIX_FMT_YUV420P || frame->format == AV_PIX_FMT_YUVJ420P)
        {
            const UINT width = Even(frame->width);
            const UINT height = Even(frame->height);

            if (!EnsureProcessor(width, height, DXGI_FORMAT_NV12) || !EnsureUpload(width, height))
            {
                return false;
            }

            // A redraw reuses what is already in the input texture, which survives a new processor
            // as long as the picture keeps its size.
            if (upload && !Upload(frame))
            {
                return false;
            }

            input = m_inputView.get();
        }

        if (input == nullptr)
        {
            return false;
        }

        auto backBuffer = BackBuffer();
        auto* output = OutputView(backBuffer.get());

        // Outside the picture is left transparent: the processor only writes the target rect, so
        // whatever the panel shows behind it shows through the letterbox.
        const float transparent[4] = { 0, 0, 0, 0 };
        m_context->ClearRenderTargetView(TargetView(backBuffer.get()), transparent);

        const RECT source{ 0, 0, frame->width, frame->height };
        const RECT destination = Fit(frame->width, frame->height);

        m_videoContext->VideoProcessorSetStreamSourceRect(m_processor.get(), 0, TRUE, &source);
        m_videoContext->VideoProcessorSetStreamDestRect(m_processor.get(), 0, TRUE, &destination);
        m_videoContext->VideoProcessorSetOutputTargetRect(m_processor.get(), TRUE, &destination);
        m_videoContext->VideoProcessorSetStreamRotation(m_processor.get(), 0, m_format.rotation != 0, RotationOf(m_format.rotation));

        if (m_videoContext1)
        {
            m_videoContext1->VideoProcessorSetStreamColorSpace1(m_processor.get(), 0, SupportedSpace(ColorSpaceOf(frame)));
            m_videoContext1->VideoProcessorSetOutputColorSpace1(m_processor.get(), DXGI_COLOR_SPACE_RGB_FULL_G22_NONE_P709);
        }
        else
        {
            D3D11_VIDEO_PROCESSOR_COLOR_SPACE input{};
            input.YCbCr_Matrix = MatrixOf(frame) == Matrix::BT601 ? 0 : 1;
            input.Nominal_Range = IsFullRange(frame) ? D3D11_VIDEO_PROCESSOR_NOMINAL_RANGE_0_255 : D3D11_VIDEO_PROCESSOR_NOMINAL_RANGE_16_235;

            D3D11_VIDEO_PROCESSOR_COLOR_SPACE outputSpace{};
            outputSpace.RGB_Range = 0;

            m_videoContext->VideoProcessorSetStreamColorSpace(m_processor.get(), 0, &input);
            m_videoContext->VideoProcessorSetOutputColorSpace(m_processor.get(), &outputSpace);
        }

        D3D11_VIDEO_PROCESSOR_STREAM stream{};
        stream.Enable = TRUE;
        stream.pInputSurface = input;

        const HRESULT result = m_videoContext->VideoProcessorBlt(m_processor.get(), output, 0, 1, &stream);

        if (result == DXGI_ERROR_DEVICE_REMOVED || result == DXGI_ERROR_DEVICE_RESET)
        {
            DeviceLost(L"drawing", result);
        }

        return SUCCEEDED(result);
    }

    bool MediaVideoRenderer::DrawSoftware()
    {
        AVFrame const* frame = m_frame;

        const uint8_t* srcY = frame->data[0];
        const uint8_t* srcU = frame->data[1];
        const uint8_t* srcV = frame->data[2];
        int strideY = frame->linesize[0];
        int strideU = frame->linesize[1];
        int strideV = frame->linesize[2];
        bool nv12 = frame->format == AV_PIX_FMT_NV12;

        if (frame->format == AV_PIX_FMT_D3D11)
        {
            // Only once the processor has failed on a surface the GPU decoded: copied back, which is
            // slow but still a picture.
            av_frame_unref(m_transfer);

            if (av_hwframe_transfer_data(m_transfer, frame, 0) < 0)
            {
                return false;
            }

            av_frame_copy_props(m_transfer, frame);
            frame = m_transfer;

            srcY = frame->data[0];
            srcU = frame->data[1];
            strideY = frame->linesize[0];
            strideU = frame->linesize[1];
            nv12 = true;

            if (frame->format == AV_PIX_FMT_P010)
            {
                // Ten bits, as HDR decodes to, brought down to eight: shown, if not as it should be.
                const int32_t chromaHeight = (frame->height + 1) / 2;

                m_narrowY.stride = frame->width;
                m_narrowUV.stride = static_cast<int32_t>(Even(frame->width));
                m_narrowY.data.resize(std::max(m_narrowY.data.size(), static_cast<size_t>(m_narrowY.stride) * frame->height));
                m_narrowUV.data.resize(std::max(m_narrowUV.data.size(), static_cast<size_t>(m_narrowUV.stride) * chromaHeight));

                // Strides of 16 bit planes are counted in samples, not bytes.
                libyuv::P010ToNV12(
                    reinterpret_cast<const uint16_t*>(frame->data[0]), frame->linesize[0] / 2,
                    reinterpret_cast<const uint16_t*>(frame->data[1]), frame->linesize[1] / 2,
                    m_narrowY.data.data(), m_narrowY.stride,
                    m_narrowUV.data.data(), m_narrowUV.stride,
                    frame->width, frame->height);

                srcY = m_narrowY.data.data();
                srcU = m_narrowUV.data.data();
                strideY = m_narrowY.stride;
                strideU = m_narrowUV.stride;
            }
            else if (frame->format != AV_PIX_FMT_NV12)
            {
                return false;
            }
        }

        if (nv12)
        {
            m_converted.Resize(frame->width, frame->height);

            libyuv::NV12ToI420(
                srcY, strideY,
                srcU, strideU,
                m_converted.y.data.data(), m_converted.y.stride,
                m_converted.u.data.data(), m_converted.u.stride,
                m_converted.v.data.data(), m_converted.v.stride,
                frame->width, frame->height);

            srcY = m_converted.y.data.data();
            srcU = m_converted.u.data.data();
            srcV = m_converted.v.data.data();
            strideY = m_converted.y.stride;
            strideU = m_converted.u.stride;
            strideV = m_converted.v.stride;
        }
        else if (frame->format != AV_PIX_FMT_YUV420P && frame->format != AV_PIX_FMT_YUVJ420P)
        {
            return false;
        }

        const RECT destination = Fit(frame->width, frame->height);
        const int32_t width = destination.right - destination.left;
        const int32_t height = destination.bottom - destination.top;

        const bool sideways = m_format.rotation == 90 || m_format.rotation == 270;
        const int32_t scaledWidth = sideways ? height : width;
        const int32_t scaledHeight = sideways ? width : height;

        m_scaled.Resize(scaledWidth, scaledHeight);

        libyuv::I420Scale(
            srcY, strideY, srcU, strideU, srcV, strideV, frame->width, frame->height,
            m_scaled.y.data.data(), m_scaled.y.stride,
            m_scaled.u.data.data(), m_scaled.u.stride,
            m_scaled.v.data.data(), m_scaled.v.stride,
            scaledWidth, scaledHeight, libyuv::kFilterBilinear);

        I420* output = &m_scaled;

        if (m_format.rotation != 0)
        {
            m_rotated.Resize(width, height);

            libyuv::I420Rotate(
                m_scaled.y.data.data(), m_scaled.y.stride,
                m_scaled.u.data.data(), m_scaled.u.stride,
                m_scaled.v.data.data(), m_scaled.v.stride,
                m_rotated.y.data.data(), m_rotated.y.stride,
                m_rotated.u.data.data(), m_rotated.u.stride,
                m_rotated.v.data.data(), m_rotated.v.stride,
                scaledWidth, scaledHeight, static_cast<libyuv::RotationMode>(m_format.rotation));

            output = &m_rotated;
        }

        const size_t bgraSize = static_cast<size_t>(width) * height * 4;
        if (m_bgra.size() < bgraSize)
        {
            m_bgra.resize(bgraSize);
        }

        libyuv::I420ToARGBMatrix(
            output->y.data.data(), output->y.stride,
            output->u.data.data(), output->u.stride,
            output->v.data.data(), output->v.stride,
            m_bgra.data(), width * 4,
            YuvConstantsOf(frame), width, height);

        if (!m_softwareTexture || m_softwareWidth != static_cast<UINT>(width) || m_softwareHeight != static_cast<UINT>(height))
        {
            m_softwareTexture = nullptr;

            D3D11_TEXTURE2D_DESC texture{};
            texture.Width = width;
            texture.Height = height;
            texture.MipLevels = 1;
            texture.ArraySize = 1;
            texture.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
            texture.SampleDesc.Count = 1;
            texture.Usage = D3D11_USAGE_DEFAULT;

            winrt::check_hresult(m_device->CreateTexture2D(&texture, nullptr, m_softwareTexture.put()));

            m_softwareWidth = width;
            m_softwareHeight = height;
        }

        m_context->UpdateSubresource(m_softwareTexture.get(), 0, nullptr, m_bgra.data(), width * 4, 0);

        auto backBuffer = BackBuffer();

        const float transparent[4] = { 0, 0, 0, 0 };
        m_context->ClearRenderTargetView(TargetView(backBuffer.get()), transparent);
        m_context->CopySubresourceRegion(backBuffer.get(), 0, destination.left, destination.top, 0, m_softwareTexture.get(), 0, nullptr);

        return true;
    }
}
