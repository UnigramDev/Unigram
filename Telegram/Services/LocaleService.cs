//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Td;
using Telegram.Td.Api;
using Windows.Globalization;
using Windows.Storage;
using Windows.UI.Xaml;

namespace Telegram.Services
{
    public interface ILocaleService
    {
        Task<Object> SetLanguageAsync(LanguagePackInfo info, bool refresh);

        CultureInfo CurrentCulture { get; }
        string Id { get; }

        FlowDirection FlowDirection { get; }

        string GetString(string key);
        string GetString(string key, int quantity);

        void GetDatePositions(out int dayPosition, out int monthPosition, out int yearPosition);

        void Handle(UpdateLanguagePackStrings update);

        event EventHandler<LocaleChangedEventArgs> Changed;
    }

    public partial class LocaleChangedEventArgs : EventArgs
    {
        public Vector<LanguagePackString> Strings { get; }

        public LocaleChangedEventArgs(Vector<LanguagePackString> strings)
        {
            Strings = strings;
        }
    }

    public partial class LocaleService : ILocaleService
    {
        public const string LANGPACK = "unigram";

        private const int QUANTITY_OTHER = 0x0000;
        private const int QUANTITY_ZERO = 0x0001;
        private const int QUANTITY_ONE = 0x0002;
        private const int QUANTITY_TWO = 0x0004;
        private const int QUANTITY_FEW = 0x0008;
        private const int QUANTITY_MANY = 0x0010;

        private readonly ConcurrentDictionary<string, LanguagePack> _languagePacks = new();

        // The pack for _languageCode, so the accessors don't look it up by name on every call.
        private LanguagePack _pack;

        private string _languageCode;
        private string _languageBase;
        private string _languagePlural;

        private CultureInfo _currentCulture;

        private readonly string _languagePath;

        public LocaleService()
        {
            _languagePath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "langpack");

            _languageCode = AppSettings.LanguagePackId;
            _languageBase = AppSettings.LanguageBaseId;
            _languagePlural = AppSettings.LanguagePluralId;
            _pack = GetLanguagePack(_languageCode);

            LoadCurrentCulture();
        }

        public event EventHandler<LocaleChangedEventArgs> Changed;

        private void LoadCurrentCulture()
        {
            string[] args;
            if (!string.IsNullOrEmpty(_languagePlural))
            {
                args = _languagePlural.Split('_');
            }
            else if (!string.IsNullOrEmpty(_languageBase))
            {
                args = _languageBase.Split('_');
            }
            else
            {
                args = _languageCode.Split('_');
            }

            if (args.Length == 1)
            {
                _currentCulture = new CultureInfo(args[0]);
            }
            else
            {
                _currentCulture = new CultureInfo($"{args[0]}_{args[1]}");
            }

            Locale.SetRules(_languagePlural);
        }

        private static ILocaleService _current;
        public static ILocaleService Current => _current ??= new LocaleService();

        public static string SystemLanguageId()
        {
            var languageId = ApplicationLanguages.Languages[0].Split('-');
            if (languageId[0] == "pt")
            {
                return ApplicationLanguages.Languages[0].ToLower();
            }

            return languageId[0];
        }

        public CultureInfo CurrentCulture => _currentCulture;

        public string Id => CurrentCulture.TwoLetterISOLanguageName;

        public FlowDirection FlowDirection => _currentCulture.TextInfo.IsRightToLeft && AppSettings.Diagnostics.AllowRightToLeft
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;

        public async Task<Object> SetLanguageAsync(LanguagePackInfo info, bool refresh)
        {
            _languageCode = info.Id;
            _languageBase = info.BaseLanguagePackId;
            _languagePlural = info.PluralCode;
            _pack = GetLanguagePack(_languageCode);

            AppSettings.LanguagePackId = info.Id;
            AppSettings.LanguageBaseId = info.BaseLanguagePackId;
            AppSettings.LanguagePluralId = info.PluralCode;

            LoadCurrentCulture();

            foreach (var clientService in LifetimeService.Current.ResolveAll<IClientService>())
            {
                var response = await clientService.SendAsync(new SetOption("language_pack_id", new OptionValueString(info.Id)));
                if (response is Ok && refresh)
                {
                    if (!info.IsOfficial && !info.IsInstalled)
                    {
                        clientService.Send(new AddCustomServerLanguagePack(info.Id));
                    }

                    response = await clientService.SendAsync(new SynchronizeLanguagePack(info.Id));

                    if (response is Error)
                    {
                        return response;
                    }
                }
                else
                {
                    return response;
                }
            }

            return new Ok();
        }

        public string GetString(string key)
        {
            if (_pack.Ordinary.TryGetValue(key, out string value))
            {
                return value;
            }

            var result = Client.Execute(new GetLanguagePackString(_languagePath, LANGPACK, _languageCode, key));
            if (result is LanguagePackStringValueOrdinary ordinary)
            {
                _pack.Ordinary[key] = ordinary.Value;
                return ordinary.Value;
            }

#if zDEBUG
            return LocaleFallback.GetString(key) ?? key;
#else
            return LocaleFallback.GetString(key) ?? string.Empty;
#endif
        }

