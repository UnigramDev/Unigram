#pragma once

#include <winrt/Telegram.Native.Media.h>

#include <d3d11_1.h>

#include <atomic>
#include <cstdint>
#include <functional>
#include <memory>
#include <mutex>
#include <vector>

extern "C"
{
#include <libavutil/frame.h>
#include <libavutil/rational.h>
}

namespace winrt::Telegram::Native::Media::implementation
{
    struct MediaVideoFormat
    {
        // Clockwise, in degrees: 0, 90, 180 or 270.
        int32_t rotation{ 0 };

        // Zero when the pixels are square.
        AVRational aspect{ 0, 1 };
    };

    // Draws decoded pictures into the player's swap chain and presents them.
    //
    // Shared by every session of one player, so that opening another file leaves the last picture
    // of the previous one up until the new one has a frame of its own. Only the active session
    // draws: one still winding down is ignored.
    class MediaVideoRenderer : public std::enable_shared_from_this<MediaVideoRenderer>
    {
    public:
        explicit MediaVideoRenderer(winrt::Telegram::Native::Media::AsyncMediaPlayerSwapChain const& swapChain);
        ~MediaVideoRenderer();

        MediaVideoRenderer(const MediaVideoRenderer&) = delete;
        MediaVideoRenderer& operator=(const MediaVideoRenderer&) = delete;

        // The callback runs on the panel's thread whenever its size changes.
        void Attach(std::function<void()> sizeChanged);
        void Detach();

        void Activate(uint64_t session);

        bool Render(uint64_t session, AVFrame const* frame, MediaVideoFormat const& format);

        // Draws the last frame again if the panel changed size since it was drawn. Without this a
        // paused video stays stretched to the old size.
        void Refresh(uint64_t session);

    private:
        struct Plane
        {
            std::vector<uint8_t> data;
            int32_t stride{ 0 };
        };

        struct I420
        {
            Plane y;
            Plane u;
            Plane v;

            void Resize(int32_t width, int32_t height);
        };

        bool Draw(bool upload);
        bool DrawProcessor(bool upload);
        bool DrawSoftware();
        void Present();

        void UpdateSize();
        bool EnsureProcessor(UINT width, UINT height);
        void ReleaseProcessor();
        void ReleaseTargets();

        bool Upload(AVFrame const* frame);

        winrt::com_ptr<ID3D11Texture2D> BackBuffer();
        ID3D11RenderTargetView* TargetView(ID3D11Texture2D* backBuffer);
        ID3D11VideoProcessorOutputView* OutputView(ID3D11Texture2D* backBuffer);

        RECT Fit(int32_t width, int32_t height) const;

        // The projected type only: naming the implementation here would make every file that
        // includes this resolve AsyncMediaPlayerSwapChain to it instead of to the projection.
        winrt::Telegram::Native::Media::AsyncMediaPlayerSwapChain m_owner{ nullptr };

        winrt::com_ptr<ID3D11Device> m_device;
        winrt::com_ptr<ID3D11DeviceContext> m_context;
        winrt::com_ptr<IDXGISwapChain1> m_swapChain;
        winrt::com_ptr<ID3D11VideoDevice> m_videoDevice;
        winrt::com_ptr<ID3D11VideoContext> m_videoContext;
        winrt::com_ptr<ID3D11VideoContext1> m_videoContext1;

        // Our own handle to the swap chain's context mutex: Close() on the swap chain closes its
        // handle, and this one may still be waited on by a frame in flight.
        HANDLE m_contextMutex{ nullptr };

        std::mutex m_lock;
        uint64_t m_session{ 0 };

        AVFrame* m_frame{ nullptr };
        MediaVideoFormat m_format;

        UINT m_width{ 0 };
        UINT m_height{ 0 };
        bool m_resized{ false };
        bool m_software{ false };
        bool m_lost{ false };

        winrt::com_ptr<ID3D11VideoProcessorEnumerator> m_enumerator;
        winrt::com_ptr<ID3D11VideoProcessor> m_processor;
        winrt::com_ptr<ID3D11Texture2D> m_input;
        winrt::com_ptr<ID3D11Texture2D> m_staging[2];
        winrt::com_ptr<ID3D11VideoProcessorInputView> m_inputView;
        uint32_t m_stagingIndex{ 0 };
        UINT m_inputWidth{ 0 };
        UINT m_inputHeight{ 0 };
        UINT m_processorWidth{ 0 };
        UINT m_processorHeight{ 0 };

        struct Target
        {
            winrt::com_ptr<ID3D11Texture2D> buffer;
            winrt::com_ptr<ID3D11RenderTargetView> renderTarget;
            winrt::com_ptr<ID3D11VideoProcessorOutputView> output;
        };

        // One per buffer of the swap chain, which takes turns being the back buffer.
        std::vector<Target> m_targets;

        winrt::com_ptr<ID3D11Texture2D> m_softwareTexture;
        UINT m_softwareWidth{ 0 };
        UINT m_softwareHeight{ 0 };
        I420 m_converted;
        I420 m_scaled;
        I420 m_rotated;
        std::vector<uint8_t> m_bgra;
    };
}
