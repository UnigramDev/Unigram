namespace Telegram.Td.Api
{
    public class UpdateChatAffiliatePrograms
    {
        public UpdateChatAffiliatePrograms(AffiliateType affiliateType)
        {
            AffiliateType = affiliateType;
        }

        public AffiliateType AffiliateType { get; set; }
    }
}
