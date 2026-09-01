# Untranslated system error messages

Windows returns system error text in the user's language, and the crash grouping hash includes
the message, so one fault splits into a group per locale and every one of them looks small.
`ExceptionSerializer.TranslateText` undoes this by mapping each localised sentence onto the
English one the same fault produces in an English install.

The rule that governs that switch is that **the English target is never invented**. It has to be
a wording the same fault actually produces — taken from an English report for the same fault, or
from the file's own canonical set. A plausible-looking translation is worse than no entry: it
merges the group under text that Windows never emits, and the next person has no way to tell the
invented string from a real one.

The messages below are the ones where no English counterpart exists yet, so there is nothing to
map them onto. They are parked here rather than guessed at. When an English report for one of
them turns up, add the `case` beside its siblings and delete its section here, in the same
commit.

## How to check whether the English has landed

```
crashctl list --days 28 --status all --limit 5000 --json
```

and search the messages for the suggested English fragment under each entry. That fragment is a
search term, not a proposed target: what goes in the `case` is whatever wording a real English
report carries.

Two things to get right when adding one:

- The `case` text is the sentence **with the HRESULT suffix stripped**, the way `_hresultSuffix`
  strips it — `(0x…)` on the CsWinRT build, `(Exception from HRESULT: 0x…)` on .NET Native.
- A report often carries an outer message and an originating description. Those are separate
  lines and are translated separately, so the `case` is the localised line on its own, not the
  pair the dashboard shows joined together.

## Pending

### Portuguese — OLE clipboard

```
Falha na inicialização da área de transferência OLE.
```

Seen on 12.8.1 as `Exception`, arriving as the originating description under an outer
`Unspecified error`, so it carries no HRESULT of its own. Search for: `clipboard`.

### German — DirectWrite font cache

```
Der Schriftartcache beinhaltet ungültige Daten.
```

Seen on 12.8.1 as `Exception`, with `(Exception from HRESULT: 0x88985007)` attached. The code is
known, but it is a DirectWrite one, and the remarks on `TranslateHResult` explain why that family
must not be keyed on the number: `UnhandledErrorDetected` flattens the propagated exception to
`E_FAIL` while the message keeps the original wording, so these have to be matched as sentences.
Search for: `font cache`.

### French — missing procedure

```
La procédure spécifiée est introuvable.
```

Seen on 12.9.1 as `Exception`, no HRESULT attached. Search for: `specified procedure`.

### Russian — invalid object state

```
Объект находился в состоянии, недопустимом для обработки метода.
```

Seen on 12.9.1 as `Exception`, no HRESULT attached. Search for: `state` together with `method`.

### Russian — XAML property value type

```
Недопустимый тип значения oldValue для этого свойства
```

Seen on 12.8.1 as `Exception`, as the originating description under an outer
`The application called an interface that was marshalled for a different thread.` — the outer
half is already handled. Note there is no full stop at the end; the `case` has to match that.
This is XAML's own wording rather than a system error, so the English may only ever appear in a
report from an English install hitting the same XAML failure. Search for: `oldValue`.

## Not fixable by translation

```
Unspecified error ��� ��Ƽ����Ʈ �ڵ��������� �����ڵ� ������ ������ �����ϴ�.
```

Seen on 12.8.1 as `Exception`. The Korean text was already mojibake when the report was built —
the bytes are destroyed, not merely localised — so no `case` can match it and no English target
would help. It is recorded only so it is not mistaken for a missing translation. If these become
frequent it is an encoding bug on the reporting path, which is a different change.
