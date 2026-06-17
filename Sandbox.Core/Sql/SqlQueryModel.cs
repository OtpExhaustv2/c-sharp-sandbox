namespace Sandbox.Core.Sql
{
    /// <summary>Mutable holder the translator fills while walking the operator chain.</summary>
    internal sealed class SqlQueryModel
    {
        public string Table { get; set; } = "";
        public List<string> SelectColumns { get; } = new();   // empty => SELECT *
        public List<string> WhereFragments { get; } = new();   // joined with AND
        public List<(string Column, bool Descending)> OrderBy { get; } = new();
        public int? Skip { get; set; }
        public int? Take { get; set; }
    }
}
