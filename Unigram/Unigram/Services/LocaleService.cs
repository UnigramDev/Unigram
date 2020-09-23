using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        string Language { get; }

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
        public static ILocaleService Current => _current ??= new LocaleService();

        public string Language => _languageCode;

        public async Task<BaseObject> SetLanguageAsync(LanguagePackInfo info, bool refresh)
        {
            _languageCode = info.Id;
            _languagePlural = info.PluralCode;

            SettingsService.Current.LanguagePackId = info.Id;
            SettingsService.Current.LanguagePluralId = info.PluralCode;

            Locale.SetRules(info.PluralCode);

            foreach (var protoService in TLContainer.Current.ResolveAll<IProtoService>())
            {
#if DEBUG
                await protoService.SendAsync(new SynchronizeLanguagePack(info.Id));

                var test = await protoService.SendAsync(new GetLanguagePackStrings(info.Id, new string[0]));
                if (test is LanguagePackStrings strings)
                {
                    saveRemoteLocaleStrings(info.Id, strings);
                }

                return new Error();
#endif

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

        public void saveRemoteLocaleStrings(string lang, LanguagePackStrings difference)
        {
            string GetName(string value)
            {
                return value;

                var split = value.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
                var result = string.Empty;

                foreach (var item in split)
                {
                    result += item.Substring(0, 1).ToUpper();
                    result += item.Substring(1);
                }

                return result;
            }

            var fileName = Path.Combine(ApplicationData.Current.LocalFolder.Path, "test", lang, "Resources.resw");
            try
            {
                var values = new Dictionary<string, string>();
                var already = new List<string>();
                for (int a = 0; a < difference.Strings.Count; a++)
                {
                    if (difference.Strings[a].Value is LanguagePackStringValueOrdinary single)
                    {
                        if (difference.Strings[a].Key == difference.Strings[a].Key.ToLower())
                        {
                            continue;
                        }
                        //else if (difference.Strings[a].Key == difference.Strings[a].Key.ToUpper())
                        //{
                        //    continue;
                        //}
                        else if (difference.Strings[a].Key.StartsWith('_'))
                        {
                            continue;
                        }

                        if (already.Contains(GetName(difference.Strings[a].Key).ToLower()))
                        {
                            continue;
                        }

                        values[GetName(difference.Strings[a].Key)] = GetValue(single.Value);
                        already.Add(GetName(difference.Strings[a].Key).ToLower());
                    }
                    else if (difference.Strings[a].Value is LanguagePackStringValuePluralized pluralized)
                    {
                        values[GetName(difference.Strings[a].Key) + "Zero"] = GetValue(pluralized.ZeroValue ?? string.Empty);
                        values[GetName(difference.Strings[a].Key) + "One"] = GetValue(pluralized.OneValue ?? string.Empty);
                        values[GetName(difference.Strings[a].Key) + "Two"] = GetValue(pluralized.TwoValue ?? string.Empty);
                        values[GetName(difference.Strings[a].Key) + "Few"] = GetValue(pluralized.FewValue ?? string.Empty);
                        values[GetName(difference.Strings[a].Key) + "Many"] = GetValue(pluralized.ManyValue ?? string.Empty);
                        values[GetName(difference.Strings[a].Key) + "Other"] = GetValue(pluralized.OtherValue ?? string.Empty);
                    }
                    else if (difference.Strings[a].Value is LanguagePackStringValueDeleted deleted)
                    {
                        values.Remove(GetName(difference.Strings[a].Key));
                    }
                }

                Directory.CreateDirectory(Path.GetDirectoryName(fileName));
                System.IO.File.Delete(fileName);

                var writer = new StreamWriter(new FileStream(fileName, FileMode.OpenOrCreate));
                //writer.Write("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
                //writer.Write("<resources>\n");
                writer.Write("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
                writer.Write("<root>\n");
                writer.Write("  <xsd:schema id=\"root\" xmlns=\"\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:msdata=\"urn:schemas-microsoft-com:xml-msdata\">\n");
                writer.Write("    <xsd:import namespace=\"http://www.w3.org/XML/1998/namespace\" />\n");
                writer.Write("    <xsd:element name=\"root\" msdata:IsDataSet=\"true\">\n");
                writer.Write("      <xsd:complexType>\n");
                writer.Write("        <xsd:choice maxOccurs=\"unbounded\">\n");
                writer.Write("          <xsd:element name=\"metadata\">\n");
                writer.Write("            <xsd:complexType>\n");
                writer.Write("              <xsd:sequence>\n");
                writer.Write("                <xsd:element name=\"value\" type=\"xsd:string\" minOccurs=\"0\" />\n");
                writer.Write("              </xsd:sequence>\n");
                writer.Write("              <xsd:attribute name=\"name\" use=\"required\" type=\"xsd:string\" />\n");
                writer.Write("              <xsd:attribute name=\"type\" type=\"xsd:string\" />\n");
                writer.Write("              <xsd:attribute name=\"mimetype\" type=\"xsd:string\" />\n");
                writer.Write("              <xsd:attribute ref=\"xml:space\" />\n");
                writer.Write("            </xsd:complexType>\n");
                writer.Write("          </xsd:element>\n");
                writer.Write("          <xsd:element name=\"assembly\">\n");
                writer.Write("            <xsd:complexType>\n");
                writer.Write("              <xsd:attribute name=\"alias\" type=\"xsd:string\" />\n");
                writer.Write("              <xsd:attribute name=\"name\" type=\"xsd:string\" />\n");
                writer.Write("            </xsd:complexType>\n");
                writer.Write("          </xsd:element>\n");
                writer.Write("          <xsd:element name=\"data\">\n");
                writer.Write("            <xsd:complexType>\n");
                writer.Write("              <xsd:sequence>\n");
                writer.Write("                <xsd:element name=\"value\" type=\"xsd:string\" minOccurs=\"0\" msdata:Ordinal=\"1\" />\n");
                writer.Write("                <xsd:element name=\"comment\" type=\"xsd:string\" minOccurs=\"0\" msdata:Ordinal=\"2\" />\n");
                writer.Write("              </xsd:sequence>\n");
                writer.Write("              <xsd:attribute name=\"name\" type=\"xsd:string\" use=\"required\" msdata:Ordinal=\"1\" />\n");
                writer.Write("              <xsd:attribute name=\"type\" type=\"xsd:string\" msdata:Ordinal=\"3\" />\n");
                writer.Write("              <xsd:attribute name=\"mimetype\" type=\"xsd:string\" msdata:Ordinal=\"4\" />\n");
                writer.Write("              <xsd:attribute ref=\"xml:space\" />\n");
                writer.Write("            </xsd:complexType>\n");
                writer.Write("          </xsd:element>\n");
                writer.Write("          <xsd:element name=\"resheader\">\n");
                writer.Write("            <xsd:complexType>\n");
                writer.Write("              <xsd:sequence>\n");
                writer.Write("                <xsd:element name=\"value\" type=\"xsd:string\" minOccurs=\"0\" msdata:Ordinal=\"1\" />\n");
                writer.Write("              </xsd:sequence>\n");
                writer.Write("              <xsd:attribute name=\"name\" type=\"xsd:string\" use=\"required\" />\n");
                writer.Write("            </xsd:complexType>\n");
                writer.Write("          </xsd:element>\n");
                writer.Write("        </xsd:choice>\n");
                writer.Write("      </xsd:complexType>\n");
                writer.Write("    </xsd:element>\n");
                writer.Write("  </xsd:schema>\n");
                writer.Write("  <resheader name=\"resmimetype\">\n");
                writer.Write("    <value>text/microsoft-resx</value>\n");
                writer.Write("  </resheader>\n");
                writer.Write("  <resheader name=\"version\">\n");
                writer.Write("    <value>2.0</value>\n");
                writer.Write("  </resheader>\n");
                writer.Write("  <resheader name=\"reader\">\n");
                writer.Write("    <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>\n");
                writer.Write("  </resheader>\n");
                writer.Write("  <resheader name=\"writer\">\n");
                writer.Write("    <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>\n");
                writer.Write("  </resheader>\n");


                foreach (var entry in values.OrderBy(x => x.Key))
                {
                    //writer.Write($"<string name=\"{entry.Key}\">{entry.Value}</string>\n");
                    writer.Write($"  <data name=\"{entry.Key}\" xml:space=\"preserve\">\n");
                    if (string.IsNullOrEmpty(entry.Value))
                    {
                        writer.Write($"    <value/>\n");
                    }
                    else
                    {
                        writer.Write($"    <value>{entry.Value.Replace("&", "&amp;")}</value>\n");
                    }
                    writer.Write($"  </data>\n");
                }

                writer.Write("</root>");
                //writer.Write("</resources>");
                writer.Dispose();

                //var valuesToSet = getLocaleFileStrings(finalFile);
                //{
                //    LocaleInfo localeInfo = getLanguageFromDict(difference.lang_code);
                //    if (localeInfo != null)
                //    {
                //        localeInfo.version = difference.version;
                //    }
                //    saveOtherLanguages();
                //    if (currentLocaleInfo != null && currentLocaleInfo.isLocal())
                //    {
                //        return;
                //    }
                //    try
                //    {
                //        Locale newLocale;
                //        String[] args = localeInfo.shortName.split("_");
                //        if (args.length == 1)
                //        {
                //            newLocale = new Locale(localeInfo.shortName);
                //        }
                //        else
                //        {
                //            newLocale = new Locale(args[0], args[1]);
                //        }
                //        if (newLocale != null)
                //        {
                //            languageOverride = localeInfo.shortName;

                //            SharedPreferences preferences = ApplicationLoader.applicationContext.getSharedPreferences("mainconfig", Activity.MODE_PRIVATE);
                //            SharedPreferences.Editor editor = preferences.edit();
                //            editor.putString("language", localeInfo.getKey());
                //            editor.commit();
                //        }
                //        if (newLocale != null)
                //        {
                //            localeValues = valuesToSet;
                //            currentLocale = newLocale;
                //            currentLocaleInfo = localeInfo;
                //            currentPluralRules = allRules.get(currentLocale.getLanguage());
                //            if (currentPluralRules == null)
                //            {
                //                currentPluralRules = allRules.get("en");
                //            }
                //            changingConfiguration = true;
                //            Locale.setDefault(currentLocale);
                //            android.content.res.Configuration config = new android.content.res.Configuration();
                //            config.locale = currentLocale;
                //            ApplicationLoader.applicationContext.getResources().updateConfiguration(config, ApplicationLoader.applicationContext.getResources().getDisplayMetrics());
                //            changingConfiguration = false;
                //        }
                //    }
                //    catch (Exception e)
                //    {
                //        FileLog.e(e);
                //        changingConfiguration = false;
                //    }
                //    recreateFormatters();
                //    NotificationCenter.getInstance().postNotificationName(NotificationCenter.reloadInterface);
                //}
            }
            catch (Exception ignore)
            {

            }
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
