using Ferre.Models;
using Ferre.Filters;
using Ferre.Models.Auth;
using Ferre.Models.Catalog;
using Ferre.Models.Support;
using Ferre.Models.Ui;
using Ferre.Options;
using Ferre.Services.Auth;
using Ferre.Services.Catalog;
using Ferre.Services.Notifications;
using Ferre.Services.Orders;
using Ferre.Services.Support;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace Ferre.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ISupabaseAuthService _authService;
        private readonly ICategoryService _categoryService;
        private readonly IProductService _productService;
        private readonly INotificationService _notificationService;
        private readonly IClientPurchaseService _clientPurchaseService;
        private readonly IPurchaseReceiptPdfService _purchaseReceiptPdfService;
        private readonly IClientContactMessageService _clientContactMessageService;
        private readonly IAdminPermissionService _adminPermissionService;
        private readonly IDataProtector _rememberMeProtector;
        private readonly IDataProtector _rememberedAccountsProtector;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly SupabaseSettings _supabaseSettings;
        private readonly PayPalSettings _payPalSettings;

        private const string ProductImagesBucket = "Imagenes";
        private const string SupportStatusPending = "pendiente";
        private const string SupportStatusResolved = "resuelto";

        public HomeController(
            ILogger<HomeController> logger,
            ISupabaseAuthService authService,
            ICategoryService categoryService,
            IProductService productService,
            INotificationService notificationService,
            IClientPurchaseService clientPurchaseService,
            IPurchaseReceiptPdfService purchaseReceiptPdfService,
            IClientContactMessageService clientContactMessageService,
            IAdminPermissionService adminPermissionService,
            IDataProtectionProvider dataProtectionProvider,
            IHttpClientFactory httpClientFactory,
            IOptions<SupabaseSettings> supabaseSettings,
            IOptions<PayPalSettings> payPalSettings)
        {
            _logger = logger;
            _authService = authService;
            _categoryService = categoryService;
            _productService = productService;
            _notificationService = notificationService;
            _clientPurchaseService = clientPurchaseService;
            _purchaseReceiptPdfService = purchaseReceiptPdfService;
            _clientContactMessageService = clientContactMessageService;
            _adminPermissionService = adminPermissionService;
            _rememberMeProtector = dataProtectionProvider.CreateProtector(RememberMeConstants.ProtectorPurpose);
            _rememberedAccountsProtector = dataProtectionProvider.CreateProtector(RememberMeConstants.AccountsProtectorPurpose);
            _httpClientFactory = httpClientFactory;
            _supabaseSettings = supabaseSettings.Value;
            _payPalSettings = payPalSettings.Value;
        }

        public IActionResult Index()
        {
            return View();
        }
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            var redirect = RedirectIfAuthenticated();
            if (redirect is not null)
            {
                return redirect;
            }

            var rememberedAccounts = GetRememberedAccounts();
            var lastAccount = rememberedAccounts.FirstOrDefault();

            return View(new LoginViewModel
            {
                Email = lastAccount?.Email ?? string.Empty,
                RememberMe = lastAccount is not null,
                RememberedAccounts = rememberedAccounts,
                ReturnUrl = returnUrl
            });
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View(new ForgotPasswordViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _authService.SendPasswordResetAsync(model.Email);
            if (!result.Succeeded)
            {
                ViewBag.StatusMessage = result.ErrorMessage;
                return View(model);
            }

            TempData["StatusMessage"] = "Te enviamos un código de 6 dígitos para restablecer tu contraseña.";
            return RedirectToAction(nameof(ResetPassword), new { email = model.Email });
        }

        [HttpGet]
        public IActionResult ResetPassword(string? email = null, string? accessToken = null, string? refreshToken = null)
        {
            var model = new ResetPasswordViewModel
            {
                Email = email ?? string.Empty,
                AccessToken = accessToken,
                RefreshToken = refreshToken
            };

            ViewBag.StatusMessage = TempData["StatusMessage"];

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            AuthResult result;
            if (!string.IsNullOrWhiteSpace(model.AccessToken))
            {
                result = await _authService.ResetPasswordAsync(model.AccessToken, model.RefreshToken, model.Password);
            }
            else
            {
                result = await _authService.ResetPasswordWithRecoveryCodeAsync(model.Email!, model.RecoveryCode!, model.Password);
            }

            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "No fue posible actualizar la contraseña.");
                return View(model);
            }

            TempData["LoginSuccessMessage"] = "Contraseña actualizada correctamente. Ya puedes iniciar sesión.";
            return RedirectToAction(nameof(Login));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _authService.SignInAsync(model.Email, model.Password);
            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Credenciales inválidas.");
                return View(model);
            }

            HttpContext.Session.SetString("IsAuthenticated", "true");
            HttpContext.Session.SetString("UserRole", result.Role ?? "cliente");
            HttpContext.Session.SetString("UserEmail", model.Email);
            var displayName = string.Join(" ", new[] { result.FirstName, result.LastName }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
            HttpContext.Session.SetString("UserName", string.IsNullOrWhiteSpace(displayName) ? model.Email : displayName);

            if (model.RememberMe)
            {
                var payload = new RememberMePayload
                {
                    UserRole = result.Role ?? "cliente",
                    UserEmail = model.Email,
                    UserName = string.IsNullOrWhiteSpace(displayName) ? model.Email : displayName
                };

                var protectedPayload = _rememberMeProtector.Protect(JsonSerializer.Serialize(payload));
                HttpContext.Response.Cookies.Append(RememberMeConstants.CookieName, protectedPayload, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = HttpContext.Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    IsEssential = true,
                    Expires = DateTimeOffset.UtcNow.AddDays(30)
                });

                SaveRememberedAccount(model.Email, string.IsNullOrWhiteSpace(displayName) ? model.Email : displayName);
            }
            else
            {
                HttpContext.Response.Cookies.Delete(RememberMeConstants.CookieName);
                RemoveRememberedAccount(model.Email);
            }

            if (string.Equals(result.Role, "cliente", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(model.ReturnUrl)
                && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }

            return result.Role?.ToLowerInvariant() switch
            {
                "vendedor" => RedirectToAction(nameof(Vendedor)),
                "administrador" => RedirectToAction(nameof(Admin)),
                "cliente" => RedirectToAction(nameof(Portada))
            };
        }
        /*rol*/
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireSession("administrador")]
        public async Task<IActionResult> UpdateUserRole(string userId, string role)
        {
            if (!CanAccessAdminArea(AdminAreas.Users))
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(role))
            {
                return BadRequest("Parámetros inválidos.");
            }

            if (string.Equals(role, "administrador", StringComparison.OrdinalIgnoreCase)
                && !HasFullAdminPermissions())
            {
                return BadRequest("No tienes todos los permisos para asignar el rol administrador.");
            }

            var result = await _authService.UpdateUserRoleAsync(userId, role);
            if (!result.Succeeded)
            {
                return BadRequest(result.ErrorMessage);
            }

            return Ok(new { userId, role = result.Role });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireSession("administrador")]
        public async Task<IActionResult> CreateUser(AdminUserCreateModel model)
        {
            if (!CanAccessAdminArea(AdminAreas.Users))
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                var validationErrors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .Where(message => !string.IsNullOrWhiteSpace(message))
                    .ToArray();

                return BadRequest(new
                {
                    succeeded = false,
                    errorMessage = validationErrors.FirstOrDefault() ?? "Datos inválidos para crear el usuario."
                });
            }

            var existingProfile = await _authService.GetClientProfileByEmailAsync(model.Email.Trim());
            if (existingProfile is not null)
            {
                return BadRequest(new
                {
                    succeeded = false,
                    errorMessage = "El correo ya está registrado. Usa otro correo para crear el usuario."
                });
            }

            var result = await _authService.SignUpAsync(new RegisterUserRequest(
                model.Email,
                model.Password,
                model.FirstName,
                model.LastName,
                model.Phone ?? string.Empty));

            if (!result.Succeeded)
            {
                return BadRequest(new
                {
                    succeeded = false,
                    errorMessage = result.ErrorMessage ?? "No se pudo registrar el usuario."
                });
            }

            var normalizedRole = NormalizeRole(model.Role);
            if (string.Equals(normalizedRole, "administrador", StringComparison.OrdinalIgnoreCase)
                && !HasFullAdminPermissions())
            {
                return BadRequest(new
                {
                    succeeded = false,
                    errorMessage = "No tienes todos los permisos para crear administradores."
                });
            }

            var roleResult = await _authService.UpdateUserRoleAsync(result.UserId!, normalizedRole);
            if (!roleResult.Succeeded)
            {
                return BadRequest(new
                {
                    succeeded = false,
                    errorMessage = roleResult.ErrorMessage ?? "No se pudo asignar el rol al usuario."
                });
            }

            var createdUser = new AdminUserViewModel
            {
                Id = result.UserId ?? string.Empty,
                FirstName = model.FirstName,
                LastName = model.LastName,
                Email = model.Email,
                Phone = model.Phone,
                Role = normalizedRole
            };

            var notification = _notificationService.Add($"Usuario registrado: {createdUser.FirstName} {createdUser.LastName}".Trim());
            var unreadCount = _notificationService.GetAll().Count(x => !x.IsRead);

            return Json(new
            {
                succeeded = true,
                user = createdUser,
                notification,
                unreadCount
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireSession("administrador")]
        public async Task<IActionResult> UpdateAdminUser(AdminUserUpdateModel model)
        {
            if (!CanAccessAdminArea(AdminAreas.Users))
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                var validationErrors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .Where(message => !string.IsNullOrWhiteSpace(message))
                    .ToArray();

                return BadRequest(new
                {
                    succeeded = false,
                    errorMessage = validationErrors.FirstOrDefault() ?? "Datos inválidos para actualizar el usuario."
                });
            }

            model.Role = NormalizeRole(model.Role);
            var existingUsers = await _authService.GetUsersByRolesAsync(new[] { "administrador", "vendedor", "cliente" });
            var existingUser = existingUsers.FirstOrDefault(x => string.Equals(x.Id, model.Id, StringComparison.OrdinalIgnoreCase));
            if (existingUser is null)
            {
                return BadRequest(new { succeeded = false, errorMessage = "No se encontró el usuario a actualizar." });
            }

            var existingRole = NormalizeRole(existingUser.Role);
            var isPromotingToAdmin = !string.Equals(existingRole, "administrador", StringComparison.OrdinalIgnoreCase)
                && string.Equals(model.Role, "administrador", StringComparison.OrdinalIgnoreCase);

            if (isPromotingToAdmin && !HasFullAdminPermissions())
            {
                return BadRequest(new { succeeded = false, errorMessage = "No tienes todos los permisos para asignar el rol administrador." });
            }

            var result = await _authService.UpdateUserAsync(model);
            if (!result.Succeeded)
            {
                return BadRequest(new { succeeded = false, errorMessage = result.ErrorMessage });
            }

            var updatedUser = new AdminUserViewModel
            {
                Id = model.Id,
                FirstName = model.FirstName,
                LastName = model.LastName,
                Email = model.Email,
                Phone = model.Phone,
                Role = model.Role
            };

            var notification = _notificationService.Add($"Usuario actualizado: {updatedUser.FirstName} {updatedUser.LastName}".Trim());
            var unreadCount = _notificationService.GetAll().Count(x => !x.IsRead);

            return Json(new
            {
                succeeded = true,
                user = updatedUser,
                notification,
                unreadCount
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireSession("administrador")]
        public async Task<IActionResult> DeleteAdminUser(string id)
        {
            if (!CanAccessAdminArea(AdminAreas.Users))
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                return BadRequest(new { succeeded = false, errorMessage = "Usuario inválido." });
            }

            var allUsers = await _authService.GetUsersByRolesAsync(new[] { "administrador", "vendedor", "cliente" });
            var targetUser = allUsers.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
            if (targetUser is null)
            {
                return BadRequest(new { succeeded = false, errorMessage = "No se encontró el usuario a eliminar." });
            }

            var currentEmail = GetCurrentUserEmail();
            if (string.Equals(targetUser.Email, currentEmail, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { succeeded = false, errorMessage = "No puedes eliminar tu propio usuario mientras tienes la sesión activa." });
            }

            if (_adminPermissionService.IsSuperAdmin(targetUser.Email))
            {
                return BadRequest(new { succeeded = false, errorMessage = "No se puede eliminar el administrador principal del sistema." });
            }

            var result = await _authService.DeleteUserAsync(id);
            if (!result.Succeeded)
            {
                return BadRequest(new { succeeded = false, errorMessage = result.ErrorMessage });
            }

            var notification = _notificationService.Add("Usuario eliminado.");
            var unreadCount = _notificationService.GetAll().Count(x => !x.IsRead);

            return Json(new { succeeded = true, notification, unreadCount });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireSession("administrador")]
        public IActionResult UpdateAdminPermissions(string email, bool dashboard, bool products, bool inventory, bool users, bool categories, bool permissions, bool orders, bool support, bool allPermissions)
        {
            if (!CanAccessAdminArea(AdminAreas.Permissions))
            {
                TempData["ErrorMessage"] = "No tienes permisos para modificar permisos de administradores.";
                return Redirect($"{Url.Action(nameof(Admin))}#permissions");
            }

            var targetEmail = email?.Trim();
            if (string.IsNullOrWhiteSpace(targetEmail))
            {
                TempData["ErrorMessage"] = "Debe seleccionar un administrador válido.";
                return Redirect($"{Url.Action(nameof(Admin))}#permissions");
            }

            var permissionMap = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
            {
                [AdminAreas.Dashboard] = dashboard,
                [AdminAreas.Products] = products,
                [AdminAreas.Inventory] = inventory,
                [AdminAreas.Users] = users,
                [AdminAreas.Categories] = categories,
                [AdminAreas.Permissions] = permissions,
                [AdminAreas.Orders] = orders,
                [AdminAreas.Support] = support
            };

            if (allPermissions)
            {
                foreach (var area in AdminAreas.All)
                {
                    permissionMap[area] = true;
                }
            }

            if (!permissionMap.Values.Any(x => x))
            {
                TempData["ErrorMessage"] = "Debes habilitar al menos un área para el administrador.";
                return Redirect($"{Url.Action(nameof(Admin))}#permissions");
            }

            _adminPermissionService.SavePermissions(targetEmail, permissionMap);
            TempData["SuccessMessage"] = "Permisos actualizados correctamente.";
            return Redirect($"{Url.Action(nameof(Admin))}#permissions");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireSession("administrador", "vendedor")]
        public IActionResult MarkNotificationAsRead(Guid id)
        {
            if (id == Guid.Empty)
            {
                return BadRequest(new { succeeded = false, errorMessage = "Notificación inválida." });
            }

            var unreadCount = _notificationService.MarkAsRead(id);
            return Json(new { succeeded = true, unreadCount });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireSession("administrador", "vendedor")]
        public IActionResult ClearNotifications()
        {
            _notificationService.Clear();
            return Json(new { succeeded = true, unreadCount = 0 });
        }

        [HttpGet]
        public IActionResult Cuenta()
        {
            return View(new RegisterViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cuenta(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _authService.SignUpAsync(new RegisterUserRequest(
                model.Email,
                model.Password,
                model.FirstName,
                model.LastName,
                model.Phone));

            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "No se pudo completar el registro.");
                return View(model);
            }

            TempData["LoginSuccessMessage"] = "Registro de usuario exitoso.";
            return RedirectToAction(nameof(Login));
        }
        public async Task<IActionResult> Portada()
        {
            var isAuthenticated = string.Equals(HttpContext.Session.GetString("IsAuthenticated"), "true", StringComparison.Ordinal);
            var sessionEmail = HttpContext.Session.GetString("UserEmail") ?? string.Empty;
            var sessionName = HttpContext.Session.GetString("UserName") ?? string.Empty;
            var sessionRole = HttpContext.Session.GetString("UserRole") ?? "cliente";
            var categories = await _categoryService.GetAllAsync();
            var products = await _productService.GetAllAsync();
            var categoriesById = categories.ToDictionary(x => x.Id, x => x.Name);
            var orderedProducts = products
                .OrderBy(product => categoriesById.TryGetValue(product.CategoryId, out var categoryName) ? categoryName : "Sin categoría")
                .ThenBy(product => product.Name)
                .ToList();

            ClientProfileViewModel profile;
            if (isAuthenticated)
            {
                profile = await _authService.GetClientProfileByEmailAsync(sessionEmail) ?? new ClientProfileViewModel
                {
                    Email = sessionEmail,
                    Role = sessionRole
                };

                var splitName = sessionName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (splitName.Length > 0)
                {
                    profile.FirstName = splitName[0];
                    if (splitName.Length > 1)
                    {
                        profile.LastName = string.Join(' ', splitName.Skip(1));
                    }
                }
            }
            else
            {
                profile = new ClientProfileViewModel
                {
                    FirstName = "Invitado",
                    LastName = string.Empty,
                    Email = string.Empty,
                    Role = "cliente"
                };
            }

            profile.Categories = categories;
            profile.Products = orderedProducts;

            ViewData["IsAuthenticated"] = isAuthenticated;
            ViewData["LoginUrl"] = Url.Action(nameof(Login), "Home", new { returnUrl = Url.Action(nameof(Portada), "Home") });
            ViewData["PayPalClientId"] = _payPalSettings.ClientId;
            ViewData["PayPalEnabled"] = IsPayPalConfigured();

            return View(profile);
        }

        [HttpGet]
        public async Task<IActionResult> GetCatalogProducts()
        {
            var categories = await _categoryService.GetAllAsync();
            var products = await _productService.GetAllAsync();
            var categoriesById = categories.ToDictionary(x => x.Id, x => x.Name);

            var payload = products
                .Select(product => new
                {
                    id = product.Id,
                    name = product.Name,
                    category = categoriesById.TryGetValue(product.CategoryId, out var categoryName) ? categoryName : "Sin categoría",
                    price = product.Price,
                    image = product.ImageUrl1,
                    image2 = product.ImageUrl2,
                    image3 = product.ImageUrl3,
                    description = product.Description,
                    rating = 4.5,
                    stock = product.Stock,
                    minStock = product.MinStock
                })
                .OrderBy(product => product.category)
                .ThenBy(product => product.name)
                .ToList();

            return Json(new { succeeded = true, products = payload });
        }

        [HttpPost]
        [RequireSession]
        public async Task<IActionResult> CreatePayPalOrder([FromBody] CartCheckoutRequest request)
        {
            if (!IsPayPalConfigured())
            {
                return BadRequest(new { succeeded = false, errorMessage = "PayPal no está configurado en el servidor." });
            }

            var linesResult = await GetCheckoutLinesAsync(request?.Items);
            if (!linesResult.Succeeded)
            {
                return BadRequest(new { succeeded = false, errorMessage = linesResult.ErrorMessage });
            }

            var total = linesResult.Lines.Sum(line => line.Product.Price * line.Quantity);
            if (total <= 0)
            {
                return BadRequest(new { succeeded = false, errorMessage = "El total de la orden no es válido." });
            }

            var (succeeded, orderId, errorMessage) = await CreatePayPalOrderAsync(total);
            if (!succeeded)
            {
                return BadRequest(new { succeeded = false, errorMessage = errorMessage ?? "No se pudo iniciar el pago con PayPal." });
            }

            return Json(new { succeeded = true, orderId });
        }

        [HttpPost]
        [RequireSession]
        public async Task<IActionResult> CheckoutCart([FromBody] CartCheckoutRequest request)
        {
            var paymentValidationMessage = ValidatePaymentData(request);
            if (!string.IsNullOrWhiteSpace(paymentValidationMessage))
            {
                return BadRequest(new { succeeded = false, errorMessage = paymentValidationMessage });
            }

            var linesResult = await GetCheckoutLinesAsync(request?.Items);
            if (!linesResult.Succeeded)
            {
                return BadRequest(new { succeeded = false, errorMessage = linesResult.ErrorMessage });
            }

            var paymentMethod = request.PaymentMethod.Trim().ToLowerInvariant();
            if (paymentMethod == "paypal")
            {
                var orderId = request.PayPal?.OrderId?.Trim() ?? string.Empty;
                var captureResult = await CapturePayPalOrderAsync(orderId);
                if (!captureResult.Succeeded)
                {
                    return BadRequest(new { succeeded = false, errorMessage = captureResult.ErrorMessage ?? "No se pudo confirmar el pago con PayPal." });
                }
            }

            foreach (var line in linesResult.Lines)
            {
                var product = line.Product;

                var updateModel = new ProductFormModel
                {
                    Id = product.Id,
                    Name = product.Name,
                    Price = product.Price,
                    Stock = product.Stock - line.Quantity,
                    MinStock = product.MinStock,
                    Description = product.Description,
                    CategoryId = product.CategoryId,
                    ImageUrl1 = product.ImageUrl1,
                    ImageUrl2 = product.ImageUrl2,
                    ImageUrl3 = product.ImageUrl3
                };

                var updateResult = await _productService.UpdateAsync(updateModel);
                if (!updateResult.Succeeded)
                {
                    return BadRequest(new { succeeded = false, errorMessage = updateResult.ErrorMessage ?? "No se pudo procesar la compra." });
                }
            }

            var userEmail = HttpContext.Session.GetString("UserEmail") ?? string.Empty;
            var purchaseResult = await _clientPurchaseService.RegisterPurchaseAsync(userEmail, paymentMethod, linesResult.Lines);
            if (!purchaseResult.Succeeded || purchaseResult.Receipt is null)
            {
                return BadRequest(new { succeeded = false, errorMessage = purchaseResult.ErrorMessage ?? "No se pudo generar el comprobante de compra." });
            }

            var paymentLabel = request.PaymentMethod.Trim().ToLowerInvariant() switch
            {
                "tarjeta" => "tarjeta",
                "paypal" => "PayPal",
                "efectivo" => "efectivo",
                _ => "método seleccionado"
            };

            return Json(new
            {
                succeeded = true,
                successMessage = $"Compra confirmada con {paymentLabel}.",
                receipt = purchaseResult.Receipt
            });
        }

        [HttpGet]
        [RequireSession]
        public async Task<IActionResult> GetPurchaseHistory()
        {
            var userEmail = HttpContext.Session.GetString("UserEmail") ?? string.Empty;
            var purchases = await _clientPurchaseService.GetPurchaseHistoryAsync(userEmail);
            return Json(new { succeeded = true, purchases });
        }

        [HttpGet]
        [RequireSession]
        public async Task<IActionResult> DownloadPurchaseReceiptPdf(Guid receiptId)
        {
            if (receiptId == Guid.Empty)
            {
                return BadRequest("Comprobante inválido.");
            }

            var userEmail = HttpContext.Session.GetString("UserEmail") ?? string.Empty;
            var userName = HttpContext.Session.GetString("UserName") ?? string.Empty;
            var receipt = await _clientPurchaseService.GetPurchaseByIdAsync(userEmail, receiptId);
            if (receipt is null)
            {
                return NotFound("No se encontró el comprobante solicitado.");
            }

            var pdfBytes = _purchaseReceiptPdfService.Generate(receipt, userEmail, userName);
            var sanitizedNumber = Regex.Replace(receipt.ReceiptNumber ?? "Comprobante", "[^a-zA-Z0-9_-]", string.Empty);
            var fileName = $"{(string.IsNullOrWhiteSpace(sanitizedNumber) ? "Comprobante" : sanitizedNumber)}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }

        [HttpGet]
        [RequireSession("administrador", "vendedor")]
        public async Task<IActionResult> PreviewOrderReceiptPdf(Guid receiptId)
        {
            if (receiptId == Guid.Empty)
            {
                return BadRequest("Comprobante inválido.");
            }

            var receipt = (await _clientPurchaseService.GetAllPurchasesAsync())
                .FirstOrDefault(x => x.Id == receiptId);
            if (receipt is null)
            {
                return NotFound("No se encontró el comprobante solicitado.");
            }

            var customerEmail = string.IsNullOrWhiteSpace(receipt.UserEmail) ? "cliente@nexoferretero.local" : receipt.UserEmail;
            var pdfBytes = _purchaseReceiptPdfService.Generate(receipt, customerEmail, customerEmail);
            Response.Headers.ContentDisposition = "inline";
            return File(pdfBytes, "application/pdf");
        }

        [HttpGet]
        [RequireSession("administrador", "vendedor")]
        public async Task<IActionResult> DownloadOrderReceiptPdf(Guid receiptId)
        {
            if (receiptId == Guid.Empty)
            {
                return BadRequest("Comprobante inválido.");
            }

            var receipt = (await _clientPurchaseService.GetAllPurchasesAsync())
                .FirstOrDefault(x => x.Id == receiptId);
            if (receipt is null)
            {
                return NotFound("No se encontró el comprobante solicitado.");
            }

            var customerEmail = string.IsNullOrWhiteSpace(receipt.UserEmail) ? "cliente@nexoferretero.local" : receipt.UserEmail;
            var pdfBytes = _purchaseReceiptPdfService.Generate(receipt, customerEmail, customerEmail);
            var sanitizedNumber = Regex.Replace(receipt.ReceiptNumber ?? "Comprobante", "[^a-zA-Z0-9_-]", string.Empty);
            var fileName = $"{(string.IsNullOrWhiteSpace(sanitizedNumber) ? "Comprobante" : sanitizedNumber)}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireSession("administrador")]
        public async Task<IActionResult> CancelOrder(Guid receiptId, string? ordersDate)
        {
            if (!CanAccessAdminArea(AdminAreas.Orders))
            {
                TempData["ErrorMessage"] = "No tienes permisos para anular pedidos.";
                return RedirectToAction(nameof(Admin), new { section = "orders", ordersDate });
            }

            var result = await _clientPurchaseService.CancelPurchaseAsync(receiptId);
            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = result.ErrorMessage ?? "No se pudo anular el pedido.";
                return RedirectToAction(nameof(Admin), new { section = "orders", ordersDate });
            }

            TempData["SuccessMessage"] = "Pedido anulado correctamente.";
            return RedirectToAction(nameof(Admin), new { section = "orders", ordersDate });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireSession]
        public async Task<IActionResult> ContactStore([FromForm] ClientContactFormRequest request)
        {
            var isAjaxRequest = string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

            if (!ModelState.IsValid)
            {
                var validationMessage = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                    ?? "No se pudo enviar tu solicitud de contacto.";

                if (isAjaxRequest)
                {
                    return BadRequest(new { succeeded = false, errorMessage = validationMessage });
                }

                TempData["ErrorMessage"] = validationMessage;
                return RedirectToAction(nameof(Portada));
            }

            var sessionEmail = HttpContext.Session.GetString("UserEmail") ?? string.Empty;
            if (!string.Equals(sessionEmail, request.Email?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                if (isAjaxRequest)
                {
                    return BadRequest(new { succeeded = false, errorMessage = "No fue posible validar el remitente de la consulta." });
                }

                TempData["ErrorMessage"] = "No fue posible validar el remitente de la consulta.";
                return RedirectToAction(nameof(Portada));
            }

            var cleanSubject = request.Subject.Trim();
            var cleanMessage = request.Message.Trim();
            var cleanName = request.Name.Trim();

            var contactMessage = new ClientContactMessage
            {
                Id = Guid.NewGuid(),
                ConversationId = Guid.NewGuid(),
                CreatedAtUtc = DateTime.UtcNow,
                Name = cleanName,
                Email = sessionEmail,
                Subject = cleanSubject,
                Message = cleanMessage,
                SenderRole = "cliente",
                Status = SupportStatusPending,
                IsSystemEvent = false
            };

            await _clientContactMessageService.SaveAsync(contactMessage);

            _logger.LogInformation(
                "Solicitud de contacto recibida de {Email}. Nombre: {Name}. Asunto: {Subject}. Mensaje: {Message}",
                sessionEmail,
                cleanName,
                cleanSubject,
                cleanMessage);

            if (isAjaxRequest)
            {
                return Json(new
                {
                    succeeded = true,
                    successMessage = "Tu consulta fue enviada correctamente. Te responderemos pronto.",
                    conversationId = contactMessage.ConversationId
                });
            }

            TempData["SuccessMessage"] = "Tu consulta fue enviada correctamente. Te responderemos pronto.";
            return RedirectToAction(nameof(Portada));
        }

        [HttpGet]
        [RequireSession]
        public async Task<IActionResult> GetClientSupportInbox()
        {
            var sessionEmail = HttpContext.Session.GetString("UserEmail") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(sessionEmail))
            {
                return Unauthorized(new { succeeded = false, errorMessage = "Debes iniciar sesión para ver tu bandeja." });
            }

            var supportMessages = await _clientContactMessageService.GetAllAsync();
            var conversations = BuildSupportConversations(supportMessages)
                .Where(x => string.Equals(x.ClientEmail, sessionEmail, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return Json(new { succeeded = true, conversations });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireSession]
        public async Task<IActionResult> ReplySupportConversation([FromForm] SupportReplyRequest request)
        {
            var isAjaxRequest = string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
            if (!ModelState.IsValid || request.ConversationId == Guid.Empty)
            {
                var validationMessage = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                    ?? "No se pudo enviar la respuesta.";

                if (isAjaxRequest)
                {
                    return BadRequest(new { succeeded = false, errorMessage = validationMessage });
                }

                TempData["ErrorMessage"] = validationMessage;
                return RedirectToAction(nameof(Portada));
            }

            var sessionEmail = HttpContext.Session.GetString("UserEmail") ?? string.Empty;
            var allMessages = await _clientContactMessageService.GetAllAsync();
            var conversations = BuildSupportConversations(allMessages);
            var conversation = conversations.FirstOrDefault(x => x.ConversationId == request.ConversationId);

            if (conversation is null || !string.Equals(conversation.ClientEmail, sessionEmail, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { succeeded = false, errorMessage = "No se encontró la conversación indicada." });
            }

            var cleanMessage = request.Message.Trim();
            var senderName = HttpContext.Session.GetString("UserName") ?? conversation.ClientName;
            var reply = new ClientContactMessage
            {
                Id = Guid.NewGuid(),
                ConversationId = conversation.ConversationId,
                CreatedAtUtc = DateTime.UtcNow,
                Name = string.IsNullOrWhiteSpace(senderName) ? conversation.ClientName : senderName,
                Email = conversation.ClientEmail,
                Subject = conversation.Subject,
                Message = cleanMessage,
                SenderRole = "cliente",
                Status = SupportStatusPending,
                IsSystemEvent = false
            };

            await _clientContactMessageService.SaveAsync(reply);

            if (isAjaxRequest)
            {
                return Json(new { succeeded = true, successMessage = "Tu respuesta fue enviada correctamente." });
            }

            TempData["SuccessMessage"] = "Tu respuesta fue enviada correctamente.";
            return RedirectToAction(nameof(Portada));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireSession("administrador")]
        public async Task<IActionResult> ReplySupportConversationFromAdmin([FromForm] SupportReplyRequest request, string? supportDate)
        {
            if (!CanAccessAdminArea(AdminAreas.Support))
            {
                TempData["ErrorMessage"] = "No tienes permisos para responder mensajes de soporte.";
                return RedirectToAction(nameof(Admin), new { section = "support", supportDate });
            }

            if (!ModelState.IsValid || request.ConversationId == Guid.Empty)
            {
                TempData["ErrorMessage"] = "Debes escribir una respuesta válida.";
                return RedirectToAction(nameof(Admin), new { section = "support", supportDate });
            }

            var allMessages = await _clientContactMessageService.GetAllAsync();
            var conversation = BuildSupportConversations(allMessages).FirstOrDefault(x => x.ConversationId == request.ConversationId);
            if (conversation is null)
            {
                TempData["ErrorMessage"] = "No se encontró la conversación de soporte.";
                return RedirectToAction(nameof(Admin), new { section = "support", supportDate });
            }

            var cleanMessage = request.Message.Trim();
            var adminName = HttpContext.Session.GetString("UserName") ?? "Administrador";
            await _clientContactMessageService.SaveAsync(new ClientContactMessage
            {
                Id = Guid.NewGuid(),
                ConversationId = conversation.ConversationId,
                CreatedAtUtc = DateTime.UtcNow,
                Name = adminName,
                Email = conversation.ClientEmail,
                Subject = conversation.Subject,
                Message = cleanMessage,
                SenderRole = "administrador",
                Status = conversation.Status,
                IsSystemEvent = false
            });

            TempData["SuccessMessage"] = "Respuesta enviada al cliente.";
            return RedirectToAction(nameof(Admin), new { section = "support", supportDate });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireSession("administrador")]
        public async Task<IActionResult> UpdateSupportConversationStatus(Guid conversationId, string status, string? supportDate)
        {
            if (!CanAccessAdminArea(AdminAreas.Support))
            {
                TempData["ErrorMessage"] = "No tienes permisos para gestionar estados de soporte.";
                return RedirectToAction(nameof(Admin), new { section = "support", supportDate });
            }

            if (conversationId == Guid.Empty)
            {
                TempData["ErrorMessage"] = "La conversación seleccionada no es válida.";
                return RedirectToAction(nameof(Admin), new { section = "support", supportDate });
            }

            var normalizedStatus = NormalizeSupportStatus(status);
            if (normalizedStatus is null)
            {
                TempData["ErrorMessage"] = "El estado indicado no es válido.";
                return RedirectToAction(nameof(Admin), new { section = "support", supportDate });
            }

            var allMessages = await _clientContactMessageService.GetAllAsync();
            var conversation = BuildSupportConversations(allMessages).FirstOrDefault(x => x.ConversationId == conversationId);
            if (conversation is null)
            {
                TempData["ErrorMessage"] = "No se encontró la conversación de soporte.";
                return RedirectToAction(nameof(Admin), new { section = "support", supportDate });
            }

            var adminName = HttpContext.Session.GetString("UserName") ?? "Administrador";
            await _clientContactMessageService.SaveAsync(new ClientContactMessage
            {
                Id = Guid.NewGuid(),
                ConversationId = conversation.ConversationId,
                CreatedAtUtc = DateTime.UtcNow,
                Name = adminName,
                Email = conversation.ClientEmail,
                Subject = conversation.Subject,
                Message = normalizedStatus == SupportStatusResolved
                    ? "Estado actualizado a Resuelto."
                    : "Estado actualizado a Pendiente.",
                SenderRole = "administrador",
                Status = normalizedStatus,
                IsSystemEvent = true
            });

            TempData["SuccessMessage"] = normalizedStatus == SupportStatusResolved
                ? "La conversación fue marcada como resuelta."
                : "La conversación fue marcada como pendiente.";

            return RedirectToAction(nameof(Admin), new { section = "support", supportDate });
        }

        private string? ValidatePaymentData(CartCheckoutRequest? request)
        {
            if (request is null)
            {
                return "Solicitud de compra inválida.";
            }

            var paymentMethod = request.PaymentMethod?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(paymentMethod)
                || (paymentMethod != "tarjeta" && paymentMethod != "paypal" && paymentMethod != "efectivo"))
            {
                return "Selecciona un método de pago válido.";
            }

            if (paymentMethod == "tarjeta")
            {
                var card = request.Card;
                if (card is null)
                {
                    return "Completa la información de tu tarjeta.";
                }

                var holderName = (card.HolderName ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(holderName))
                {
                    return "El nombre del titular es obligatorio.";
                }

                if (!Regex.IsMatch(holderName, "^[a-zA-ZáéíóúÁÉÍÓÚñÑ\\s]+$"))
                {
                    return "El nombre del titular contiene caracteres inválidos.";
                }

                var cardNumber = Regex.Replace(card.Number ?? string.Empty, "\\s+", string.Empty);
                if (!Regex.IsMatch(cardNumber, "^\\d{13,19}$"))
                {
                    return "El número de tarjeta es inválido.";
                }

                if (card.ExpiryMonth < 1 || card.ExpiryMonth > 12)
                {
                    return "El mes de vencimiento de la tarjeta es inválido.";
                }

                var today = DateTime.UtcNow;
                var minYear = today.Year;
                var maxYear = today.Year + 20;
                if (card.ExpiryYear < minYear || card.ExpiryYear > maxYear)
                {
                    return "El año de vencimiento de la tarjeta es inválido.";
                }

                if (card.ExpiryYear == today.Year && card.ExpiryMonth < today.Month)
                {
                    return "La tarjeta ya está vencida.";
                }

                var cvv = (card.Cvv ?? string.Empty).Trim();
                if (!Regex.IsMatch(cvv, "^\\d{3,4}$"))
                {
                    return "El CVV de la tarjeta es inválido.";
                }

                return null;
            }

            if (paymentMethod == "paypal")
            {
                if (!IsPayPalConfigured())
                {
                    return "PayPal no está configurado en el servidor.";
                }

                var orderId = request.PayPal?.OrderId?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(orderId))
                {
                    return "No se encontró una orden de PayPal válida.";
                }

                return null;
            }

            var customerName = request.Cash?.CustomerName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(customerName))
            {
                return "El nombre para pago en efectivo es obligatorio.";
            }

            if (!Regex.IsMatch(customerName, "^[a-zA-ZáéíóúÁÉÍÓÚñÑ\\s]+$"))
            {
                return "El nombre para pago en efectivo contiene caracteres inválidos.";
            }

            return null;
        }

        private bool IsPayPalConfigured()
        {
            return !string.IsNullOrWhiteSpace(_payPalSettings.ClientId)
                && !string.IsNullOrWhiteSpace(_payPalSettings.ClientSecret)
                && !string.IsNullOrWhiteSpace(_payPalSettings.BaseUrl);
        }

        private async Task<(bool Succeeded, List<(Product Product, int Quantity)> Lines, string? ErrorMessage)> GetCheckoutLinesAsync(List<CartCheckoutItem>? items)
        {
            if (items is null || items.Count == 0)
            {
                return (false, new List<(Product Product, int Quantity)>(), "El carrito está vacío.");
            }

            var lines = new List<(Product Product, int Quantity)>();

            foreach (var item in items)
            {
                if (item.ProductId == Guid.Empty || item.Quantity <= 0)
                {
                    return (false, new List<(Product Product, int Quantity)>(), "Hay productos inválidos en el carrito.");
                }

                var product = await _productService.GetByIdAsync(item.ProductId);
                if (product is null)
                {
                    return (false, new List<(Product Product, int Quantity)>(), "Uno de los productos ya no está disponible.");
                }

                if (product.Stock < item.Quantity)
                {
                    return (false, new List<(Product Product, int Quantity)>(), $"Stock insuficiente para {product.Name}. Disponible: {product.Stock}.");
                }

                lines.Add((product, item.Quantity));
            }

            return (true, lines, null);
        }

        private async Task<(bool Succeeded, string? AccessToken, string? ErrorMessage)> GetPayPalAccessTokenAsync()
        {
            try
            {
                using var client = _httpClientFactory.CreateClient();
                var endpoint = $"{_payPalSettings.BaseUrl.TrimEnd('/')}/v1/oauth2/token";
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_payPalSettings.ClientId}:{_payPalSettings.ClientSecret}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials"
                });

                var response = await client.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("PayPal token error. Status: {Status}. Body: {Body}", response.StatusCode, content);
                    return (false, null, "No se pudo autenticar la pasarela de PayPal.");
                }

                using var document = JsonDocument.Parse(content);
                var accessToken = document.RootElement.GetProperty("access_token").GetString();
                if (string.IsNullOrWhiteSpace(accessToken))
                {
                    return (false, null, "No se pudo obtener el token de PayPal.");
                }

                return (true, accessToken, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo token de PayPal.");
                return (false, null, "No se pudo iniciar la autenticación con PayPal.");
            }
        }

        private async Task<(bool Succeeded, string? OrderId, string? ErrorMessage)> CreatePayPalOrderAsync(decimal total)
        {
            var tokenResult = await GetPayPalAccessTokenAsync();
            if (!tokenResult.Succeeded || string.IsNullOrWhiteSpace(tokenResult.AccessToken))
            {
                return (false, null, tokenResult.ErrorMessage);
            }

            try
            {
                using var client = _httpClientFactory.CreateClient();
                var endpoint = $"{_payPalSettings.BaseUrl.TrimEnd('/')}/v2/checkout/orders";
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult.AccessToken);

                var payload = new
                {
                    intent = "CAPTURE",
                    purchase_units = new[]
                    {
                        new
                        {
                            amount = new
                            {
                                currency_code = string.IsNullOrWhiteSpace(_payPalSettings.CurrencyCode) ? "USD" : _payPalSettings.CurrencyCode,
                                value = total.ToString("0.00", CultureInfo.InvariantCulture)
                            }
                        }
                    }
                };

                request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var response = await client.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("PayPal create order error. Status: {Status}. Body: {Body}", response.StatusCode, content);
                    return (false, null, "No se pudo crear la orden de PayPal.");
                }

                using var document = JsonDocument.Parse(content);
                var orderId = document.RootElement.TryGetProperty("id", out var idElement)
                    ? idElement.GetString()
                    : null;

                if (string.IsNullOrWhiteSpace(orderId))
                {
                    return (false, null, "PayPal no devolvió un identificador de orden válido.");
                }

                return (true, orderId, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creando orden de PayPal.");
                return (false, null, "No se pudo iniciar el pago con PayPal.");
            }
        }

        private async Task<(bool Succeeded, string? ErrorMessage)> CapturePayPalOrderAsync(string orderId)
        {
            if (string.IsNullOrWhiteSpace(orderId))
            {
                return (false, "No se encontró una orden de PayPal válida.");
            }

            var tokenResult = await GetPayPalAccessTokenAsync();
            if (!tokenResult.Succeeded || string.IsNullOrWhiteSpace(tokenResult.AccessToken))
            {
                return (false, tokenResult.ErrorMessage);
            }

            try
            {
                using var client = _httpClientFactory.CreateClient();
                var endpoint = $"{_payPalSettings.BaseUrl.TrimEnd('/')}/v2/checkout/orders/{orderId}/capture";
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult.AccessToken);
                request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

                var response = await client.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("PayPal capture error. Status: {Status}. Body: {Body}", response.StatusCode, content);
                    return (false, "No fue posible confirmar el pago en PayPal.");
                }

                using var document = JsonDocument.Parse(content);
                var status = document.RootElement.TryGetProperty("status", out var statusElement)
                    ? statusElement.GetString()
                    : string.Empty;

                if (!string.Equals(status, "COMPLETED", StringComparison.OrdinalIgnoreCase))
                {
                    return (false, "El pago de PayPal no fue aprobado.");
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error capturando orden de PayPal {OrderId}.", orderId);
                return (false, "No se pudo confirmar el pago con PayPal.");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireSession]
        public async Task<IActionResult> UpdateClientProfile(ClientProfileViewModel model)
        {
            var isAjaxRequest = string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

            if (!ModelState.IsValid)
            {
                var validationMessage = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                    ?? "No fue posible actualizar tu perfil.";

                if (isAjaxRequest)
                {
                    return BadRequest(new { succeeded = false, errorMessage = validationMessage });
                }

                TempData["ErrorMessage"] = validationMessage;
                return RedirectToAction(nameof(Portada), new { section = "profile" });
            }

            var sessionEmail = HttpContext.Session.GetString("UserEmail") ?? string.Empty;
            if (!string.Equals(sessionEmail, model.Email, StringComparison.OrdinalIgnoreCase))
            {
                if (isAjaxRequest)
                {
                    return BadRequest(new { succeeded = false, errorMessage = "No fue posible validar el perfil a actualizar." });
                }

                TempData["ErrorMessage"] = "No fue posible validar el perfil a actualizar.";
                return RedirectToAction(nameof(Portada), new { section = "profile" });
            }

            var currentProfile = await _authService.GetClientProfileByEmailAsync(sessionEmail);
            if (currentProfile is null)
            {
                if (isAjaxRequest)
                {
                    return BadRequest(new { succeeded = false, errorMessage = "No se encontró el perfil del usuario." });
                }

                TempData["ErrorMessage"] = "No se encontró el perfil del usuario.";
                return RedirectToAction(nameof(Portada), new { section = "profile" });
            }

            currentProfile.FirstName = model.FirstName?.Trim() ?? string.Empty;
            currentProfile.LastName = model.LastName?.Trim() ?? string.Empty;
            currentProfile.Phone = model.Phone?.Trim();
            currentProfile.Address = model.Address?.Trim();

            string? normalizedProfileImageUrl = null;
            if (!string.IsNullOrWhiteSpace(model.ProfileImageUrl))
            {
                var postedImageValue = model.ProfileImageUrl.Trim();
                if (postedImageValue.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
                {
                    var uploadResult = await UploadProfileImageDataUrlAsync(postedImageValue);
                    if (!uploadResult.Succeeded)
                    {
                        if (isAjaxRequest)
                        {
                            return BadRequest(new { succeeded = false, errorMessage = uploadResult.ErrorMessage ?? "No fue posible subir la imagen de perfil." });
                        }

                        TempData["ErrorMessage"] = uploadResult.ErrorMessage ?? "No fue posible subir la imagen de perfil.";
                        return RedirectToAction(nameof(Portada), new { section = "profile" });
                    }

                    normalizedProfileImageUrl = uploadResult.PublicUrl;
                }
                else
                {
                    normalizedProfileImageUrl = postedImageValue;
                }
            }

            currentProfile.ProfileImageUrl = normalizedProfileImageUrl;

            var updateResult = await _authService.UpdateClientProfileAsync(currentProfile);
            if (!updateResult.Succeeded)
            {
                if (isAjaxRequest)
                {
                    return BadRequest(new { succeeded = false, errorMessage = updateResult.ErrorMessage ?? "No fue posible actualizar tu perfil." });
                }

                TempData["ErrorMessage"] = updateResult.ErrorMessage ?? "No fue posible actualizar tu perfil.";
                return RedirectToAction(nameof(Portada), new { section = "profile" });
            }

            var newDisplayName = currentProfile.DisplayName;
            HttpContext.Session.SetString("UserName", newDisplayName);

            if (isAjaxRequest)
            {
                return Json(new
                {
                    succeeded = true,
                    successMessage = "Perfil actualizado correctamente.",
                    displayName = newDisplayName,
                    profileImageUrl = currentProfile.ProfileImageUrl
                });
            }

            TempData["SuccessMessage"] = "Perfil actualizado correctamente.";
            return RedirectToAction(nameof(Portada), new { section = "profile" });
        }

        [HttpGet]
        [RequireSession]
        public IActionResult UpdateClientProfile()
        {
            return RedirectToAction(nameof(Portada), new { section = "profile" });
        }

        private async Task<(bool Succeeded, string? PublicUrl, string? ErrorMessage)> UploadProfileImageDataUrlAsync(string dataUrl)
        {
            if (string.IsNullOrWhiteSpace(_supabaseSettings.Url)
                || string.IsNullOrWhiteSpace(_supabaseSettings.ServiceRoleKey))
            {
                _logger.LogWarning("No se pudo subir imagen de perfil por configuración incompleta de Supabase.");
                return (false, null, "No se pudo procesar la imagen por configuración incompleta del servidor.");
            }

            var commaIndex = dataUrl.IndexOf(',');
            if (commaIndex <= 0)
            {
                return (false, null, "El formato de imagen no es válido.");
            }

            var metadata = dataUrl[..commaIndex];
            var base64Content = dataUrl[(commaIndex + 1)..];
            if (!metadata.Contains(";base64", StringComparison.OrdinalIgnoreCase))
            {
                return (false, null, "La imagen enviada no es compatible.");
            }

            var contentType = metadata.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                ? metadata[5..metadata.IndexOf(';')]
                : "image/jpeg";

            byte[] imageBytes;
            try
            {
                imageBytes = Convert.FromBase64String(base64Content);
            }
            catch
            {
                return (false, null, "La imagen enviada no es válida.");
            }

            if (imageBytes.Length == 0)
            {
                return (false, null, "No se recibió contenido de imagen.");
            }

            if (imageBytes.Length > 4 * 1024 * 1024)
            {
                return (false, null, "La imagen de perfil excede el tamaño máximo permitido (4 MB).");
            }

            var extension = contentType.ToLowerInvariant() switch
            {
                "image/png" => ".png",
                "image/webp" => ".webp",
                "image/gif" => ".gif",
                _ => ".jpg"
            };

            var fileName = $"profiles/{Guid.NewGuid():N}{extension}";
            var uploadUrl = $"{_supabaseSettings.Url.TrimEnd('/')}/storage/v1/object/{ProductImagesBucket}/{fileName}";

            using var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
            request.Headers.TryAddWithoutValidation("apikey", _supabaseSettings.ServiceRoleKey);
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_supabaseSettings.ServiceRoleKey}");
            request.Headers.TryAddWithoutValidation("x-upsert", "true");

            request.Content = new ByteArrayContent(imageBytes);
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);

            var response = await client.SendAsync(request).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var details = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                _logger.LogWarning("Error subiendo imagen de perfil a Supabase Storage. Status: {Status}, Response: {Response}", response.StatusCode, details);
                return (false, null, "No fue posible subir la imagen de perfil.");
            }

            return (true, BuildSupabasePublicObjectUrl(fileName), null);
        }
        [RequireSession("administrador")]
        public async Task<IActionResult> Admin(
            Guid? editId,
            string? search,
            string? sort = "az",
            int page = 1,
            Guid? productEditId = null,
            string? productSearch = null,
            string? productSort = "az",
            int productPage = 1,
            DateTime? ordersDate = null,
            DateTime? supportDate = null)
        {
            var currentEmail = GetCurrentUserEmail();
            var currentPermissions = BuildCurrentAdminPermissions(currentEmail);
            if (!currentPermissions.Values.Any(x => x))
            {
                return RedirectToAction(nameof(Portada));
            }

            var allCategories = await _categoryService.GetAllAsync();
            var allProducts = await _productService.GetAllAsync();
            var users = await _authService.GetUsersByRolesAsync(new[] { "administrador", "vendedor" });
            var clientUsersCount = (await _authService.GetUsersByRolesAsync(new[] { "cliente" })).Count;
            var adminEmails = users
                .Where(x => string.Equals(x.Role, "administrador", StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Email)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
            var permissionsByEmail = _adminPermissionService.GetPermissionsForAdmins(adminEmails!);
            var notifications = _notificationService.GetAll();
            var orderDateFilter = ordersDate.HasValue ? DateOnly.FromDateTime(ordersDate.Value.Date) : (DateOnly?)null;
            var supportDateFilter = supportDate.HasValue ? DateOnly.FromDateTime(supportDate.Value.Date) : (DateOnly?)null;
            var adminOrders = await _clientPurchaseService.GetAllPurchasesAsync(orderDateFilter);
            var supportMessages = await _clientContactMessageService.GetAllAsync(supportDateFilter);
            var supportConversations = BuildSupportConversations(supportMessages);
            var model = BuildAdminViewModel(
                allCategories,
                allProducts,
                search,
                sort,
                page,
                new CategoryFormModel(),
                productSearch,
                productSort,
                productPage,
                new ProductFormModel(),
                users,
                notifications,
                clientUsersCount,
                currentPermissions,
                _adminPermissionService.IsSuperAdmin(currentEmail),
                permissionsByEmail);

            ViewBag.AdminOrders = adminOrders;
            ViewBag.SupportPendingConversations = supportConversations
                .Where(x => string.Equals(x.Status, SupportStatusPending, StringComparison.OrdinalIgnoreCase))
                .ToList();
            ViewBag.SupportResolvedConversations = supportConversations
                .Where(x => string.Equals(x.Status, SupportStatusResolved, StringComparison.OrdinalIgnoreCase))
                .ToList();
            ViewBag.OrdersDateFilter = ordersDate?.ToString("yyyy-MM-dd");
            ViewBag.SupportDateFilter = supportDate?.ToString("yyyy-MM-dd");

            if (editId.HasValue)
            {
                var category = await _categoryService.GetByIdAsync(editId.Value);
                if (category is null)
                {
                    TempData["ErrorMessage"] = "No se encontró la categoría solicitada.";
                }
                else
                {
                    model = BuildAdminViewModel(
                        allCategories,
                        allProducts,
                        search,
                        sort,
                        page,
                        new CategoryFormModel
                        {
                            Id = category.Id,
                            Name = category.Name,
                            Description = category.Description
                        },
                        productSearch,
                        productSort,
                        productPage,
                        new ProductFormModel(),
                        users,
                        notifications,
                        clientUsersCount,
                        currentPermissions,
                        _adminPermissionService.IsSuperAdmin(currentEmail),
                        permissionsByEmail);
                    ViewBag.EditMode = true;
                }
            }

            if (productEditId.HasValue)
            {
                var product = await _productService.GetByIdAsync(productEditId.Value);
                if (product is null)
                {
                    TempData["ErrorMessage"] = "No se encontró el producto solicitado.";
                }
                else
                {
                    model = BuildAdminViewModel(
                        allCategories,
                        allProducts,
                        search,
                        sort,
                        page,
                        model.Form,
                        productSearch,
                        productSort,
                        productPage,
                        new ProductFormModel
                        {
                            Id = product.Id,
                            Name = product.Name,
                            Price = product.Price,
                            Stock = product.Stock,
                            MinStock = product.MinStock,
                            Description = product.Description,
                            CategoryId = product.CategoryId,
                            ImageUrl1 = product.ImageUrl1,
                            ImageUrl2 = product.ImageUrl2,
                            ImageUrl3 = product.ImageUrl3
                        },
                        users,
                        notifications,
                        clientUsersCount,
                        currentPermissions,
                        _adminPermissionService.IsSuperAdmin(currentEmail),
                        permissionsByEmail);
                    ViewBag.ProductEditMode = true;
                }
            }

            return View(model);
        }
        [RequireSession("vendedor")]
        public async Task<IActionResult> Vendedor(string? status = null, string? receipt = null)
        {
            var categories = await _categoryService.GetAllAsync();
            var products = await _productService.GetAllAsync();
            var statusFilter = NormalizeSellerOrderStatus(status);
            var receiptFilter = NormalizeReceiptSearch(receipt);
            var allOrders = await _clientPurchaseService.GetAllPurchasesAsync();
            var filteredOrders = allOrders
                .Where(order => string.IsNullOrWhiteSpace(statusFilter)
                    || string.Equals(NormalizeSellerOrderStatus(order.Status), statusFilter, StringComparison.OrdinalIgnoreCase))
                .Where(order => string.IsNullOrWhiteSpace(receiptFilter)
                    || IsReceiptMatchByLastDigits(order.ReceiptNumber, receiptFilter))
                .OrderBy(order => order.CreatedAtUtc)
                .ToList();
            var categoryNameById = categories.ToDictionary(category => category.Id, category => category.Name);
            var inventoryProducts = products
                .OrderBy(product => categoryNameById.TryGetValue(product.CategoryId, out var categoryName)
                    ? categoryName
                    : "Sin categoría")
                .ThenBy(product => product.Name)
                .ToList();

            var model = new VendedorDashboardViewModel
            {
                CategoryOptions = categories.OrderBy(x => x.Name).ToList(),
                InventoryProducts = inventoryProducts,
                Notifications = _notificationService.GetAll(),
                Orders = filteredOrders,
                StatusFilter = statusFilter ?? string.Empty,
                ReceiptSearchFilter = receiptFilter ?? string.Empty
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireSession("vendedor")]
        public async Task<IActionResult> RegisterCashOrderPayment(Guid receiptId, string? status, string? receipt)
        {
            var result = await _clientPurchaseService.RegisterCashPaymentAsync(receiptId);
            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = result.ErrorMessage ?? "No se pudo registrar el pago en efectivo.";
                return RedirectToAction(nameof(Vendedor), new { status, receipt, section = "orders" });
            }

            TempData["SuccessMessage"] = "Pago en efectivo registrado correctamente.";
            return RedirectToAction(nameof(Vendedor), new { status, receipt, section = "orders" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireSession("vendedor")]
        public async Task<IActionResult> MarkOrderAsDelivered(Guid receiptId, string? status, string? receipt)
        {
            var result = await _clientPurchaseService.MarkPurchaseAsDeliveredAsync(receiptId);
            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = result.ErrorMessage ?? "No se pudo actualizar el estado del pedido.";
                return RedirectToAction(nameof(Vendedor), new { status, receipt, section = "orders" });
            }

            TempData["SuccessMessage"] = "Pedido marcado como entregado.";
            return RedirectToAction(nameof(Vendedor), new { status, receipt, section = "orders" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireSession("administrador")]
        public async Task<IActionResult> SaveCategory(
            [Bind(Prefix = "Form")] CategoryFormModel model,
            string? search,
            string? sort = "az",
            int page = 1,
            string? productSearch = null,
            string? productSort = "az",
            int productPage = 1)
        {
            if (!CanAccessAdminArea(AdminAreas.Categories))
            {
                TempData["ErrorMessage"] = "No tienes permiso para gestionar categorías.";
                return RedirectToAction(nameof(Admin));
            }

            if (!ModelState.IsValid)
            {
                var categories = await _categoryService.GetAllAsync();
                var products = await _productService.GetAllAsync();
                var users = await _authService.GetUsersByRolesAsync(new[] { "administrador", "vendedor" });
                var clientUsersCount = (await _authService.GetUsersByRolesAsync(new[] { "cliente" })).Count;
                var notifications = _notificationService.GetAll();
                ViewBag.ActiveSection = "categories";
                ViewBag.OpenCategoryDrawer = true;
                ViewBag.EditMode = model.Id.HasValue;
                return View("Admin", BuildAdminViewModel(
                    categories,
                    products,
                    search,
                    sort,
                    page,
                    model,
                    productSearch,
                    productSort,
                    productPage,
                    new ProductFormModel(),
                    users,
                    notifications,
                    clientUsersCount));
            }

            var result = model.Id.HasValue
                ? await _categoryService.UpdateAsync(model)
                : await _categoryService.CreateAsync(model);

            if (!result.Succeeded)
            {
                var categories = await _categoryService.GetAllAsync();
                var products = await _productService.GetAllAsync();
                var users = await _authService.GetUsersByRolesAsync(new[] { "administrador", "vendedor" });
                var clientUsersCount = (await _authService.GetUsersByRolesAsync(new[] { "cliente" })).Count;
                var notifications = _notificationService.GetAll();
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "No se pudo guardar la categoría.");
                ViewBag.ActiveSection = "categories";
                ViewBag.OpenCategoryDrawer = true;
                ViewBag.EditMode = model.Id.HasValue;
                return View("Admin", BuildAdminViewModel(
                    categories,
                    products,
                    search,
                    sort,
                    page,
                    model,
                    productSearch,
                    productSort,
                    productPage,
                    new ProductFormModel(),
                    users,
                    notifications,
                    clientUsersCount));
            }

            TempData["SuccessMessage"] = model.Id.HasValue
                ? "Categoría actualizada correctamente."
                : "Categoría registrada correctamente.";

            var categoryName = model.Name?.Trim();
            if (!model.Id.HasValue)
            {
                _notificationService.Add($"Nueva categoría registrada: {categoryName}");
            }
            else
            {
                _notificationService.Add($"Categoría actualizada: {categoryName}");
            }

            return RedirectToAdminCategories(search, sort, page, productSearch, productSort, productPage);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireSession("administrador")]
        public async Task<IActionResult> DeleteCategory(
            Guid id,
            string? search,
            string? sort = "az",
            int page = 1,
            string? productSearch = null,
            string? productSort = "az",
            int productPage = 1)
        {
            if (!CanAccessAdminArea(AdminAreas.Categories))
            {
                TempData["ErrorMessage"] = "No tienes permiso para gestionar categorías.";
                return RedirectToAction(nameof(Admin));
            }

            var category = await _categoryService.GetByIdAsync(id);
            var result = await _categoryService.DeleteAsync(id);
            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = result.ErrorMessage ?? "No se pudo eliminar la categoría.";
                return RedirectToAdminCategories(search, sort, page, productSearch, productSort, productPage);
            }

            TempData["SuccessMessage"] = "Categoría eliminada correctamente.";
            var categoryName = string.IsNullOrWhiteSpace(category?.Name) ? "sin nombre" : category.Name.Trim();
            _notificationService.Add($"Categoría eliminada: {categoryName}");
            return RedirectToAdminCategories(search, sort, page, productSearch, productSort, productPage);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireSession("administrador")]
        public async Task<IActionResult> SaveProduct(
            [Bind(Prefix = "ProductForm")] ProductFormModel model,
            string? search,
            string? sort = "az",
            int page = 1,
            string? productSearch = null,
            string? productSort = "az",
            int productPage = 1,
            IFormFile? imageFile1 = null,
            IFormFile? imageFile2 = null,
            IFormFile? imageFile3 = null)
        {
            if (!CanAccessAdminArea(AdminAreas.Products))
            {
                TempData["ErrorMessage"] = "No tienes permiso para gestionar productos.";
                return RedirectToAction(nameof(Admin));
            }

            Product? existingProduct = null;
            if (model.Id.HasValue)
            {
                existingProduct = await _productService.GetByIdAsync(model.Id.Value);
            }

            var image1 = await ResolveProductImageAsync(imageFile1, model.ImageUrl1, existingProduct?.ImageUrl1);
            var image2 = await ResolveProductImageAsync(imageFile2, model.ImageUrl2, existingProduct?.ImageUrl2);
            var image3 = await ResolveProductImageAsync(imageFile3, model.ImageUrl3, existingProduct?.ImageUrl3);

            model.ImageUrl1 = image1.ImageUrl;
            model.ImageUrl2 = image2.ImageUrl;
            model.ImageUrl3 = image3.ImageUrl;

            ModelState.Clear();
            TryValidateModel(model, "ProductForm");

            var uploadErrors = new[] { image1.ErrorMessage, image2.ErrorMessage, image3.ErrorMessage }
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Distinct()
                .ToList();

            foreach (var uploadError in uploadErrors)
            {
                ModelState.AddModelError(string.Empty, uploadError!);
            }

            if (!ModelState.IsValid)
            {
                var categories = await _categoryService.GetAllAsync();
                var products = await _productService.GetAllAsync();
                var users = await _authService.GetUsersByRolesAsync(new[] { "administrador", "vendedor" });
                var clientUsersCount = (await _authService.GetUsersByRolesAsync(new[] { "cliente" })).Count;
                var notifications = _notificationService.GetAll();
                ViewBag.ActiveSection = "products";
                ViewBag.OpenProductDrawer = true;
                ViewBag.ProductEditMode = model.Id.HasValue;
                return View("Admin", BuildAdminViewModel(
                    categories,
                    products,
                    search,
                    sort,
                    page,
                    new CategoryFormModel(),
                    productSearch,
                    productSort,
                    productPage,
                    model,
                    users,
                    notifications,
                    clientUsersCount));
            }

            var result = model.Id.HasValue
                ? await _productService.UpdateAsync(model)
                : await _productService.CreateAsync(model);

            if (!result.Succeeded)
            {
                var categories = await _categoryService.GetAllAsync();
                var products = await _productService.GetAllAsync();
                var users = await _authService.GetUsersByRolesAsync(new[] { "administrador", "vendedor" });
                var clientUsersCount = (await _authService.GetUsersByRolesAsync(new[] { "cliente" })).Count;
                var notifications = _notificationService.GetAll();
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "No se pudo guardar el producto.");
                ViewBag.ActiveSection = "products";
                ViewBag.OpenProductDrawer = true;
                ViewBag.ProductEditMode = model.Id.HasValue;
                return View("Admin", BuildAdminViewModel(
                    categories,
                    products,
                    search,
                    sort,
                    page,
                    new CategoryFormModel(),
                    productSearch,
                    productSort,
                    productPage,
                    model,
                    users,
                    notifications,
                    clientUsersCount));
            }

            TempData["SuccessMessage"] = model.Id.HasValue
                ? "Producto actualizado correctamente."
                : "Producto registrado correctamente.";

            _notificationService.Add(model.Id.HasValue
                ? $"Producto actualizado: {model.Name}"
                : $"Nuevo producto registrado: {model.Name}");

            return RedirectToAdminProducts(search, sort, page, productSearch, productSort, productPage);
        }

        private async Task<(string? ImageUrl, string? ErrorMessage)> ResolveProductImageAsync(IFormFile? file, string? postedValue, string? existingValue)
        {
            if (file is null || file.Length == 0)
            {
                if (!string.IsNullOrWhiteSpace(postedValue))
                {
                    return (postedValue.Trim(), null);
                }

                return (string.IsNullOrWhiteSpace(existingValue) ? null : existingValue.Trim(), null);
            }

            var uploadedUrl = await UploadProductImageToSupabaseAsync(file).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(uploadedUrl))
            {
                return (null, $"No fue posible subir la imagen '{file.FileName}' a Supabase Storage.");
            }

            return (uploadedUrl, null);
        }

        private async Task<string?> UploadProductImageToSupabaseAsync(IFormFile file)
        {
            if (string.IsNullOrWhiteSpace(_supabaseSettings.Url)
                || string.IsNullOrWhiteSpace(_supabaseSettings.ServiceRoleKey))
            {
                _logger.LogWarning("No se pudo subir imagen a Supabase Storage por configuración incompleta.");
                return null;
            }

            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".jpg";
            }

            var contentType = string.IsNullOrWhiteSpace(file.ContentType)
                ? "application/octet-stream"
                : file.ContentType;
            var fileName = $"products/{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
            var uploadUrl = $"{_supabaseSettings.Url.TrimEnd('/')}/storage/v1/object/{ProductImagesBucket}/{fileName}";

            using var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
            request.Headers.TryAddWithoutValidation("apikey", _supabaseSettings.ServiceRoleKey);
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_supabaseSettings.ServiceRoleKey}");
            request.Headers.TryAddWithoutValidation("x-upsert", "true");

            await using var fileStream = file.OpenReadStream();
            request.Content = new StreamContent(fileStream);
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);

            var response = await client.SendAsync(request).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var details = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                _logger.LogWarning("Error subiendo imagen a Supabase Storage. Status: {Status}, Response: {Response}", response.StatusCode, details);
                return null;
            }

            return BuildSupabasePublicObjectUrl(fileName);
        }

        private string BuildSupabasePublicObjectUrl(string objectPath)
        {
            return $"{_supabaseSettings.Url.TrimEnd('/')}/storage/v1/object/public/{ProductImagesBucket}/{objectPath}";
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireSession("administrador")]
        public async Task<IActionResult> DeleteProduct(
            Guid id,
            string? search,
            string? sort = "az",
            int page = 1,
            string? productSearch = null,
            string? productSort = "az",
            int productPage = 1)
        {
            if (!CanAccessAdminArea(AdminAreas.Products))
            {
                TempData["ErrorMessage"] = "No tienes permiso para gestionar productos.";
                return RedirectToAction(nameof(Admin));
            }

            var result = await _productService.DeleteAsync(id);
            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = result.ErrorMessage ?? "No se pudo eliminar el producto.";
                return RedirectToAdminProducts(search, sort, page, productSearch, productSort, productPage);
            }

            TempData["SuccessMessage"] = "Producto eliminado correctamente.";
            _notificationService.Add("Producto eliminado correctamente.");
            return RedirectToAdminProducts(search, sort, page, productSearch, productSort, productPage);
        }

        private List<SupportConversationViewModel> BuildSupportConversations(IReadOnlyList<ClientContactMessage> messages)
        {
            return messages
                .GroupBy(message => message.ConversationId == Guid.Empty ? message.Id : message.ConversationId)
                .Select(group =>
                {
                    var ordered = group.OrderBy(x => x.CreatedAtUtc).ToList();
                    var firstClientMessage = ordered
                        .FirstOrDefault(x => !x.IsSystemEvent && string.Equals(x.SenderRole, "cliente", StringComparison.OrdinalIgnoreCase))
                        ?? ordered.First();
                    var lastMessage = ordered.Last();
                    var status = NormalizeSupportStatus(lastMessage.Status) ?? SupportStatusPending;

                    return new SupportConversationViewModel
                    {
                        ConversationId = group.Key,
                        Subject = firstClientMessage.Subject,
                        ClientName = firstClientMessage.Name,
                        ClientEmail = firstClientMessage.Email,
                        Status = status,
                        CreatedAtUtc = ordered.First().CreatedAtUtc,
                        UpdatedAtUtc = lastMessage.CreatedAtUtc,
                        Messages = ordered
                            .Select(message => new SupportConversationMessageViewModel
                            {
                                Id = message.Id,
                                CreatedAtUtc = message.CreatedAtUtc,
                                SenderRole = string.IsNullOrWhiteSpace(message.SenderRole) ? "cliente" : message.SenderRole,
                                SenderName = message.Name,
                                Message = message.Message,
                                IsSystemEvent = message.IsSystemEvent
                            })
                            .ToList()
                    };
                })
                .OrderByDescending(conversation => conversation.UpdatedAtUtc)
                .ToList();
        }

        private string? NormalizeSupportStatus(string? status)
        {
            var normalized = (status ?? string.Empty).Trim().ToLowerInvariant();
            return normalized switch
            {
                SupportStatusPending => SupportStatusPending,
                SupportStatusResolved => SupportStatusResolved,
                _ => null
            };
        }

        private static string? NormalizeReceiptSearch(string? receipt)
        {
            var digits = new string((receipt ?? string.Empty).Where(char.IsDigit).ToArray());
            if (string.IsNullOrWhiteSpace(digits))
            {
                return null;
            }

            return digits.Length <= 4 ? digits : digits[^4..];
        }

        private static bool IsReceiptMatchByLastDigits(string? receiptNumber, string requestedDigits)
        {
            if (string.IsNullOrWhiteSpace(requestedDigits))
            {
                return true;
            }

            var receiptDigits = new string((receiptNumber ?? string.Empty).Where(char.IsDigit).ToArray());
            return !string.IsNullOrWhiteSpace(receiptDigits)
                && receiptDigits.EndsWith(requestedDigits, StringComparison.Ordinal);
        }

        private static string? NormalizeSellerOrderStatus(string? status)
        {
            var normalized = (status ?? string.Empty).Trim().ToLowerInvariant();
            return normalized switch
            {
                "pending" => "pendiente",
                "pendiente" => "pendiente",
                "paid" => "pagado",
                "pagado" => "pagado",
                "delivered" => "entregado",
                "entregado" => "entregado",
                "canceled" => "cancelado",
                "cancelled" => "cancelado",
                "cancelado" => "cancelado",
                _ => null
            };
        }

        private CategoriesIndexViewModel BuildAdminViewModel(
            IReadOnlyList<Category> categories,
            IReadOnlyList<Product> products,
            string? categorySearch,
            string? categorySort,
            int categoryPage,
            CategoryFormModel categoryForm,
            string? productSearch,
            string? productSort,
            int productPage,
            ProductFormModel productForm,
            IReadOnlyList<AdminUserViewModel>? users = null,
            IReadOnlyList<AdminNotificationViewModel>? notifications = null,
            int clientUsersCount = 0,
            IReadOnlyDictionary<string, bool>? currentAdminPermissions = null,
            bool isSuperAdmin = false,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, bool>>? adminPermissionsByEmail = null)
        {
            var currentEmail = GetCurrentUserEmail();
            currentAdminPermissions ??= BuildCurrentAdminPermissions(currentEmail);
            if (!isSuperAdmin)
            {
                isSuperAdmin = _adminPermissionService.IsSuperAdmin(currentEmail);
            }

            if (adminPermissionsByEmail is null && isSuperAdmin)
            {
                var adminEmails = (users ?? Array.Empty<AdminUserViewModel>())
                    .Where(x => string.Equals(x.Role, "administrador", StringComparison.OrdinalIgnoreCase))
                    .Select(x => x.Email)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();
                adminPermissionsByEmail = _adminPermissionService.GetPermissionsForAdmins(adminEmails!);
            }

            const int pageSize = 10;
            var normalizedCategorySort = string.Equals(categorySort, "za", StringComparison.OrdinalIgnoreCase) ? "za" : "az";
            var normalizedProductSort = string.Equals(productSort, "za", StringComparison.OrdinalIgnoreCase) ? "za" : "az";

            IEnumerable<Category> categoryQuery = categories;
            if (!string.IsNullOrWhiteSpace(categorySearch))
            {
                var term = categorySearch.Trim();
                categoryQuery = categoryQuery.Where(c =>
                    c.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (c.Description?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            categoryQuery = normalizedCategorySort == "za"
                ? categoryQuery.OrderByDescending(c => c.Name)
                : categoryQuery.OrderBy(c => c.Name);

            var filteredCategories = categoryQuery.ToList();
            var categoryTotalItems = filteredCategories.Count;
            var categoryTotalPages = Math.Max(1, (int)Math.Ceiling(categoryTotalItems / (double)pageSize));
            var currentCategoryPage = Math.Clamp(categoryPage, 1, categoryTotalPages);
            var pagedCategories = filteredCategories
                .Skip((currentCategoryPage - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            IEnumerable<Product> productQuery = products;
            if (!string.IsNullOrWhiteSpace(productSearch))
            {
                var term = productSearch.Trim();
                productQuery = productQuery.Where(p =>
                    p.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (p.Description?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            productQuery = normalizedProductSort == "za"
                ? productQuery.OrderByDescending(p => p.Name)
                : productQuery.OrderBy(p => p.Name);

            var filteredProducts = productQuery.ToList();
            var productTotalItems = filteredProducts.Count;
            var productTotalPages = Math.Max(1, (int)Math.Ceiling(productTotalItems / (double)pageSize));
            var currentProductPage = Math.Clamp(productPage, 1, productTotalPages);
            var pagedProducts = filteredProducts
                .Skip((currentProductPage - 1) * pageSize)
                .Take(pageSize)
                .ToList();
            var categoryNameById = categories.ToDictionary(category => category.Id, category => category.Name);
            var inventoryProducts = products
                .OrderBy(product => categoryNameById.TryGetValue(product.CategoryId, out var categoryName)
                    ? categoryName
                    : "Sin categoría")
                .ThenBy(product => product.Name)
                .ToList();

            return new CategoriesIndexViewModel(
                pagedCategories,
                categoryForm,
                categorySearch,
                normalizedCategorySort,
                currentCategoryPage,
                categoryTotalPages,
                categoryTotalItems,
                pageSize,
                pagedProducts,
                productForm,
                productSearch,
                normalizedProductSort,
                currentProductPage,
                productTotalPages,
                productTotalItems,
                pageSize,
                categories.OrderBy(c => c.Name).ToList(),
                users,
                notifications,
                inventoryProducts,
                clientUsersCount,
                currentAdminPermissions,
                isSuperAdmin,
                adminPermissionsByEmail);
        }

        private IReadOnlyDictionary<string, bool> BuildCurrentAdminPermissions(string? email)
        {
            return AdminAreas.All.ToDictionary(
                area => area,
                area => _adminPermissionService.HasAccess(email, area),
                StringComparer.OrdinalIgnoreCase);
        }

        private static string NormalizeRole(string? role)
        {
            return string.Equals(role, "administrador", StringComparison.OrdinalIgnoreCase)
                ? "administrador"
                : "vendedor";
        }

        private string GetCurrentUserEmail()
        {
            return HttpContext.Session.GetString("UserEmail") ?? string.Empty;
        }

        private bool CanAccessAdminArea(string area)
        {
            var role = HttpContext.Session.GetString("UserRole") ?? string.Empty;
            if (!string.Equals(role, "administrador", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return _adminPermissionService.HasAccess(GetCurrentUserEmail(), area);
        }

        private bool HasFullAdminPermissions()
        {
            var email = GetCurrentUserEmail();
            return AdminAreas.All.All(area => _adminPermissionService.HasAccess(email, area));
        }

        private IActionResult RedirectToAdminCategories(
            string? categorySearch,
            string? categorySort,
            int categoryPage,
            string? productSearch,
            string? productSort,
            int productPage)
        {
            var values = BuildAdminRouteValues(categorySearch, categorySort, categoryPage, productSearch, productSort, productPage);
            var url = Url.Action(nameof(Admin), values) ?? Url.Action(nameof(Admin))!;
            return Redirect($"{url}#categories");
        }

        private IActionResult RedirectToAdminProducts(
            string? categorySearch,
            string? categorySort,
            int categoryPage,
            string? productSearch,
            string? productSort,
            int productPage)
        {
            var values = BuildAdminRouteValues(categorySearch, categorySort, categoryPage, productSearch, productSort, productPage);
            var url = Url.Action(nameof(Admin), values) ?? Url.Action(nameof(Admin))!;
            return Redirect($"{url}#products");
        }

        private static RouteValueDictionary BuildAdminRouteValues(
            string? categorySearch,
            string? categorySort,
            int categoryPage,
            string? productSearch,
            string? productSort,
            int productPage)
        {
            var values = new RouteValueDictionary();

            if (!string.IsNullOrWhiteSpace(categorySearch))
            {
                values["search"] = categorySearch;
            }

            if (!string.IsNullOrWhiteSpace(categorySort) && !string.Equals(categorySort, "az", StringComparison.OrdinalIgnoreCase))
            {
                values["sort"] = categorySort;
            }

            if (categoryPage > 1)
            {
                values["page"] = categoryPage;
            }

            if (!string.IsNullOrWhiteSpace(productSearch))
            {
                values["productSearch"] = productSearch;
            }

            if (!string.IsNullOrWhiteSpace(productSort) && !string.Equals(productSort, "az", StringComparison.OrdinalIgnoreCase))
            {
                values["productSort"] = productSort;
            }

            if (productPage > 1)
            {
                values["productPage"] = productPage;
            }

            return values;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            HttpContext.Session.Remove("IsAuthenticated");
            HttpContext.Session.Remove("UserRole");
            HttpContext.Session.Remove("UserEmail");
            HttpContext.Session.Remove("UserName");
            HttpContext.Response.Cookies.Delete(RememberMeConstants.CookieName);
            return RedirectToAction(nameof(Login));
        }

        private IActionResult? RedirectIfAuthenticated()
        {
            if (!string.Equals(HttpContext.Session.GetString("IsAuthenticated"), "true", StringComparison.Ordinal))
            {
                return null;
            }

            var role = HttpContext.Session.GetString("UserRole")?.ToLowerInvariant();
            return role switch
            {
                "vendedor" => RedirectToAction(nameof(Vendedor)),
                "administrador" => RedirectToAction(nameof(Admin)),
                "cliente" => RedirectToAction(nameof(Portada)),
                _ => null
            };
        }

        private IReadOnlyList<RememberedLoginAccount> GetRememberedAccounts()
        {
            if (!HttpContext.Request.Cookies.TryGetValue(RememberMeConstants.AccountsCookieName, out var protectedValue)
                || string.IsNullOrWhiteSpace(protectedValue))
            {
                return Array.Empty<RememberedLoginAccount>();
            }

            try
            {
                var json = _rememberedAccountsProtector.Unprotect(protectedValue);
                var accounts = JsonSerializer.Deserialize<List<RememberedLoginAccount>>(json)
                    ?? new List<RememberedLoginAccount>();

                return accounts
                    .Where(x => !string.IsNullOrWhiteSpace(x.Email))
                    .OrderByDescending(x => x.LastUsedAtUtc)
                    .Take(RememberMeConstants.MaxRememberedAccounts)
                    .ToList();
            }
            catch
            {
                HttpContext.Response.Cookies.Delete(RememberMeConstants.AccountsCookieName);
                return Array.Empty<RememberedLoginAccount>();
            }
        }

        private void SaveRememberedAccount(string email, string name)
        {
            var accounts = GetRememberedAccounts().ToList();
            accounts.RemoveAll(x => string.Equals(x.Email, email, StringComparison.OrdinalIgnoreCase));
            accounts.Insert(0, new RememberedLoginAccount
            {
                Email = email,
                Name = name,
                LastUsedAtUtc = DateTime.UtcNow
            });

            var trimmed = accounts
                .Take(RememberMeConstants.MaxRememberedAccounts)
                .ToList();

            var protectedPayload = _rememberedAccountsProtector.Protect(JsonSerializer.Serialize(trimmed));
            HttpContext.Response.Cookies.Append(RememberMeConstants.AccountsCookieName, protectedPayload, new CookieOptions
            {
                HttpOnly = true,
                Secure = HttpContext.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                IsEssential = true,
                Expires = DateTimeOffset.UtcNow.AddDays(90)
            });
        }

        private void RemoveRememberedAccount(string email)
        {
            var accounts = GetRememberedAccounts().ToList();
            var removed = accounts.RemoveAll(x => string.Equals(x.Email, email, StringComparison.OrdinalIgnoreCase));
            if (removed == 0)
            {
                return;
            }

            if (accounts.Count == 0)
            {
                HttpContext.Response.Cookies.Delete(RememberMeConstants.AccountsCookieName);
                return;
            }

            var protectedPayload = _rememberedAccountsProtector.Protect(JsonSerializer.Serialize(accounts));
            HttpContext.Response.Cookies.Append(RememberMeConstants.AccountsCookieName, protectedPayload, new CookieOptions
            {
                HttpOnly = true,
                Secure = HttpContext.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                IsEssential = true,
                Expires = DateTimeOffset.UtcNow.AddDays(90)
            });
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [HttpGet]
        public IActionResult StatusCode(int code)
        {
            if (code == StatusCodes.Status404NotFound)
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                return View("NotFound");
            }

            return RedirectToAction(nameof(Error));
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
