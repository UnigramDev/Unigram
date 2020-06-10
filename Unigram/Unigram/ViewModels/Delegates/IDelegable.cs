namespace Unigram.ViewModels.Delegates
{
    public interface IDelegable<TDelegate> where TDelegate : IViewModelDelegate
    {
        TDelegate Delegate { get; set; }
    }

    public interface IViewModelDelegate
    {

    }
}
