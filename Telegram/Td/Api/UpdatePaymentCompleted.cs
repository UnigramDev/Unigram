namespace Telegram.Td.Api
{
    public class UpdatePaymentCompleted
    {
        public string Slug { get; set; }

        public string Status { get; set; }

        public UpdatePaymentCompleted(string slug, string status)
        {
            Slug = slug;
            Status = status;
        }
    }
}
