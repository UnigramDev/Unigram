using System.Collections.Generic;
using Unigram.Collections;
using Windows.Globalization;

namespace Unigram.Entities
{
    public class Country
    {
        public string Code { get; set; }

        public string PhoneCode { get; set; }

        public string Name { get; set; }

        public string DisplayName { get; set; }

        public string GetKey()
        {
            return Name.Substring(0, 1).ToLowerInvariant();
        }

        #region Static

        static Country()
        {
            foreach (Country c in Countries)
            {
                if (GeographicRegion.IsSupported(c.Code))
                {
                    c.DisplayName = new GeographicRegion(c.Code).DisplayName;
                }
                else
                {
                    c.DisplayName = c.Name;
                }
            }

            var alphabet = "abcdefghijklmnopqrstuvwxyz";
            var list = new List<KeyedList<string, Country>>(alphabet.Length);
            var dictionary = new Dictionary<string, KeyedList<string, Country>>();
            var keyed = new Dictionary<string, Country>();

            for (int i = 0; i < alphabet.Length; i++)
            {
                var key = alphabet[i].ToString();
                var group = new KeyedList<string, Country>(key);

                list.Add(group);
                dictionary[key] = group;
            }

            foreach (var country in Countries)
            {
                dictionary[country.GetKey()].Add(country);
                keyed[country.PhoneCode] = country;
            }

            GroupedCountries = list;
            KeyedCountries = keyed;
        }

        public static readonly List<KeyedList<string, Country>> GroupedCountries;

        public static readonly Dictionary<string, Country> KeyedCountries;

