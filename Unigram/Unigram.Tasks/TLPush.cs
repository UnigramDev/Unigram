using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unigram.Tasks
{
    internal sealed class TLPushNotification
    {
        public int date
        {
            get;
            set;
        }

        public TLPushData data
        {
            get;
            set;
        }
    }

    internal sealed class TLPushData
    {
        public TLPushCustom custom
        {
            get;
            set;
        }

        public string sound
        {
            get;
            set;
        }

        public string mute
        {
            get;
            set;
        }

        public int badge
        {
            get;
            set;
        }

        public string loc_key
        {
            get;
            set;
        }

        public string[] loc_args
        {
            get;
            set;
        }

        public int random_id
        {
            get;
            set;
        }

        public int user_id
        {
            get;
            set;
        }

        public string text
        {
            get;
            set;
        }

        public string system
        {
            get;
            set;
        }

        public string group
        {
            get
            {
                if (custom == null)
                {
                    return null;
                }
                return custom.group;
            }
        }

        public string tag
        {
            get
            {
                if (custom == null)
                {
                    return null;
                }
                return custom.tag;
            }
        }
    }

    internal sealed class TLPushCustom
    {
        public string msg_id { get; set; }

        public string from_id { get; set; }

        public string chat_id { get; set; }

        public string channel_id { get; set; }

        public string group
        {
            get
            {
                if (channel_id != null)
                {
                    return "c" + channel_id;
                }
                if (chat_id != null)
                {
                    return "c" + chat_id;
                }
                if (from_id != null)
                {
                    return "u" + from_id;
                }
                return null;
            }
        }

        public string tag
        {
            get
            {
                return msg_id;
            }
        }

        public IEnumerable<string> GetParams()
        {
            if (msg_id != null)
            {
                yield return "msg_id=" + msg_id;
            }
            if (from_id != null)
            {
                yield return "from_id=" + from_id;
            }
            if (chat_id != null)
            {
                yield return "chat_id=" + chat_id;
            }
            if (channel_id != null)
            {
                yield return "channel_id=" + channel_id;
            }
            yield break;
        }
    }
}