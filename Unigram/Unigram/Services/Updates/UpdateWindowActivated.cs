namespace Unigram.Services.Updates
{
    public class UpdateWindowActivated
    {
        public bool IsActive { get; set; }

        public UpdateWindowActivated(bool active)
        {
            IsActive = active;
        }
    }
}
