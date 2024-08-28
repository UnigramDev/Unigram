namespace Rg.DiffUtils
{
    public interface IDiffHandler<T> : IDiffEqualityComparer<T>
    {
        void UpdateItem(T oldItem, T newItem);
    }
}
