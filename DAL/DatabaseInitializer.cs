using System.Data;
using MySql.Data.MySqlClient;

namespace DAL
{
    public static class DatabaseInitializer
    {
        private const string DatabaseName = "My_TournamentDB";

        public static async Task InitializeAsync(string serverConnectionString)
        {
            await CreateDatabaseAsync(serverConnectionString);
            string dbConnectionString = $"{serverConnectionString}Database={DatabaseName};";
            await CreateTablesAsync(dbConnectionString);
            await SeedDefaultDataAsync(dbConnectionString);
            await HashLegacyPasswordsAsync(dbConnectionString);
        }

        private static async Task CreateDatabaseAsync(string serverConnectionString)
        {
            string sql = $"CREATE DATABASE IF NOT EXISTS `{DatabaseName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";
            using var connection = new MySqlConnection(serverConnectionString);
            await connection.OpenAsync();
            using var command = new MySqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }

        private static async Task CreateTablesAsync(string dbConnectionString)
        {
            string createTablesSql = @"
                CREATE TABLE IF NOT EXISTS Roles (
                    RoleID INT AUTO_INCREMENT PRIMARY KEY,
                    RoleName VARCHAR(30) NOT NULL UNIQUE
                ) ENGINE=InnoDB;

                CREATE TABLE IF NOT EXISTS Users (
                    UserID INT AUTO_INCREMENT PRIMARY KEY,
                    Username VARCHAR(50) NOT NULL UNIQUE,
                    Email VARCHAR(100) NOT NULL UNIQUE,
                    PasswordHash VARCHAR(255) NOT NULL,
                    RoleID INT NOT NULL,
                    IsActive BOOLEAN NOT NULL DEFAULT TRUE,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (RoleID) REFERENCES Roles(RoleID) ON DELETE RESTRICT ON UPDATE CASCADE
                ) ENGINE=InnoDB;

                CREATE TABLE IF NOT EXISTS Categories (
                    CategoryID INT AUTO_INCREMENT PRIMARY KEY,
                    CategoryName VARCHAR(50) NOT NULL UNIQUE
                ) ENGINE=InnoDB;

                CREATE TABLE IF NOT EXISTS Tournaments (
                    TournamentID INT AUTO_INCREMENT PRIMARY KEY,
                    Title VARCHAR(150) NOT NULL,
                    Description TEXT NULL,
                    HostID INT NOT NULL,
                    CategoryID INT NOT NULL,
                    Status VARCHAR(30) DEFAULT 'RegistrationOpen',
                    StartDate DATETIME NOT NULL,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (HostID) REFERENCES Users(UserID) ON DELETE RESTRICT ON UPDATE CASCADE,
                    FOREIGN KEY (CategoryID) REFERENCES Categories(CategoryID) ON DELETE RESTRICT ON UPDATE CASCADE
                ) ENGINE=InnoDB;

                CREATE TABLE IF NOT EXISTS Teams (
                    TeamID INT AUTO_INCREMENT PRIMARY KEY,
                    TeamName VARCHAR(100) NOT NULL UNIQUE,
                    CaptainID INT NOT NULL,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (CaptainID) REFERENCES Users(UserID) ON DELETE RESTRICT ON UPDATE CASCADE
                ) ENGINE=InnoDB;

                CREATE TABLE IF NOT EXISTS TournamentTeams (
                    TournamentID INT NOT NULL,
                    TeamID INT NOT NULL,
                    Status VARCHAR(20) DEFAULT 'Pending',
                    RegisteredAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (TournamentID, TeamID),
                    FOREIGN KEY (TournamentID) REFERENCES Tournaments(TournamentID) ON DELETE CASCADE ON UPDATE CASCADE,
                    FOREIGN KEY (TeamID) REFERENCES Teams(TeamID) ON DELETE CASCADE ON UPDATE CASCADE
                ) ENGINE=InnoDB;

                CREATE TABLE IF NOT EXISTS Matches (
                    MatchID INT AUTO_INCREMENT PRIMARY KEY,
                    TournamentID INT NOT NULL,
                    TeamA_ID INT NOT NULL,
                    TeamB_ID INT NOT NULL,
                    ScoreA INT DEFAULT 0,
                    ScoreB INT DEFAULT 0,
                    MatchDate DATETIME NOT NULL,
                    Status VARCHAR(20) DEFAULT 'Scheduled',
                    FOREIGN KEY (TournamentID) REFERENCES Tournaments(TournamentID) ON DELETE CASCADE ON UPDATE CASCADE,
                    FOREIGN KEY (TeamA_ID) REFERENCES Teams(TeamID) ON DELETE RESTRICT ON UPDATE CASCADE,
                    FOREIGN KEY (TeamB_ID) REFERENCES Teams(TeamID) ON DELETE RESTRICT ON UPDATE CASCADE
                ) ENGINE=InnoDB;
            ";

            using var connection = new MySqlConnection(dbConnectionString);
            await connection.OpenAsync();
            using var command = new MySqlCommand(createTablesSql, connection);
            await command.ExecuteNonQueryAsync();
        }

