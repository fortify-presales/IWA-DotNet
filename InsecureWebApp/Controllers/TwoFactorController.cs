using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using MicroFocus.InsecureWebApp.Models;

namespace MicroFocus.InsecureWebApp.Controllers
{
    [ApiController]
    [Route("api/account")]
    [Authorize]
    public class TwoFactorController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public TwoFactorController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        // GET: api/account/2fa
        [HttpGet("2fa")]
        public async Task<IActionResult> GetTwoFactorInfo()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            var twoFactorEnabled = await _userManager.GetTwoFactorEnabledAsync(user);
            var authenticatorKey = await _userManager.GetAuthenticatorKeyAsync(user);
            var recoveryCodes = await _userManager.CountRecoveryCodesAsync(user);

            return Ok(new
            {
                twoFactorEnabled,
                hasAuthenticatorKey = !string.IsNullOrEmpty(authenticatorKey),
                recoveryCodesLeft = recoveryCodes
            });
        }

        // GET: api/account/2fa/key
        [HttpGet("2fa/key")]
        public async Task<IActionResult> GetAuthenticatorKey()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            var authenticatorKey = await _userManager.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrEmpty(authenticatorKey))
            {
                return NotFound(new { message = "No authenticator key is configured for this user." });
            }

            return Ok(new { sharedKey = authenticatorKey });
        }
    }
}
