using SNAP.domain.enums;

namespace SNAP.domain.services
{
    public interface IPermissionService
    {
        bool HasAccess(SystemRole role, SystemModule module, AccessLevel required);

        AccessLevel GetAccessLevel(SystemRole role, SystemModule module);
    }
}
