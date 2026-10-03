#include "pch.h"
#include "Scene3DPanel.h"
#if __has_include("Graphics/Scene3DPanel.g.cpp")
#include "Graphics/Scene3DPanel.g.cpp"
#endif

#include "DiamondScene.h"

#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.System.h>
#include <winrt/Windows.UI.Composition.h>
#include <winrt/Windows.UI.Xaml.Automation.h>
#include <winrt/Windows.UI.Xaml.Interop.h>
#include <winrt/Windows.UI.Input.h>
#include <winrt/Windows.UI.Xaml.Hosting.h>

#include <algorithm>
#include <chrono>

using namespace winrt::Windows::Foundation;
using namespace winrt::Windows::System;
using namespace winrt::Windows::UI::Xaml;
using namespace winrt::Windows::UI::Xaml::Automation;
using namespace winrt::Windows::UI::Xaml::Controls;
using namespace winrt::Windows::UI::Composition;
using namespace winrt::Windows::UI::Xaml::Hosting;
using namespace winrt::Windows::UI::Xaml::Input;

namespace winrt::Telegram::Native::Graphics::implementation
{
    namespace
    {
        std::unique_ptr<::Graphics3D::IScene3D> CreateScene(Graphics::Scene3DModel model)
        {
            switch (model)
            {
            case Graphics::Scene3DModel::Diamond:
            default:
                return std::make_unique<::Graphics3D::DiamondScene>();
            }
        }
    }

    Scene3DPanel::Scene3DPanel()
    {
        // Before the parser assigns anything: it applies a {ThemeResource} to a property it cannot
        // find registered as to a plain one, and that fails.
        FallbackForegroundProperty();

        // Never unhooked, and deliberately not through a revoker: the event source is this very
        // object, so the handlers die with it, while the destructor can run on the finalizer
        // thread whenever a managed subclass owns the outer object - and XAML's remove_* is
        // thread-affine, so revoking there fails and fail-fasts. Same reasoning as
        // FrameworkElementEx.
        SizeChanged({ this, &Scene3DPanel::OnSizeChanged });
        CompositionScaleChanged({ this, &Scene3DPanel::OnCompositionScaleChanged });

        PointerPressed({ this, &Scene3DPanel::OnPointerPressed });
        PointerMoved({ this, &Scene3DPanel::OnPointerMoved });
        PointerReleased({ this, &Scene3DPanel::OnPointerReleased });
        PointerCanceled({ this, &Scene3DPanel::OnPointerReleased });
        PointerCaptureLost({ this, &Scene3DPanel::OnPointerCaptureLost });
    }

    Scene3DPanel::~Scene3DPanel()
    {
        // Joins the render thread. The destructor may be on the finalizer thread, which is fine:
        // nothing here touches XAML, and the renderer owns everything it has to release.
        m_renderer.reset();
    }

    void Scene3DPanel::Model(Graphics::Scene3DModel value)
    {
        if (m_model == value)
        {
            return;
        }

        m_model = value;

        // Takes effect the next time the panel is shown. A scene owns its shaders, its buffers
        // and its targets, so exchanging one mid-flight is a reload - and there is no screen in
        // the app that changes its mind about which model it is showing.
        if (m_renderer != nullptr)
        {
            m_renderer->Detach();
            m_renderer.reset();
        }

        // A different scene may well load where the last one did not.
        m_failed = false;
        UpdateFallback();

        UpdateRunning();
    }

    void Scene3DPanel::IsSpinning(bool value)
    {
        m_spinning = value;
        m_spinningSet = true;

        if (m_renderer != nullptr)
        {
            m_renderer->SetSpinning(value);
        }
    }

    void Scene3DPanel::IsInteractive(bool value)
    {
        m_interactive = value;

        if (!value)
        {
            EndDrag();
        }
    }

    void Scene3DPanel::Palette(Graphics::Scene3DPalette value)
    {
        m_palette = value;

        if (m_renderer != nullptr)
        {
            m_renderer->SetVariant(static_cast<int>(value));
        }
    }

    void Scene3DPanel::PressScale(double value)
    {
        if (m_pressScale == value)
        {
            return;
        }

        m_pressScale = value;

        // The surface is sized for the largest the panel will be drawn at, so changing that
        // changes how big it has to be.
        UpdateSize();
    }

