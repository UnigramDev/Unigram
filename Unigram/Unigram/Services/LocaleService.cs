using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Windows.Storage;

namespace Unigram.Services
{
    public interface ILocaleService
    {
        Task<bool> SetLanguageAsync(string code, bool refresh);

        string GetString(string key);
    }

    public class LocaleService : IHandle<UpdateOption>, IHandle<UpdateLanguagePackStrings>
    {
        private readonly IProtoService _protoService;
        private readonly IEventAggregator _aggregator;

        private readonly Dictionary<string, Dictionary<string, string>> _languagePack = new Dictionary<string, Dictionary<string, string>>();
        private string _languageCode;

        public LocaleService(IProtoService protoService, IEventAggregator aggregator)
        {
            _protoService = protoService;
            _aggregator = aggregator;

            _aggregator.Subscribe(this);
        }

        public async Task<bool> SetLanguageAsync(string code, bool refresh)
        {
            var response = await _protoService.SendAsync(new SetOption("language_code", new OptionValueString(code)));
            if (response is Ok && refresh)
            {
                _languageCode = code;

                response = await _protoService.SendAsync(new GetLanguagePackStrings(code, new string[0]));
                return response is LanguagePackStrings;
            }
            else
            {
                return response is Ok;
            }
        }

        public string GetString(string key)
        {
            var values = GetLanguagePack(key);
            if (values.TryGetValue(key, out string value))
            {
                return value;
            }

            var path = Path.Combine(ApplicationData.Current.LocalFolder.Path, $"{_protoService.SessionId}");
            var result = _protoService.Execute(new GetLanguagePackString(path, "android", _languageCode, key));
            if (result is LanguagePackStringValueOrdinary ordinary)
            {
                values[key] = ordinary.Value;
                return ordinary.Value;
            }

            return null;
        }

        #region Handle

        public void Handle(UpdateOption update)
        {
            if (update.Name.Equals("language_code") && update.Value is OptionValueString languageCode)
            {
                _languageCode = languageCode.Value;
            }
        }

        public void Handle(UpdateLanguagePackStrings update)
        {
            var values = GetLanguagePack(update.LanguagePackId);

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
                    case LanguagePackStringValueDeleted deleted:
                        values.Remove(value.Key);
                        break;
                }
            }
        }

        #endregion

        private Dictionary<string, string> GetLanguagePack(string key)
        {
            if (_languagePack.TryGetValue(_languageCode, out Dictionary<string, string> values))
            {
            }
            else
            {
                values = _languagePack[_languageCode] = new Dictionary<string, string>();
            }

            return values;
        }
    }
}
