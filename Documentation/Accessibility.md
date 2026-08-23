# Accessibility conventions

## Screen-reader message summaries

Every non-service message exposed through UI Automation starts with a stable sender prefix. The same policy is used for message-list items and compact history items.

| Message | Prefix |
| --- | --- |
| Incoming private or group message | Sender display name |
| Outgoing private or group message | Localized “You” label |
| Channel post | Sender or channel title |
| Saved Message | Original source title, when available |
| Service message | No added prefix; the service text identifies its actor and action |

The prefix is included on every message, even when adjacent messages are visually grouped. This keeps keyboard and screen-reader navigation unambiguous. A colon separates the prefix from the content summary.

Messages with an inline keyboard add a concise localized “Message has inline buttons” indication to the summary. Button labels are not duplicated in the message summary; they remain available when the user navigates to the buttons.

## New-message notifications

Newly inserted incoming and outgoing messages in the active chat raise a UI Automation notification event. Notifications use the same prefix and content-summary policy as message items, include the inline-button indication, and use `AutomationNotificationProcessing.All` so a burst of messages is announced in order. Loading existing history does not raise these notifications.

UI code must use `AccessibilityService` for UI Automation listener detection and notification delivery instead of calling `AutomationPeer.ListenerExists` directly.
