#pragma once

#include "Text/CoreTextTextRequest.g.h"
#include "Text/CoreTextTextRequestedEventArgs.g.h"
#include "Text/CoreTextSelectionRequest.g.h"
#include "Text/CoreTextSelectionRequestedEventArgs.g.h"
#include "Text/CoreTextTextUpdatingEventArgs.g.h"
#include "Text/CoreTextSelectionUpdatingEventArgs.g.h"
#include "Text/CoreTextFormatUpdatingEventArgs.g.h"
#include "Text/CoreTextLayoutBounds.g.h"
#include "Text/CoreTextLayoutRequest.g.h"
#include "Text/CoreTextLayoutRequestedEventArgs.g.h"
#include "Text/CoreTextCompositionStartedEventArgs.g.h"
#include "Text/CoreTextCompositionCompletedEventArgs.g.h"
#include "Text/CoreTextEditContext.g.h"
#include "Text/CoreTextServicesManager.g.h"

#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Globalization.h>
#include <winrt/Windows.UI.Text.h>
#include <winrt/Windows.UI.Text.Core.h>
#include <winrt/Windows.UI.ViewManagement.h>

#include <msctf.h>

#include <vector>

namespace winrt::Telegram::Native::Text::implementation
{
    using winrt::Windows::Foundation::IReference;
    using winrt::Windows::Foundation::Rect;
    using winrt::Windows::UI::Text::Core::CoreTextFormatUpdatingResult;
    using winrt::Windows::UI::Text::Core::CoreTextInputPaneDisplayPolicy;
    using winrt::Windows::UI::Text::Core::CoreTextInputScope;
    using winrt::Windows::UI::Text::Core::CoreTextRange;
    using winrt::Windows::UI::Text::Core::CoreTextSelectionUpdatingResult;
    using winrt::Windows::UI::Text::Core::CoreTextTextUpdatingResult;
    using winrt::Windows::UI::ViewManagement::UIElementType;

    struct CoreTextTextRequest : CoreTextTextRequestT<CoreTextTextRequest>
    {
        CoreTextTextRequest(CoreTextRange range)
            : m_range(range)
        {
        }

        CoreTextRange Range() { return m_range; }

        hstring Text() { return m_text; }
        void Text(hstring const& value) { m_text = value; }

    private:
        CoreTextRange m_range;
        hstring m_text;
    };

    struct CoreTextTextRequestedEventArgs : CoreTextTextRequestedEventArgsT<CoreTextTextRequestedEventArgs>
    {
        CoreTextTextRequestedEventArgs(winrt::Telegram::Native::Text::CoreTextTextRequest const& request)
            : m_request(request)
        {
        }

        winrt::Telegram::Native::Text::CoreTextTextRequest Request() { return m_request; }

    private:
        winrt::Telegram::Native::Text::CoreTextTextRequest m_request;
    };

    struct CoreTextSelectionRequest : CoreTextSelectionRequestT<CoreTextSelectionRequest>
    {
        CoreTextSelectionRequest() = default;

        CoreTextRange Selection() { return m_selection; }
        void Selection(CoreTextRange const& value) { m_selection = value; }

    private:
        CoreTextRange m_selection{};
    };

    struct CoreTextSelectionRequestedEventArgs : CoreTextSelectionRequestedEventArgsT<CoreTextSelectionRequestedEventArgs>
    {
        CoreTextSelectionRequestedEventArgs(winrt::Telegram::Native::Text::CoreTextSelectionRequest const& request)
            : m_request(request)
        {
        }

        winrt::Telegram::Native::Text::CoreTextSelectionRequest Request() { return m_request; }

    private:
        winrt::Telegram::Native::Text::CoreTextSelectionRequest m_request;
    };

    struct CoreTextTextUpdatingEventArgs : CoreTextTextUpdatingEventArgsT<CoreTextTextUpdatingEventArgs>
    {
        CoreTextTextUpdatingEventArgs(CoreTextRange range, hstring const& text, CoreTextRange newSelection)
            : m_range(range)
            , m_text(text)
            , m_newSelection(newSelection)
        {
        }

        CoreTextRange Range() { return m_range; }
        hstring Text() { return m_text; }
        CoreTextRange NewSelection() { return m_newSelection; }

