namespace Unigram.Services.Updates
{
    public class UpdateChatListLayout
    {
        public UpdateChatListLayout(bool threeLines)
        {
            UseThreeLinesLayout = threeLines;
        }

        public bool UseThreeLinesLayout { get; private set; }
    }
}
