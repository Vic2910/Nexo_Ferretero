using Ferre.Options;
using Ferre.Services.Auth;
using Ferre.Services.Notifications;
using Ferre.Services.Orders;
using Ferre.Services.Support;
using Ferre.Middleware;
using Microsoft.AspNetCore.DataProtection;
using Supabase;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services
    .AddDataProtection()
    .SetApplicationName("Ferre")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtectionKeys")));

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.AddScoped<Ferre.Services.Catalog.ICategoryService, Ferre.Services.Catalog.SupabaseCategoryService>();
builder.Services.AddScoped<Ferre.Services.Catalog.IProductService, Ferre.Services.Catalog.SupabaseProductService>();

builder.Services.Configure<SupabaseSettings>(builder.Configuration.GetSection("Supabase"));
builder.Services.Configure<PayPalSettings>(builder.Configuration.GetSection("PayPal"));
builder.Services.AddSingleton(provider =>
{
    var settings = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SupabaseSettings>>().Value;
    var options = new SupabaseOptions
    {
        AutoConnectRealtime = false,
        AutoRefreshToken = true
    };

    return new Client(settings.Url, settings.AnonKey, options);
});
builder.Services.AddScoped<ISupabaseAuthService, SupabaseAuthService>();
builder.Services.AddScoped<IAdminPermissionService, AdminPermissionService>();
builder.Services.AddScoped<INotificationService, SessionNotificationService>();
builder.Services.AddScoped<IClientPurchaseService, SupabaseClientPurchaseService>();
builder.Services.AddSingleton<IPurchaseReceiptPdfService, PurchaseReceiptPdfService>();
builder.Services.AddScoped<IClientContactMessageService, SupabaseClientContactMessageService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();
app.UseMiddleware<RememberMeSessionMiddleware>();

app.UseAuthorization();

app.UseStatusCodePagesWithReExecute("/Home/StatusCode", "?code={0}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