    void Scene3DPanel::IsPaused(bool value)
    {
        if (m_paused == value)
        {
            return;
        }

        m_paused = value;
        UpdateRunning();
    }

    void Scene3DPanel::Kick(double degreesPerSecond)
    {
        if (m_renderer != nullptr)
        {
            m_renderer->Kick(degreesPerSecond);
        }
    }

    void Scene3DPanel::OnLoaded()
    {
        if (!m_viewportToken)
        {
            m_viewportToken = EffectiveViewportChanged(
                { this, &Scene3DPanel::OnEffectiveViewportChanged });
        }

        UpdateRunning();
    }

    void Scene3DPanel::OnUnloaded()
    {
        if (m_viewportToken)
        {
            // Safe to revoke here, unlike in the destructor: Unloaded runs on the thread that
            // owns the event.
            EffectiveViewportChanged(m_viewportToken);
            m_viewportToken = {};
        }

        // Nothing has measured the next placement yet, so the next show starts out drawing.
        m_visible = true;

        UpdateRunning();
    }

    void Scene3DPanel::OnEffectiveViewportChanged(FrameworkElement const& sender,
        EffectiveViewportChangedEventArgs const& args)
    {
        // The same test AnimatedImageBase uses: how far it would have to travel to be brought
        // into view, against its own size. This catches scrolling and nothing else.
        const bool visible = args.BringIntoViewDistanceX() < sender.ActualWidth()
            && args.BringIntoViewDistanceY() < sender.ActualHeight();

        if (visible != m_visible)
        {
            m_visible = visible;
            UpdateRunning();
        }
    }

    void Scene3DPanel::UpdateRunning()
    {
        // Two policies, because scrolled out of view is not the same as gone.
        //
        // Gone - out of the tree, or paused outright - releases everything. A closed popup may
        // never reopen, and a device, a swap chain and half a megabyte of geometry held against
        // that is a cost paid for the whole session.
        //
        // Out of view only stops the loop. Coming back from a scroll has to be immediate, so the
        // swap chain, the shaders and the buffers stay exactly where they are and starting again
        // is a flag and nothing else. Releasing here is what made it take a few frames to reappear.
        if (!IsConnected() || m_paused)
        {
            if (m_renderer != nullptr)
            {
                EndDrag();

                // Here rather than in the destructor: handing the panel its swap chain back is a
                // XAML call and has to happen on this thread, while a native object owned by a
                // managed subclass can be destroyed on the finalizer one.
                m_renderer->Detach();
                m_renderer.reset();
            }

            return;
        }

        if (!m_visible)
        {
            if (m_renderer != nullptr)
            {
                EndDrag();
                m_renderer->Stop();
            }

            return;
        }

        if (m_renderer == nullptr)
        {
            if (m_failed)
            {
                return;
            }

            try
            {
                m_renderer = std::make_unique<::Graphics3D::Scene3DRenderer>(CreateScene(m_model));

                if (!m_renderer->Attach(*this, NotifyHandler()))
                {
                    // No device on this machine, or the panel refused the swap chain.
                    Fail();
                    return;
                }
            }
            catch (...)
            {
                // Out of the Loaded handler, an exception would end the process.
                ::Graphics3D::LogException(L"Scene3D: attach");
                Fail();
                return;
            }

            if (m_spinningSet)
            {
                m_renderer->SetSpinning(m_spinning);
            }
            else
            {
                // The scene's own answer, which is the one the app being matched uses.
                m_spinning = m_renderer->IsSpinning();
            }

            if (m_palette != Graphics::Scene3DPalette::Default)
            {
                m_renderer->SetVariant(static_cast<int>(m_palette));
            }
        }

        m_renderer->Start();
    }

    std::function<void()> Scene3DPanel::NotifyHandler()
    {
        // Called on the render thread with the device lock held, where neither XAML nor the
        // renderer may be touched, so all it does is queue the work here.
        return [queue = DispatcherQueue::GetForCurrentThread(), weak = get_weak()]
        {
            try
            {
                if (queue)
                {
                    queue.TryEnqueue([weak]
                    {
                        // An exception escaping a dispatcher callback ends the process.
                        try
                        {
                            if (auto strong = weak.get())
                            {
                                strong->OnRendererNotified();
                            }
                        }
                        catch (...)
                        {
                            ::Graphics3D::LogException(L"Scene3D: notification");
                        }
                    });
                }
            }
            catch (...)
            {
                // The window is closing, and the panel with it.
            }
        };
    }

