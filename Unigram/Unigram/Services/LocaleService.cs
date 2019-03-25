using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Views;
using Windows.ApplicationModel.Resources;
using Windows.Storage;

namespace Unigram.Services
{
    public interface ILocaleService : IHandle<UpdateLanguagePackStrings>
    {
        Task<BaseObject> SetLanguageAsync(LanguagePackInfo info, bool refresh);

        string GetString(string key);
        string GetString(string key, int quantity);
    }

    public class LocaleService : ILocaleService
    {
        private const int QUANTITY_OTHER = 0x0000;
        private const int QUANTITY_ZERO = 0x0001;
        private const int QUANTITY_ONE = 0x0002;
        private const int QUANTITY_TWO = 0x0004;
        private const int QUANTITY_FEW = 0x0008;
        private const int QUANTITY_MANY = 0x0010;

        private readonly ResourceLoader _loader;

        private readonly Dictionary<string, Dictionary<string, string>> _languagePack = new Dictionary<string, Dictionary<string, string>>();
        private string _languageCode;
        private string _languagePlural;

        private string _languagePath;

        public LocaleService()
        {
            _loader = ResourceLoader.GetForViewIndependentUse("Resources");

            _languagePath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "langpack");

            _languageCode = SettingsService.Current.LanguagePackId;
            _languagePlural = SettingsService.Current.LanguagePluralId;

            Locale.SetRules(_languagePlural);
        }

        private static ILocaleService _current;
        public static ILocaleService Current => _current = _current ?? new LocaleService();

        public async Task<BaseObject> SetLanguageAsync(LanguagePackInfo info, bool refresh)
        {
            _languageCode = info.Id;
            _languagePlural = info.PluralCode;

            SettingsService.Current.LanguagePackId = info.Id;
            SettingsService.Current.LanguagePluralId = info.PluralCode;

            Locale.SetRules(info.PluralCode);

            foreach (var protoService in TLContainer.Current.ResolveAll<IProtoService>())
            {
                var response = await protoService.SendAsync(new SetOption("language_pack_id", new OptionValueString(info.Id)));
                if (response is Ok && refresh)
                {
                    if (!info.IsOfficial && !info.IsInstalled)
                    {
                        protoService.Send(new AddCustomServerLanguagePack(info.Id));
                    }

                    response = await protoService.SendAsync(new SynchronizeLanguagePack(info.Id));

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

            var result = Client.Execute(new GetLanguagePackString(_languagePath, "android", _languageCode, key));
            if (result is LanguagePackStringValueOrdinary ordinary)
            {
                return values[key] = GetValue(ordinary.Value);
            }

#if DEBUG
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

            var result = Client.Execute(new GetLanguagePackString(_languagePath, "android", _languageCode, key));
            if (result is LanguagePackStringValuePluralized pluralized)
            {
                values[key + "Zero"] = GetValue(pluralized.ZeroValue);
                values[key + "One"] = GetValue(pluralized.OneValue);
                values[key + "Two"] = GetValue(pluralized.TwoValue);
                values[key + "Few"] = GetValue(pluralized.FewValue);
                values[key + "Many"] = GetValue(pluralized.ManyValue);
                values[key + "Other"] = GetValue(pluralized.OtherValue);

                switch (quantity)
                {
                    case QUANTITY_ZERO:
                        return GetValue(pluralized.ZeroValue);
                    case QUANTITY_ONE:
                        return GetValue(pluralized.OneValue);
                    case QUANTITY_TWO:
                        return GetValue(pluralized.TwoValue);
                    case QUANTITY_FEW:
                        return GetValue(pluralized.FewValue);
                    case QUANTITY_MANY:
                        return GetValue(pluralized.ManyValue);
                    default:
                        return GetValue(pluralized.OtherValue);
                }
            }

#if DEBUG
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

        private string GetValue(string value)
        {
            if (value.StartsWith("\"") && value.EndsWith("\""))
            {
                value = value.Trim('"');
            }

            value = value.Replace("%%", "%");
            value = value.Replace("%s", "{0}");

            return System.Text.RegularExpressions.Regex.Replace(value, "%([0-9]*?)\\$[ds]", match =>
            {
                var index = int.Parse(match.Groups[1].Value);
                return "{" + (index - 1) + "}";
            });
        }

        #region Handle

        public void Handle(UpdateLanguagePackStrings update)
        {
            var values = GetLanguagePack(update.LanguagePackId);

            foreach (var value in update.Strings)
            {
                switch (value.Value)
                {
                    case LanguagePackStringValueOrdinary ordinary:
                        values[value.Key] = GetValue(ordinary.Value);
                        break;
                    case LanguagePackStringValuePluralized pluralized:
                        values[value.Key + "Zero"] = GetValue(pluralized.ZeroValue);
                        values[value.Key + "One"] = GetValue(pluralized.OneValue);
                        values[value.Key + "Two"] = GetValue(pluralized.TwoValue);
                        values[value.Key + "Few"] = GetValue(pluralized.FewValue);
                        values[value.Key + "Many"] = GetValue(pluralized.ManyValue);
                        values[value.Key + "Other"] = GetValue(pluralized.OtherValue);
                        break;
                    case LanguagePackStringValueDeleted deleted:
                        values.Remove(value.Key);
                        break;
                }
            }
        }

        #endregion

        private Dictionary<string, string> GetLanguagePack(string key)
        {
            if (_languagePack.TryGetValue(key, out Dictionary<string, string> values))
            {
            }
            else
            {
                values = _languagePack[key] = new Dictionary<string, string>();
            }

            return values;
        }

        private string StringForQuantity(int quantity)
        {
            switch (quantity)
            {
                case QUANTITY_ZERO:
                    return "Zero";
                case QUANTITY_ONE:
                    return "One";
                case QUANTITY_TWO:
                    return "Two";
                case QUANTITY_FEW:
                    return "Few";
                case QUANTITY_MANY:
                    return "Many";
                default:
                    return "Other";
            }
        }
    }
}
