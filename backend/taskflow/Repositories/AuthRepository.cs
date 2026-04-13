using System.ComponentModel.DataAnnotations;
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
                throw new ValidationException("Email and password are required");

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
                throw new InvalidOperationException("Failed to register user");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error registering user: {ex.Message}");
                throw new InvalidOperationException("Error registering user", ex);
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
                throw new ValidationException("Email and password are required");

            try
            {
                var user = await GetUserByEmail(loginDto.Email);
                if (user == null || string.IsNullOrWhiteSpace(user.Password) || !PasswordHasher.VerifyPassword(loginDto.Password, user.Password))
                    throw new UnauthorizedAccessException("Invalid credentials");

                var token = JwtHandler.GenerateToken(user.Id.ToString(), user.Email ?? string.Empty);
                return Response<string?>.Success(token, "Login successful");
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during login: {ex.Message}");
                throw new InvalidOperationException("Error during login", ex);
            }
        }
    }
}

