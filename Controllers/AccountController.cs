using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Security.Claims;
using System.Net;
using System.Net.Mail;
using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TestingDemo.Data;
using TestingDemo.Models;

public class AccountController : BaseController
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private static ConcurrentDictionary<string, (int FailCount, DateTime? BlockUntil)> _changePwAttempts = new();
    private readonly IConfiguration _config;
    private readonly IHubContext<NotificationHub> _hubContext;

    public AccountController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager, IConfiguration config, IHubContext<NotificationHub> hubContext)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _config = config;
        _hubContext = hubContext;
    }

    [AllowAnonymous] //Shows Login View
    public IActionResult Login()
    {
        if (TempData["Success"] != null)
            ViewBag.Success = TempData["Success"];
        return View();
    }

    [HttpPost] //Handles Login
    [AllowAnonymous]
    public async Task<IActionResult> Login(string email, string password)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user != null)
        {
            var roles = await _userManager.GetRolesAsync(user);
            bool isAdmin = roles.Contains("Admin");
            if (!isAdmin && !user.IsApproved)
            {
                ViewBag.Error = "Your account is pending admin approval. Please wait for approval.";
                return View();
            }
            var result = await _signInManager.PasswordSignInAsync(user, password, false, false);
            if (result.Succeeded)
            {
                // Notify all clients of a data change
                await _hubContext.Clients.All.SendAsync("ReceiveUpdate", "User logged in");

                if (roles.Contains("Finance"))
                    return RedirectToAction("Index", "Finance");
                else if (roles.Contains("PlanningOfficer"))
                    return RedirectToAction("Index", "PlanningOfficer");
                else if (roles.Contains("CustomerCare"))
                    return RedirectToAction("Index", "CustomerCare");
                else if (roles.Contains("DocumentOfficer"))
                    return RedirectToAction("Index", "DocumentOfficer");
                else
                    return RedirectToAction("Index", "Home"); // Default for Admin or others
            }
        }
        ViewBag.Error = "Invalid login attempt.";
        return View();
    }

    [AllowAnonymous] //Returns Login View after logout
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Login");
    }

    public IActionResult AccessDenied() //Shows access denied page
    {
        return View();
    }

    public IActionResult Settings() //Shows settings page
    {
        return View();
    }
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]

    public class DashboardController : Controller //Nested Control, s       hows Dashboard view
    {
        public IActionResult Index()
        {
            return View();
        }
    }

    [HttpGet] //Shows ChangePassword View
    public IActionResult ChangePassword()
    {
        return View(new ChangePasswordViewModel());
    }

    [HttpPost] //Main Logic for changing passwords
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            ModelState.AddModelError("", "User not found.");
            return View(model);
        }
        // Rate limiting
        var key = user.Id;
        if (_changePwAttempts.TryGetValue(key, out var entry) && entry.BlockUntil.HasValue && entry.BlockUntil > DateTime.Now)
        {
            ModelState.AddModelError("", $"Too many failed attempts. Try again at {entry.BlockUntil.Value:HH:mm:ss}.");
            return View(model);
        }
        // Verify current password
        if (!await _userManager.CheckPasswordAsync(user, model.CurrentPassword))
        {
            _changePwAttempts.AddOrUpdate(key, (1, null), (k, v) => (v.FailCount + 1, v.FailCount + 1 >= 5 ? DateTime.Now.AddMinutes(15) : null));
            ModelState.AddModelError("CurrentPassword", "Current password is incorrect.");
            return View(model);
        }
        // Password requirements
        if (string.IsNullOrWhiteSpace(model.NewPassword) || model.NewPassword.Length < 8 ||
            !model.NewPassword.Any(char.IsUpper) || !model.NewPassword.Any(char.IsLower) ||
            !model.NewPassword.Any(char.IsDigit) || !model.NewPassword.Any(ch => !char.IsLetterOrDigit(ch)))
        {
            ModelState.AddModelError("NewPassword", "Password must be at least 8 characters and include uppercase, lowercase, number, and special character.");
            return View(model);
        }
        if (model.NewPassword != model.ConfirmPassword)
        {
            ModelState.AddModelError("ConfirmPassword", "Passwords do not match.");
            return View(model);
        }
        // Generate OTP
        var otp = new Random().Next(100000, 999999).ToString();
        HttpContext.Session.SetString("ChangePwOtp", otp);
        HttpContext.Session.SetString("ChangePwNewPassword", model.NewPassword);
        HttpContext.Session.SetString("ChangePwCurrentPassword", model.CurrentPassword);
        HttpContext.Session.SetString("ChangePwOtpTime", DateTime.UtcNow.ToString("o"));
        // Email OTP
        try
        {
            var smtpSection = _config.GetSection("Smtp");
            var smtpHost = smtpSection["Host"] ?? "";
            var smtpPortStr = smtpSection["Port"];
            var smtpPort = 587;
            if (!string.IsNullOrEmpty(smtpPortStr) && int.TryParse(smtpPortStr, out var parsedPort))
                smtpPort = parsedPort;
            var smtpUser = smtpSection["Username"] ?? "";
            var smtpPass = smtpSection["Password"] ?? "";
            var smtpFrom = smtpSection["From"] ?? smtpUser;
            var toEmail = user.Email ?? "";
            var smtp = new SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new NetworkCredential(smtpUser, smtpPass),
                EnableSsl = true
            };
            var mail = new MailMessage(smtpFrom, toEmail)
            {
                Subject = "Password Change OTP",
                Body = $"Your password change code is: {otp}"
            };
            smtp.Send(mail);
        }
        catch (Exception ex) { System.IO.File.AppendAllText("email_error.log", ex.ToString() + "\n"); }
        TempData["OtpNotice"] = "A code has been sent to your email. Enter it to confirm your password change.";
        return RedirectToAction("ConfirmChangePassword");
    }

    [HttpGet] //Shows OTP Entry Page
    public IActionResult ConfirmChangePassword()
    {
        if (TempData["OtpNotice"] != null)
            ViewBag.OtpNotice = TempData["OtpNotice"];
        return View();
    }

    [HttpPost] //Verifies OTP
    public async Task<IActionResult> ConfirmChangePassword(string otp)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            ModelState.AddModelError("", "User not found.");
            return View();
        }
        var expectedOtp = HttpContext.Session.GetString("ChangePwOtp");
        var newPassword = HttpContext.Session.GetString("ChangePwNewPassword");
        var currentPassword = HttpContext.Session.GetString("ChangePwCurrentPassword");
        var otpTimeStr = HttpContext.Session.GetString("ChangePwOtpTime");
        if (expectedOtp == null || newPassword == null || currentPassword == null || otpTimeStr == null)
        {
            ModelState.AddModelError("", "OTP session expired. Please try again.");
            return View();
        }
        if ((DateTime.UtcNow - DateTime.Parse(otpTimeStr)).TotalMinutes > 2)
        {
            ModelState.AddModelError("", "OTP expired. Please try again.");
            return View();
        }
        if (otp != expectedOtp)
        {
            TempData["OtpError"] = "The OTP code you entered is incorrect. Please try again.";
            ModelState.AddModelError("", "Invalid code. Please check your email and try again.");
            return View();
        }
        // Change password
        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
        {
            ModelState.AddModelError("", string.Join("; ", result.Errors.Select(e => e.Description)));
            return View();
        }
        // Reset rate limit
        _changePwAttempts.TryRemove(user.Id, out _);
        // Audit log
        System.IO.File.AppendAllText("password_change_audit.log", $"{DateTime.Now:u} | {user.Email} | {Request.HttpContext.Connection.RemoteIpAddress} | {Request.Headers["User-Agent"]} | OTP\n");
        // Email notification
        try
        {
            var smtpSection = _config.GetSection("Smtp");
            var smtpHost = smtpSection["Host"] ?? "";
            var smtpPortStr = smtpSection["Port"];
            var smtpPort = 587;
            if (!string.IsNullOrEmpty(smtpPortStr) && int.TryParse(smtpPortStr, out var parsedPort))
                smtpPort = parsedPort;
            var smtpUser = smtpSection["Username"] ?? "";
            var smtpPass = smtpSection["Password"] ?? "";
            var smtpFrom = smtpSection["From"] ?? smtpUser;
            var toEmail = user.Email ?? "";
            var smtp = new SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new NetworkCredential(smtpUser, smtpPass),
                EnableSsl = true
            };
            var mail = new MailMessage(smtpFrom, toEmail)
            {
                Subject = "Password Changed",
                Body = "Your password was changed. If this wasn't you, please contact support."
            };
            smtp.Send(mail);
        }
        catch (Exception ex) { System.IO.File.AppendAllText("email_error.log", ex.ToString() + "\n"); }
        // Sign out all sessions except current
        await _signInManager.RefreshSignInAsync(user);
        // Clear session
        HttpContext.Session.Remove("ChangePwOtp");
        HttpContext.Session.Remove("ChangePwNewPassword");
        HttpContext.Session.Remove("ChangePwCurrentPassword");
        HttpContext.Session.Remove("ChangePwOtpTime");
        ViewBag.Success = "Password changed successfully.";
        TempData["PasswordChanged"] = "Your password has been changed successfully!";
        return RedirectToAction("Settings");
    }

    [HttpPost] //Generates and resets OTP email (with a 3-per-10 minute rate limit)
    public IActionResult ResendChangePasswordOtp()
    {
        var user = _userManager.GetUserAsync(User).Result;
        if (user == null)
        {
            TempData["OtpNotice"] = "User not found.";
            return RedirectToAction("ConfirmChangePassword");
        }
        // Rate limit: allow max 3 resends per 10 minutes
        var resendKey = $"ResendOtp_{user.Id}";
        var resendCount = HttpContext.Session.GetInt32(resendKey) ?? 0;
        var resendTimeKey = $"ResendOtpTime_{user.Id}";
        var lastResendStr = HttpContext.Session.GetString(resendTimeKey);
        if (lastResendStr != null && DateTime.TryParse(lastResendStr, out var lastResend))
        {
            if ((DateTime.UtcNow - lastResend).TotalMinutes < 10 && resendCount >= 3)
            {
                TempData["OtpNotice"] = "You have reached the maximum number of resends. Please try again later.";
                return RedirectToAction("ConfirmChangePassword");
            }
            if ((DateTime.UtcNow - lastResend).TotalMinutes >= 10)
            {
                resendCount = 0; // Reset after 10 minutes
            }
        }
        // Generate new OTP
        var otp = new Random().Next(100000, 999999).ToString();
        HttpContext.Session.SetString("ChangePwOtp", otp);
        HttpContext.Session.SetString("ChangePwOtpTime", DateTime.UtcNow.ToString("o"));
        // Email OTP
        try
        {
            var smtpSection = _config.GetSection("Smtp");
            var smtpHost = smtpSection["Host"] ?? "";
            var smtpPortStr = smtpSection["Port"];
            var smtpPort = 587;
            if (!string.IsNullOrEmpty(smtpPortStr) && int.TryParse(smtpPortStr, out var parsedPort))
                smtpPort = parsedPort;
            var smtpUser = smtpSection["Username"] ?? "";
            var smtpPass = smtpSection["Password"] ?? "";
            var smtpFrom = smtpSection["From"] ?? smtpUser;
            var toEmail = user.Email ?? "";
            var smtp = new SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new NetworkCredential(smtpUser, smtpPass),
                EnableSsl = true
            };
            var mail = new MailMessage(smtpFrom, toEmail)
            {
                Subject = "Password Change OTP",
                Body = $"Your new password change code is: {otp}"
            };
            smtp.Send(mail);
        }
        catch (Exception ex) { System.IO.File.AppendAllText("email_error.log", ex.ToString() + "\n"); }
        HttpContext.Session.SetInt32(resendKey, resendCount + 1);
        HttpContext.Session.SetString(resendTimeKey, DateTime.UtcNow.ToString("o"));
        TempData["OtpNotice"] = "A new code has been sent to your email.";
        return RedirectToAction("ConfirmChangePassword");
    }

    [HttpGet] //Shows ForgotPassword View
    [AllowAnonymous]
    public IActionResult ForgotPassword()
    {
        return View();
    }

    [HttpPost] //Handles ForgotPassword (Guest Mode)
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            ViewBag.Error = "No account found with that email.";
            return View();
        }
        if (!user.IsApproved)
        {
            ViewBag.Error = "Your account is pending admin approval. You cannot reset your password until it is approved.";
            return View();
        }
        // Generate OTP
        var otp = new Random().Next(100000, 999999).ToString();
        HttpContext.Session.SetString("ResetPwOtp", otp);
        HttpContext.Session.SetString("ResetPwEmail", email);
        HttpContext.Session.SetString("ResetPwOtpTime", DateTime.UtcNow.ToString("o"));
        // Email OTP
        try
        {
            var smtpSection = _config.GetSection("Smtp");
            var smtpHost = smtpSection["Host"] ?? "";
            var smtpPortStr = smtpSection["Port"];
            var smtpPort = 587;
            if (!string.IsNullOrEmpty(smtpPortStr) && int.TryParse(smtpPortStr, out var parsedPort))
                smtpPort = parsedPort;
            var smtpUser = smtpSection["Username"] ?? "";
            var smtpPass = smtpSection["Password"] ?? "";
            var smtpFrom = smtpSection["From"] ?? smtpUser;
            var toEmail = user.Email ?? "";
            var smtp = new SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new NetworkCredential(smtpUser, smtpPass),
                EnableSsl = true
            };
            var mail = new MailMessage(smtpFrom, toEmail)
            {
                Subject = "Password Reset OTP",
                Body = $"Your password reset code is: {otp}"
            };
            smtp.Send(mail);
        }
        catch (Exception ex) { System.IO.File.AppendAllText("email_error.log", ex.ToString() + "\n"); }
        TempData["OtpNotice"] = "A code has been sent to your email. Enter it to continue.";
        return RedirectToAction("ResetPasswordOtp");
    }

    [HttpGet] //Shows OTP Entry form (Guest Mode)
    [AllowAnonymous]
    public IActionResult ResetPasswordOtp()
    {
        if (TempData["OtpNotice"] != null)
            ViewBag.OtpNotice = TempData["OtpNotice"];
        return View();
    }

    [HttpPost] //Verifies OTP (Guest Mode)
    [AllowAnonymous]
    public IActionResult ResetPasswordOtp(string otp)
    {
        var expectedOtp = HttpContext.Session.GetString("ResetPwOtp");
        var otpTimeStr = HttpContext.Session.GetString("ResetPwOtpTime");
        if (expectedOtp == null || otpTimeStr == null)
        {
            ModelState.AddModelError("", "OTP session expired. Please try again.");
            return View();
        }
        if ((DateTime.UtcNow - DateTime.Parse(otpTimeStr)).TotalMinutes > 5)
        {
            ModelState.AddModelError("", "OTP expired. Please try again.");
            return View();
        }
        if (otp != expectedOtp)
        {
            ModelState.AddModelError("", "Invalid code. Please check your email and try again.");
            return View();
        }
        // OTP is valid, set session flag and redirect to new password page
        HttpContext.Session.SetString("ResetPwOtpVerified", "true");
        return RedirectToAction("ResetPasswordNew");
    }

    [HttpGet] //Shows NewPassword View only when OTP is verified
    [AllowAnonymous]
    public IActionResult ResetPasswordNew()
    {
        // Only allow if OTP was just verified
        if (HttpContext.Session.GetString("ResetPwOtpVerified") != "true")
        {
            return RedirectToAction("ForgotPassword");
        }
        return View();
    }

    [HttpPost] //Handles password reset
    [AllowAnonymous]
    public async Task<IActionResult> ResetPasswordNew(string newPassword, string confirmPassword)
    {
        if (HttpContext.Session.GetString("ResetPwOtpVerified") != "true")
        {
            return RedirectToAction("ForgotPassword");
        }
        var email = HttpContext.Session.GetString("ResetPwEmail");
        if (email == null)
        {
            return RedirectToAction("ForgotPassword");
        }
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8 ||
            !newPassword.Any(char.IsUpper) || !newPassword.Any(char.IsLower) ||
            !newPassword.Any(char.IsDigit) || !newPassword.Any(ch => !char.IsLetterOrDigit(ch)))
        {
            ModelState.AddModelError("NewPassword", "Password must be at least 8 characters and include uppercase, lowercase, number, and special character.");
            return View();
        }
        if (newPassword != confirmPassword)
        {
            ModelState.AddModelError("ConfirmPassword", "Passwords do not match.");
            return View();
        }
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            ModelState.AddModelError("", "User not found.");
            return View();
        }
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
        {
            ModelState.AddModelError("", string.Join("; ", result.Errors.Select(e => e.Description)));
            return View();
        }
        // Set IsApproved = false ONLY after password reset
        user.IsApproved = false;
        await _userManager.UpdateAsync(user);
        // Clear session
        HttpContext.Session.Remove("ResetPwOtp");
        HttpContext.Session.Remove("ResetPwEmail");
        HttpContext.Session.Remove("ResetPwOtpTime");
        HttpContext.Session.Remove("ResetPwOtpVerified");
        TempData["Success"] = "Your account changes will need approval of the administrator. Please contact the authorized person.";
        return RedirectToAction("Login");
    }
}