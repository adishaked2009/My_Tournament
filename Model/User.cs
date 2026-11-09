namespace Model
{
    public class User : BaseEntity
    {
        public string Username { get; set; }
        public string Email { get; set; }
        public string Password { get; set; } // ממופה לשדה PasswordHash במסד הנתונים
        public int RoleID { get; set; }
        public string RoleName { get; set; } = string.Empty; // מגיע מטבלת Roles באמצעות JOIN
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }

        // בנאי למשתמש חדש (טרם נשמר במסד)
        public User(string username, string email, string password) : base()
        {
            Username = username;
            Email = email;
            Password = password;
            IsActive = true;
        }

        // בנאי לשליפת משתמש קיים ממסד הנתונים
        public User(int id, string username, string email, string passwordHash, int roleID, string roleName, bool isActive, DateTime createdAt) : base(id)
        {
            Username = username;
            Email = email;
            Password = passwordHash;
            RoleID = roleID;
            RoleName = roleName;
            IsActive = isActive;
            CreatedAt = createdAt;
        }

        public override string ToString()
        {
            return $"{Username} ({RoleName})";
        }
    }
}
