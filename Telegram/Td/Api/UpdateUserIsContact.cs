namespace Telegram.Td.Api
{
    public class UpdateUserIsContact
    {
        public UpdateUserIsContact(long userId)
        {
            UserId = userId;
        }

        public long UserId { get; set; }
    }
}