        private static async Task SeedDefaultDataAsync(string dbConnectionString)
        {
            using var connection = new MySqlConnection(dbConnectionString);
            await connection.OpenAsync();

            // Ensure all supported roles exist, including in databases initialized by older versions.
            const string insertRolesSql = @"
                INSERT IGNORE INTO Roles (RoleName) VALUES
                    ('SuperAdmin'), ('Host'), ('Player'), ('Guest');";
            using var insertRolesCmd = new MySqlCommand(insertRolesSql, connection);
            await insertRolesCmd.ExecuteNonQueryAsync();

            // 2. הכנסת קטגוריות
            string checkCatSql = "SELECT COUNT(*) FROM Categories;";
            using var checkCatCmd = new MySqlCommand(checkCatSql, connection);



            if (Convert.ToInt64(await checkCatCmd.ExecuteScalarAsync()) == 0)
            {
                string insertCatSql = @"
                    INSERT INTO Categories (CategoryName) VALUES 
                    ('כדורגל'), ('כדורסל'), ('FIFA'), ('League of Legends');";
                using var insertCatCmd = new MySqlCommand(insertCatSql, connection);
                await insertCatCmd.ExecuteNonQueryAsync();
            }

            // 3. משתמשי ברירת מחדל לסביבת הלימוד
            string checkUsersSql = "SELECT COUNT(*) FROM Users;";
            using var checkUsersCmd = new MySqlCommand(checkUsersSql, connection);
            if (Convert.ToInt64(await checkUsersCmd.ExecuteScalarAsync()) == 0)
            {
                await AddSeedUserAsync(connection, "admin", "admin@example.com", "admin", "SuperAdmin");
                await AddSeedUserAsync(connection, "user", "user@example.com", "user", "Player");
            }
        }

        private static async Task AddSeedUserAsync(
            MySqlConnection connection,
            string username,
            string email,
            string password,
            string roleName)
        {
            const string sql = @"
                INSERT INTO Users (Username, Email, PasswordHash, RoleID, IsActive)
                VALUES (@Username, @Email, @PasswordHash,
                    (SELECT RoleID FROM Roles WHERE RoleName = @RoleName), TRUE);";
            using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Username", username);
            command.Parameters.AddWithValue("@Email", email);
            command.Parameters.AddWithValue("@PasswordHash", BCrypt.Net.BCrypt.HashPassword(password));
            command.Parameters.AddWithValue("@RoleName", roleName);
            await command.ExecuteNonQueryAsync();
        }

        private static async Task HashLegacyPasswordsAsync(string dbConnectionString)
        {
            var passwordsToHash = new List<(int UserId, string Password)>();
            await using (var connection = new MySqlConnection(dbConnectionString))
            {
                await connection.OpenAsync();
                const string selectSql = "SELECT UserID, PasswordHash FROM Users;";
                await using var selectCommand = new MySqlCommand(selectSql, connection);
                await using var reader = await selectCommand.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    string storedValue = reader.GetString(1);
                    bool isBcrypt = storedValue.StartsWith("$2", StringComparison.Ordinal);
                    bool isAspNetIdentityHash = storedValue.StartsWith("AQAAAA", StringComparison.Ordinal);
                    if (!isBcrypt && !isAspNetIdentityHash)
                    {
                        passwordsToHash.Add((reader.GetInt32(0), storedValue));
                    }
                }
            }

            await using var updateConnection = new MySqlConnection(dbConnectionString);
            await updateConnection.OpenAsync();
            foreach ((int userId, string password) in passwordsToHash)
            {
                const string updateSql = "UPDATE Users SET PasswordHash = @PasswordHash WHERE UserID = @UserID;";
                await using var updateCommand = new MySqlCommand(updateSql, updateConnection);
                updateCommand.Parameters.AddWithValue("@PasswordHash", BCrypt.Net.BCrypt.HashPassword(password));
                updateCommand.Parameters.AddWithValue("@UserID", userId);
                await updateCommand.ExecuteNonQueryAsync();
            }
        }
    }
}
