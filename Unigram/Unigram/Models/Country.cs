using System;
using System.Collections.Generic;
using Windows.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Common;

namespace Unigram.Core.Models
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
                try
                {
                    c.DisplayName = new GeographicRegion(c.Code.ToUpper()).DisplayName;
                }
                catch { }

                if (string.IsNullOrWhiteSpace(c.DisplayName))
                {
                    c.DisplayName = c.Name;
                }
            }

            var alphabet = "abcdefghijklmnopqrstuvwxyz";
            var list = new List<KeyedList<string, Country>>(alphabet.Length);
            var dictionary = new Dictionary<string, KeyedList<string, Country>>();
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
            }

            GroupedCountries = list;
        }

        public static readonly List<KeyedList<string, Country>> GroupedCountries;

        public static readonly IList<Country> Countries = new List<Country>
        {
            new Country { Code = "ad", PhoneCode = "376", Name = "Andorra" },
            new Country { Code = "ae", PhoneCode = "971", Name = "United Arab Emirates" },
            new Country { Code = "af", PhoneCode = "93", Name = "Afghanistan" },
            new Country { Code = "ag", PhoneCode = "1268", Name = "Antigua & Barbuda" },
            new Country { Code = "ai", PhoneCode = "1264", Name = "Anguilla" },
            new Country { Code = "al", PhoneCode = "355", Name = "Albania" },
            new Country { Code = "am", PhoneCode = "374", Name = "Armenia" },
            new Country { Code = "ao", PhoneCode = "244", Name = "Angola" },
            new Country { Code = "ar", PhoneCode = "54", Name = "Argentina" },
            new Country { Code = "as", PhoneCode = "1684", Name = "American Samoa" },
            new Country { Code = "at", PhoneCode = "43", Name = "Austria" },
            new Country { Code = "au", PhoneCode = "61", Name = "Australia" },
            new Country { Code = "aw", PhoneCode = "297", Name = "Aruba" },
            new Country { Code = "az", PhoneCode = "994", Name = "Azerbaijan" },
            new Country { Code = "ba", PhoneCode = "387", Name = "Bosnia & Herzegovina" },
            new Country { Code = "bb", PhoneCode = "1246", Name = "Barbados" },
            new Country { Code = "bd", PhoneCode = "880", Name = "Bangladesh" },
            new Country { Code = "be", PhoneCode = "32", Name = "Belgium" },
            new Country { Code = "bf", PhoneCode = "226", Name = "Burkina Faso" },
            new Country { Code = "bg", PhoneCode = "359", Name = "Bulgaria" },
            new Country { Code = "bh", PhoneCode = "973", Name = "Bahrain" },
            new Country { Code = "bi", PhoneCode = "257", Name = "Burundi" },
            new Country { Code = "bj", PhoneCode = "229", Name = "Benin" },
            new Country { Code = "bm", PhoneCode = "1441", Name = "Bermuda" },
            new Country { Code = "bn", PhoneCode = "673", Name = "Brunei Darussalam" },
            new Country { Code = "bo", PhoneCode = "591", Name = "Bolivia" },
            new Country { Code = "bq", PhoneCode = "599", Name = "Bonaire, Sint Eustatius & Saba" },
            new Country { Code = "br", PhoneCode = "55", Name = "Brazil" },
            new Country { Code = "bs", PhoneCode = "1242", Name = "Bahamas" },
            new Country { Code = "bt", PhoneCode = "975", Name = "Bhutan" },
            new Country { Code = "bw", PhoneCode = "267", Name = "Botswana" },
            new Country { Code = "by", PhoneCode = "375", Name = "Belarus" },
            new Country { Code = "bz", PhoneCode = "501", Name = "Belize" },
            new Country { Code = "ca", PhoneCode = "1", Name = "Canada" },
            new Country { Code = "cd", PhoneCode = "243", Name = "Congo (Dem. Rep.)" },
            new Country { Code = "cf", PhoneCode = "236", Name = "Central African Rep." },
            new Country { Code = "cg", PhoneCode = "242", Name = "Congo (Rep.)" },
            new Country { Code = "ch", PhoneCode = "41", Name = "Switzerland" },
            new Country { Code = "ci", PhoneCode = "225", Name = "Côte d`Ivoire" },
            new Country { Code = "ck", PhoneCode = "682", Name = "Cook Islands" },
            new Country { Code = "cl", PhoneCode = "56", Name = "Chile" },
            new Country { Code = "cm", PhoneCode = "237", Name = "Cameroon" },
            new Country { Code = "cn", PhoneCode = "86", Name = "China" },
            new Country { Code = "co", PhoneCode = "57", Name = "Colombia" },
            new Country { Code = "cr", PhoneCode = "506", Name = "Costa Rica" },
            new Country { Code = "cu", PhoneCode = "53", Name = "Cuba" },
            new Country { Code = "cv", PhoneCode = "238", Name = "Cape Verde" },
            new Country { Code = "cw", PhoneCode = "599", Name = "Curaçao" },
            new Country { Code = "cy", PhoneCode = "357", Name = "Cyprus" },
            new Country { Code = "cz", PhoneCode = "420", Name = "Czech Republic" },
            new Country { Code = "de", PhoneCode = "49", Name = "Germany" },
            new Country { Code = "dj", PhoneCode = "253", Name = "Djibouti" },
            new Country { Code = "dk", PhoneCode = "45", Name = "Denmark" },
            new Country { Code = "dm", PhoneCode = "1767", Name = "Dominica" },
            new Country { Code = "do", PhoneCode = "1", Name = "Dominican Rep." },
            new Country { Code = "dz", PhoneCode = "213", Name = "Algeria" },
            new Country { Code = "ec", PhoneCode = "593", Name = "Ecuador" },
            new Country { Code = "ee", PhoneCode = "372", Name = "Estonia" },
            new Country { Code = "eg", PhoneCode = "20", Name = "Egypt" },
            new Country { Code = "er", PhoneCode = "291", Name = "Eritrea" },
            new Country { Code = "es", PhoneCode = "34", Name = "Spain" },
            new Country { Code = "et", PhoneCode = "251", Name = "Ethiopia" },
            new Country { Code = "fi", PhoneCode = "358", Name = "Finland" },
            new Country { Code = "fj", PhoneCode = "679", Name = "Fiji" },
            new Country { Code = "fk", PhoneCode = "500", Name = "Falkland Islands" },
            new Country { Code = "fm", PhoneCode = "691", Name = "Micronesia" },
            new Country { Code = "fo", PhoneCode = "298", Name = "Faroe Islands" },
            new Country { Code = "fr", PhoneCode = "33", Name = "France" },
            new Country { Code = "ga", PhoneCode = "241", Name = "Gabon" },
            new Country { Code = "gb", PhoneCode = "44", Name = "United Kingdom" },
            new Country { Code = "gd", PhoneCode = "1473", Name = "Grenada" },
            new Country { Code = "ge", PhoneCode = "995", Name = "Georgia" },
            new Country { Code = "gf", PhoneCode = "594", Name = "French Guiana" },
            new Country { Code = "gh", PhoneCode = "233", Name = "Ghana" },
            new Country { Code = "gi", PhoneCode = "350", Name = "Gibraltar" },
            new Country { Code = "gl", PhoneCode = "299", Name = "Greenland" },
            new Country { Code = "gm", PhoneCode = "220", Name = "Gambia" },
            new Country { Code = "gn", PhoneCode = "224", Name = "Guinea" },
            new Country { Code = "gp", PhoneCode = "590", Name = "Guadeloupe" },
            new Country { Code = "gq", PhoneCode = "240", Name = "Equatorial Guinea" },
            new Country { Code = "gr", PhoneCode = "30", Name = "Greece" },
            new Country { Code = "gt", PhoneCode = "502", Name = "Guatemala" },
            new Country { Code = "gu", PhoneCode = "1671", Name = "Guam" },
            new Country { Code = "gw", PhoneCode = "245", Name = "Guinea-Bissau" },
            new Country { Code = "gy", PhoneCode = "592", Name = "Guyana" },
            new Country { Code = "hk", PhoneCode = "852", Name = "Hong Kong" },
            new Country { Code = "hn", PhoneCode = "504", Name = "Honduras" },
            new Country { Code = "hr", PhoneCode = "385", Name = "Croatia" },
            new Country { Code = "ht", PhoneCode = "509", Name = "Haiti" },
            new Country { Code = "hu", PhoneCode = "36", Name = "Hungary" },
            new Country { Code = "id", PhoneCode = "62", Name = "Indonesia" },
            new Country { Code = "ie", PhoneCode = "353", Name = "Ireland" },
            new Country { Code = "il", PhoneCode = "972", Name = "Israel" },
            new Country { Code = "in", PhoneCode = "91", Name = "India" },
            new Country { Code = "io", PhoneCode = "246", Name = "Diego Garcia" },
            new Country { Code = "iq", PhoneCode = "964", Name = "Iraq" },
            new Country { Code = "ir", PhoneCode = "98", Name = "Iran" },
            new Country { Code = "is", PhoneCode = "354", Name = "Iceland" },
            new Country { Code = "it", PhoneCode = "39", Name = "Italy" },
            new Country { Code = "jm", PhoneCode = "1876", Name = "Jamaica" },
            new Country { Code = "jo", PhoneCode = "962", Name = "Jordan" },
            new Country { Code = "jp", PhoneCode = "81", Name = "Japan" },
            new Country { Code = "ke", PhoneCode = "254", Name = "Kenya" },
            new Country { Code = "kg", PhoneCode = "996", Name = "Kyrgyzstan" },
            new Country { Code = "kh", PhoneCode = "855", Name = "Cambodia" },
            new Country { Code = "ki", PhoneCode = "686", Name = "Kiribati" },
            new Country { Code = "km", PhoneCode = "269", Name = "Comoros" },
            new Country { Code = "kn", PhoneCode = "1869", Name = "Saint Kitts & Nevis" },
            new Country { Code = "kp", PhoneCode = "850", Name = "North Korea" },
            new Country { Code = "kr", PhoneCode = "82", Name = "South Korea" },
            new Country { Code = "kw", PhoneCode = "965", Name = "Kuwait" },
            new Country { Code = "ky", PhoneCode = "1345", Name = "Cayman Islands" },
            new Country { Code = "kz", PhoneCode = "7", Name = "Kazakhstan" },
            new Country { Code = "la", PhoneCode = "856", Name = "Laos" },
            new Country { Code = "lb", PhoneCode = "961", Name = "Lebanon" },
            new Country { Code = "lc", PhoneCode = "1758", Name = "Saint Lucia" },
            new Country { Code = "li", PhoneCode = "423", Name = "Liechtenstein" },
            new Country { Code = "lk", PhoneCode = "94", Name = "Sri Lanka" },
            new Country { Code = "lr", PhoneCode = "231", Name = "Liberia" },
            new Country { Code = "ls", PhoneCode = "266", Name = "Lesotho" },
            new Country { Code = "lt", PhoneCode = "370", Name = "Lithuania" },
            new Country { Code = "lu", PhoneCode = "352", Name = "Luxembourg" },
            new Country { Code = "lv", PhoneCode = "371", Name = "Latvia" },
            new Country { Code = "ly", PhoneCode = "218", Name = "Libya" },
            new Country { Code = "ma", PhoneCode = "212", Name = "Morocco" },
            new Country { Code = "mc", PhoneCode = "377", Name = "Monaco" },
            new Country { Code = "md", PhoneCode = "373", Name = "Moldova" },
            new Country { Code = "me", PhoneCode = "382", Name = "Montenegro" },
            new Country { Code = "mg", PhoneCode = "261", Name = "Madagascar" },
            new Country { Code = "mh", PhoneCode = "692", Name = "Marshall Islands" },
            new Country { Code = "mk", PhoneCode = "389", Name = "Macedonia" },
            new Country { Code = "ml", PhoneCode = "223", Name = "Mali" },
            new Country { Code = "mm", PhoneCode = "95", Name = "Myanmar" },
            new Country { Code = "mn", PhoneCode = "976", Name = "Mongolia" },
            new Country { Code = "mo", PhoneCode = "853", Name = "Macau" },
            new Country { Code = "mp", PhoneCode = "1670", Name = "Northern Mariana Islands" },
            new Country { Code = "mq", PhoneCode = "596", Name = "Martinique" },
            new Country { Code = "mr", PhoneCode = "222", Name = "Mauritania" },
            new Country { Code = "ms", PhoneCode = "1664", Name = "Montserrat" },
            new Country { Code = "mt", PhoneCode = "356", Name = "Malta" },
            new Country { Code = "mu", PhoneCode = "230", Name = "Mauritius" },
            new Country { Code = "mv", PhoneCode = "960", Name = "Maldives" },
            new Country { Code = "mw", PhoneCode = "265", Name = "Malawi" },
            new Country { Code = "mx", PhoneCode = "52", Name = "Mexico" },
            new Country { Code = "my", PhoneCode = "60", Name = "Malaysia" },
            new Country { Code = "mz", PhoneCode = "258", Name = "Mozambique" },
            new Country { Code = "na", PhoneCode = "264", Name = "Namibia" },
            new Country { Code = "nc", PhoneCode = "687", Name = "New Caledonia" },
            new Country { Code = "ne", PhoneCode = "227", Name = "Niger" },
            new Country { Code = "nf", PhoneCode = "672", Name = "Norfolk Island" },
            new Country { Code = "ng", PhoneCode = "234", Name = "Nigeria" },
            new Country { Code = "ni", PhoneCode = "505", Name = "Nicaragua" },
            new Country { Code = "nl", PhoneCode = "31", Name = "Netherlands" },
            new Country { Code = "no", PhoneCode = "47", Name = "Norway" },
            new Country { Code = "np", PhoneCode = "977", Name = "Nepal" },
            new Country { Code = "nr", PhoneCode = "674", Name = "Nauru" },
            new Country { Code = "nu", PhoneCode = "683", Name = "Niue" },
            new Country { Code = "nz", PhoneCode = "64", Name = "New Zealand" },
            new Country { Code = "om", PhoneCode = "968", Name = "Oman" },
            new Country { Code = "pa", PhoneCode = "507", Name = "Panama" },
            new Country { Code = "pe", PhoneCode = "51", Name = "Peru" },
            new Country { Code = "pf", PhoneCode = "689", Name = "French Polynesia" },
            new Country { Code = "pg", PhoneCode = "675", Name = "Papua New Guinea" },
            new Country { Code = "ph", PhoneCode = "63", Name = "Philippines" },
            new Country { Code = "pk", PhoneCode = "92", Name = "Pakistan" },
            new Country { Code = "pl", PhoneCode = "48", Name = "Poland" },
            new Country { Code = "pm", PhoneCode = "508", Name = "Saint Pierre & Miquelon" },
            new Country { Code = "pr", PhoneCode = "1", Name = "Puerto Rico" },
            new Country { Code = "ps", PhoneCode = "970", Name = "Palestine" },
            new Country { Code = "pt", PhoneCode = "351", Name = "Portugal" },
            new Country { Code = "pw", PhoneCode = "680", Name = "Palau" },
            new Country { Code = "py", PhoneCode = "595", Name = "Paraguay" },
            new Country { Code = "qa", PhoneCode = "974", Name = "Qatar" },
            new Country { Code = "re", PhoneCode = "262", Name = "Réunion" },
            new Country { Code = "ro", PhoneCode = "40", Name = "Romania" },
            new Country { Code = "rs", PhoneCode = "381", Name = "Serbia" },
            new Country { Code = "ru", PhoneCode = "7", Name = "Russian Federation" },
            new Country { Code = "rw", PhoneCode = "250", Name = "Rwanda" },
            new Country { Code = "sa", PhoneCode = "966", Name = "Saudi Arabia" },
            new Country { Code = "sb", PhoneCode = "677", Name = "Solomon Islands" },
            new Country { Code = "sc", PhoneCode = "248", Name = "Seychelles" },
            new Country { Code = "sd", PhoneCode = "249", Name = "Sudan" },
            new Country { Code = "se", PhoneCode = "46", Name = "Sweden" },
            new Country { Code = "sg", PhoneCode = "65", Name = "Singapore" },
            new Country { Code = "sh", PhoneCode = "290", Name = "Saint Helena" },
            new Country { Code = "sh", PhoneCode = "247", Name = "Saint Helena" },
            new Country { Code = "si", PhoneCode = "386", Name = "Slovenia" },
            new Country { Code = "sk", PhoneCode = "421", Name = "Slovakia" },
            new Country { Code = "sl", PhoneCode = "232", Name = "Sierra Leone" },
            new Country { Code = "sm", PhoneCode = "378", Name = "San Marino" },
            new Country { Code = "sn", PhoneCode = "221", Name = "Senegal" },
            new Country { Code = "so", PhoneCode = "252", Name = "Somalia" },
            new Country { Code = "sr", PhoneCode = "597", Name = "Suriname" },
            new Country { Code = "ss", PhoneCode = "211", Name = "South Sudan" },
            new Country { Code = "st", PhoneCode = "239", Name = "São Tomé & Príncipe" },
            new Country { Code = "sv", PhoneCode = "503", Name = "El Salvador" },
            new Country { Code = "sx", PhoneCode = "1721", Name = "Sint Maarten" },
            new Country { Code = "sy", PhoneCode = "963", Name = "Syria" },
            new Country { Code = "sz", PhoneCode = "268", Name = "Swaziland" },
            new Country { Code = "tc", PhoneCode = "1649", Name = "Turks & Caicos Islands" },
            new Country { Code = "td", PhoneCode = "235", Name = "Chad" },
            new Country { Code = "tg", PhoneCode = "228", Name = "Togo" },
            new Country { Code = "th", PhoneCode = "66", Name = "Thailand" },
            new Country { Code = "tj", PhoneCode = "992", Name = "Tajikistan" },
            new Country { Code = "tk", PhoneCode = "690", Name = "Tokelau" },
            new Country { Code = "tl", PhoneCode = "670", Name = "Timor-Leste" },
            new Country { Code = "tm", PhoneCode = "993", Name = "Turkmenistan" },
            new Country { Code = "tn", PhoneCode = "216", Name = "Tunisia" },
            new Country { Code = "to", PhoneCode = "676", Name = "Tonga" },
            new Country { Code = "tr", PhoneCode = "90", Name = "Turkey" },
            new Country { Code = "tt", PhoneCode = "1868", Name = "Trinidad & Tobago" },
            new Country { Code = "tv", PhoneCode = "688", Name = "Tuvalu" },
            new Country { Code = "tw", PhoneCode = "886", Name = "Taiwan" },
            new Country { Code = "tz", PhoneCode = "255", Name = "Tanzania" },
            new Country { Code = "ua", PhoneCode = "380", Name = "Ukraine" },
            new Country { Code = "ug", PhoneCode = "256", Name = "Uganda" },
            new Country { Code = "us", PhoneCode = "1", Name = "USA" },
            new Country { Code = "uy", PhoneCode = "598", Name = "Uruguay" },
            new Country { Code = "uz", PhoneCode = "998", Name = "Uzbekistan" },
            new Country { Code = "vc", PhoneCode = "1784", Name = "Saint Vincent & the Grenadines" },
            new Country { Code = "ve", PhoneCode = "58", Name = "Venezuela" },
            new Country { Code = "vg", PhoneCode = "1284", Name = "British Virgin Islands" },
            new Country { Code = "vi", PhoneCode = "1340", Name = "US Virgin Islands" },
            new Country { Code = "vn", PhoneCode = "84", Name = "Vietnam" },
            new Country { Code = "vu", PhoneCode = "678", Name = "Vanuatu" },
            new Country { Code = "wf", PhoneCode = "681", Name = "Wallis & Futuna" },
            new Country { Code = "ws", PhoneCode = "685", Name = "Samoa" },
            new Country { Code = "ye", PhoneCode = "967", Name = "Yemen" },
            new Country { Code = "yl", PhoneCode = "42", Name = "Y-land" },
            new Country { Code = "za", PhoneCode = "27", Name = "South Africa" },
            new Country { Code = "zm", PhoneCode = "260", Name = "Zambia" },
            new Country { Code = "zw", PhoneCode = "263", Name = "Zimbabwe" }
        };

        #endregion
    }
}
