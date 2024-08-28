namespace Rg.DiffUtils
{
    public interface IDiffEqualityComparer<in T>
    {
        bool CompareItems(T oldItem, T newItem);
    }
}