        CoreTextTextUpdatingResult Result() { return m_result; }
        void Result(CoreTextTextUpdatingResult value) { m_result = value; }

    private:
        CoreTextRange m_range;
        hstring m_text;
        CoreTextRange m_newSelection;
        CoreTextTextUpdatingResult m_result = CoreTextTextUpdatingResult::Succeeded;
    };

    struct CoreTextSelectionUpdatingEventArgs : CoreTextSelectionUpdatingEventArgsT<CoreTextSelectionUpdatingEventArgs>
    {
        CoreTextSelectionUpdatingEventArgs(CoreTextRange selection)
            : m_selection(selection)
        {
        }

        CoreTextRange Selection() { return m_selection; }

        CoreTextSelectionUpdatingResult Result() { return m_result; }
        void Result(CoreTextSelectionUpdatingResult value) { m_result = value; }

    private:
        CoreTextRange m_selection;
        CoreTextSelectionUpdatingResult m_result = CoreTextSelectionUpdatingResult::Succeeded;
    };

    struct CoreTextFormatUpdatingEventArgs : CoreTextFormatUpdatingEventArgsT<CoreTextFormatUpdatingEventArgs>
    {
        CoreTextFormatUpdatingEventArgs(CoreTextRange range, IReference<winrt::Windows::UI::Text::UnderlineType> const& underlineType, IReference<UIElementType> const& underlineColor, IReference<UIElementType> const& backgroundColor)
            : m_range(range)
            , m_underlineType(underlineType)
            , m_underlineColor(underlineColor)
            , m_backgroundColor(backgroundColor)
        {
        }

        CoreTextRange Range() { return m_range; }
        IReference<winrt::Windows::UI::Text::UnderlineType> UnderlineType() { return m_underlineType; }
        IReference<UIElementType> UnderlineColor() { return m_underlineColor; }
        IReference<UIElementType> BackgroundColor() { return m_backgroundColor; }

        CoreTextFormatUpdatingResult Result() { return m_result; }
        void Result(CoreTextFormatUpdatingResult value) { m_result = value; }

    private:
        CoreTextRange m_range;
        IReference<winrt::Windows::UI::Text::UnderlineType> m_underlineType;
        IReference<UIElementType> m_underlineColor;
        IReference<UIElementType> m_backgroundColor;
        CoreTextFormatUpdatingResult m_result = CoreTextFormatUpdatingResult::Succeeded;
    };

    struct CoreTextLayoutBounds : CoreTextLayoutBoundsT<CoreTextLayoutBounds>
    {
        CoreTextLayoutBounds() = default;

        Rect TextBounds() { return m_textBounds; }
        void TextBounds(Rect const& value) { m_textBounds = value; }

        Rect ControlBounds() { return m_controlBounds; }
        void ControlBounds(Rect const& value) { m_controlBounds = value; }

    private:
        Rect m_textBounds{};
        Rect m_controlBounds{};
    };

    struct CoreTextLayoutRequest : CoreTextLayoutRequestT<CoreTextLayoutRequest>
    {
        CoreTextLayoutRequest(CoreTextRange range);

        CoreTextRange Range() { return m_range; }
        winrt::Telegram::Native::Text::CoreTextLayoutBounds LayoutBounds() { return m_layoutBounds; }

    private:
        CoreTextRange m_range;
        winrt::Telegram::Native::Text::CoreTextLayoutBounds m_layoutBounds;
    };

    struct CoreTextLayoutRequestedEventArgs : CoreTextLayoutRequestedEventArgsT<CoreTextLayoutRequestedEventArgs>
    {
        CoreTextLayoutRequestedEventArgs(winrt::Telegram::Native::Text::CoreTextLayoutRequest const& request)
            : m_request(request)
        {
        }

        winrt::Telegram::Native::Text::CoreTextLayoutRequest Request() { return m_request; }

    private:
        winrt::Telegram::Native::Text::CoreTextLayoutRequest m_request;
    };

    struct CoreTextCompositionStartedEventArgs : CoreTextCompositionStartedEventArgsT<CoreTextCompositionStartedEventArgs>
    {
        bool IsCanceled() { return false; }
    };

