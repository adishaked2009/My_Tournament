namespace Model
{
    // Safe user details returned after sign-in. Password data never leaves the API.
    public sealed class UserSessionInfo
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;

        public UserSessionInfo()
        {
        }

        public UserSessionInfo(User user)
        {
            Id = user.Id;
            Username = user.Username;
            Email = user.Email;
            RoleId = user.RoleID;
            RoleName = user.RoleName;
        }
    }
}
