//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Rg.DiffUtils;
using System.Collections.Generic;
using System.Linq;
using Windows.Globalization;

namespace Telegram.Entities
{
    public partial class Country
    {
        public Country(string code, string phoneCode, string name)
        {
            Code = code;
            PhoneCode = phoneCode;
            Name = name;
            DisplayName = GetDisplayName(code, name);

            if (code == "FT")
            {
                Emoji = "\U0001F3F4\u200D\u2620";
            }
            else
            {
                Emoji = char.ConvertFromUtf32(127462 + (code[0] - 'A'))
                    + char.ConvertFromUtf32(127462 + (code[1] - 'A'));
            }
        }

        public static string GetDisplayName(string code, string englishName)
        {
            if (GeographicRegion.IsSupported(code))
            {
                try
                {
                    return new GeographicRegion(code).DisplayName;
                }
                catch
                {
                    // All the remote procedure calls must be wrapped in a try-catch block
                }
            }

            return englishName;
        }

        public string Code { get; set; }

        public string PhoneCode { get; set; }

        public string Name { get; set; }

        public string DisplayName { get; set; }

        public string Emoji { get; set; }

        public override string ToString()
        {
            return DisplayName;
        }

        #region Static

        static Country()
        {
            var keyed = new Dictionary<string, Country>();
            var iso = new Dictionary<string, Country>();

            foreach (var country in All)
            {
                keyed[country.PhoneCode] = country;
                iso[country.Code] = country;
            }

            KeyedCountries = keyed;
            Codes = iso;
        }

        public static readonly Dictionary<string, Country> KeyedCountries;
        public static readonly Dictionary<string, Country> Codes;

