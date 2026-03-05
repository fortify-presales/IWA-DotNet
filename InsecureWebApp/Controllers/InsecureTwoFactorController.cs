using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using MicroFocus.InsecureWebApp.Models;

// WARNING: This controller intentionally exposes sensitive information for security testing.
// It is wrapped in #if DEBUG so it is only compiled in non-production builds. Do NOT enable
// this in production environments.

#if DEBUG
namespace MicroFocus.InsecureWebApp.Controllers
{
    [ApiController]
    [Route("api/insecure/account")]
    public class InsecureTwoFactorController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public InsecureTwoFactorController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        // INSECURE: No authentication or authorization. Accepts a username query param and
        // returns 2FA status and the authenticator key (if present) for that user.
        // GET: /api/insecure/account/2fa?username=alice
        [HttpGet("2fa")]
        public async Task<IActionResult> GetTwoFactorInfoInsecure([FromQuery] string username)
        {
            if (string.IsNullOrEmpty(username))
                return BadRequest(new { message = "username query parameter is required" });

            var user = await _userManager.FindByNameAsync(username);
            if (user == null)
                return NotFound(new { message = "user not found" });

            var twoFactorEnabled = await _userManager.GetTwoFactorEnabledAsync(user);
            var authenticatorKey = await _userManager.GetAuthenticatorKeyAsync(user);
            var recoveryCodes = await _userManager.CountRecoveryCodesAsync(user);

            return Ok(new
            {
                username = user.UserName,
                twoFactorEnabled,
                authenticatorKey, // deliberately included for testing
                recoveryCodesLeft = recoveryCodes
            });
        }

        // INSECURE: No authentication. Returns the raw shared key for the provided username.
        // GET: /api/insecure/account/2fa/key?username=alice
        [HttpGet("2fa/key")]
        public async Task<IActionResult> GetAuthenticatorKeyInsecure([FromQuery] string username)
        {
            if (string.IsNullOrEmpty(username))
                return BadRequest(new { message = "username query parameter is required" });

            var user = await _userManager.FindByNameAsync(username);
            if (user == null)
                return NotFound(new { message = "user not found" });

            var authenticatorKey = await _userManager.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrEmpty(authenticatorKey))
                return NotFound(new { message = "No authenticator key is configured for this user." });

            return Ok(new { username = user.UserName, sharedKey = authenticatorKey });
        }
    }
}
#endif