        public static readonly IList<Country> Countries = new List<Country>
        {
            new Country { Code = "AD", PhoneCode = "376", Name = "Andorra" },
            new Country { Code = "AE", PhoneCode = "971", Name = "United Arab Emirates" },
            new Country { Code = "AF", PhoneCode = "93", Name = "Afghanistan" },
            new Country { Code = "AG", PhoneCode = "1268", Name = "Antigua & Barbuda" },
            new Country { Code = "AI", PhoneCode = "1264", Name = "Anguilla" },
            new Country { Code = "AL", PhoneCode = "355", Name = "Albania" },
            new Country { Code = "AM", PhoneCode = "374", Name = "Armenia" },
            new Country { Code = "AO", PhoneCode = "244", Name = "Angola" },
            new Country { Code = "AR", PhoneCode = "54", Name = "Argentina" },
            new Country { Code = "AS", PhoneCode = "1684", Name = "American Samoa" },
            new Country { Code = "AT", PhoneCode = "43", Name = "Austria" },
            new Country { Code = "AU", PhoneCode = "61", Name = "Australia" },
            new Country { Code = "AW", PhoneCode = "297", Name = "Aruba" },
            new Country { Code = "AZ", PhoneCode = "994", Name = "Azerbaijan" },
            new Country { Code = "BA", PhoneCode = "387", Name = "Bosnia & Herzegovina" },
            new Country { Code = "BB", PhoneCode = "1246", Name = "Barbados" },
            new Country { Code = "BD", PhoneCode = "880", Name = "Bangladesh" },
            new Country { Code = "BE", PhoneCode = "32", Name = "Belgium" },
            new Country { Code = "BF", PhoneCode = "226", Name = "Burkina Faso" },
            new Country { Code = "BG", PhoneCode = "359", Name = "Bulgaria" },
            new Country { Code = "BH", PhoneCode = "973", Name = "Bahrain" },
            new Country { Code = "BI", PhoneCode = "257", Name = "Burundi" },
            new Country { Code = "BJ", PhoneCode = "229", Name = "Benin" },
            new Country { Code = "BM", PhoneCode = "1441", Name = "Bermuda" },
            new Country { Code = "BN", PhoneCode = "673", Name = "Brunei Darussalam" },
            new Country { Code = "BO", PhoneCode = "591", Name = "Bolivia" },
            new Country { Code = "BQ", PhoneCode = "599", Name = "Bonaire, Sint Eustatius & Saba" },
            new Country { Code = "BR", PhoneCode = "55", Name = "Brazil" },
            new Country { Code = "BS", PhoneCode = "1242", Name = "Bahamas" },
            new Country { Code = "BT", PhoneCode = "975", Name = "Bhutan" },
            new Country { Code = "BW", PhoneCode = "267", Name = "Botswana" },
            new Country { Code = "BY", PhoneCode = "375", Name = "Belarus" },
            new Country { Code = "BZ", PhoneCode = "501", Name = "Belize" },
            new Country { Code = "CA", PhoneCode = "1", Name = "Canada" },
            new Country { Code = "CD", PhoneCode = "243", Name = "Congo (Dem. Rep.)" },
            new Country { Code = "CF", PhoneCode = "236", Name = "Central African Rep." },
            new Country { Code = "CG", PhoneCode = "242", Name = "Congo (Rep.)" },
            new Country { Code = "CH", PhoneCode = "41", Name = "Switzerland" },
            new Country { Code = "CI", PhoneCode = "225", Name = "Côte d`Ivoire" },
            new Country { Code = "CK", PhoneCode = "682", Name = "Cook Islands" },
            new Country { Code = "CL", PhoneCode = "56", Name = "Chile" },
            new Country { Code = "CM", PhoneCode = "237", Name = "Cameroon" },
            new Country { Code = "CN", PhoneCode = "86", Name = "China" },
            new Country { Code = "CO", PhoneCode = "57", Name = "Colombia" },
            new Country { Code = "CR", PhoneCode = "506", Name = "Costa Rica" },
            new Country { Code = "CU", PhoneCode = "53", Name = "Cuba" },
            new Country { Code = "CV", PhoneCode = "238", Name = "Cape Verde" },
            new Country { Code = "CW", PhoneCode = "599", Name = "Curaçao" },
            new Country { Code = "CY", PhoneCode = "357", Name = "Cyprus" },
            new Country { Code = "CZ", PhoneCode = "420", Name = "Czech Republic" },
            new Country { Code = "DE", PhoneCode = "49", Name = "Germany" },
            new Country { Code = "DJ", PhoneCode = "253", Name = "Djibouti" },
            new Country { Code = "DK", PhoneCode = "45", Name = "Denmark" },
            new Country { Code = "DM", PhoneCode = "1767", Name = "Dominica" },
            new Country { Code = "DO", PhoneCode = "1", Name = "Dominican Rep." },
            new Country { Code = "DZ", PhoneCode = "213", Name = "Algeria" },
            new Country { Code = "EC", PhoneCode = "593", Name = "Ecuador" },
            new Country { Code = "EE", PhoneCode = "372", Name = "Estonia" },
            new Country { Code = "EG", PhoneCode = "20", Name = "Egypt" },
            new Country { Code = "ER", PhoneCode = "291", Name = "Eritrea" },
            new Country { Code = "ES", PhoneCode = "34", Name = "Spain" },
            new Country { Code = "ET", PhoneCode = "251", Name = "Ethiopia" },
            new Country { Code = "FI", PhoneCode = "358", Name = "Finland" },
            new Country { Code = "FJ", PhoneCode = "679", Name = "Fiji" },
            new Country { Code = "FK", PhoneCode = "500", Name = "Falkland Islands" },
            new Country { Code = "FM", PhoneCode = "691", Name = "Micronesia" },
            new Country { Code = "FO", PhoneCode = "298", Name = "Faroe Islands" },
            new Country { Code = "FR", PhoneCode = "33", Name = "France" },
            new Country { Code = "GA", PhoneCode = "241", Name = "Gabon" },
            new Country { Code = "GB", PhoneCode = "44", Name = "United Kingdom" },
            new Country { Code = "GD", PhoneCode = "1473", Name = "Grenada" },
            new Country { Code = "GE", PhoneCode = "995", Name = "Georgia" },
            new Country { Code = "GF", PhoneCode = "594", Name = "French Guiana" },
            new Country { Code = "GH", PhoneCode = "233", Name = "Ghana" },
            new Country { Code = "GI", PhoneCode = "350", Name = "Gibraltar" },
            new Country { Code = "GL", PhoneCode = "299", Name = "Greenland" },
            new Country { Code = "GM", PhoneCode = "220", Name = "Gambia" },
            new Country { Code = "GN", PhoneCode = "224", Name = "Guinea" },
            new Country { Code = "GP", PhoneCode = "590", Name = "Guadeloupe" },
            new Country { Code = "GQ", PhoneCode = "240", Name = "Equatorial Guinea" },
            new Country { Code = "GR", PhoneCode = "30", Name = "Greece" },
            new Country { Code = "GT", PhoneCode = "502", Name = "Guatemala" },
            new Country { Code = "GU", PhoneCode = "1671", Name = "Guam" },
            new Country { Code = "GW", PhoneCode = "245", Name = "Guinea-Bissau" },
            new Country { Code = "GY", PhoneCode = "592", Name = "Guyana" },
            new Country { Code = "HK", PhoneCode = "852", Name = "Hong Kong" },
            new Country { Code = "HN", PhoneCode = "504", Name = "Honduras" },
            new Country { Code = "HR", PhoneCode = "385", Name = "Croatia" },
            new Country { Code = "HT", PhoneCode = "509", Name = "Haiti" },
            new Country { Code = "HU", PhoneCode = "36", Name = "Hungary" },
            new Country { Code = "ID", PhoneCode = "62", Name = "Indonesia" },
            new Country { Code = "IE", PhoneCode = "353", Name = "Ireland" },
            new Country { Code = "IL", PhoneCode = "972", Name = "Israel" },
            new Country { Code = "IN", PhoneCode = "91", Name = "India" },
            new Country { Code = "IO", PhoneCode = "246", Name = "Diego Garcia" },
            new Country { Code = "IQ", PhoneCode = "964", Name = "Iraq" },
            new Country { Code = "IR", PhoneCode = "98", Name = "Iran" },
            new Country { Code = "IS", PhoneCode = "354", Name = "Iceland" },
            new Country { Code = "IT", PhoneCode = "39", Name = "Italy" },
            new Country { Code = "JM", PhoneCode = "1876", Name = "Jamaica" },
            new Country { Code = "JO", PhoneCode = "962", Name = "Jordan" },
            new Country { Code = "JP", PhoneCode = "81", Name = "Japan" },
            new Country { Code = "KE", PhoneCode = "254", Name = "Kenya" },
            new Country { Code = "KG", PhoneCode = "996", Name = "Kyrgyzstan" },
            new Country { Code = "KH", PhoneCode = "855", Name = "Cambodia" },
            new Country { Code = "KI", PhoneCode = "686", Name = "Kiribati" },
            new Country { Code = "KM", PhoneCode = "269", Name = "Comoros" },
            new Country { Code = "KN", PhoneCode = "1869", Name = "Saint Kitts & Nevis" },
            new Country { Code = "KP", PhoneCode = "850", Name = "North Korea" },
            new Country { Code = "KR", PhoneCode = "82", Name = "South Korea" },
            new Country { Code = "KW", PhoneCode = "965", Name = "Kuwait" },
            new Country { Code = "KY", PhoneCode = "1345", Name = "Cayman Islands" },
            new Country { Code = "KZ", PhoneCode = "7", Name = "Kazakhstan" },
            new Country { Code = "LA", PhoneCode = "856", Name = "Laos" },
            new Country { Code = "LB", PhoneCode = "961", Name = "Lebanon" },
            new Country { Code = "LC", PhoneCode = "1758", Name = "Saint Lucia" },
            new Country { Code = "LI", PhoneCode = "423", Name = "Liechtenstein" },
            new Country { Code = "LK", PhoneCode = "94", Name = "Sri Lanka" },
            new Country { Code = "LR", PhoneCode = "231", Name = "Liberia" },
            new Country { Code = "LS", PhoneCode = "266", Name = "Lesotho" },
            new Country { Code = "LT", PhoneCode = "370", Name = "Lithuania" },
            new Country { Code = "LU", PhoneCode = "352", Name = "Luxembourg" },
            new Country { Code = "LV", PhoneCode = "371", Name = "Latvia" },
            new Country { Code = "LY", PhoneCode = "218", Name = "Libya" },
            new Country { Code = "MA", PhoneCode = "212", Name = "Morocco" },
            new Country { Code = "MC", PhoneCode = "377", Name = "Monaco" },
            new Country { Code = "MD", PhoneCode = "373", Name = "Moldova" },
            new Country { Code = "ME", PhoneCode = "382", Name = "Montenegro" },
            new Country { Code = "MG", PhoneCode = "261", Name = "Madagascar" },
            new Country { Code = "MH", PhoneCode = "692", Name = "Marshall Islands" },
            new Country { Code = "MK", PhoneCode = "389", Name = "Macedonia" },
            new Country { Code = "ML", PhoneCode = "223", Name = "Mali" },
            new Country { Code = "MM", PhoneCode = "95", Name = "Myanmar" },
            new Country { Code = "MN", PhoneCode = "976", Name = "Mongolia" },
            new Country { Code = "MO", PhoneCode = "853", Name = "Macau" },
            new Country { Code = "MP", PhoneCode = "1670", Name = "Northern Mariana Islands" },
            new Country { Code = "MQ", PhoneCode = "596", Name = "Martinique" },
            new Country { Code = "MR", PhoneCode = "222", Name = "Mauritania" },
            new Country { Code = "MS", PhoneCode = "1664", Name = "Montserrat" },
            new Country { Code = "MT", PhoneCode = "356", Name = "Malta" },
            new Country { Code = "MU", PhoneCode = "230", Name = "Mauritius" },
            new Country { Code = "MV", PhoneCode = "960", Name = "Maldives" },
            new Country { Code = "MW", PhoneCode = "265", Name = "Malawi" },
            new Country { Code = "MX", PhoneCode = "52", Name = "Mexico" },
            new Country { Code = "MY", PhoneCode = "60", Name = "Malaysia" },
            new Country { Code = "MZ", PhoneCode = "258", Name = "Mozambique" },
            new Country { Code = "NA", PhoneCode = "264", Name = "Namibia" },
            new Country { Code = "NC", PhoneCode = "687", Name = "New Caledonia" },
            new Country { Code = "NE", PhoneCode = "227", Name = "Niger" },
            new Country { Code = "NF", PhoneCode = "672", Name = "Norfolk Island" },
            new Country { Code = "NG", PhoneCode = "234", Name = "Nigeria" },
            new Country { Code = "NI", PhoneCode = "505", Name = "Nicaragua" },
            new Country { Code = "NL", PhoneCode = "31", Name = "Netherlands" },
            new Country { Code = "NO", PhoneCode = "47", Name = "Norway" },
            new Country { Code = "NP", PhoneCode = "977", Name = "Nepal" },
            new Country { Code = "NR", PhoneCode = "674", Name = "Nauru" },
            new Country { Code = "NU", PhoneCode = "683", Name = "Niue" },
            new Country { Code = "NZ", PhoneCode = "64", Name = "New Zealand" },
            new Country { Code = "OM", PhoneCode = "968", Name = "Oman" },
            new Country { Code = "PA", PhoneCode = "507", Name = "Panama" },
            new Country { Code = "PE", PhoneCode = "51", Name = "Peru" },
            new Country { Code = "PF", PhoneCode = "689", Name = "French Polynesia" },
            new Country { Code = "PG", PhoneCode = "675", Name = "Papua New Guinea" },
            new Country { Code = "PH", PhoneCode = "63", Name = "Philippines" },
            new Country { Code = "PK", PhoneCode = "92", Name = "Pakistan" },
            new Country { Code = "PL", PhoneCode = "48", Name = "Poland" },
            new Country { Code = "PM", PhoneCode = "508", Name = "Saint Pierre & Miquelon" },
            new Country { Code = "PR", PhoneCode = "1", Name = "Puerto Rico" },
            new Country { Code = "PS", PhoneCode = "970", Name = "Palestine" },
            new Country { Code = "PT", PhoneCode = "351", Name = "Portugal" },
            new Country { Code = "PW", PhoneCode = "680", Name = "Palau" },
            new Country { Code = "PY", PhoneCode = "595", Name = "Paraguay" },
            new Country { Code = "QA", PhoneCode = "974", Name = "Qatar" },
            new Country { Code = "RE", PhoneCode = "262", Name = "Réunion" },
            new Country { Code = "RO", PhoneCode = "40", Name = "Romania" },
            new Country { Code = "RS", PhoneCode = "381", Name = "Serbia" },
            new Country { Code = "RU", PhoneCode = "7", Name = "Russian Federation" },
            new Country { Code = "RW", PhoneCode = "250", Name = "Rwanda" },
            new Country { Code = "SA", PhoneCode = "966", Name = "Saudi Arabia" },
            new Country { Code = "SB", PhoneCode = "677", Name = "Solomon Islands" },
            new Country { Code = "SC", PhoneCode = "248", Name = "Seychelles" },
            new Country { Code = "SD", PhoneCode = "249", Name = "Sudan" },
            new Country { Code = "SE", PhoneCode = "46", Name = "Sweden" },
            new Country { Code = "SG", PhoneCode = "65", Name = "Singapore" },
            new Country { Code = "SH", PhoneCode = "290", Name = "Saint Helena" },
            new Country { Code = "SH", PhoneCode = "247", Name = "Saint Helena" },
            new Country { Code = "SI", PhoneCode = "386", Name = "Slovenia" },
            new Country { Code = "SK", PhoneCode = "421", Name = "Slovakia" },
            new Country { Code = "SL", PhoneCode = "232", Name = "Sierra Leone" },
            new Country { Code = "SM", PhoneCode = "378", Name = "San Marino" },
            new Country { Code = "SN", PhoneCode = "221", Name = "Senegal" },
            new Country { Code = "SO", PhoneCode = "252", Name = "Somalia" },
            new Country { Code = "SR", PhoneCode = "597", Name = "Suriname" },
            new Country { Code = "SS", PhoneCode = "211", Name = "South Sudan" },
            new Country { Code = "ST", PhoneCode = "239", Name = "São Tomé & Príncipe" },
            new Country { Code = "SV", PhoneCode = "503", Name = "El Salvador" },
            new Country { Code = "SX", PhoneCode = "1721", Name = "Sint Maarten" },
            new Country { Code = "SY", PhoneCode = "963", Name = "Syria" },
            new Country { Code = "SZ", PhoneCode = "268", Name = "Swaziland" },
            new Country { Code = "TC", PhoneCode = "1649", Name = "Turks & Caicos Islands" },
            new Country { Code = "TD", PhoneCode = "235", Name = "Chad" },
            new Country { Code = "TG", PhoneCode = "228", Name = "Togo" },
            new Country { Code = "TH", PhoneCode = "66", Name = "Thailand" },
            new Country { Code = "TJ", PhoneCode = "992", Name = "Tajikistan" },
            new Country { Code = "TK", PhoneCode = "690", Name = "Tokelau" },
            new Country { Code = "TL", PhoneCode = "670", Name = "Timor-Leste" },
            new Country { Code = "TM", PhoneCode = "993", Name = "Turkmenistan" },
            new Country { Code = "TN", PhoneCode = "216", Name = "Tunisia" },
            new Country { Code = "TO", PhoneCode = "676", Name = "Tonga" },
            new Country { Code = "TR", PhoneCode = "90", Name = "Turkey" },
            new Country { Code = "TT", PhoneCode = "1868", Name = "Trinidad & Tobago" },
            new Country { Code = "TV", PhoneCode = "688", Name = "Tuvalu" },
            new Country { Code = "TW", PhoneCode = "886", Name = "Taiwan" },
            new Country { Code = "TZ", PhoneCode = "255", Name = "Tanzania" },
            new Country { Code = "UA", PhoneCode = "380", Name = "Ukraine" },
            new Country { Code = "UG", PhoneCode = "256", Name = "Uganda" },
            new Country { Code = "US", PhoneCode = "1", Name = "USA" },
            new Country { Code = "UY", PhoneCode = "598", Name = "Uruguay" },
            new Country { Code = "UZ", PhoneCode = "998", Name = "Uzbekistan" },
            new Country { Code = "VC", PhoneCode = "1784", Name = "Saint Vincent & the Grenadines" },
            new Country { Code = "VE", PhoneCode = "58", Name = "Venezuela" },
            new Country { Code = "VG", PhoneCode = "1284", Name = "British Virgin Islands" },
            new Country { Code = "VI", PhoneCode = "1340", Name = "US Virgin Islands" },
            new Country { Code = "VN", PhoneCode = "84", Name = "Vietnam" },
            new Country { Code = "VU", PhoneCode = "678", Name = "Vanuatu" },
            new Country { Code = "WF", PhoneCode = "681", Name = "Wallis & Futuna" },
            new Country { Code = "WS", PhoneCode = "685", Name = "Samoa" },
            new Country { Code = "YE", PhoneCode = "967", Name = "Yemen" },
            new Country { Code = "YL", PhoneCode = "42", Name = "Y-land" },
            new Country { Code = "ZA", PhoneCode = "27", Name = "South Africa" },
            new Country { Code = "ZM", PhoneCode = "260", Name = "Zambia" },
            new Country { Code = "ZW", PhoneCode = "263", Name = "Zimbabwe" }
        };

        #endregion
    }
}
