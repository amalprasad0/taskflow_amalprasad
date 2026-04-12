using taskFlow.DTOs;
using taskFlow.Models;
using taskFlow.Services;

namespace taskFlow.Repositories
{
    public class AuthRepository : BaseSqlHandler<User>
    {
        public AuthRepository(string connectionString) : base(connectionString)
        {
        }

        public async Task<Response<bool>> Register(RegisterUserDto userDto)
        {
            if (userDto == null || string.IsNullOrWhiteSpace(userDto.Email) || string.IsNullOrWhiteSpace(userDto.Password))
                return Response<bool>.Failure("Email and password are required");

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
                var rowsAffected = await ExecuteAsync(sql, parameters);
                if (rowsAffected > 0)
                    return Response<bool>.Success(true, "User registered successfully");
                return Response<bool>.Failure("Failed to register user");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error registering user: {ex.Message}");
                return Response<bool>.Failure($"Error registering user: {ex.Message}");
            }
        }

        public async Task<User?> GetUserByEmail(string email)
        {
            var sql = "SELECT * FROM users WHERE email = @Email";
            var users = await QueryAsync(sql, new { Email = email });
            return users.FirstOrDefault();
        }

        public async Task<Response<string?>> Login(LoginUserDto loginDto)
        {
            if (loginDto == null || string.IsNullOrWhiteSpace(loginDto.Email) || string.IsNullOrWhiteSpace(loginDto.Password))
                return Response<string?>.Failure("Email and password are required");

            try
            {
                var user = await GetUserByEmail(loginDto.Email);
                if (user == null || string.IsNullOrWhiteSpace(user.Password) || !PasswordHasher.VerifyPassword(loginDto.Password, user.Password))
                    return Response<string?>.Failure("Invalid credentials");

                // Generate JWT token
                var token = JwtHandler.GenerateToken(user.Id.ToString(), user.Email ?? string.Empty);
                return Response<string?>.Success(token, "Login successful");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during login: {ex.Message}");
                return Response<string?>.Failure($"Error during login: {ex.Message}");
            }
        }
    }
}