    struct CoreTextCompositionCompletedEventArgs : CoreTextCompositionCompletedEventArgsT<CoreTextCompositionCompletedEventArgs>
    {
        bool IsCanceled() { return false; }
    };

    struct TextStore;
    struct CoreTextServicesManager;

    // Not agile: everything here is the thread manager's, which belongs to the thread that made
    // it, and an agile object's last release can come from the finalizer thread.
    struct CoreTextEditContext : CoreTextEditContextT<CoreTextEditContext, winrt::non_agile>
    {
        CoreTextEditContext(CoreTextServicesManager* manager);
        ~CoreTextEditContext();

        hstring Name() { return m_name; }
        void Name(hstring const& value) { m_name = value; }

        CoreTextInputScope InputScope() { return m_inputScope; }
        void InputScope(CoreTextInputScope value) { m_inputScope = value; }

        CoreTextInputPaneDisplayPolicy InputPaneDisplayPolicy() { return m_inputPaneDisplayPolicy; }
        void InputPaneDisplayPolicy(CoreTextInputPaneDisplayPolicy value) { m_inputPaneDisplayPolicy = value; }

        void NotifyFocusEnter();
        void NotifyFocusLeave();
        void NotifyTextChanged(CoreTextRange const& modifiedRange, int32_t newLength, CoreTextRange const& newSelection);
        void NotifySelectionChanged(CoreTextRange const& selection);
        void NotifyLayoutChanged();

        void ReceiveCharacter(char16_t character);

        winrt::event_token TextRequested(winrt::Windows::Foundation::TypedEventHandler<winrt::Telegram::Native::Text::CoreTextEditContext, winrt::Telegram::Native::Text::CoreTextTextRequestedEventArgs> const& handler) { return m_textRequested.add(handler); }
        void TextRequested(winrt::event_token const& token) { m_textRequested.remove(token); }

        winrt::event_token SelectionRequested(winrt::Windows::Foundation::TypedEventHandler<winrt::Telegram::Native::Text::CoreTextEditContext, winrt::Telegram::Native::Text::CoreTextSelectionRequestedEventArgs> const& handler) { return m_selectionRequested.add(handler); }
        void SelectionRequested(winrt::event_token const& token) { m_selectionRequested.remove(token); }

        winrt::event_token TextUpdating(winrt::Windows::Foundation::TypedEventHandler<winrt::Telegram::Native::Text::CoreTextEditContext, winrt::Telegram::Native::Text::CoreTextTextUpdatingEventArgs> const& handler) { return m_textUpdating.add(handler); }
        void TextUpdating(winrt::event_token const& token) { m_textUpdating.remove(token); }

        winrt::event_token SelectionUpdating(winrt::Windows::Foundation::TypedEventHandler<winrt::Telegram::Native::Text::CoreTextEditContext, winrt::Telegram::Native::Text::CoreTextSelectionUpdatingEventArgs> const& handler) { return m_selectionUpdating.add(handler); }
        void SelectionUpdating(winrt::event_token const& token) { m_selectionUpdating.remove(token); }

        winrt::event_token FormatUpdating(winrt::Windows::Foundation::TypedEventHandler<winrt::Telegram::Native::Text::CoreTextEditContext, winrt::Telegram::Native::Text::CoreTextFormatUpdatingEventArgs> const& handler) { return m_formatUpdating.add(handler); }
        void FormatUpdating(winrt::event_token const& token) { m_formatUpdating.remove(token); }

        winrt::event_token LayoutRequested(winrt::Windows::Foundation::TypedEventHandler<winrt::Telegram::Native::Text::CoreTextEditContext, winrt::Telegram::Native::Text::CoreTextLayoutRequestedEventArgs> const& handler) { return m_layoutRequested.add(handler); }
        void LayoutRequested(winrt::event_token const& token) { m_layoutRequested.remove(token); }

        winrt::event_token CompositionStarted(winrt::Windows::Foundation::TypedEventHandler<winrt::Telegram::Native::Text::CoreTextEditContext, winrt::Telegram::Native::Text::CoreTextCompositionStartedEventArgs> const& handler) { return m_compositionStarted.add(handler); }
        void CompositionStarted(winrt::event_token const& token) { m_compositionStarted.remove(token); }

