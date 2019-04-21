#pragma once

#include "pch.h"

using namespace Platform;

ref class LanguageEntry
{
internal:
	property String^ TwoLetterCode { String^ get() { return twoLetterCode; } }
	property String^ EnglishName { String^ get() { return englishName; } }

	LanguageEntry(String^ twoLetterCode, String^ englishName)
	{
		this->twoLetterCode = twoLetterCode;
		this->englishName = englishName;
	}

private:
	String^ twoLetterCode;
	String^ englishName;
};

ref class LanguageTagConverter
{
private:
	static std::map<String^, LanguageEntry^> map;

internal:
	static LanguageEntry^ TryGetLanguage(String^ languageTag)
	{
		auto result = map.find(languageTag);
		if (result != map.end())
		{
			return result->second;
		}

		return nullptr;
	}

	static void Initialize()
	{
		map["aar"] = ref new LanguageEntry("aa", "Afar");
		map["abk"] = ref new LanguageEntry("ab", "Abkhaz");
		map["afr"] = ref new LanguageEntry("af", "Afrikaans");
		map["aka"] = ref new LanguageEntry("ak", "Akan");
		map["alb"] = ref new LanguageEntry("sq", "Albanian");
		map["amh"] = ref new LanguageEntry("am", "Amharic");
		map["ara"] = ref new LanguageEntry("ar", "Arabic");
		map["arg"] = ref new LanguageEntry("an", "Aragonese");
		map["arm"] = ref new LanguageEntry("hy", "Armenian");
		map["asm"] = ref new LanguageEntry("as", "Assamese");
		map["ava"] = ref new LanguageEntry("av", "Avaric");
		map["ave"] = ref new LanguageEntry("ae", "Avestan");
		map["aym"] = ref new LanguageEntry("ay", "Aymara");
		map["aze"] = ref new LanguageEntry("az", "Azerbaijani");
		map["bak"] = ref new LanguageEntry("ba", "Bashkir");
		map["bam"] = ref new LanguageEntry("bm", "Bambara");
		map["baq"] = ref new LanguageEntry("eu", "Basque");
		map["bel"] = ref new LanguageEntry("be", "Belarusian");
		map["ben"] = ref new LanguageEntry("bn", "Bengali");
		map["bih"] = ref new LanguageEntry("bh", "Bihari");
		map["bis"] = ref new LanguageEntry("bi", "Bislama");
		map["bod"] = ref new LanguageEntry("bo", "Tibetan Standard");
		map["bos"] = ref new LanguageEntry("bs", "Bosnian");
		map["bre"] = ref new LanguageEntry("br", "Breton");
		map["bul"] = ref new LanguageEntry("bg", "Bulgarian");
		map["bur"] = ref new LanguageEntry("my", "Burmese");
		map["cat"] = ref new LanguageEntry("ca", "Catalan");
		map["ces"] = ref new LanguageEntry("cs", "Czech");
		map["cha"] = ref new LanguageEntry("ch", "Chamorro");
		map["che"] = ref new LanguageEntry("ce", "Chechen");
		map["chi"] = ref new LanguageEntry("zh", "Chinese");
		map["chu"] = ref new LanguageEntry("cu", "Old Church Slavonic");
		map["chv"] = ref new LanguageEntry("cv", "Chuvash");
		map["cor"] = ref new LanguageEntry("kw", "Cornish");
		map["cos"] = ref new LanguageEntry("co", "Corsican");
		map["cre"] = ref new LanguageEntry("cr", "Cree");
		map["cym"] = ref new LanguageEntry("cy", "Welsh");
		map["cze"] = ref new LanguageEntry("cs", "Czech");
		map["dan"] = ref new LanguageEntry("da", "Danish");
		map["deu"] = ref new LanguageEntry("de", "German");
		map["div"] = ref new LanguageEntry("dv", "Divehi");
		map["dut"] = ref new LanguageEntry("nl", "Dutch");
		map["dzo"] = ref new LanguageEntry("dz", "Dzongkha");
		map["ell"] = ref new LanguageEntry("el", "Greek");
		map["eng"] = ref new LanguageEntry("en", "English");
		map["epo"] = ref new LanguageEntry("eo", "Esperanto");
		map["est"] = ref new LanguageEntry("et", "Estonian");
		map["eus"] = ref new LanguageEntry("eu", "Basque");
		map["ewe"] = ref new LanguageEntry("ee", "Ewe");
		map["fao"] = ref new LanguageEntry("fo", "Faroese");
		map["fas"] = ref new LanguageEntry("fa", "Persian");
		map["fij"] = ref new LanguageEntry("fj", "Fijian");
		map["fin"] = ref new LanguageEntry("fi", "Finnish");
		map["fra"] = ref new LanguageEntry("fr", "French");
		map["fre"] = ref new LanguageEntry("fr", "French");
		map["fry"] = ref new LanguageEntry("fy", "Western Frisian");
		map["ful"] = ref new LanguageEntry("ff", "Fula");
		map["geo"] = ref new LanguageEntry("ka", "Georgian");
		map["ger"] = ref new LanguageEntry("de", "German");
		map["gla"] = ref new LanguageEntry("gd", "Gaelic");
		map["gle"] = ref new LanguageEntry("ga", "Irish");
		map["glg"] = ref new LanguageEntry("gl", "Galician");
		map["glv"] = ref new LanguageEntry("gv", "Manx");
		map["gre"] = ref new LanguageEntry("el", "Greek");
		map["grn"] = ref new LanguageEntry("gn", "Guaraní");
		map["guj"] = ref new LanguageEntry("gu", "Gujarati");
		map["hat"] = ref new LanguageEntry("ht", "Haitian");
		map["hau"] = ref new LanguageEntry("ha", "Hausa");
		map["heb"] = ref new LanguageEntry("he", "Hebrew");
		map["her"] = ref new LanguageEntry("hz", "Herero");
		map["hin"] = ref new LanguageEntry("hi", "Hindi");
		map["hmo"] = ref new LanguageEntry("ho", "Hiri Motu");
		map["hrv"] = ref new LanguageEntry("hr", "Croatian");
		map["hun"] = ref new LanguageEntry("hu", "Hungarian");
		map["hye"] = ref new LanguageEntry("hy", "Armenian");
		map["ibo"] = ref new LanguageEntry("ig", "Igbo");
		map["ice"] = ref new LanguageEntry("is", "Icelandic");
		map["ido"] = ref new LanguageEntry("io", "Ido");
		map["iii"] = ref new LanguageEntry("ii", "Nuosu");
		map["iku"] = ref new LanguageEntry("iu", "Inuktitut");
		map["ile"] = ref new LanguageEntry("ie", "Interlingue");
		map["ina"] = ref new LanguageEntry("ia", "Interlingua");
		map["ind"] = ref new LanguageEntry("id", "Indonesian");
		map["ipk"] = ref new LanguageEntry("ik", "Inupiaq");
		map["isl"] = ref new LanguageEntry("is", "Icelandic");
		map["ita"] = ref new LanguageEntry("it", "Italian");
		map["jav"] = ref new LanguageEntry("jv", "Javanese");
		map["jpn"] = ref new LanguageEntry("ja", "Japanese");
		map["kal"] = ref new LanguageEntry("kl", "Kalaallisut");
		map["kan"] = ref new LanguageEntry("kn", "Kannada");
		map["kas"] = ref new LanguageEntry("ks", "Kashmiri");
		map["kat"] = ref new LanguageEntry("ka", "Georgian");
		map["kau"] = ref new LanguageEntry("kr", "Kanuri");
		map["kaz"] = ref new LanguageEntry("kk", "Kazakh");
		map["khm"] = ref new LanguageEntry("km", "Khmer");
		map["kik"] = ref new LanguageEntry("ki", "Kikuyu");
		map["kin"] = ref new LanguageEntry("rw", "Kinyarwanda");
		map["kir"] = ref new LanguageEntry("ky", "Kyrgyz");
		map["kom"] = ref new LanguageEntry("kv", "Komi");
		map["kon"] = ref new LanguageEntry("kg", "Kongo");
		map["kor"] = ref new LanguageEntry("ko", "Korean");
		map["kua"] = ref new LanguageEntry("kj", "Kwanyama");
		map["kur"] = ref new LanguageEntry("ku", "Kurdish");
		map["lao"] = ref new LanguageEntry("lo", "Lao");
		map["lat"] = ref new LanguageEntry("la", "Latin");
		map["lav"] = ref new LanguageEntry("lv", "Latvian");
		map["lim"] = ref new LanguageEntry("li", "Limburgish");
		map["lin"] = ref new LanguageEntry("ln", "Lingala");
		map["lit"] = ref new LanguageEntry("lt", "Lithuanian");
		map["ltz"] = ref new LanguageEntry("lb", "Luxembourgish");
		map["lub"] = ref new LanguageEntry("lu", "Luba-Katanga");
		map["lug"] = ref new LanguageEntry("lg", "Ganda");
		map["mac"] = ref new LanguageEntry("mk", "Macedonian");
		map["mah"] = ref new LanguageEntry("mh", "Marshallese");
		map["mal"] = ref new LanguageEntry("ml", "Malayalam");
		map["mao"] = ref new LanguageEntry("mi", "Maori");
		map["mar"] = ref new LanguageEntry("mr", "Marathi");
		map["may"] = ref new LanguageEntry("ms", "Malay");
		map["mkd"] = ref new LanguageEntry("mk", "Macedonian");
		map["mlg"] = ref new LanguageEntry("mg", "Malagasy");
		map["mlt"] = ref new LanguageEntry("mt", "Maltese");
		map["mon"] = ref new LanguageEntry("mn", "Mongolian");
		map["mri"] = ref new LanguageEntry("mi", "Maori");
		map["msa"] = ref new LanguageEntry("ms", "Malay");
		map["mya"] = ref new LanguageEntry("my", "Burmese");
		map["nau"] = ref new LanguageEntry("na", "Nauru");
		map["nav"] = ref new LanguageEntry("nv", "Navajo");
		map["nbl"] = ref new LanguageEntry("nr", "Southern Ndebele");
		map["nde"] = ref new LanguageEntry("nd", "Northern Ndebele");
		map["ndo"] = ref new LanguageEntry("ng", "Ndonga");
		map["nep"] = ref new LanguageEntry("ne", "Nepali");
		map["nld"] = ref new LanguageEntry("nl", "Dutch");
		map["nno"] = ref new LanguageEntry("nn", "Norwegian Nynorsk");
		map["nob"] = ref new LanguageEntry("nb", "Norwegian Bokmål");
		map["nor"] = ref new LanguageEntry("no", "Norwegian");
		map["nya"] = ref new LanguageEntry("ny", "Chichewa");
		map["oci"] = ref new LanguageEntry("oc", "Occitan");
		map["oji"] = ref new LanguageEntry("oj", "Ojibwe");
		map["ori"] = ref new LanguageEntry("or", "Oriya");
		map["orm"] = ref new LanguageEntry("om", "Oromo");
		map["oss"] = ref new LanguageEntry("os", "Ossetian");
		map["pan"] = ref new LanguageEntry("pa", "Panjabi");
		map["per"] = ref new LanguageEntry("fa", "Persian");
		map["pli"] = ref new LanguageEntry("pi", "Pali");
		map["pol"] = ref new LanguageEntry("pl", "Polish");
		map["por"] = ref new LanguageEntry("pt", "Portuguese");
		map["pus"] = ref new LanguageEntry("ps", "Pashto");
		map["que"] = ref new LanguageEntry("qu", "Quechua");
		map["roh"] = ref new LanguageEntry("rm", "Romansh");
		map["ron"] = ref new LanguageEntry("ro", "Romanian");
		map["rum"] = ref new LanguageEntry("ro", "Romanian");
		map["run"] = ref new LanguageEntry("rn", "Kirundi");
		map["rus"] = ref new LanguageEntry("ru", "Russian");
		map["sag"] = ref new LanguageEntry("sg", "Sango");
		map["san"] = ref new LanguageEntry("sa", "Sanskrit");
		map["sin"] = ref new LanguageEntry("si", "Sinhala");
		map["slk"] = ref new LanguageEntry("sk", "Slovak");
		map["slo"] = ref new LanguageEntry("sk", "Slovak");
		map["slv"] = ref new LanguageEntry("sl", "Slovene");
		map["sme"] = ref new LanguageEntry("se", "Northern Sami");
		map["smo"] = ref new LanguageEntry("sm", "Samoan");
		map["sna"] = ref new LanguageEntry("sn", "Shona");
		map["snd"] = ref new LanguageEntry("sd", "Sindhi");
		map["som"] = ref new LanguageEntry("so", "Somali");
		map["sot"] = ref new LanguageEntry("st", "Southern Sotho");
		map["spa"] = ref new LanguageEntry("es", "Spanish");
		map["sqi"] = ref new LanguageEntry("sq", "Albanian");
		map["srd"] = ref new LanguageEntry("sc", "Sardinian");
		map["srp"] = ref new LanguageEntry("sr", "Serbian");
		map["ssw"] = ref new LanguageEntry("ss", "Swati");
		map["sun"] = ref new LanguageEntry("su", "Sundanese");
		map["swa"] = ref new LanguageEntry("sw", "Swahili");
		map["swe"] = ref new LanguageEntry("sv", "Swedish");
		map["tah"] = ref new LanguageEntry("ty", "Tahitian");
		map["tam"] = ref new LanguageEntry("ta", "Tamil");
		map["tat"] = ref new LanguageEntry("tt", "Tatar");
		map["tel"] = ref new LanguageEntry("te", "Telugu");
		map["tgk"] = ref new LanguageEntry("tg", "Tajik");
		map["tgl"] = ref new LanguageEntry("tl", "Tagalog");
		map["tha"] = ref new LanguageEntry("th", "Thai");
		map["tib"] = ref new LanguageEntry("bo", "Tibetan Standard");
		map["tir"] = ref new LanguageEntry("ti", "Tigrinya");
		map["ton"] = ref new LanguageEntry("to", "Tonga");
		map["tsn"] = ref new LanguageEntry("tn", "Tswana");
		map["tso"] = ref new LanguageEntry("ts", "Tsonga");
		map["tuk"] = ref new LanguageEntry("tk", "Turkmen");
		map["tur"] = ref new LanguageEntry("tr", "Turkish");
		map["twi"] = ref new LanguageEntry("tw", "Twi");
		map["uig"] = ref new LanguageEntry("ug", "Uyghur");
		map["ukr"] = ref new LanguageEntry("uk", "Ukrainian");
		map["urd"] = ref new LanguageEntry("ur", "Urdu");
		map["uzb"] = ref new LanguageEntry("uz", "Uzbek");
		map["ven"] = ref new LanguageEntry("ve", "Venda");
		map["vie"] = ref new LanguageEntry("vi", "Vietnamese");
		map["vol"] = ref new LanguageEntry("vo", "Volapük");
		map["wel"] = ref new LanguageEntry("cy", "Welsh");
		map["wln"] = ref new LanguageEntry("wa", "Walloon");
		map["wol"] = ref new LanguageEntry("wo", "Wolof");
		map["xho"] = ref new LanguageEntry("xh", "Xhosa");
		map["yid"] = ref new LanguageEntry("yi", "Yiddish");
		map["yor"] = ref new LanguageEntry("yo", "Yoruba");
		map["zha"] = ref new LanguageEntry("za", "Zhuang");
		map["zho"] = ref new LanguageEntry("zh", "Chinese");
		map["zul"] = ref new LanguageEntry("zu", "Zulu");
	}
};
