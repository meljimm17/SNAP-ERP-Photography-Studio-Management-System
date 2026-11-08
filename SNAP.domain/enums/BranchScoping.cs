namespace SNAP.domain.enums
{
    /// <summary>
    /// Which roles are bound to a single branch. This is the one definition of that rule: the user
    /// dialog uses it to decide whether to ask for a branch, and login uses it to decide whether
    /// to issue the branchId claim that pins every later query.
    ///
    /// Login deliberately re-derives the pin from the role rather than trusting TenantUser.BranchId
    /// alone. That column is nullable and only meaningful for the roles below, so a leftover value
    /// on an account promoted to Admin would otherwise silently trap them in one branch with no
    /// "All Branches" view and no obvious cause.
    /// </summary>
    public static class BranchScoping
    {
        public static bool RoleIsBranchBound(SystemRole role) =>
            role is SystemRole.Staff or SystemRole.Photographer or SystemRole.BranchManager;
    }
}
