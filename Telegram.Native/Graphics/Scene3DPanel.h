#pragma once

#include "Graphics/Scene3DPanel.g.h"

#include "../Controls/FrameworkElementEx.h"
#include "Scene3DRenderer.h"

#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.UI.Xaml.Controls.h>
#include <winrt/Windows.UI.Xaml.Input.h>
#include <winrt/Windows.UI.Xaml.Media.h>

#include <chrono>
#include <functional>
#include <memory>

namespace winrt::Telegram::Native::Graphics::implementation
{
    /// <summary>
    /// A <c>SwapChainPanel</c> that draws one 3D scene, turns under the pointer and stops dead
    /// when it leaves the tree.
    /// </summary>
    /// <remarks>
    /// The whole of the per-frame work is native and on a render thread shared by every panel, so
    /// this type is only the lifetime and the input: the panel is shown, the renderer starts; the
    /// panel leaves the tree, the renderer stops and gives its swap chain back. That matters because this is a vsynced
    /// loop, and leaving one running off screen costs a GPU and a core for as long as the app is
    /// open.
    ///
    /// It stops on three signals, and they are all there are: leaving the tree, scrolling out of
    /// view, and <c>IsPaused</c>. The third exists because **a collapsed ancestor is not
    /// observable from here** - Visibility is local to the element it is set on, and the effective
    /// viewport only moves for scrolling - so a host that hides this by collapsing something above
    /// it has to say so itself.
    ///
    /// The pointer is handled here rather than in C# so that a drag never crosses the managed
    /// boundary per move, and the motion itself - the spin, the clamp, the overshoot on release -
    /// is the renderer's, where the frame clock already is.
    /// </remarks>
    struct Scene3DPanel : FrameworkElementEx<Scene3DPanel, Scene3DPanelT<Scene3DPanel>>
    {
        Scene3DPanel();
        ~Scene3DPanel();

        Graphics::Scene3DModel Model() const noexcept { return m_model; }
        void Model(Graphics::Scene3DModel value);

        bool IsSpinning() const noexcept { return m_spinning; }
        void IsSpinning(bool value);

        double SpinSpeed() const noexcept { return m_spinSpeed; }
        void SpinSpeed(double value);

        bool IsInteractive() const noexcept { return m_interactive; }
        void IsInteractive(bool value);

        Graphics::Scene3DPalette Palette() const noexcept { return m_palette; }
        void Palette(Graphics::Scene3DPalette value);

        double PressScale() const noexcept { return m_pressScale; }
        void PressScale(double value);

        winrt::Windows::Foundation::Point PressScaleOrigin() const noexcept { return m_pressOrigin; }
        void PressScaleOrigin(winrt::Windows::Foundation::Point const& value) { m_pressOrigin = value; }

        bool IsPaused() const noexcept { return m_paused; }
        void IsPaused(bool value);

        void Kick(double degreesPerSecond);

        bool TransferTo(Graphics::Scene3DPanel const& target);

        hstring FallbackText() const noexcept { return m_fallbackText; }
        void FallbackText(hstring const& value);

        double FallbackFontSize() const noexcept { return m_fallbackFontSize; }
        void FallbackFontSize(double value);

        winrt::Windows::UI::Xaml::Media::FontFamily FallbackFontFamily() const noexcept { return m_fallbackFontFamily; }
        void FallbackFontFamily(winrt::Windows::UI::Xaml::Media::FontFamily const& value);

        winrt::Windows::UI::Xaml::Media::Brush FallbackForeground();
        void FallbackForeground(winrt::Windows::UI::Xaml::Media::Brush const& value);
        static winrt::Windows::UI::Xaml::DependencyProperty FallbackForegroundProperty();

        void OnLoaded() override;
        void OnUnloaded() override;

    private:
        void OnSizeChanged(winrt::Windows::Foundation::IInspectable const& sender,
            winrt::Windows::UI::Xaml::SizeChangedEventArgs const& args);

        void OnCompositionScaleChanged(winrt::Windows::UI::Xaml::Controls::SwapChainPanel const& sender,
            winrt::Windows::Foundation::IInspectable const& args);

