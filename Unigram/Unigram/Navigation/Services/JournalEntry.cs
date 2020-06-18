using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Template10.Services.NavigationService
{
    public class JournalEntry
    {
        public Type SourcePageType { get; internal set; }
        public object Parameter { get; internal set; }

        public override bool Equals(object obj)
        {
            var je = obj as JournalEntry;

            if (je == null)
            {
                return false;
            }

            bool ret =
                SourcePageType.Equals(je.SourcePageType) &&
                ((Parameter == null && je.Parameter == null) ||
                 (Parameter.Equals(je.Parameter)));

            return ret;
        }

        public override int GetHashCode()
        {
            int hash = 17;

            if (Parameter != null)
            {
                hash = hash * 23 + Parameter.GetHashCode();
            }
            else
            {
                hash = hash * 23;
            }

            hash = hash * 23 + SourcePageType.GetHashCode();

            return hash;
        }
    }
}
