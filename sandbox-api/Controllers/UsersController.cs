using Microsoft.AspNetCore.Mvc;
using sandbox_api.Models;
using sandbox_api.Repositories;

namespace sandbox_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly IUserRepository _userRepository;

        public UsersController(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        /// <summary>
        /// Get all users
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllUsers()
        {
            var result = await _userRepository.GetAllUsersAsync();

            return result.Match<IActionResult>(
                users => Ok(users),
                error => StatusCode(500, new { error.Code, error.Message })
            );
        }

        /// <summary>
        /// Get user by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetUser(int id)
        {
            var result = await _userRepository.GetUserByIdAsync(id);

            return result.Match<IActionResult>(
                user => Ok(user),
                error => error switch
                {
                    NotFoundError => NotFound(new { error.Code, error.Message }),
                    _ => StatusCode(500, new { error.Code, error.Message })
                }
            );
        }

        /// <summary>
        /// Get user by email
        /// </summary>
        [HttpGet("by-email/{email}")]
        public async Task<IActionResult> GetUserByEmail(string email)
        {
            var result = await _userRepository.GetUserByEmailAsync(email);

            return result.Match<IActionResult>(
                user => Ok(user),
                error => error switch
                {
                    NotFoundError => NotFound(new { error.Code, error.Message }),
                    ValidationError => BadRequest(new { error.Code, error.Message }),
                    _ => StatusCode(500, new { error.Code, error.Message })
                }
            );
        }

        /// <summary>
        /// Create a new user
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
        {
            var result = await _userRepository.CreateUserAsync(
                request.Username,
                request.Email,
                request.FullName
            );

            return result.Match<IActionResult>(
                user => CreatedAtAction(nameof(GetUser), new { id = user.Id }, user),
                error => error switch
                {
                    ValidationError => BadRequest(new { error.Code, error.Message }),
                    DuplicateError => Conflict(new { error.Code, error.Message }),
                    _ => StatusCode(500, new { error.Code, error.Message })
                }
            );
        }

        /// <summary>
        /// Update an existing user
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserRequest request)
        {
            var result = await _userRepository.UpdateUserAsync(
                id,
                request.Username,
                request.Email,
                request.FullName,
                request.IsActive
            );

            return result.Match<IActionResult>(
                user => Ok(user),
                error => error switch
                {
                    NotFoundError => NotFound(new { error.Code, error.Message }),
                    ValidationError => BadRequest(new { error.Code, error.Message }),
                    DuplicateError => Conflict(new { error.Code, error.Message }),
                    _ => StatusCode(500, new { error.Code, error.Message })
                }
            );
        }

        /// <summary>
        /// Delete a user
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var result = await _userRepository.DeleteUserAsync(id);

            return result.Match<IActionResult>(
                _ => NoContent(),
                error => error switch
                {
                    NotFoundError => NotFound(new { error.Code, error.Message }),
                    _ => StatusCode(500, new { error.Code, error.Message })
                }
            );
        }

        /// <summary>
        /// Record user login
        /// </summary>
        [HttpPost("{id}/login")]
        public async Task<IActionResult> RecordLogin(int id)
        {
            var result = await _userRepository.RecordLoginAsync(id);

            return result.Match<IActionResult>(
                user => Ok(new { message = "Login recorded successfully", user.LastLoginAt }),
                error => error switch
                {
                    NotFoundError => NotFound(new { error.Code, error.Message }),
                    InvalidOperationError => BadRequest(new { error.Code, error.Message }),
                    _ => StatusCode(500, new { error.Code, error.Message })
                }
            );
        }
    }

    // DTOs
    public record CreateUserRequest(string Username, string Email, string FullName);
    public record UpdateUserRequest(string? Username, string? Email, string? FullName, bool? IsActive);
}
