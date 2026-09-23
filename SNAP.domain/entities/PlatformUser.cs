using SNAP.domain.enums;

namespace SNAP.domain.entities
{
    public class PlatformUser
    {
        public int PlatformUserId { get; set; }

        public string Username { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public SystemRole Role { get; set; } = SystemRole.SuperAdmin;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
