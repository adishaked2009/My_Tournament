using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using Microsoft.AspNetCore.Identity;
using Model;

namespace DAL
{
    public class UserDB
    {
        private readonly string _connectionString;
        private readonly PasswordHasher<User> _passwordHasher = new();

        // הבנאי מקבל את מחרוזת החיבור מבחוץ (הפרדת שכבות)
        public UserDB(string connectionString)
        {
            _connectionString = connectionString;
        }

        // הוספת משתמש חדש למסד הנתונים
        public async Task<int> AddUserAsync(User user)
        {
            const string sql = @"
                INSERT INTO Users (Username, Email, PasswordHash, RoleID, IsActive)
                VALUES (@Username, @Email, @PasswordHash,
                    (SELECT RoleID FROM Roles WHERE RoleName = 'Player'), TRUE);
                SELECT LAST_INSERT_ID();";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Username", user.Username);
            command.Parameters.AddWithValue("@Email", user.Email);
            user.Password = BCrypt.Net.BCrypt.HashPassword(user.Password);
            command.Parameters.AddWithValue("@PasswordHash", user.Password);

            object? result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        public async Task<bool> UpdatePasswordAsync(int userId, string newPassword)
        {
            const string sql = "UPDATE Users SET PasswordHash = @PasswordHash WHERE UserID = @UserID;";
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@PasswordHash", BCrypt.Net.BCrypt.HashPassword(newPassword));
            command.Parameters.AddWithValue("@UserID", userId);
            return await command.ExecuteNonQueryAsync() == 1;
        }

        public Task<bool> UsernameExistsAsync(string username) =>
            ExistsAsync("Username", username);

        public Task<bool> EmailExistsAsync(string email) =>
            ExistsAsync("Email", email);

        private async Task<bool> ExistsAsync(string column, string value)
        {
            // The column name is selected only by the two methods above; the value is parameterized.
            string sql = $"SELECT EXISTS(SELECT 1 FROM Users WHERE {column} = @Value);";
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Value", value);
            return Convert.ToInt32(await command.ExecuteScalarAsync()) != 0;
        }

        // בדיקת התחברות לפי שם משתמש וסיסמה
        public async Task<User?> CheckLoginAsync(string username, string password)
        {
            const string sql = @"
                SELECT u.UserID, u.Username, u.Email, u.PasswordHash, u.RoleID,
                       r.RoleName, u.IsActive, u.CreatedAt
                FROM Users u
                INNER JOIN Roles r ON r.RoleID = u.RoleID
                WHERE u.Username = @Username AND u.IsActive = TRUE
                LIMIT 1;";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Username", username);
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return null;
            }

            User user = ReadUser(reader);
            bool passwordMatches;
            bool upgradeHash = false;
            if (user.Password.StartsWith("$2", StringComparison.Ordinal))
            {
                passwordMatches = BCrypt.Net.BCrypt.Verify(password, user.Password);
            }
            else if (user.Password.StartsWith("AQAAAA", StringComparison.Ordinal))
            {
                // Accounts created by the previous app version used ASP.NET Identity hashes.
                PasswordVerificationResult verification;
                try
                {
                    verification = _passwordHasher.VerifyHashedPassword(user, user.Password, password);
                }
                catch (FormatException)
                {
                    verification = PasswordVerificationResult.Failed;
                }

                passwordMatches = verification != PasswordVerificationResult.Failed;
                upgradeHash = passwordMatches;
            }
            else
            {
                // Supports plain-text rows created before hashing was introduced.
                passwordMatches = string.Equals(user.Password, password, StringComparison.Ordinal);
                upgradeHash = passwordMatches;
            }

            if (!passwordMatches)
            {
                return null;
            }

            if (upgradeHash)
            {
                user.Password = BCrypt.Net.BCrypt.HashPassword(password);
                await reader.CloseAsync();
                await using var update = new MySqlCommand(
                    "UPDATE Users SET PasswordHash = @PasswordHash WHERE UserID = @UserID;", connection);
                update.Parameters.AddWithValue("@PasswordHash", user.Password);
                update.Parameters.AddWithValue("@UserID", user.Id);
                await update.ExecuteNonQueryAsync();
            }

            return user;
        }

        // שליפת רשימת כל המשתמשים כולל שם התפקיד
        public async Task<List<User>> GetAllUsersAsync()
        {
            const string sql = @"
                SELECT u.UserID, u.Username, u.Email, u.PasswordHash, u.RoleID,
                       r.RoleName, u.IsActive, u.CreatedAt
                FROM Users u
                INNER JOIN Roles r ON r.RoleID = u.RoleID
                ORDER BY u.UserID;";

            var users = new List<User>();
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                users.Add(ReadUser(reader));
            }

            return users;
        }

        // מתודת עזר פרטית להמרת שורת תוצאה מהמסד לאובייקט User
        private User ReadUser(DbDataReader reader)
        {
            return new User(
                reader.GetInt32(reader.GetOrdinal("UserID")),
                reader.GetString(reader.GetOrdinal("Username")),
                reader.GetString(reader.GetOrdinal("Email")),
                reader.GetString(reader.GetOrdinal("PasswordHash")),
                reader.GetInt32(reader.GetOrdinal("RoleID")),
                reader.GetString(reader.GetOrdinal("RoleName")),
                reader.GetBoolean(reader.GetOrdinal("IsActive")),
                reader.GetDateTime(reader.GetOrdinal("CreatedAt")));
        }
    }
}
