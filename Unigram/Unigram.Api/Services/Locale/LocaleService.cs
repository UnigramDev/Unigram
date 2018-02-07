using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.TL;

namespace Telegram.Api.Services.Locale
{
    public class LocaleService
    {
        private readonly IMTProtoService _protoService;

        public LocaleService(IMTProtoService protoService)
        {
            _protoService = protoService;
        }

        public void applyRemoteLanguage(/*LocaleInfo localeInfo,*/ TLLangPackLanguage language, bool force)
        {
            //if (localeInfo == null && language == null || localeInfo != null && !localeInfo.isRemote())
            //{
            //    return;
            //}
            //if (localeInfo.version != 0 && !BuildVars.DEBUG_VERSION && !force)
            //{
            //    _protoService.GetDifferenceAsync(0, result =>
            //    {
            //        saveRemoteLocaleStrings(result);
            //    });
            //}
            //else
            {
                _protoService.GetLangPackAsync(language?.LangCode ?? "en", result =>
                {
                    saveRemoteLocaleStrings(result);
                });
            }
        }

        private string GetName(string value)
        {
            var split = value.Split('_');
            var result = string.Empty;

            foreach (var item in split)
            {
                result += item.Substring(0, 1).ToUpper();
                result += item.Substring(1);
            }

            return result;
        }

        private string GetValue(string value)
        {
            if (value.StartsWith("\"") && value.EndsWith("\""))
            {
                value = value.Trim('"');
            }

            return Regex.Replace(value, "%([0-9]*?)\\$[ds]", match =>
            {
                var index = int.Parse(match.Groups[1].Value);
                return "{" + (index - 1) + "}";
            });
        }

