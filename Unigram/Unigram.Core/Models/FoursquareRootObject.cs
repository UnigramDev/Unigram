using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unigram.Core.Models
{
    public class FoursquareRootObject
    {
        public Meta meta { get; set; }

        public FoursquareResponse response { get; set; }
    }

    public class Meta
    {
        public int code { get; set; }
    }

    public class FoursquareResponse
    {
        public bool confident { get; set; }

        public List<Venue> venues { get; set; }
    }

    public class Venue
    {
        public List<Category> categories { get; set; }

        public Contact contact { get; set; }

        public HereNow hereNow { get; set; }

        public string id { get; set; }

        public Location location { get; set; }

        public Menu menu { get; set; }

        public string name { get; set; }

        public string referralId { get; set; }

        public Specials specials { get; set; }

        public Stats stats { get; set; }

        public string storeId { get; set; }

        public string url { get; set; }

        public VenuePage venuePage { get; set; }

        public bool verified { get; set; }
    }

    public class Category
    {
        public Icon icon { get; set; }

        public string id { get; set; }

        public string name { get; set; }

        public string pluralName { get; set; }

        public bool primary { get; set; }

        public string shortName { get; set; }
    }

    public class Icon
    {
        public string prefix { get; set; }

        public string suffix { get; set; }
    }

    public class Contact
    {
        public string facebook { get; set; }

        public string facebookName { get; set; }

        public string facebookUsername { get; set; }

        public string formattedPhone { get; set; }

        public string phone { get; set; }

        public string twitter { get; set; }
    }

    public class HereNow
    {
        public int count { get; set; }

        public List<object> groups { get; set; }

        public string summary { get; set; }
    }

    public class Location
    {
        public string address { get; set; }

        public string cc { get; set; }

        public string city { get; set; }

        public string country { get; set; }

        public string crossStreet { get; set; }

        public int distance { get; set; }

        public List<string> formattedAddress { get; set; }

        public double lat { get; set; }

        public double lng { get; set; }

        public string neighborhood { get; set; }

        public string postalCode { get; set; }
    }

    public class Menu
    {
        public string anchor { get; set; }

        public string externalUrl { get; set; }

        public string label { get; set; }

        public string mobileUrl { get; set; }

        public string type { get; set; }

        public string url { get; set; }
    }

    public class Specials
    {
        public int count { get; set; }

        public List<object> items { get; set; }
    }

    public class Stats
    {
        public int checkinsCount { get; set; }

        public int tipCount { get; set; }

        public int usersCount { get; set; }
    }

    public class VenuePage
    {
        public string id { get; set; }
    }
}
