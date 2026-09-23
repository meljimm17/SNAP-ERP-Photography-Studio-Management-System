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

            return role switch
            {
                SystemRole.Admin => module switch
                {
                    SystemModule.CustomerManagement => AccessLevel.Full,
                    SystemModule.PackageManagement => AccessLevel.Full,
                    SystemModule.BookingScheduling => AccessLevel.Full,
                    SystemModule.PaymentManagement => AccessLevel.Full,
                    SystemModule.UserManagement => AccessLevel.Full,
                    SystemModule.EquipmentManagement => AccessLevel.Full,
                    SystemModule.InventoryManagement => AccessLevel.Full,
                    SystemModule.FinanceManagement => AccessLevel.Full,
                    SystemModule.ReportsManagement => AccessLevel.Full,
                    _ => AccessLevel.None
                },
                SystemRole.Staff => module switch
                {
                    SystemModule.CustomerManagement => AccessLevel.Manage,
                    SystemModule.PackageManagement => AccessLevel.View,
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
                SystemRole.BranchManager => module switch
                {
                    SystemModule.BranchManagement => AccessLevel.Full,
                    SystemModule.CustomerManagement => AccessLevel.Manage,
                    SystemModule.PackageManagement => AccessLevel.Manage,
                    SystemModule.BookingScheduling => AccessLevel.Manage,
                    SystemModule.PaymentManagement => AccessLevel.Manage,
                    _ => AccessLevel.None
                },
                SystemRole.FinanceManager => module switch
                {
                    SystemModule.PaymentManagement => AccessLevel.Full,
                    SystemModule.FinanceManagement => AccessLevel.Full,
                    SystemModule.BookingScheduling => AccessLevel.View,
                    SystemModule.CustomerManagement => AccessLevel.View,
                    _ => AccessLevel.None
                },
                _ => AccessLevel.None
            };
        }
    }
}
