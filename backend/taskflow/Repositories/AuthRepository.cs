using taskFlow.DTOs;
using taskFlow.Models;
using taskFlow.Services;

namespace taskFlow.Repositories
{
    public class AuthRepository : DapperRepository<User>
    {
        public AuthRepository(string connectionString) : base(connectionString)
        {
        }

        public async Task<bool> Register(RegisterUserDto userDto)
        {
            if (userDto == null || string.IsNullOrWhiteSpace(userDto.Email) || string.IsNullOrWhiteSpace(userDto.Password))
                return false;

            var sql = @"
                INSERT INTO users (id, name, email, password, created_at) 
                VALUES (@Id, @Name, @Email, @Password, @CreatedAt)
            ";

            var hashedPassword = PasswordHasher.HashPassword(userDto.Password);

            var parameters = new
            {
                Id = Guid.NewGuid(),
                Name = userDto.Username,
                Email = userDto.Email,
                Password = hashedPassword,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                var rowsAffected = await CreateAsync(sql, parameters);
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error registering user: {ex.Message}");
                return false;
            }
        }

        public async Task<User?> GetUserByEmail(string email)
        {
            var sql = "SELECT * FROM users WHERE email = @Email";
            return await QueryAsync(sql, new { Email = email }).ContinueWith(t => t.Result.FirstOrDefault());
        }

        public async Task<string?> Login(LoginUserDto loginDto)
        {
            if (loginDto == null || string.IsNullOrWhiteSpace(loginDto.Email) || string.IsNullOrWhiteSpace(loginDto.Password))
                return null;

            var user = await GetUserByEmail(loginDto.Email);
            if (user == null || string.IsNullOrWhiteSpace(user.Password) || !PasswordHasher.VerifyPassword(loginDto.Password, user.Password))
                return null;

            // Generate JWT token
            var token = JwtHandler.GenerateToken(user.Id.ToString(), user.Email ?? string.Empty);
            return token;
        }
    }
}