    void Scene3DPanel::OnRendererNotified()
    {
        // Read off the current renderer rather than carried in the notification: one queued for
        // a renderer that has since been replaced then finds nothing to do.
        if (m_renderer == nullptr)
        {
            return;
        }

        if (m_renderer->HasFailed())
        {
            Fail();
        }
        else if (m_renderer->WasReplaced())
        {
            // A new renderer rather than new resources under the old one: everything it holds
            // came from the device that went, and the pose starting over is no loss next to a
            // GPU reset.
            EndDrag();

            m_renderer->Detach();
            m_renderer.reset();

            UpdateRunning();
        }
    }

    void Scene3DPanel::Fail()
    {
        m_failed = true;

        if (m_renderer != nullptr)
        {
            EndDrag();

            m_renderer->Detach();
            m_renderer.reset();
        }

        UpdateFallback();
    }

    void Scene3DPanel::FallbackText(hstring const& value)
    {
        m_fallbackText = value;
        UpdateFallback();
    }

    void Scene3DPanel::FallbackFontSize(double value)
    {
        m_fallbackFontSize = value;
        UpdateFallback();
    }

    void Scene3DPanel::FallbackFontFamily(winrt::Windows::UI::Xaml::Media::FontFamily const& value)
    {
        m_fallbackFontFamily = value;
        UpdateFallback();
    }

    winrt::Windows::UI::Xaml::Media::Brush Scene3DPanel::FallbackForeground()
    {
        return GetValue(FallbackForegroundProperty()).try_as<winrt::Windows::UI::Xaml::Media::Brush>();
    }

    void Scene3DPanel::FallbackForeground(winrt::Windows::UI::Xaml::Media::Brush const& value)
    {
        SetValue(FallbackForegroundProperty(), value);
    }

    DependencyProperty Scene3DPanel::FallbackForegroundProperty()
    {
        // On first use rather than as a static member, whose initialiser would run at module
        // load - before there is a XAML core to register with.
        static DependencyProperty const property = DependencyProperty::Register(
            L"FallbackForeground",
            winrt::xaml_typename<winrt::Windows::UI::Xaml::Media::Brush>(),
            winrt::xaml_typename<Graphics::Scene3DPanel>(),
            PropertyMetadata{ nullptr, PropertyChangedCallback{ &Scene3DPanel::OnFallbackForegroundChanged } });

        return property;
    }

    void Scene3DPanel::OnFallbackForegroundChanged(DependencyObject const& sender,
        DependencyPropertyChangedEventArgs const&)
    {
        if (auto panel = sender.try_as<Graphics::Scene3DPanel>())
        {
            winrt::get_self<Scene3DPanel>(panel)->UpdateFallback();
        }
    }

    void Scene3DPanel::UpdateFallback()
    {
        if (!m_failed || m_fallbackText.empty())
        {
            if (m_fallback != nullptr)
            {
                uint32_t index;
                if (Children().IndexOf(m_fallback, index))
                {
                    Children().RemoveAt(index);
                }

                m_fallback = nullptr;
            }

            return;
        }

        if (m_fallback == nullptr)
        {
            TextBlock text;
            text.HorizontalAlignment(HorizontalAlignment::Center);
            text.VerticalAlignment(VerticalAlignment::Center);
            text.TextAlignment(TextAlignment::Center);
            text.IsHitTestVisible(false);

            // A glyph from an icon font reads as nothing, or as a private-use code point, to a
            // screen reader.
            AutomationProperties::SetAccessibilityView(text, winrt::Windows::UI::Xaml::Automation::Peers::AccessibilityView::Raw);

            Children().Append(text);
            m_fallback = text;
        }

        m_fallback.Text(m_fallbackText);

        if (m_fallbackFontSize > 0)
        {
            m_fallback.FontSize(m_fallbackFontSize);
        }
        else
        {
            m_fallback.ClearValue(TextBlock::FontSizeProperty());
        }

        if (m_fallbackFontFamily != nullptr)
        {
            m_fallback.FontFamily(m_fallbackFontFamily);
        }
        else
        {
            m_fallback.ClearValue(TextBlock::FontFamilyProperty());
        }

        if (const auto foreground = FallbackForeground())
        {
            m_fallback.Foreground(foreground);
        }
        else
        {
            m_fallback.ClearValue(TextBlock::ForegroundProperty());
        }
    }