        public static readonly IList<Country> All = new List<Country>
        {
            new("FT", "888", "Anonymous Numbers"),
            new("AD", "376", "Andorra"),
            new("AE", "971", "United Arab Emirates"),
            new("AF", "93", "Afghanistan"),
            new("AG", "1268", "Antigua & Barbuda"),
            new("AI", "1264", "Anguilla"),
            new("AL", "355", "Albania"),
            new("AM", "374", "Armenia"),
            new("AO", "244", "Angola"),
            new("AR", "54", "Argentina"),
            new("AS", "1684", "American Samoa"),
            new("AT", "43", "Austria"),
            new("AU", "61", "Australia"),
            new("AW", "297", "Aruba"),
            new("AZ", "994", "Azerbaijan"),
            new("BA", "387", "Bosnia & Herzegovina"),
            new("BB", "1246", "Barbados"),
            new("BD", "880", "Bangladesh"),
            new("BE", "32", "Belgium"),
            new("BF", "226", "Burkina Faso"),
            new("BG", "359", "Bulgaria"),
            new("BH", "973", "Bahrain"),
            new("BI", "257", "Burundi"),
            new("BJ", "229", "Benin"),
            new("BM", "1441", "Bermuda"),
            new("BN", "673", "Brunei Darussalam"),
            new("BO", "591", "Bolivia"),
            new("BQ", "599", "Bonaire, Sint Eustatius & Saba"),
            new("BR", "55", "Brazil"),
            new("BS", "1242", "Bahamas"),
            new("BT", "975", "Bhutan"),
            new("BW", "267", "Botswana"),
            new("BY", "375", "Belarus"),
            new("BZ", "501", "Belize"),
            new("CA", "1", "Canada"),
            new("CD", "243", "Congo (Dem. Rep.)"),
            new("CF", "236", "Central African Rep."),
            new("CG", "242", "Congo (Rep.)"),
            new("CH", "41", "Switzerland"),
            new("CI", "225", "Côte d`Ivoire"),
            new("CK", "682", "Cook Islands"),
            new("CL", "56", "Chile"),
            new("CM", "237", "Cameroon"),
            new("CN", "86", "China"),
            new("CO", "57", "Colombia"),
            new("CR", "506", "Costa Rica"),
            new("CU", "53", "Cuba"),
            new("CV", "238", "Cape Verde"),
            new("CW", "599", "Curaçao"),
            new("CY", "357", "Cyprus"),
            new("CZ", "420", "Czech Republic"),
            new("DE", "49", "Germany"),
            new("DJ", "253", "Djibouti"),
            new("DK", "45", "Denmark"),
            new("DM", "1767", "Dominica"),
            new("DO", "1", "Dominican Rep."),
            new("DZ", "213", "Algeria"),
            new("EC", "593", "Ecuador"),
            new("EE", "372", "Estonia"),
            new("EG", "20", "Egypt"),
            new("ER", "291", "Eritrea"),
            new("ES", "34", "Spain"),
            new("ET", "251", "Ethiopia"),
            new("FI", "358", "Finland"),
            new("FJ", "679", "Fiji"),
            new("FK", "500", "Falkland Islands"),
            new("FM", "691", "Micronesia"),
            new("FO", "298", "Faroe Islands"),
            new("FR", "33", "France"),
            new("GA", "241", "Gabon"),
            new("GB", "44", "United Kingdom"),
            new("GD", "1473", "Grenada"),
            new("GE", "995", "Georgia"),
            new("GF", "594", "French Guiana"),
            new("GH", "233", "Ghana"),
            new("GI", "350", "Gibraltar"),
            new("GL", "299", "Greenland"),
            new("GM", "220", "Gambia"),
            new("GN", "224", "Guinea"),
            new("GP", "590", "Guadeloupe"),
            new("GQ", "240", "Equatorial Guinea"),
            new("GR", "30", "Greece"),
            new("GT", "502", "Guatemala"),
            new("GU", "1671", "Guam"),
            new("GW", "245", "Guinea-Bissau"),
            new("GY", "592", "Guyana"),
            new("HK", "852", "Hong Kong"),
            new("HN", "504", "Honduras"),
            new("HR", "385", "Croatia"),
            new("HT", "509", "Haiti"),
            new("HU", "36", "Hungary"),
            new("ID", "62", "Indonesia"),
            new("IE", "353", "Ireland"),
            new("IL", "972", "Israel"),
            new("IN", "91", "India"),
            new("IO", "246", "Diego Garcia"),
            new("IQ", "964", "Iraq"),
            new("IR", "98", "Iran"),
            new("IS", "354", "Iceland"),
            new("IT", "39", "Italy"),
            new("JM", "1876", "Jamaica"),
            new("JO", "962", "Jordan"),
            new("JP", "81", "Japan"),
            new("KE", "254", "Kenya"),
            new("KG", "996", "Kyrgyzstan"),
            new("KH", "855", "Cambodia"),
            new("KI", "686", "Kiribati"),
            new("KM", "269", "Comoros"),
            new("KN", "1869", "Saint Kitts & Nevis"),
            new("KP", "850", "North Korea"),
            new("KR", "82", "South Korea"),
            new("KW", "965", "Kuwait"),
            new("KY", "1345", "Cayman Islands"),
            new("KZ", "7", "Kazakhstan"),
            new("LA", "856", "Laos"),
            new("LB", "961", "Lebanon"),
            new("LC", "1758", "Saint Lucia"),
            new("LI", "423", "Liechtenstein"),
            new("LK", "94", "Sri Lanka"),
            new("LR", "231", "Liberia"),
            new("LS", "266", "Lesotho"),
            new("LT", "370", "Lithuania"),
            new("LU", "352", "Luxembourg"),
            new("LV", "371", "Latvia"),
            new("LY", "218", "Libya"),
            new("MA", "212", "Morocco"),
            new("MC", "377", "Monaco"),
            new("MD", "373", "Moldova"),
            new("ME", "382", "Montenegro"),
            new("MG", "261", "Madagascar"),
            new("MH", "692", "Marshall Islands"),
            new("MK", "389", "Macedonia"),
            new("ML", "223", "Mali"),
            new("MM", "95", "Myanmar"),
            new("MN", "976", "Mongolia"),
            new("MO", "853", "Macau"),
            new("MP", "1670", "Northern Mariana Islands"),
            new("MQ", "596", "Martinique"),
            new("MR", "222", "Mauritania"),
            new("MS", "1664", "Montserrat"),
            new("MT", "356", "Malta"),
            new("MU", "230", "Mauritius"),
            new("MV", "960", "Maldives"),
            new("MW", "265", "Malawi"),
            new("MX", "52", "Mexico"),
            new("MY", "60", "Malaysia"),
            new("MZ", "258", "Mozambique"),
            new("NA", "264", "Namibia"),
            new("NC", "687", "New Caledonia"),
            new("NE", "227", "Niger"),
            new("NF", "672", "Norfolk Island"),
            new("NG", "234", "Nigeria"),
            new("NI", "505", "Nicaragua"),
            new("NL", "31", "Netherlands"),
            new("NO", "47", "Norway"),
            new("NP", "977", "Nepal"),
            new("NR", "674", "Nauru"),
            new("NU", "683", "Niue"),
            new("NZ", "64", "New Zealand"),
            new("OM", "968", "Oman"),
            new("PA", "507", "Panama"),
            new("PE", "51", "Peru"),
            new("PF", "689", "French Polynesia"),
            new("PG", "675", "Papua New Guinea"),
            new("PH", "63", "Philippines"),
            new("PK", "92", "Pakistan"),
            new("PL", "48", "Poland"),
            new("PM", "508", "Saint Pierre & Miquelon"),
            new("PR", "1", "Puerto Rico"),
            new("PS", "970", "Palestine"),
            new("PT", "351", "Portugal"),
            new("PW", "680", "Palau"),
            new("PY", "595", "Paraguay"),
            new("QA", "974", "Qatar"),
            new("RE", "262", "Réunion"),
            new("RO", "40", "Romania"),
            new("RS", "381", "Serbia"),
            new("RU", "7", "Russian Federation"),
            new("RW", "250", "Rwanda"),
            new("SA", "966", "Saudi Arabia"),
            new("SB", "677", "Solomon Islands"),
            new("SC", "248", "Seychelles"),
            new("SD", "249", "Sudan"),
            new("SE", "46", "Sweden"),
            new("SG", "65", "Singapore"),
            new("SH", "290", "Saint Helena"),
            new("SH", "247", "Saint Helena"),
            new("SI", "386", "Slovenia"),
            new("SK", "421", "Slovakia"),
            new("SL", "232", "Sierra Leone"),
            new("SM", "378", "San Marino"),
            new("SN", "221", "Senegal"),
            new("SO", "252", "Somalia"),
            new("SR", "597", "Suriname"),
            new("SS", "211", "South Sudan"),
            new("ST", "239", "São Tomé & Príncipe"),
            new("SV", "503", "El Salvador"),
            new("SX", "1721", "Sint Maarten"),
            new("SY", "963", "Syria"),
            new("SZ", "268", "Swaziland"),
            new("TC", "1649", "Turks & Caicos Islands"),
            new("TD", "235", "Chad"),
            new("TG", "228", "Togo"),
            new("TH", "66", "Thailand"),
            new("TJ", "992", "Tajikistan"),
            new("TK", "690", "Tokelau"),
            new("TL", "670", "Timor-Leste"),
            new("TM", "993", "Turkmenistan"),
            new("TN", "216", "Tunisia"),
            new("TO", "676", "Tonga"),
            new("TR", "90", "Turkey"),
            new("TT", "1868", "Trinidad & Tobago"),
            new("TV", "688", "Tuvalu"),
            new("TW", "886", "Taiwan"),
            new("TZ", "255", "Tanzania"),
            new("UA", "380", "Ukraine"),
            new("UG", "256", "Uganda"),
            new("US", "1", "USA"),
            new("UY", "598", "Uruguay"),
            new("UZ", "998", "Uzbekistan"),
            new("VC", "1784", "Saint Vincent & the Grenadines"),
            new("VE", "58", "Venezuela"),
            new("VG", "1284", "British Virgin Islands"),
            new("VI", "1340", "US Virgin Islands"),
            new("VN", "84", "Vietnam"),
            new("VU", "678", "Vanuatu"),
            new("WF", "681", "Wallis & Futuna"),
            new("WS", "685", "Samoa"),
            new("YE", "967", "Yemen"),
            new("YL", "42", "Y-land"),
            new("ZA", "27", "South Africa"),
            new("ZM", "260", "Zambia"),
            new("ZW", "263", "Zimbabwe")
        }.OrderBy(x => x.DisplayName).ToList();

        #endregion
    }

    public partial class CountryDiffHandler : IDiffHandler<Country>
    {
        public bool CompareItems(Country oldItem, Country newItem)
        {
            return oldItem.Code == newItem.Code;
        }

        public void UpdateItem(Country oldItem, Country newItem)
        {

        }
    }
}
