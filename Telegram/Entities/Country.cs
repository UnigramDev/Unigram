//
// Copyright Fela Ameghino & Contributors 2015-2025
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

            if (GeographicRegion.IsSupported(code))
            {
                DisplayName = new GeographicRegion(code).DisplayName;
            }
            else
            {
                DisplayName = name;
            }

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
            new Country("FT", "888", "Anonymous Numbers"),
            new Country("AD", "376", "Andorra"),
            new Country("AE", "971", "United Arab Emirates"),
            new Country("AF", "93", "Afghanistan"),
            new Country("AG", "1268", "Antigua & Barbuda"),
            new Country("AI", "1264", "Anguilla"),
            new Country("AL", "355", "Albania"),
            new Country("AM", "374", "Armenia"),
            new Country("AO", "244", "Angola"),
            new Country("AR", "54", "Argentina"),
            new Country("AS", "1684", "American Samoa"),
            new Country("AT", "43", "Austria"),
            new Country("AU", "61", "Australia"),
            new Country("AW", "297", "Aruba"),
            new Country("AZ", "994", "Azerbaijan"),
            new Country("BA", "387", "Bosnia & Herzegovina"),
            new Country("BB", "1246", "Barbados"),
            new Country("BD", "880", "Bangladesh"),
            new Country("BE", "32", "Belgium"),
            new Country("BF", "226", "Burkina Faso"),
            new Country("BG", "359", "Bulgaria"),
            new Country("BH", "973", "Bahrain"),
            new Country("BI", "257", "Burundi"),
            new Country("BJ", "229", "Benin"),
            new Country("BM", "1441", "Bermuda"),
            new Country("BN", "673", "Brunei Darussalam"),
            new Country("BO", "591", "Bolivia"),
            new Country("BQ", "599", "Bonaire, Sint Eustatius & Saba"),
            new Country("BR", "55", "Brazil"),
            new Country("BS", "1242", "Bahamas"),
            new Country("BT", "975", "Bhutan"),
            new Country("BW", "267", "Botswana"),
            new Country("BY", "375", "Belarus"),
            new Country("BZ", "501", "Belize"),
            new Country("CA", "1", "Canada"),
            new Country("CD", "243", "Congo (Dem. Rep.)"),
            new Country("CF", "236", "Central African Rep."),
            new Country("CG", "242", "Congo (Rep.)"),
            new Country("CH", "41", "Switzerland"),
            new Country("CI", "225", "Côte d`Ivoire"),
            new Country("CK", "682", "Cook Islands"),
            new Country("CL", "56", "Chile"),
            new Country("CM", "237", "Cameroon"),
            new Country("CN", "86", "China"),
            new Country("CO", "57", "Colombia"),
            new Country("CR", "506", "Costa Rica"),
            new Country("CU", "53", "Cuba"),
            new Country("CV", "238", "Cape Verde"),
            new Country("CW", "599", "Curaçao"),
            new Country("CY", "357", "Cyprus"),
            new Country("CZ", "420", "Czech Republic"),
            new Country("DE", "49", "Germany"),
            new Country("DJ", "253", "Djibouti"),
            new Country("DK", "45", "Denmark"),
            new Country("DM", "1767", "Dominica"),
            new Country("DO", "1", "Dominican Rep."),
            new Country("DZ", "213", "Algeria"),
            new Country("EC", "593", "Ecuador"),
            new Country("EE", "372", "Estonia"),
            new Country("EG", "20", "Egypt"),
            new Country("ER", "291", "Eritrea"),
            new Country("ES", "34", "Spain"),
            new Country("ET", "251", "Ethiopia"),
            new Country("FI", "358", "Finland"),
            new Country("FJ", "679", "Fiji"),
            new Country("FK", "500", "Falkland Islands"),
            new Country("FM", "691", "Micronesia"),
            new Country("FO", "298", "Faroe Islands"),
            new Country("FR", "33", "France"),
            new Country("GA", "241", "Gabon"),
            new Country("GB", "44", "United Kingdom"),
            new Country("GD", "1473", "Grenada"),
            new Country("GE", "995", "Georgia"),
            new Country("GF", "594", "French Guiana"),
            new Country("GH", "233", "Ghana"),
            new Country("GI", "350", "Gibraltar"),
            new Country("GL", "299", "Greenland"),
            new Country("GM", "220", "Gambia"),
            new Country("GN", "224", "Guinea"),
            new Country("GP", "590", "Guadeloupe"),
            new Country("GQ", "240", "Equatorial Guinea"),
            new Country("GR", "30", "Greece"),
            new Country("GT", "502", "Guatemala"),
            new Country("GU", "1671", "Guam"),
            new Country("GW", "245", "Guinea-Bissau"),
            new Country("GY", "592", "Guyana"),
            new Country("HK", "852", "Hong Kong"),
            new Country("HN", "504", "Honduras"),
            new Country("HR", "385", "Croatia"),
            new Country("HT", "509", "Haiti"),
            new Country("HU", "36", "Hungary"),
            new Country("ID", "62", "Indonesia"),
            new Country("IE", "353", "Ireland"),
            new Country("IL", "972", "Israel"),
            new Country("IN", "91", "India"),
            new Country("IO", "246", "Diego Garcia"),
            new Country("IQ", "964", "Iraq"),
            new Country("IR", "98", "Iran"),
            new Country("IS", "354", "Iceland"),
            new Country("IT", "39", "Italy"),
            new Country("JM", "1876", "Jamaica"),
            new Country("JO", "962", "Jordan"),
            new Country("JP", "81", "Japan"),
            new Country("KE", "254", "Kenya"),
            new Country("KG", "996", "Kyrgyzstan"),
            new Country("KH", "855", "Cambodia"),
            new Country("KI", "686", "Kiribati"),
            new Country("KM", "269", "Comoros"),
            new Country("KN", "1869", "Saint Kitts & Nevis"),
            new Country("KP", "850", "North Korea"),
            new Country("KR", "82", "South Korea"),
            new Country("KW", "965", "Kuwait"),
            new Country("KY", "1345", "Cayman Islands"),
            new Country("KZ", "7", "Kazakhstan"),
            new Country("LA", "856", "Laos"),
            new Country("LB", "961", "Lebanon"),
            new Country("LC", "1758", "Saint Lucia"),
            new Country("LI", "423", "Liechtenstein"),
            new Country("LK", "94", "Sri Lanka"),
            new Country("LR", "231", "Liberia"),
            new Country("LS", "266", "Lesotho"),
            new Country("LT", "370", "Lithuania"),
            new Country("LU", "352", "Luxembourg"),
            new Country("LV", "371", "Latvia"),
            new Country("LY", "218", "Libya"),
            new Country("MA", "212", "Morocco"),
            new Country("MC", "377", "Monaco"),
            new Country("MD", "373", "Moldova"),
            new Country("ME", "382", "Montenegro"),
            new Country("MG", "261", "Madagascar"),
            new Country("MH", "692", "Marshall Islands"),
            new Country("MK", "389", "Macedonia"),
            new Country("ML", "223", "Mali"),
            new Country("MM", "95", "Myanmar"),
            new Country("MN", "976", "Mongolia"),
            new Country("MO", "853", "Macau"),
            new Country("MP", "1670", "Northern Mariana Islands"),
            new Country("MQ", "596", "Martinique"),
            new Country("MR", "222", "Mauritania"),
            new Country("MS", "1664", "Montserrat"),
            new Country("MT", "356", "Malta"),
            new Country("MU", "230", "Mauritius"),
            new Country("MV", "960", "Maldives"),
            new Country("MW", "265", "Malawi"),
            new Country("MX", "52", "Mexico"),
            new Country("MY", "60", "Malaysia"),
            new Country("MZ", "258", "Mozambique"),
            new Country("NA", "264", "Namibia"),
            new Country("NC", "687", "New Caledonia"),
            new Country("NE", "227", "Niger"),
            new Country("NF", "672", "Norfolk Island"),
            new Country("NG", "234", "Nigeria"),
            new Country("NI", "505", "Nicaragua"),
            new Country("NL", "31", "Netherlands"),
            new Country("NO", "47", "Norway"),
            new Country("NP", "977", "Nepal"),
            new Country("NR", "674", "Nauru"),
            new Country("NU", "683", "Niue"),
            new Country("NZ", "64", "New Zealand"),
            new Country("OM", "968", "Oman"),
            new Country("PA", "507", "Panama"),
            new Country("PE", "51", "Peru"),
            new Country("PF", "689", "French Polynesia"),
            new Country("PG", "675", "Papua New Guinea"),
            new Country("PH", "63", "Philippines"),
            new Country("PK", "92", "Pakistan"),
            new Country("PL", "48", "Poland"),
            new Country("PM", "508", "Saint Pierre & Miquelon"),
            new Country("PR", "1", "Puerto Rico"),
            new Country("PS", "970", "Palestine"),
            new Country("PT", "351", "Portugal"),
            new Country("PW", "680", "Palau"),
            new Country("PY", "595", "Paraguay"),
            new Country("QA", "974", "Qatar"),
            new Country("RE", "262", "Réunion"),
            new Country("RO", "40", "Romania"),
            new Country("RS", "381", "Serbia"),
            new Country("RU", "7", "Russian Federation"),
            new Country("RW", "250", "Rwanda"),
            new Country("SA", "966", "Saudi Arabia"),
            new Country("SB", "677", "Solomon Islands"),
            new Country("SC", "248", "Seychelles"),
            new Country("SD", "249", "Sudan"),
            new Country("SE", "46", "Sweden"),
            new Country("SG", "65", "Singapore"),
            new Country("SH", "290", "Saint Helena"),
            new Country("SH", "247", "Saint Helena"),
            new Country("SI", "386", "Slovenia"),
            new Country("SK", "421", "Slovakia"),
            new Country("SL", "232", "Sierra Leone"),
            new Country("SM", "378", "San Marino"),
            new Country("SN", "221", "Senegal"),
            new Country("SO", "252", "Somalia"),
            new Country("SR", "597", "Suriname"),
            new Country("SS", "211", "South Sudan"),
            new Country("ST", "239", "São Tomé & Príncipe"),
            new Country("SV", "503", "El Salvador"),
            new Country("SX", "1721", "Sint Maarten"),
            new Country("SY", "963", "Syria"),
            new Country("SZ", "268", "Swaziland"),
            new Country("TC", "1649", "Turks & Caicos Islands"),
            new Country("TD", "235", "Chad"),
            new Country("TG", "228", "Togo"),
            new Country("TH", "66", "Thailand"),
            new Country("TJ", "992", "Tajikistan"),
            new Country("TK", "690", "Tokelau"),
            new Country("TL", "670", "Timor-Leste"),
            new Country("TM", "993", "Turkmenistan"),
            new Country("TN", "216", "Tunisia"),
            new Country("TO", "676", "Tonga"),
            new Country("TR", "90", "Turkey"),
            new Country("TT", "1868", "Trinidad & Tobago"),
            new Country("TV", "688", "Tuvalu"),
            new Country("TW", "886", "Taiwan"),
            new Country("TZ", "255", "Tanzania"),
            new Country("UA", "380", "Ukraine"),
            new Country("UG", "256", "Uganda"),
            new Country("US", "1", "USA"),
            new Country("UY", "598", "Uruguay"),
            new Country("UZ", "998", "Uzbekistan"),
            new Country("VC", "1784", "Saint Vincent & the Grenadines"),
            new Country("VE", "58", "Venezuela"),
            new Country("VG", "1284", "British Virgin Islands"),
            new Country("VI", "1340", "US Virgin Islands"),
            new Country("VN", "84", "Vietnam"),
            new Country("VU", "678", "Vanuatu"),
            new Country("WF", "681", "Wallis & Futuna"),
            new Country("WS", "685", "Samoa"),
            new Country("YE", "967", "Yemen"),
            new Country("YL", "42", "Y-land"),
            new Country("ZA", "27", "South Africa"),
            new Country("ZM", "260", "Zambia"),
            new Country("ZW", "263", "Zimbabwe")
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
