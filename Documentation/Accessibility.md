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