        winrt::event_token CompositionCompleted(winrt::Windows::Foundation::TypedEventHandler<winrt::Telegram::Native::Text::CoreTextEditContext, winrt::Telegram::Native::Text::CoreTextCompositionCompletedEventArgs> const& handler) { return m_compositionCompleted.add(handler); }
        void CompositionCompleted(winrt::event_token const& token) { m_compositionCompleted.remove(token); }

        winrt::event_token FocusRemoved(winrt::Windows::Foundation::TypedEventHandler<winrt::Telegram::Native::Text::CoreTextEditContext, winrt::Windows::Foundation::IInspectable> const& handler) { return m_focusRemoved.add(handler); }
        void FocusRemoved(winrt::event_token const& token) { m_focusRemoved.remove(token); }

        // From the manager: TSF moved its focus off this context, not at this context's asking.
        void OnFocusMoved(ITfDocumentMgr* focus);

    private:
        friend struct TextStore;

        void EnterFocus();
        void LeaveFocus();

        // Whatever the owner asked for while TSF held the store locked, done once it is not.
        void FlushDeferred();

        CoreTextRange RequestSelection();
        hstring RequestText(LONG start, LONG end);
        winrt::Telegram::Native::Text::CoreTextLayoutBounds RequestLayout(LONG start, LONG end);
        bool UpdateText(LONG start, LONG end, hstring const& text, CoreTextRange newSelection);

        // The composition's look, from the attribute property TSF keeps on the context.
        void ReportFormats(ITfContext* context, TfEditCookie cookie);

        winrt::com_ptr<CoreTextServicesManager> m_manager;
        winrt::com_ptr<TextStore> m_store;

        winrt::com_ptr<ITfDocumentMgr> m_documentMgr;
        winrt::com_ptr<ITfContext> m_context;
        TfEditCookie m_contextCookie = TF_INVALID_EDIT_COOKIE;
        DWORD m_textEditCookie = TF_INVALID_COOKIE;

        HWND m_window = nullptr;
        bool m_focused = false;

        // The length TSF believes the text has; it never sees the text but through requests.
        LONG m_length = 0;

        bool m_deferredLeave = false;
        bool m_deferredEnter = false;
        bool m_deferredText = false;
        bool m_deferredSelection = false;
        bool m_deferredLayout = false;
        LONG m_deferredLength = 0;
        LONG m_deferredStart = 0;
        LONG m_deferredOldEnd = 0;
        LONG m_deferredNewEnd = 0;

        // A character outside the basic plane arrives as two, the high one first.
        char16_t m_highSurrogate = 0;

        hstring m_name;
        CoreTextInputScope m_inputScope = CoreTextInputScope::Default;
        CoreTextInputPaneDisplayPolicy m_inputPaneDisplayPolicy = CoreTextInputPaneDisplayPolicy::Automatic;

        winrt::event<winrt::Windows::Foundation::TypedEventHandler<winrt::Telegram::Native::Text::CoreTextEditContext, winrt::Telegram::Native::Text::CoreTextTextRequestedEventArgs>> m_textRequested;
        winrt::event<winrt::Windows::Foundation::TypedEventHandler<winrt::Telegram::Native::Text::CoreTextEditContext, winrt::Telegram::Native::Text::CoreTextSelectionRequestedEventArgs>> m_selectionRequested;
        winrt::event<winrt::Windows::Foundation::TypedEventHandler<winrt::Telegram::Native::Text::CoreTextEditContext, winrt::Telegram::Native::Text::CoreTextTextUpdatingEventArgs>> m_textUpdating;
        winrt::event<winrt::Windows::Foundation::TypedEventHandler<winrt::Telegram::Native::Text::CoreTextEditContext, winrt::Telegram::Native::Text::CoreTextSelectionUpdatingEventArgs>> m_selectionUpdating;
        winrt::event<winrt::Windows::Foundation::TypedEventHandler<winrt::Telegram::Native::Text::CoreTextEditContext, winrt::Telegram::Native::Text::CoreTextFormatUpdatingEventArgs>> m_formatUpdating;
        winrt::event<winrt::Windows::Foundation::TypedEventHandler<winrt::Telegram::Native::Text::CoreTextEditContext, winrt::Telegram::Native::Text::CoreTextLayoutRequestedEventArgs>> m_layoutRequested;
        winrt::event<winrt::Windows::Foundation::TypedEventHandler<winrt::Telegram::Native::Text::CoreTextEditContext, winrt::Telegram::Native::Text::CoreTextCompositionStartedEventArgs>> m_compositionStarted;
        winrt::event<winrt::Windows::Foundation::TypedEventHandler<winrt::Telegram::Native::Text::CoreTextEditContext, winrt::Telegram::Native::Text::CoreTextCompositionCompletedEventArgs>> m_compositionCompleted;
        winrt::event<winrt::Windows::Foundation::TypedEventHandler<winrt::Telegram::Native::Text::CoreTextEditContext, winrt::Windows::Foundation::IInspectable>> m_focusRemoved;
    };

