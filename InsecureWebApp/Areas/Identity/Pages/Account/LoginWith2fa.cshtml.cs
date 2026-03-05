using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using MicroFocus.InsecureWebApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace MicroFocus.InsecureWebApp.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class LoginWith2faModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<LoginWith2faModel> _logger;

        public LoginWith2faModel(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager, ILogger<LoginWith2faModel> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public bool RememberMe { get; set; }

        public string ReturnUrl { get; set; }
        // Base32 shared secret (authenticator key) to display for dev testing
        public string SharedKey { get; set; }

        public class InputModel
        {
            [Required]
            [StringLength(7, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Text)]
            [Display(Name = "Authenticator code")]
            public string TwoFactorCode { get; set; }

            [Display(Name = "Remember this machine")]
            public bool RememberMachine { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(bool rememberMe, string returnUrl = null)
        {
            // Ensure the user has gone through the username & password screen first
            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();

            if (user == null)
            {
                throw new InvalidOperationException($"Unable to load two-factor authentication user.");
            }

            ReturnUrl = returnUrl;
            RememberMe = rememberMe;

            return Page();
        }

        public async Task<IActionResult> OnPostShowCodeAsync(bool rememberMe, string returnUrl = null)
        {
            // Ensure the user has gone through the username & password screen first
            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();

            if (user == null)
            {
                throw new InvalidOperationException($"Unable to load two-factor authentication user.");
            }

            // Retrieve the authenticator key (Base32) and generate current TOTP code
            var key = await _userManager.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrEmpty(key))
            {
                ModelState.AddModelError(string.Empty, "Authenticator key not available for this user.");
                return Page();
            }

            try
            {
                // Expose the raw Base32 secret so it can be pasted into an external TOTP tester
                SharedKey = key;
                // Remove any model-state validation for the TwoFactorCode field so the
                // "Authenticator code is required" message doesn't persist after show.
                ModelState.Remove("Input.TwoFactorCode");
                if (ModelState.ContainsKey("Input.TwoFactorCode"))
                {
                    ModelState["Input.TwoFactorCode"].Errors.Clear();
                }
                // Optionally also compute the current code (kept for reference)
                // var code = GenerateTotp(key);
                // GeneratedCode = code;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating TOTP code for user {UserId}", user.Id);
                ModelState.AddModelError(string.Empty, "Unable to generate TOTP code.");
            }

            ReturnUrl = returnUrl;
            RememberMe = rememberMe;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(bool rememberMe, string returnUrl = null)
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            returnUrl = returnUrl ?? Url.Content("~/");

            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
            {
                throw new InvalidOperationException($"Unable to load two-factor authentication user.");
            }

            var authenticatorCode = Input.TwoFactorCode.Replace(" ", string.Empty).Replace("-", string.Empty);

            var result = await _signInManager.TwoFactorAuthenticatorSignInAsync(authenticatorCode, rememberMe, Input.RememberMachine);

            if (result.Succeeded)
            {
                _logger.LogInformation("User with ID '{UserId}' logged in with 2fa.", user.Id);
                return LocalRedirect(returnUrl);
            }
            else if (result.IsLockedOut)
            {
                _logger.LogWarning("User with ID '{UserId}' account locked out.", user.Id);
                return RedirectToPage("./Lockout");
            }
            else
            {
                _logger.LogWarning("Invalid authenticator code entered for user with ID '{UserId}'.", user.Id);
                ModelState.AddModelError(string.Empty, "Invalid authenticator code.");
                return Page();
            }
        }

        // --- TOTP helper methods ---
        private static byte[] Base32Decode(string base32)
        {
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
            var output = new List<byte>();

            var bits = 0;
            var value = 0;

            foreach (char c in base32.ToUpperInvariant().Where(ch => ch != '=' && !char.IsWhiteSpace(ch)))
            {
                var idx = alphabet.IndexOf(c);
                if (idx < 0) continue;

                value = (value << 5) | idx;
                bits += 5;

                if (bits >= 8)
                {
                    bits -= 8;
                    output.Add((byte)((value >> bits) & 0xFF));
                }
            }

            return output.ToArray();
        }

        private static string GenerateTotp(string base32Secret, int digits = 6, long timestep = 30)
        {
            var secret = Base32Decode(base32Secret);
            var unixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var counter = unixTime / timestep;

            var counterBytes = BitConverter.GetBytes(counter);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(counterBytes);
            }

            using (var hmac = new HMACSHA1(secret))
            {
                var hash = hmac.ComputeHash(counterBytes);
                int offset = hash[hash.Length - 1] & 0x0F;
                int binaryCode = ((hash[offset] & 0x7f) << 24)
                                 | ((hash[offset + 1] & 0xff) << 16)
                                 | ((hash[offset + 2] & 0xff) << 8)
                                 | (hash[offset + 3] & 0xff);

                int otp = binaryCode % (int)Math.Pow(10, digits);
                return otp.ToString(new string('0', digits));
            }
        }
    }
}
