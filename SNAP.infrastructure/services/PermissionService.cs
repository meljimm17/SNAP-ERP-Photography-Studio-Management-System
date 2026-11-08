using SNAP.domain.enums;
using SNAP.domain.services;

namespace SNAP.infrastructure.services
{
    public class PermissionService : IPermissionService
    {
        public bool HasAccess(SystemRole role, SystemModule module, AccessLevel required)
        {
            return GetAccessLevel(role, module) >= required;
        }

        /// <summary>
        /// Resolves the effective access level a role has for a given module, e.g. to decide
        /// whether to show a sidebar button and what access level to pass into the module form.
        /// </summary>
        public AccessLevel GetAccessLevel(SystemRole role, SystemModule module)
        {
            // SuperAdmin is the top-level authority for its tenant: full access to every module.
            if (role == SystemRole.SuperAdmin)
            {
                return AccessLevel.Full;
            }

            // SystemAdministration is a pseudo-module: the single sidebar entry that now hosts
            // both User Management and Branches as tabs (SystemAdministrationForm). It's visible
            // to anyone who can reach at least one of the two real modules it bundles; each tab
            // still enforces its own module's access level once inside, so a role that can only
            // report on branches (see BranchManager below) still can't add a user from here.
            if (module == SystemModule.SystemAdministration)
            {
                var userManagement = GetAccessLevel(role, SystemModule.UserManagement);
                var branch = GetAccessLevel(role, SystemModule.BranchManagement);
                return userManagement > branch ? userManagement : branch;
            }

            return role switch
            {
                SystemRole.Admin => module switch
                {
                    SystemModule.CustomerManagement => AccessLevel.Full,
                    SystemModule.PackageManagement => AccessLevel.Full,
                    SystemModule.BookingScheduling => AccessLevel.Full,
                    SystemModule.PaymentManagement => AccessLevel.Full,
                    SystemModule.UserManagement => AccessLevel.Full,
                    // Admin is the one role meant to actually manage branches (add/edit/retire) -
                    // this was missing entirely before, which is a real bug, not by design: it
                    // silently fell through to None, so Admin never even saw the Branches screen
                    // while BranchManager (who shouldn't be creating branches at all) had Full.
                    SystemModule.BranchManagement => AccessLevel.Full,
                    SystemModule.EquipmentManagement => AccessLevel.Full,
                    SystemModule.InventoryManagement => AccessLevel.Full,
                    SystemModule.FinanceManagement => AccessLevel.Full,
                    SystemModule.ReportsManagement => AccessLevel.Full,
                    // Read-only everywhere a tenant sees it - changing plan/billing state stays
                    // SuperAdmin-only, through the Master Panel (SystemAdminCompanyForm).
                    SystemModule.SubscriptionManagement => AccessLevel.View,
                    _ => AccessLevel.None
                },
                SystemRole.Staff => module switch
                {
                    SystemModule.CustomerManagement => AccessLevel.Manage,
                    // Staff build the package menu at the front desk, so they add/edit packages
                    // too. Packages are company-wide, so a Staff change shows at every branch.
                    SystemModule.PackageManagement => AccessLevel.Manage,
                    SystemModule.BookingScheduling => AccessLevel.Manage,
                    SystemModule.PaymentManagement => AccessLevel.Manage,
                    SystemModule.EquipmentManagement => AccessLevel.Manage,
                    SystemModule.InventoryManagement => AccessLevel.Manage,
                    _ => AccessLevel.None
                },
                SystemRole.Photographer => module switch
                {
                    SystemModule.CustomerManagement => AccessLevel.AssignedView,
                    SystemModule.PackageManagement => AccessLevel.AssignedView,
                    SystemModule.BookingScheduling => AccessLevel.AssignedView,
                    SystemModule.PaymentManagement => AccessLevel.None,
                    SystemModule.EquipmentManagement => AccessLevel.AssignedView,
                    _ => AccessLevel.None
                },
                // A BranchManager runs one location end to end, so they get the operational
                // modules - but every query they make is filtered to their own TenantUser.BranchId
                // server-side. The access level here says what they may do; it says nothing about
                // whose data they may see.
                SystemRole.BranchManager => module switch
                {
                    // Read-only: a BranchManager runs their own location, not the company's branch
                    // list. They can see every branch as a report (useful context - e.g. knowing
                    // other branches exist) but Add/Edit are Admin-only, enforced both here (which
                    // disables the buttons) and server-side in BranchController (which is what
                    // actually stops a direct API call - see its class remarks).
                    SystemModule.BranchManagement => AccessLevel.View,
                    SystemModule.CustomerManagement => AccessLevel.Manage,
                    SystemModule.PackageManagement => AccessLevel.Manage,
                    SystemModule.BookingScheduling => AccessLevel.Manage,
                    SystemModule.PaymentManagement => AccessLevel.Manage,
                    SystemModule.InventoryManagement => AccessLevel.Manage,
                    SystemModule.EquipmentManagement => AccessLevel.Manage,
                    // Expenses only - Payroll and Owner Transactions are company-wide concerns and
                    // FinanceForm rejects a BranchManager on those two tabs.
                    SystemModule.FinanceManagement => AccessLevel.Manage,
                    SystemModule.ReportsManagement => AccessLevel.View,
                    _ => AccessLevel.None
                },
                SystemRole.FinanceManager => module switch
                {
                    SystemModule.PaymentManagement => AccessLevel.Full,
                    SystemModule.FinanceManagement => AccessLevel.Full,
                    SystemModule.BookingScheduling => AccessLevel.View,
                    SystemModule.CustomerManagement => AccessLevel.View,
                    // Reports is the output of the finance work this role exists to do; without it
                    // a FinanceManager can record figures but never read them back.
                    SystemModule.ReportsManagement => AccessLevel.Full,
                    // Billing status is a finance concern too, and read-only for the same reason
                    // it is for Admin - see the note on Admin's case above.
                    SystemModule.SubscriptionManagement => AccessLevel.View,
                    _ => AccessLevel.None
                },
                _ => AccessLevel.None
            };
        }
    }
}
