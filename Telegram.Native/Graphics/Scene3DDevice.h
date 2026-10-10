#pragma once

#include "Scene3D.h"

#include <winrt/base.h>

#include <d3d11_1.h>
#include <dxgi1_3.h>

#include <chrono>
#include <condition_variable>
#include <cstdint>
#include <memory>
#include <mutex>
#include <string>
#include <thread>
#include <vector>

namespace Graphics3D
{
    class Scene3DRenderer;

    // For catch blocks: logs what is in flight and swallows anything the logging itself throws,
    // because a second exception there would end the process.
    void LogException(const wchar_t* where) noexcept;

    // Reads a file from Assets\Models\<folder>\ in the package. Render thread, like every
    // scene's Load, so it reads synchronously; the error names the file for the log.
    bool ReadModelAsset(const wchar_t* folder, const wchar_t* name, std::vector<std::uint8_t>& data,
        std::wstring& error);

    /// <summary>
    /// The one device every <c>Scene3DPanel</c> draws with, and the one thread that draws them.
    /// </summary>
    /// <remarks>
    /// One for all of them because a panel is not rare: every TON transfer in chat history carries
    /// one, and a device and a vsynced thread per panel was a dozen of each in a scrolled chat.
    ///
    /// Not <c>Direct2DDevice</c>'s device. Its immediate context is driven from the UI thread by
    /// Direct2D and composition, so drawing on it from here would need <c>ID2D1Multithread</c>
    /// around every frame, Present included, stalling the UI thread on each one. It is also made
    /// at whatever feature level the machine has, with a WARP fallback, and neither suits a scene.
    ///
    /// **Everything that touches the immediate context holds <see cref="Lock"/>**: the frame pass,
    /// and a renderer creating, resizing or releasing its swap chain - DXGI drives the context for
    /// those too. Creating resources on the device is free-threaded and does not need it.
    ///
    /// On device loss the thread makes a new device, backing off while the driver resets, and only
    /// then tells every registered renderer, through the callback it registered with, to be rebuilt
    /// by its panel. A panel that attaches in between registers without a swap chain and waits for
    /// the same call.
    /// </remarks>
    class Scene3DDevice
    {
    public:
        // Null when this machine cannot draw a scene: no hardware adapter at feature level 11.
        // That answer is kept for the session rather than asked again by every panel.
        static std::shared_ptr<Scene3DDevice> Acquire() noexcept;

        ~Scene3DDevice();

        Scene3DDevice(const Scene3DDevice&) = delete;
        Scene3DDevice& operator=(const Scene3DDevice&) = delete;

        std::mutex& Lock() noexcept { return m_lock; }

        // Everything below is under Lock(). The device is null while one is being recovered.
        ID3D11Device* Device() const noexcept { return m_device.get(); }
        ID3D11DeviceContext* Context() const noexcept { return m_context.get(); }
        IDXGIFactory2* Factory() const noexcept { return m_factory.get(); }
        SceneCache& Cache() noexcept { return m_cache; }

        // A device that could not be recovered. Nothing is drawn on it again, every renderer on it
        // has been failed, and one attaching now fails too.
        bool IsAbandoned() const noexcept { return m_abandoned; }

        // Which device a renderer's swap chain belongs to. Zero is none, so a renderer that
        // registered while the device was lost never matches.
        std::uint64_t Generation() const noexcept { return m_generation; }

        void Register(Scene3DRenderer* renderer);
        void Unregister(Scene3DRenderer* renderer);
        void SetActive(Scene3DRenderer* renderer, bool active);

        // For whoever sees the device fail first. A generation that has already been replaced
        // is ignored, so a late report cannot condemn the new device.
        void MarkLost(std::uint64_t generation);

    private:
        struct Resources
        {
            winrt::com_ptr<ID3D11Device> device;
            winrt::com_ptr<ID3D11DeviceContext> context;
            winrt::com_ptr<IDXGIAdapter1> adapter;
            winrt::com_ptr<IDXGIFactory2> factory;
        };

        Scene3DDevice() = default;

        static bool Create(Resources& resources);

        void Install(Resources&& resources);
        void NotifyReplaced();
        void Abandon();

        void Run();
        void Recover(std::unique_lock<std::mutex>& lock);
        void WaitForVerticalBlank();

        std::mutex m_lock;
        std::condition_variable m_wake;
        std::thread m_thread;

        winrt::com_ptr<ID3D11Device> m_device;
        winrt::com_ptr<ID3D11DeviceContext> m_context;
        winrt::com_ptr<IDXGIAdapter1> m_adapter;
        winrt::com_ptr<IDXGIFactory2> m_factory;
        SceneCache m_cache;
        std::uint64_t m_generation = 0;

        std::vector<Scene3DRenderer*> m_renderers;
        std::size_t m_active = 0;

        bool m_lost = false;
        bool m_abandoned = false;
        bool m_stopping = false;

        // Render thread only, and only outside the lock: the wait blocks for up to a refresh.
        winrt::com_ptr<IDXGIOutput> m_output;
        std::chrono::steady_clock::time_point m_lastVerticalBlank{};
        std::chrono::steady_clock::time_point m_nextOutputSearch{};
    };
}