    /// <summary>
    /// How many surface pixels to draw per pixel the panel occupies.
    /// </summary>
    /// <remarks>
    /// A press grows the element, and growing an element magnifies the pixels already in its
    /// swap chain rather than asking for more. So while it is held the surface is sized for the
    /// scale it is drawn at, and at rest it is sized for one.
    ///
    /// Only while it is held, because the cost is an area: at a press scale of three that is nine
    /// times the pixels, and nine times the work for the diamond's pixel shader, which is the
    /// expensive half of it. Paying that at idle for a gesture that is cosmetic and rarely reached
    /// would be the wrong way round - a reallocation when it actually happens is cheaper. It is
    /// not free, mind: it resizes the swap chain and rebuilds the scene's offscreen, resolve and
    /// depth targets, so the first frame of a press can be long. For anything on the pointer's
    /// usual path that trade would invert again.
    ///
    /// Which is the bargain the beta strikes too - `setRenderScale(view, 3.5)` as the press
    /// begins and `setRenderScale(view, 1)` when it ends. It oversamples a little past its own
    /// press scale of three, which is where the headroom in the cap comes from.
    ///
    /// Safe to resize at all because neither shader reads the pixel dimensions: `viewport.x` and
    /// `.y` go unread and only the optical plane count and the flat-mode flag beside them are
    /// used, so the render size is purely resolution and changes nothing about the look.
    /// </remarks>
    float Scene3DPanel::Oversample() const noexcept
    {
        if (!m_pressed)
        {
            return 1;
        }

        // Capped because the cost grows as the square, so a careless value would ask for a
        // surface many times larger than any benefit.
        return std::clamp(static_cast<float>(m_pressScale), 1.0f, 4.0f);
    }

    /// <summary>
    /// Grows the panel while it is held, and lets it spring back when it is let go.
    /// </summary>
    /// <remarks>
    /// The whole element scales, swap chain and all, which is what the beta does to its view -
    /// and the reason this is here rather than in the renderer: the growth is a property of how
    /// the scene is presented, not of what is in it, which is why the two screens that do it
    /// centre it differently.
    ///
    /// The damping ratio is the beta's - 0.55, and being dimensionless it carries over as it is.
    /// The period does not: **Period is the period of the spring's oscillation, not the length of
    /// the animation.** A settle takes several of them, shortened by the damping, so the useful
    /// range is tens of milliseconds - Composition's own default is 50 - and a value in the
    /// hundreds reads as a slow wobble rather than a press.
    ///
    /// Which is why there is no number from the beta here. Converting its androidx stiffness of
    /// 280 as a frequency gives 2 pi / sqrt(280), or 376 ms, and that is arithmetically faithful
    /// and far too slow on screen. 37 was settled by watching it.
    ///
    /// The magnification this would otherwise cost is covered by <see cref="Oversample"/>.
    /// </remarks>
    void Scene3DPanel::UpdatePressScale(bool pressed)
    {
        if (m_pressScale == 1 || m_pressed == pressed)
        {
            return;
        }

        m_pressed = pressed;

        if (pressed)
        {
            // Before the spring, so the pixels are there by the time it has grown into them.
            UpdateSize();
        }

        const auto visual = ElementCompositionPreview::GetElementVisual(*this);

        // Read now rather than cached: the panel may have been laid out again since the last
        // press, and a stale centre scales it off to one side.
        visual.CenterPoint({ static_cast<float>(ActualWidth()) * m_pressOrigin.X,
                             static_cast<float>(ActualHeight()) * m_pressOrigin.Y, 0 });

        const auto target = static_cast<float>(pressed ? m_pressScale : 1.0);

        auto spring = visual.Compositor().CreateSpringVector3Animation();
        spring.DampingRatio(0.55f);
        spring.Period(std::chrono::milliseconds(37));
        // Spelled out rather than braced: this one takes a nullable, so there is nothing for the
        // compiler to deduce a float3 from.
        spring.FinalValue(winrt::Windows::Foundation::Numerics::float3{ target, target, 1 });

        if (pressed)
        {
            visual.StartAnimation(L"Scale", spring);
            return;
        }

        // Given back only once it has actually shrunk. Resizing on release would drop the surface
        // to a ninth while the element was still drawn at three times it, which is the softness
        // this exists to avoid, arriving at the other end of the gesture.
        auto batch = visual.Compositor().CreateScopedBatch(CompositionBatchTypes::Animation);

        batch.Completed([strong = get_strong()](auto&&, auto&&)
        {
            // A new press may have landed while this was settling, and that one owns the size.
            if (!strong->m_pressed)
            {
                strong->UpdateSize();
            }
        });

        visual.StartAnimation(L"Scale", spring);
        batch.End();
    }

