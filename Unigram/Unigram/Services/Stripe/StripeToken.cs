namespace Unigram.Services.Stripe
{
    public class StripeToken
    {
        public string Id { get; set; }
        public string Type { get; set; }

        public string Content { get; set; }
    }
}
