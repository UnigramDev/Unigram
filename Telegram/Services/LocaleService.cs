//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Text.RegularExpressions;
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
    public interface ILocaleService : IHandle<UpdateLanguagePackStrings>
    {
        Task<BaseObject> SetLanguageAsync(LanguagePackInfo info, bool refresh);

        CultureInfo CurrentCulture { get; }

        FlowDirection FlowDirection { get; }

        string GetString(string key);
        string GetString(string key, int quantity);
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

            foreach (var clientService in TLContainer.Current.ResolveAll<IClientService>())
            {
#if DEBUG
                await clientService.SendAsync(new SynchronizeLanguagePack(info.Id));

                var test = await clientService.SendAsync(new GetLanguagePackStrings(info.Id, new string[0]));
                if (test is LanguagePackStrings strings)
                {
                    SaveRemoteLocaleStrings(info.Id, strings);
                }

                return new Error();
#endif

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

#if DEBUG
        private void SaveRemoteLocaleStrings(string lang, LanguagePackStrings difference)
        {
            var fileName = Path.Combine(ApplicationData.Current.LocalFolder.Path, "test", $"Strings.{lang}.resw");
            try
            {
                var stringFormat = new Regex("%([0-9]*?)\\$[ds]", RegexOptions.Compiled);

                string GetValue(string value)
                {
                    if (value.StartsWith("\"") && value.EndsWith("\""))
                    {
                        value = value.Trim('"');
                    }

                    value = value.Replace("%%", "%");
                    value = value.Replace("%s", "{0}");
                    value = value.Replace("%d", "{0}");

                    return stringFormat.Replace(value, match =>
                    {
                        var index = int.Parse(match.Groups[1].Value);
                        return $"{{{index - 1}}}";
                    });
                }

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

                        values[difference.Strings[a].Key] = GetValue(single.Value);
                    }
                    else if (difference.Strings[a].Value is LanguagePackStringValuePluralized pluralized)
                    {
                        values[difference.Strings[a].Key + "_zero"] = GetValue(pluralized.ZeroValue ?? string.Empty);
                        values[difference.Strings[a].Key + "_one"] = GetValue(pluralized.OneValue ?? string.Empty);
                        values[difference.Strings[a].Key + "_two"] = GetValue(pluralized.TwoValue ?? string.Empty);
                        values[difference.Strings[a].Key + "_few"] = GetValue(pluralized.FewValue ?? string.Empty);
                        values[difference.Strings[a].Key + "_many"] = GetValue(pluralized.ManyValue ?? string.Empty);
                        values[difference.Strings[a].Key + "_other"] = GetValue(pluralized.OtherValue ?? string.Empty);
                    }
                    else if (difference.Strings[a].Value is LanguagePackStringValueDeleted deleted)
                    {
                        values.Remove(difference.Strings[a].Key);
                    }
                }

                Directory.CreateDirectory(Path.GetDirectoryName(fileName));
                System.IO.File.Delete(fileName);

                var writer = new StreamWriter(new FileStream(fileName, FileMode.OpenOrCreate));
                //writer.Write("<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n");
                //writer.Write("<resources>\r\n");
                writer.Write("<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n");
                writer.Write("<root>\r\n");
                writer.Write("  <xsd:schema id=\"root\" xmlns=\"\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:msdata=\"urn:schemas-microsoft-com:xml-msdata\">\r\n");
                writer.Write("    <xsd:import namespace=\"http://www.w3.org/XML/1998/namespace\" />\r\n");
                writer.Write("    <xsd:element name=\"root\" msdata:IsDataSet=\"true\">\r\n");
                writer.Write("      <xsd:complexType>\r\n");
                writer.Write("        <xsd:choice maxOccurs=\"unbounded\">\r\n");
                writer.Write("          <xsd:element name=\"metadata\">\r\n");
                writer.Write("            <xsd:complexType>\r\n");
                writer.Write("              <xsd:sequence>\r\n");
                writer.Write("                <xsd:element name=\"value\" type=\"xsd:string\" minOccurs=\"0\" />\r\n");
                writer.Write("              </xsd:sequence>\r\n");
                writer.Write("              <xsd:attribute name=\"name\" use=\"required\" type=\"xsd:string\" />\r\n");
                writer.Write("              <xsd:attribute name=\"type\" type=\"xsd:string\" />\r\n");
                writer.Write("              <xsd:attribute name=\"mimetype\" type=\"xsd:string\" />\r\n");
                writer.Write("              <xsd:attribute ref=\"xml:space\" />\r\n");
                writer.Write("            </xsd:complexType>\r\n");
                writer.Write("          </xsd:element>\r\n");
                writer.Write("          <xsd:element name=\"assembly\">\r\n");
                writer.Write("            <xsd:complexType>\r\n");
                writer.Write("              <xsd:attribute name=\"alias\" type=\"xsd:string\" />\r\n");
                writer.Write("              <xsd:attribute name=\"name\" type=\"xsd:string\" />\r\n");
                writer.Write("            </xsd:complexType>\r\n");
                writer.Write("          </xsd:element>\r\n");
                writer.Write("          <xsd:element name=\"data\">\r\n");
                writer.Write("            <xsd:complexType>\r\n");
                writer.Write("              <xsd:sequence>\r\n");
                writer.Write("                <xsd:element name=\"value\" type=\"xsd:string\" minOccurs=\"0\" msdata:Ordinal=\"1\" />\r\n");
                writer.Write("                <xsd:element name=\"comment\" type=\"xsd:string\" minOccurs=\"0\" msdata:Ordinal=\"2\" />\r\n");
                writer.Write("              </xsd:sequence>\r\n");
                writer.Write("              <xsd:attribute name=\"name\" type=\"xsd:string\" use=\"required\" msdata:Ordinal=\"1\" />\r\n");
                writer.Write("              <xsd:attribute name=\"type\" type=\"xsd:string\" msdata:Ordinal=\"3\" />\r\n");
                writer.Write("              <xsd:attribute name=\"mimetype\" type=\"xsd:string\" msdata:Ordinal=\"4\" />\r\n");
                writer.Write("              <xsd:attribute ref=\"xml:space\" />\r\n");
                writer.Write("            </xsd:complexType>\r\n");
                writer.Write("          </xsd:element>\r\n");
                writer.Write("          <xsd:element name=\"resheader\">\r\n");
                writer.Write("            <xsd:complexType>\r\n");
                writer.Write("              <xsd:sequence>\r\n");
                writer.Write("                <xsd:element name=\"value\" type=\"xsd:string\" minOccurs=\"0\" msdata:Ordinal=\"1\" />\r\n");
                writer.Write("              </xsd:sequence>\r\n");
                writer.Write("              <xsd:attribute name=\"name\" type=\"xsd:string\" use=\"required\" />\r\n");
                writer.Write("            </xsd:complexType>\r\n");
                writer.Write("          </xsd:element>\r\n");
                writer.Write("        </xsd:choice>\r\n");
                writer.Write("      </xsd:complexType>\r\n");
                writer.Write("    </xsd:element>\r\n");
                writer.Write("  </xsd:schema>\r\n");
                writer.Write("  <resheader name=\"resmimetype\">\r\n");
                writer.Write("    <value>text/microsoft-resx</value>\r\n");
                writer.Write("  </resheader>\r\n");
                writer.Write("  <resheader name=\"version\">\r\n");
                writer.Write("    <value>2.0</value>\r\n");
                writer.Write("  </resheader>\r\n");
                writer.Write("  <resheader name=\"reader\">\r\n");
                writer.Write("    <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>\r\n");
                writer.Write("  </resheader>\r\n");
                writer.Write("  <resheader name=\"writer\">\r\n");
                writer.Write("    <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>\r\n");
                writer.Write("  </resheader>\r\n");


                foreach (var entry in values.OrderBy(x => x.Key))
                {
                    if (string.IsNullOrEmpty(entry.Value) || already.Contains(entry.Key.ToLower()))
                    {
                        continue;
                    }

                    already.Add(entry.Key.ToLower());

                    //writer.Write($"<string name=\"{entry.Key}\">{entry.Value}</string>\n");
                    writer.Write($"  <data name=\"{entry.Key}\" xml:space=\"preserve\">\r\n");
                    if (string.IsNullOrEmpty(entry.Value))
                    {
                        writer.Write($"    <value/>\r\n");
                    }
                    else
                    {
                        writer.Write($"    <value>{SecurityElement.Escape(entry.Value.Replace("\n", "\r\n"))}</value>\r\n");
                    }
                    writer.Write($"  </data>\r\n");
                }

                writer.Write("</root>");
                //writer.Write("</resources>");
                writer.Dispose();
            }
            catch (Exception ignore)
            {

            }
        }
#endif


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
