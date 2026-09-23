using SNAP.domain.enums;

namespace SNAP.domain.entities
{
    public class RolePermission
    {
        public int RolePermissionId { get; set; }

        public SystemRole Role { get; set; }

        public SystemModule Module { get; set; }

        public AccessLevel AccessLevel { get; set; }
    }
}
