namespace Unigram.Services.Updates
{
    public class UpdatePremiumState
    {
        public bool IsPremium { get; }

        public bool IsPremiumAvailable { get; }

        public UpdatePremiumState(bool premium, bool available)
        {
            IsPremium = premium;
            IsPremiumAvailable = available;
        }
    }
}
