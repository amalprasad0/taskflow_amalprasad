using Microsoft.AspNetCore.Mvc;
using taskFlow.DTOs;
using taskFlow.Models;
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
                return BadRequest(new Response<object> { Status = false, Message = "Email and password are required", Data = null });

            var result = await _authRepository.Register(user);
            if (!result.Status)
                return BadRequest(result);

            return Ok(result);
        }

        [HttpPost("Login")]
        public async Task<IActionResult> Login([FromBody] LoginUserDto loginDto)
        {
            var response = await _authRepository.Login(loginDto);
            if (!response.Status)
                return BadRequest(response);

            return Ok(response);
        }
    }
}