    struct CoreTextServicesManager : CoreTextServicesManagerT<CoreTextServicesManager, winrt::non_agile, ITfThreadMgrEventSink, ITfInputProcessorProfileActivationSink>
    {
        CoreTextServicesManager() = default;

        static winrt::Telegram::Native::Text::CoreTextServicesManager GetForCurrentView();

        winrt::Telegram::Native::Text::CoreTextEditContext CreateEditContext();

        winrt::Windows::Globalization::Language InputLanguage();

        winrt::event_token InputLanguageChanged(winrt::Windows::Foundation::TypedEventHandler<winrt::Telegram::Native::Text::CoreTextServicesManager, winrt::Windows::Foundation::IInspectable> const& handler) { return m_inputLanguageChanged.add(handler); }
        void InputLanguageChanged(winrt::event_token const& token) { m_inputLanguageChanged.remove(token); }

        HRESULT STDMETHODCALLTYPE OnInitDocumentMgr(ITfDocumentMgr*) noexcept override { return S_OK; }
        HRESULT STDMETHODCALLTYPE OnUninitDocumentMgr(ITfDocumentMgr*) noexcept override { return S_OK; }
        HRESULT STDMETHODCALLTYPE OnSetFocus(ITfDocumentMgr* focus, ITfDocumentMgr* previous) noexcept override;
        HRESULT STDMETHODCALLTYPE OnPushContext(ITfContext*) noexcept override { return S_OK; }
        HRESULT STDMETHODCALLTYPE OnPopContext(ITfContext*) noexcept override { return S_OK; }

        HRESULT STDMETHODCALLTYPE OnActivated(DWORD profileType, LANGID langid, REFCLSID clsid, REFGUID catid, REFGUID profile, HKL hkl, DWORD flags) noexcept override;

        void Initialize();

        ITfThreadMgr* ThreadMgr() { return m_threadMgr.get(); }
        TfClientId ClientId() { return m_clientId; }

        // Focus goes here when an edit context gives it up: a document with no context, so that
        // no text service goes on typing into the one that left.
        ITfDocumentMgr* EmptyDocumentMgr() { return m_emptyDocumentMgr.get(); }

        ITfCategoryMgr* CategoryMgr();
        ITfDisplayAttributeMgr* DisplayAttributeMgr();

        void Register(CoreTextEditContext* context);
        void Unregister(CoreTextEditContext* context);

    private:
        winrt::com_ptr<ITfThreadMgr> m_threadMgr;
        TfClientId m_clientId = TF_CLIENTID_NULL;
        winrt::com_ptr<ITfDocumentMgr> m_emptyDocumentMgr;
        winrt::com_ptr<ITfCategoryMgr> m_categoryMgr;
        winrt::com_ptr<ITfDisplayAttributeMgr> m_displayAttributeMgr;

        DWORD m_threadMgrEventCookie = TF_INVALID_COOKIE;
        DWORD m_profileActivationCookie = TF_INVALID_COOKIE;

        // Not owning: a context takes itself out when it goes.
        std::vector<CoreTextEditContext*> m_contexts;

        winrt::event<winrt::Windows::Foundation::TypedEventHandler<winrt::Telegram::Native::Text::CoreTextServicesManager, winrt::Windows::Foundation::IInspectable>> m_inputLanguageChanged;
    };
}

namespace winrt::Telegram::Native::Text::factory_implementation
{
    struct CoreTextServicesManager : CoreTextServicesManagerT<CoreTextServicesManager, implementation::CoreTextServicesManager>
    {
    };
}
