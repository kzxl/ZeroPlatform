namespace ZeroUI.Core.Data
{
    /// <summary>
    /// Contract for obtaining the underlying strongly-typed or domain model instance from a virtual data source.
    /// Corresponds to DevExpress's data row access (e.g. GridView.GetRow).
    /// </summary>
    public interface IZeroItemSource
    {
        object? GetItem(int index);
    }
}
