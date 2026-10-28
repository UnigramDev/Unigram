#include "pch.h"
#include "WidgetTypes.h"
#if __has_include("WidgetTheme.g.cpp")
#include "WidgetTheme.g.cpp"
#endif
#if __has_include("WidgetOptions.g.cpp")
#include "WidgetOptions.g.cpp"
#endif
#if __has_include("WidgetStorageCommit.g.cpp")
#include "WidgetStorageCommit.g.cpp"
#endif
#if __has_include("WidgetRequest.g.cpp")
#include "WidgetRequest.g.cpp"
#endif

#include <cmath>

namespace winrt::Telegram::Native::Widgets::implementation
{
    twidget::WidgetTheme WidgetTheme::ToHost() const
    {
        twidget::WidgetTheme theme;
        theme.colorScheme = to_string(m_colorScheme);

        for (auto const& pair : m_params)
        {
            theme.params.emplace(to_string(pair.Key()), to_string(pair.Value()));
        }

        auto text = [](hstring const& value) { return value.empty() ? std::optional<std::string>() : to_string(value); };
        auto number = [](double value) { return std::isnan(value) ? std::optional<double>() : value; };

        theme.button.bg = text(m_buttonBackground);
        theme.button.text = text(m_buttonForeground);
        theme.button.radius = text(m_buttonRadius);
        theme.button.font = text(m_buttonFont);
        theme.button.padding = text(m_buttonPadding);
        theme.button.height = text(m_buttonHeight);
        theme.button.pressedOpacity = number(m_buttonPressedOpacity);
        theme.button.disabledOpacity = number(m_buttonDisabledOpacity);
        theme.text.fontSize = number(m_textFontSize);
        theme.text.lineHeight = number(m_textLineHeight);
        theme.text.padding = text(m_textPadding);
        return theme;
    }

    twidget::WidgetData WidgetOptions::ToData() const
    {
        return implementation::ToData(m_chatData, m_publicData);
    }

    twidget::WidgetContext WidgetOptions::ToContext() const
    {
        auto text = [](hstring const& value) { return value.empty() ? std::optional<std::string>() : to_string(value); };

        twidget::WidgetContext context;
        context.isCopy = m_isCopy;
        context.canQuery = m_canQuery;
        context.backgroundQuery = m_backgroundQuery;
        context.chatType = to_string(m_chatType);

        if (m_viewerId > 0)
        {
            twidget::WidgetViewer viewer;
            viewer.id = m_viewerId;
            viewer.firstName = text(m_viewerFirstName);
            viewer.lastName = text(m_viewerLastName);
            viewer.username = text(m_viewerUsername);
            viewer.isPremium = m_viewerIsPremium;
            context.viewer = std::move(viewer);
        }

        return context;
    }

    void WidgetRequest::Complete(hstring const& result)
    {
        twidget::WidgetRequestOutcome outcome;
        if (!result.empty())
        {
            outcome.answer.result = to_string(result);
        }

        Settle(outcome);
    }

    void WidgetRequest::Fail(Telegram::Native::Widgets::WidgetRequestFailure failure)
    {
        switch (failure)
        {
        case Telegram::Native::Widgets::WidgetRequestFailure::Timeout: Settle({ twidget::WidgetRequestFailure::Timeout, {} }); break;
        case Telegram::Native::Widgets::WidgetRequestFailure::RateLimited: Settle({ twidget::WidgetRequestFailure::RateLimited, {} }); break;
        default: Settle({ twidget::WidgetRequestFailure::Failed, {} }); break;
        }
    }

    void WidgetRequest::Settle(const twidget::WidgetRequestOutcome& outcome)
    {
        Respond respond;
        {
            std::lock_guard guard(m_lock);
            respond = std::move(m_respond);
            m_respond = nullptr;
        }

        if (respond)
        {
            respond(outcome);
        }
    }

    twidget::WidgetData ToData(hstring const& chatData, hstring const& publicData)
    {
        // "" is how the bot clears a field, and none for the widget either way.
        twidget::WidgetData data;
        if (!chatData.empty()) data.chatData = to_string(chatData);
        if (!publicData.empty()) data.publicData = to_string(publicData);
        return data;
    }

    twidget::StorageArea ToStorageArea(Windows::Foundation::Collections::IMap<hstring, hstring> const& map)
    {
        twidget::StorageArea area;
        for (auto const& pair : map)
        {
            area.emplace(to_string(pair.Key()), to_string(pair.Value()));
        }

        return area;
    }
}
