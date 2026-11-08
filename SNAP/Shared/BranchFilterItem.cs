namespace SNAP
{
    /// <summary>
    /// One entry in a branch picker. Every branch-aware screen shows the same control, so the item
    /// type is shared rather than redeclared per form - the combo holds either the literal
    /// "All Branches" string or one of these.
    /// </summary>
    public sealed record BranchFilterItem(int Id, string Display)
    {
        public override string ToString() => Display;
    }
}