        public string GetString(string key, int quantity)
        {
            // The six forms are stored together rather than under six suffixed keys, so a hit is one
            // hash and an array index. Building `key + "_other"` per call was the only allocation on
            // this path, and Formatter's relative times put it on every chat row.
            if (_pack.Pluralized.TryGetValue(key, out string[] forms))
            {
                return Select(forms, quantity);
            }

            var result = Client.Execute(new GetLanguagePackString(_languagePath, LANGPACK, _languageCode, key));
            if (result is LanguagePackStringValuePluralized pluralized)
            {
                forms = Forms(pluralized);
                _pack.Pluralized[key] = forms;

                return Select(forms, quantity);
            }

            var selector = key + StringForQuantity(quantity);

            // The English table carries all six forms, but a language's rules can ask for one the
            // key does not have, so this falls back the same way Select does.
            var fallback = LocaleFallback.GetString(selector) ?? LocaleFallback.GetString(key + "_other");

#if zDEBUG
            return fallback ?? selector;
#else
            return fallback ?? string.Empty;
#endif
        }

        public void GetDatePositions(out int dayPosition, out int monthPosition, out int yearPosition)
        {
            dayPosition = 0;
            monthPosition = 1;
            yearPosition = 2;

            // TODO: this isn't great because it does not respect the system locale
            var parts = LocaleService.Current.CurrentCulture.DateTimeFormat.ShortDatePattern.Split(LocaleService.Current.CurrentCulture.DateTimeFormat.DateSeparator);
            if (parts.Length != 3)
            {
                parts = new[] { "dd", "MM", "yyyy" };
            }

            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].StartsWith("d", StringComparison.OrdinalIgnoreCase))
                {
                    dayPosition = i;
                }
                else if (parts[i].StartsWith("M", StringComparison.OrdinalIgnoreCase))
                {
                    monthPosition = i;
                }
                else if (parts[i].StartsWith("y", StringComparison.OrdinalIgnoreCase))
                {
                    yearPosition = i;
                }
            }
        }

        #region Handle

        public void Handle(UpdateLanguagePackStrings update)
        {
            var pack = GetLanguagePack(update.LanguagePackId);

            if (update.Strings.Count > 0)
            {
                foreach (var value in update.Strings)
                {
                    switch (value.Value)
                    {
                        case LanguagePackStringValueOrdinary ordinary:
                            pack.Ordinary[value.Key] = ordinary.Value;
                            break;
                        case LanguagePackStringValuePluralized pluralized:
                            pack.Pluralized[value.Key] = Forms(pluralized);
                            break;
                        case LanguagePackStringValueDeleted:
                            pack.Ordinary.TryRemove(value.Key, out _);
                            pack.Pluralized.TryRemove(value.Key, out _);
                            break;
                    }
                }
            }
            else
            {
                pack.Ordinary.Clear();
                pack.Pluralized.Clear();
            }

            Changed?.Invoke(this, new LocaleChangedEventArgs(update.Strings));
        }

        #endregion

        /// <summary>
        /// One language's cached strings. Pluralized keys hold all six forms in one entry, in
        /// QUANTITY order, because that is how TDLib delivers them and how they are read back.
        /// </summary>
        private sealed class LanguagePack
        {
            public readonly ConcurrentDictionary<string, string> Ordinary = new();
            public readonly ConcurrentDictionary<string, string[]> Pluralized = new();
        }

        private LanguagePack GetLanguagePack(string key)
        {
            // GetOrAdd, not an add after a miss: two threads reaching a new language would
            // otherwise each build a pack and one would silently lose whatever it had cached.
            return _languagePacks.GetOrAdd(key, static _ => new LanguagePack());
        }

        private static string[] Forms(LanguagePackStringValuePluralized value)
        {
            return new[]
            {
                value.ZeroValue,
                value.OneValue,
                value.TwoValue,
                value.FewValue,
                value.ManyValue,
                value.OtherValue
            };
        }

        private static string Select(string[] forms, int quantity)
        {
            // A language pack only fills the forms its own rules use, so anything else is empty
            // and "other" is the universal answer.
            var value = forms[IndexForQuantity(quantity)];
            return string.IsNullOrEmpty(value) ? forms[5] : value;
        }

        private static int IndexForQuantity(int quantity)
        {
            switch (quantity)
            {
                case QUANTITY_ZERO:
                    return 0;
                case QUANTITY_ONE:
                    return 1;
                case QUANTITY_TWO:
                    return 2;
                case QUANTITY_FEW:
                    return 3;
                case QUANTITY_MANY:
                    return 4;
                case QUANTITY_OTHER:
                default:
                    return 5;
            }
        }

        private static string StringForQuantity(int quantity)
        {
            switch (quantity)
            {
                case QUANTITY_ZERO:
                    return "_zero";
                case QUANTITY_ONE:
                    return "_one";
                case QUANTITY_TWO:
                    return "_two";
                case QUANTITY_FEW:
                    return "_few";
                case QUANTITY_MANY:
                    return "_many";
                case QUANTITY_OTHER:
                default:
                    return "_other";
            }
        }
    }
}