        public void saveRemoteLocaleStrings(TLLangPackDifference difference)
        {
            var fileName = FileUtils.GetFileName($"remote_{difference.LangCode}.xml");
            try
            {
                Dictionary<String, String> values;
                if (difference.FromVersion == 0)
                {
                    values = new Dictionary<string, string>();
                }
                else
                {
                    values = new Dictionary<string, string>();
                    //values = getLocaleFileStrings(finalFile, true);
                }
                for (int a = 0; a < difference.Strings.Count; a++)
                {
                    if (difference.Strings[a] is TLLangPackString single)
                    {
                        values[GetName(single.Key)] = GetValue(single.Value);
                    }
                    else if (difference.Strings[a] is TLLangPackStringPluralized pluralized)
                    {
                        values[GetName(pluralized.Key) + "Zero"] = GetValue(pluralized.ZeroValue ?? string.Empty);
                        values[GetName(pluralized.Key) + "One"] = GetValue(pluralized.OneValue ?? string.Empty);
                        values[GetName(pluralized.Key) + "Two"] = GetValue(pluralized.TwoValue ?? string.Empty);
                        values[GetName(pluralized.Key) + "Few"] = GetValue(pluralized.FewValue ?? string.Empty);
                        values[GetName(pluralized.Key) + "Many"] = GetValue(pluralized.ManyValue ?? string.Empty);
                        values[GetName(pluralized.Key) + "Other"] = GetValue(pluralized.OtherValue ?? string.Empty);
                    }
                    else if (difference.Strings[a] is TLLangPackStringDeleted deleted)
                    {
                        values.Remove(GetName(deleted.Key));
                    }
                }

                var writer = new StreamWriter(new FileStream(fileName, FileMode.OpenOrCreate));
                //writer.Write("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
                //writer.Write("<resources>\n");
                writer.Write("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
                writer.Write("<root>\n");
                writer.Write("  <xsd:schema id=\"root\" xmlns=\"\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:msdata=\"urn:schemas-microsoft-com:xml-msdata\">\n");
                writer.Write("    <xsd:element name=\"root\" msdata:IsDataSet=\"true\">\n");
                writer.Write("      <xsd:complexType>\n");
                writer.Write("        <xsd:choice maxOccurs=\"unbounded\">\n");
                writer.Write("          <xsd:element name=\"data\">\n");
                writer.Write("            <xsd:complexType>\n");
                writer.Write("              <xsd:sequence>\n");
                writer.Write("                <xsd:element name=\"value\" type=\"xsd:string\" minOccurs=\"0\" msdata:Ordinal=\"1\" />\n");
                writer.Write("                <xsd:element name=\"comment\" type=\"xsd:string\" minOccurs=\"0\" msdata:Ordinal=\"2\" />\n");
                writer.Write("              </xsd:sequence>\n");
                writer.Write("              <xsd:attribute name=\"name\" type=\"xsd:string\" msdata:Ordinal=\"1\" />\n");
                writer.Write("              <xsd:attribute name=\"type\" type=\"xsd:string\" msdata:Ordinal=\"3\" />\n");
                writer.Write("              <xsd:attribute name=\"mimetype\" type=\"xsd:string\" msdata:Ordinal=\"4\" />\n");
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
                writer.Write("    <value>1.3</value>\n");
                writer.Write("  </resheader>\n");
                writer.Write("  <resheader name=\"reader\">\n");
                writer.Write("    <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=2.0.3500.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>\n");
                writer.Write("  </resheader>\n");
                writer.Write("  <resheader name=\"writer\">\n");
                writer.Write("    <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=2.0.3500.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>\n");
                writer.Write("  </resheader>\n");

                foreach (var entry in values)
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

        //private Dictionary<String, String> getLocaleFileStrings(FileInfo file)
        //{
        //    return getLocaleFileStrings(file, false);
        //}

        //private Dictionary<String, String> getLocaleFileStrings(FileInfo file, bool preserveEscapes)
        //{
        //    FileInputStream stream = null;
        //    reloadLastFile = false;
        //    try
        //    {
        //        if (!file.exists())
        //        {
        //            return new HashMap<>();
        //        }
        //        HashMap<String, String> stringMap = new HashMap<>();
        //        XmlPullParser parser = Xml.newPullParser();
        //        stream = new FileInputStream(file);
        //        parser.setInput(stream, "UTF-8");
        //        int eventType = parser.getEventType();
        //        String name = null;
        //        String value = null;
        //        String attrName = null;
        //        while (eventType != XmlPullParser.END_DOCUMENT)
        //        {
        //            if (eventType == XmlPullParser.START_TAG)
        //            {
        //                name = parser.getName();
        //                int c = parser.getAttributeCount();
        //                if (c > 0)
        //                {
        //                    attrName = parser.getAttributeValue(0);
        //                }
        //            }
        //            else if (eventType == XmlPullParser.TEXT)
        //            {
        //                if (attrName != null)
        //                {
        //                    value = parser.getText();
        //                    if (value != null)
        //                    {
        //                        value = value.trim();
        //                        if (preserveEscapes)
        //                        {
        //                            value = value.replace("<", "&lt;").replace(">", "&gt;").replace("'", "\\'").replace("& ", "&amp; ");
        //                        }
        //                        else
        //                        {
        //                            value = value.replace("\\n", "\n");
        //                            value = value.replace("\\", "");
        //                            String old = value;
        //                            value = value.replace("&lt;", "<");
        //                            if (!reloadLastFile && !value.equals(old))
        //                            {
        //                                reloadLastFile = true;
        //                            }
        //                        }
        //                    }
        //                }
        //            }
        //            else if (eventType == XmlPullParser.END_TAG)
        //            {
        //                value = null;
        //                attrName = null;
        //                name = null;
        //            }
        //            if (name != null && name.equals("string") && value != null && attrName != null && value.length() != 0 && attrName.length() != 0)
        //            {
        //                stringMap.put(attrName, value);
        //                name = null;
        //                value = null;
        //                attrName = null;
        //            }
        //            eventType = parser.next();
        //        }
        //        return stringMap;
        //    }
        //    catch (Exception e)
        //    {
        //        FileLog.e(e);
        //        reloadLastFile = true;
        //    }
        //    finally
        //    {
        //        try
        //        {
        //            if (stream != null)
        //            {
        //                stream.close();
        //            }
        //        }
        //        catch (Exception e)
        //        {
        //            FileLog.e(e);
        //        }
        //    }
        //    return new HashMap<>();
        //}
    }
}
