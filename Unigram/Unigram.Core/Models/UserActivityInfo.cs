using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Template10.Mvvm;
using Windows.UI;

namespace Unigram.Core.Models
{
    public class UserActivityInfo
    {
        public UserActivityInfo(string id, string title,
            Uri uri)
        {
            ActivityId = id;
            Title = title;
            ActivationUri = uri;
        }

        public UserActivityInfo(string id, string title,
            Uri uri, string details = null, Color? cardBg = null) : this(id, title, uri)
        {
            Details = details ?? "";

            if (cardBg != null)
            {
                ActivityCardBackground = cardBg.Value;
            }
        }        

        public string ActivityId { get; }
        public string Title { get; }
        public string Details { get; }
        public Color ActivityCardBackground { get; }
        public Uri ActivationUri { get; }
    }
}
