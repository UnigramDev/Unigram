//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.Views;
using Windows.ApplicationModel.Resources;
using Windows.Storage;
using Windows.UI.Xaml;

namespace Telegram.Services
{
    public interface ILocaleService
    {
        Task<BaseObject> SetLanguageAsync(LanguagePackInfo info, bool refresh);

        CultureInfo CurrentCulture { get; }
        string Id { get; }

        FlowDirection FlowDirection { get; }

        string GetString(string key);
        string GetString(string key, int quantity);

        void Handle(UpdateLanguagePackStrings update);

        event EventHandler<LocaleChangedEventArgs> Changed;
    }

    public class LocaleChangedEventArgs : EventArgs
    {
        public IList<LanguagePackString> Strings { get; }

        public LocaleChangedEventArgs(IList<LanguagePackString> strings)
        {
            Strings = strings;
        }
    }

    public class LocaleService : ILocaleService
    {
        public const string LANGPACK = "unigram";

        private const int QUANTITY_OTHER = 0x0000;
        private const int QUANTITY_ZERO = 0x0001;
        private const int QUANTITY_ONE = 0x0002;
        private const int QUANTITY_TWO = 0x0004;
        private const int QUANTITY_FEW = 0x0008;
        private const int QUANTITY_MANY = 0x0010;

        private readonly ResourceLoader _loader;

        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _languagePack = new ConcurrentDictionary<string, ConcurrentDictionary<string, string>>();
        private string _languageCode;
        private string _languageBase;
        private string _languagePlural;

        private CultureInfo _currentCulture;

        private readonly string _languagePath;

        public LocaleService()
        {
            _loader = ResourceLoader.GetForViewIndependentUse("Resources");

            _languagePath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "langpack");

            _languageCode = SettingsService.Current.LanguagePackId;
            _languageBase = SettingsService.Current.LanguageBaseId;
            _languagePlural = SettingsService.Current.LanguagePluralId;

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

        public CultureInfo CurrentCulture => _currentCulture;

        public string Id => CurrentCulture.TwoLetterISOLanguageName;

        public FlowDirection FlowDirection => _currentCulture.TextInfo.IsRightToLeft && SettingsService.Current.Diagnostics.AllowRightToLeft
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;

        public async Task<BaseObject> SetLanguageAsync(LanguagePackInfo info, bool refresh)
        {
            _languageCode = info.Id;
            _languageBase = info.BaseLanguagePackId;
            _languagePlural = info.PluralCode;

            SettingsService.Current.LanguagePackId = info.Id;
            SettingsService.Current.LanguageBaseId = info.BaseLanguagePackId;
            SettingsService.Current.LanguagePluralId = info.PluralCode;

            LoadCurrentCulture();

            foreach (var clientService in TypeResolver.Current.ResolveAll<IClientService>())
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
            var values = GetLanguagePack(_languageCode);
            if (values.TryGetValue(key, out string value))
            {
                return value;
            }

            var result = Client.Execute(new GetLanguagePackString(_languagePath, LANGPACK, _languageCode, key));
            if (result is LanguagePackStringValueOrdinary ordinary)
            {
                return values[key] = ordinary.Value;
            }

#if zDEBUG
            var text = _loader.GetString(key);
            if (text.Length > 0)
            {
                return text;
            }

            return key;
#else
            return _loader.GetString(key);
#endif
        }

        public string GetString(string key, int quantity)
        {
            var selector = key + StringForQuantity(quantity);

            var values = GetLanguagePack(_languageCode);
            if (values.TryGetValue(selector, out string value))
            {
                return value;
            }

            var result = Client.Execute(new GetLanguagePackString(_languagePath, LANGPACK, _languageCode, key));
            if (result is LanguagePackStringValuePluralized pluralized)
            {
                values[key + "_zero"] = pluralized.ZeroValue;
                values[key + "_one"] = pluralized.OneValue;
                values[key + "_two"] = pluralized.TwoValue;
                values[key + "_few"] = pluralized.FewValue;
                values[key + "_many"] = pluralized.ManyValue;
                values[key + "_other"] = pluralized.OtherValue;

                switch (quantity)
                {
                    case QUANTITY_ZERO:
                        return pluralized.ZeroValue;
                    case QUANTITY_ONE:
                        return pluralized.OneValue;
                    case QUANTITY_TWO:
                        return pluralized.TwoValue;
                    case QUANTITY_FEW:
                        return pluralized.FewValue;
                    case QUANTITY_MANY:
                        return pluralized.ManyValue;
                    default:
                        return pluralized.OtherValue;
                }
            }

#if zDEBUG
            var text = _loader.GetString(selector);
            if (text.Length > 0)
            {
                return text;
            }

            return selector;
#else
            return _loader.GetString(selector);
#endif
        }

        #region Handle

        public void Handle(UpdateLanguagePackStrings update)
        {
            var values = GetLanguagePack(update.LanguagePackId);

            if (update.Strings.Count > 0)
            {
                foreach (var value in update.Strings)
                {
                    switch (value.Value)
                    {
                        case LanguagePackStringValueOrdinary ordinary:
                            values[value.Key] = ordinary.Value;
                            break;
                        case LanguagePackStringValuePluralized pluralized:
                            values[value.Key + "Zero"] = pluralized.ZeroValue;
                            values[value.Key + "One"] = pluralized.OneValue;
                            values[value.Key + "Two"] = pluralized.TwoValue;
                            values[value.Key + "Few"] = pluralized.FewValue;
                            values[value.Key + "Many"] = pluralized.ManyValue;
                            values[value.Key + "Other"] = pluralized.OtherValue;
                            break;
                        case LanguagePackStringValueDeleted:
                            values.TryRemove(value.Key, out _);
                            break;
                    }
                }
            }
            else
            {
                values.Clear();
            }

            Changed?.Invoke(this, new LocaleChangedEventArgs(update.Strings));
        }

        #endregion

        private ConcurrentDictionary<string, string> GetLanguagePack(string key)
        {
            if (_languagePack.TryGetValue(key, out ConcurrentDictionary<string, string> values))
            {
            }
            else
            {
                values = _languagePack[key] = new ConcurrentDictionary<string, string>();
            }

            return values;
        }

        private string StringForQuantity(int quantity)
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
