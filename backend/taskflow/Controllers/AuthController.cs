using Microsoft.AspNetCore.Mvc;
using taskFlow.DTOs;
using taskFlow.Repositories;

namespace taskFlow.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AuthRepository _authRepository;
        public AuthController(AuthRepository authRepository)
        {
            _authRepository = authRepository;
        }
       
        [HttpPost("Register")]
        public async Task<IActionResult> Register([FromBody] RegisterUserDto user)
        {
            if (user == null || string.IsNullOrWhiteSpace(user.Email))
                return BadRequest("Username, email, and password are required");

            var result = await _authRepository.Register(user);
            return result ? Ok("User registered successfully") : BadRequest("Failed to register user");
        }
        [HttpPost("Login")]
        public async Task<IActionResult> Login([FromBody] LoginUserDto loginDto)
        {
            var token = await _authRepository.Login(loginDto);
            return token != null ? Ok(new { Token = token }) : BadRequest("Invalid email or password");
        }
    }
}