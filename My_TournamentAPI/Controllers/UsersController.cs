using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using Model;
using DAL;

namespace API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly UserDB _userDB;

        public UsersController(UserDB userDB)
        {
            _userDB = userDB;
        }

        [HttpGet("exists/{username}")]
        public async Task<ActionResult<bool>> UsernameExists(string username)
        {
            return Ok(await _userDB.UsernameExistsAsync(username));
        }

        [HttpGet("email-exists/{email}")]
        public async Task<ActionResult<bool>> EmailExists(string email)
        {
            return Ok(await _userDB.EmailExistsAsync(email));
        }

        [HttpPost]
        public async Task<IActionResult> CreateUser([FromBody] RegisterUserRequest request)
        {
            string username = request.Username?.Trim() ?? string.Empty;
            string email = request.Email?.Trim() ?? string.Empty;
            string password = request.Password ?? string.Empty;
            if (string.IsNullOrWhiteSpace(username)
                || string.IsNullOrWhiteSpace(email)
                || string.IsNullOrWhiteSpace(password))
            {
                return BadRequest("Username, email, and password are required.");
            }

            if (await _userDB.UsernameExistsAsync(username)
                || await _userDB.EmailExistsAsync(email))
            {
                return Conflict("That username or email is already registered.");
            }

            var user = new User(username, email, password);
            try
            {
                await _userDB.AddUserAsync(user);
                return StatusCode(StatusCodes.Status201Created);
            }
            catch (MySqlException exception) when (exception.Number == 1062)
            {
                // The unique indexes are the final guard against simultaneous registrations.
                return Conflict("That username or email is already registered.");
            }
        }
        // GET: api/users
        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserListItem>>> GetUsers()
        {
            var users = await _userDB.GetAllUsersAsync();
            return Ok(users.Select(user => new UserListItem
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                RoleID = user.RoleID,
                RoleName = user.RoleName,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt
            }));
        }

        // POST: api/users/login
        [HttpPost("login")]
        public async Task<ActionResult<UserSessionInfo>> Login([FromBody] LoginModel model)
        {
            string username = model.Username?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(model.Password))
            {
                return Unauthorized();
            }

            User? user = await _userDB.CheckLoginAsync(username, model.Password);
            if (user is null)
            {
                return Unauthorized();
            }

            return Ok(new UserSessionInfo(user));
        }
    }

    // מחלקה עזר לנתוני התחברות
    public class LoginModel
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class RegisterUserRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class UserListItem
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int RoleID { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