        void OnActualThemeChanged(winrt::Windows::UI::Xaml::FrameworkElement const& sender,
            winrt::Windows::Foundation::IInspectable const& args);

        void OnPointerPressed(winrt::Windows::Foundation::IInspectable const& sender,
            winrt::Windows::UI::Xaml::Input::PointerRoutedEventArgs const& args);

        void OnPointerMoved(winrt::Windows::Foundation::IInspectable const& sender,
            winrt::Windows::UI::Xaml::Input::PointerRoutedEventArgs const& args);

        void OnPointerReleased(winrt::Windows::Foundation::IInspectable const& sender,
            winrt::Windows::UI::Xaml::Input::PointerRoutedEventArgs const& args);

        void OnPointerCaptureLost(winrt::Windows::Foundation::IInspectable const& sender,
            winrt::Windows::UI::Xaml::Input::PointerRoutedEventArgs const& args);

        void OnEffectiveViewportChanged(winrt::Windows::UI::Xaml::FrameworkElement const& sender,
            winrt::Windows::UI::Xaml::EffectiveViewportChangedEventArgs const& args);

        static void OnFallbackForegroundChanged(winrt::Windows::UI::Xaml::DependencyObject const& sender,
            winrt::Windows::UI::Xaml::DependencyPropertyChangedEventArgs const& args);

        std::function<void()> NotifyHandler();
        void OnRendererNotified();

        void Fail();
        void UpdateFallback();

        void UpdateRunning();
        void UpdateSize();
        float Oversample() const noexcept;
        void UpdatePressScale(bool pressed);
        void EndDrag();

        std::unique_ptr<::Graphics3D::Scene3DRenderer> m_renderer;

        // Set once this panel's scene cannot be drawn, which only a change of model undoes: a
        // device refused, an asset missing or a device not recovered does not improve by asking
        // again every time the panel is shown.
        bool m_failed = false;

        // Set once the scene has been handed to another panel, and cleared when this one leaves
        // the tree. Without it the next viewport change would build a fresh scene here, fading in
        // a second stone while the first is on its way somewhere else.
        bool m_given = false;

        hstring m_fallbackText;
        double m_fallbackFontSize = 0;
        winrt::Windows::UI::Xaml::Media::FontFamily m_fallbackFontFamily{ nullptr };
        winrt::Windows::UI::Xaml::Controls::TextBlock m_fallback{ nullptr };

        Graphics::Scene3DModel m_model{ Graphics::Scene3DModel::Diamond };
        Graphics::Scene3DPalette m_palette{ Graphics::Scene3DPalette::Default };
        bool m_spinning = false;
        double m_spinSpeed = 0;
        bool m_interactive = true;
        bool m_paused = false;

        // Subscribed only while loaded: asking for the effective viewport makes the element take
        // part in the viewport calculation of every scroll around it.
        winrt::event_token m_viewportToken{};

        // Assumed true until something has measured it - the event arrives after a layout pass,
        // and a panel that has not had one should draw rather than stay blank.
        bool m_visible = true;

        // Whether IsSpinning was set from outside, which decides whether the scene's own default
        // survives being loaded.
        bool m_spinningSet = false;

        double m_pressScale = 1;

        // Whether the surface is currently sized for the press. See Oversample.
        bool m_pressed = false;
        winrt::Windows::Foundation::Point m_pressOrigin{ 0.5f, 0.5f };

        std::uint32_t m_pointer = 0;
        winrt::Windows::Foundation::Point m_dragFrom{};
        bool m_dragging = false;

        // Where and when the press began, and whether it has since moved too far to be a tap.
        winrt::Windows::Foundation::Point m_pressFrom{};
        std::chrono::steady_clock::time_point m_pressTime{};
        bool m_pressMoved = false;
    };
}

namespace winrt::Telegram::Native::Graphics::factory_implementation
{
    struct Scene3DPanel : Scene3DPanelT<Scene3DPanel, implementation::Scene3DPanel>
    {
    };
}
