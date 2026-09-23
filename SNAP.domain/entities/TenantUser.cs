using SNAP.domain.enums;

namespace SNAP.domain.entities
{
    public class TenantUser
    {
        public int UserId { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string Username { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public SystemRole Role { get; set; }

        public UserStatus Status { get; set; } = UserStatus.Active;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