    void Scene3DPanel::UpdateSize()
    {
        if (m_renderer == nullptr)
        {
            return;
        }

        // RasterizationScale rather than CompositionScale, which also moves when the panel sits
        // in a ScrollViewer whose zoom changes - and a zoom should scale the surface, not reshape
        // it. AsyncMediaPlayerSwapChain reads it the same way.
        float scale = 1;

        if (auto root = XamlRoot())
        {
            scale = static_cast<float>(root.RasterizationScale());
        }
        else
        {
            scale = CompositionScaleX();
        }

        m_renderer->SetSize(static_cast<float>(ActualWidth()), static_cast<float>(ActualHeight()),
            scale * Oversample());
    }

    void Scene3DPanel::OnSizeChanged(IInspectable const&, SizeChangedEventArgs const&)
    {
        UpdateSize();
    }

    void Scene3DPanel::OnCompositionScaleChanged(SwapChainPanel const&, IInspectable const&)
    {
        UpdateSize();
    }

    void Scene3DPanel::OnPointerPressed(IInspectable const&, PointerRoutedEventArgs const& args)
    {
        if (!m_interactive || m_renderer == nullptr || m_dragging)
        {
            return;
        }

        if (!CapturePointer(args.Pointer()))
        {
            return;
        }

        m_pointer = args.Pointer().PointerId();
        m_dragFrom = args.GetCurrentPoint(nullptr).Position();
        m_dragging = true;

        m_renderer->BeginDrag();
        UpdatePressScale(true);

        // Not marked handled: the panel is often inside something scrollable or dismissable, and
        // a turn of the stone is not a reason for the rest of that to stop working.
    }

    void Scene3DPanel::OnPointerMoved(IInspectable const&, PointerRoutedEventArgs const& args)
    {
        if (!m_dragging || m_renderer == nullptr || args.Pointer().PointerId() != m_pointer)
        {
            return;
        }

        // Relative to the window rather than to this panel. A press puts a Scale on the
        // element's visual, and a point asked for in the element's own space is reported through
        // that transform - so while it was grown threefold the pointer appeared to move a third
        // as far, and the stone turned a third as fast. Window space has no transform of ours in
        // it, and is in the same logical pixels the drag coefficients are calibrated for.
        const auto point = args.GetCurrentPoint(nullptr).Position();

        // Previous minus current, which is the sign the gesture detector being matched reports -
        // a drag to the right gives it a negative distance. The renderer subtracts that again,
        // as beta 11 does, and the model matrix no longer negates anything. Two negations, and
        // the stone follows the pointer.
        m_renderer->Drag(m_dragFrom.X - point.X, m_dragFrom.Y - point.Y);

        m_dragFrom = point;
    }

    void Scene3DPanel::OnPointerReleased(IInspectable const&, PointerRoutedEventArgs const& args)
    {
        if (m_dragging && args.Pointer().PointerId() == m_pointer)
        {
            ReleasePointerCapture(args.Pointer());
            EndDrag();
        }
    }

    void Scene3DPanel::OnPointerCaptureLost(IInspectable const&, PointerRoutedEventArgs const& args)
    {
        if (m_dragging && args.Pointer().PointerId() == m_pointer)
        {
            EndDrag();
        }
    }

    void Scene3DPanel::EndDrag()
    {
        if (!m_dragging)
        {
            return;
        }

        m_dragging = false;
        m_pointer = 0;

        UpdatePressScale(false);

        if (m_renderer != nullptr)
        {
            m_renderer->EndDrag();
        }
    }
}
