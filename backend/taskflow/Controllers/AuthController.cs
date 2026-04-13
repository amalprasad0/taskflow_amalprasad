using System.ComponentModel.DataAnnotations;
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
            if (user == null || string.IsNullOrWhiteSpace(user.Email) || string.IsNullOrWhiteSpace(user.Password))
                throw new ValidationException("Email and password are required");

            var result = await _authRepository.Register(user);
            if (!result.Status)
                throw new InvalidOperationException(result.Message);

            return StatusCode(201, result);
        }

        [HttpPost("Login")]
        public async Task<IActionResult> Login([FromBody] LoginUserDto loginDto)
        {
            var response = await _authRepository.Login(loginDto);
            if (!response.Status)
                throw new UnauthorizedAccessException(response.Message);

            return Ok(response);
        }
    }
}