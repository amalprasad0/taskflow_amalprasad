using System.ComponentModel.DataAnnotations;
using Serilog;
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

        public async Task<Response<RegisterResultDto>> Register(RegisterUserDto userDto)
        {
            if (userDto == null || string.IsNullOrWhiteSpace(userDto.Email) || string.IsNullOrWhiteSpace(userDto.Password) || string.IsNullOrWhiteSpace(userDto.Username))
                throw new ValidationException("Email, username, and password are required");

            var emailExistsCheck = await ExecuteScalarAsync("SELECT EXISTS(SELECT 1 FROM users WHERE email = @Email)", new { Email = userDto.Email });
            if (emailExistsCheck is bool emailExists && emailExists)
                throw new ValidationException("User with this email already exists");

            var nameExistsCheck = await ExecuteScalarAsync("SELECT EXISTS(SELECT 1 FROM users WHERE name = @Name)", new { Name = userDto.Username });
            if (nameExistsCheck is bool nameExists && nameExists)
                throw new ValidationException("User with this username already exists");

            var sql = @"
                INSERT INTO users (id, name, email, password, created_at) 
                VALUES (@Id, @Name, @Email, @Password, @CreatedAt)
                RETURNING id, name, email, created_at AS CreatedAt;
            ";

            var hashedPassword = PasswordHasher.HashPassword(userDto.Password);

            var id = Guid.NewGuid();
            var createdAt = DateTime.UtcNow;
            var parameters = new
            {
                Id = id,
                Name = userDto.Username,
                Email = userDto.Email,
                Password = hashedPassword,
                CreatedAt = createdAt
            };

            try
            {
                var user = await QuerySingleAsync<RegisterResultDto>(sql, parameters);
                if (user == null)
                    throw new InvalidOperationException("Failed to register user");

                return Response<RegisterResultDto>.Success(user, "User registered successfully");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error registering user Email={Email}", userDto.Email);
                throw new InvalidOperationException("Error registering user", ex);
            }
        }

        public async Task<User?> GetUserByEmail(string email)
        {
            var sql = "SELECT * FROM users WHERE email = @Email";
            var users = await QueryAsync(sql, new { Email = email });
            return users.FirstOrDefault();
        }

        public async Task<Response<LoginResponseDto>> Login(LoginUserDto loginDto)
        {
            if (loginDto == null || string.IsNullOrWhiteSpace(loginDto.Email) || string.IsNullOrWhiteSpace(loginDto.Password))
                throw new ValidationException("Email and password are required");

            try
            {
                var user = await GetUserByEmail(loginDto.Email);
                if (user == null || string.IsNullOrWhiteSpace(user.Password) || !PasswordHasher.VerifyPassword(loginDto.Password, user.Password))
                    throw new UnauthorizedAccessException("Invalid credentials");

                var token = JwtHandler.GenerateToken(user.Id.ToString(), user.Email ?? string.Empty);
                var loginResponse = new LoginResponseDto { accessToken = token };
                return Response<LoginResponseDto>.Success(loginResponse, "Login successful");
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during login for Email={Email}", loginDto.Email);
                throw new InvalidOperationException("Error during login", ex);
            }
        }
    }
}

