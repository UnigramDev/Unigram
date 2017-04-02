using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unigram.Core.Stripe
{
    public class StripeToken
    {
        public string Id { get; set; }
        public string Type { get; set; }

        public string Content { get; set; }
    }
}
