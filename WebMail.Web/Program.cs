using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Razor;
using WebMail.Web.Middlewares;
using WebMail.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<MailConfigService>();
builder.Services.AddScoped<MailService>();
builder.Services.AddSingleton<IDataProtector>(provider =>
{
    var dataProtectionProvider = provider.GetRequiredService<IDataProtectionProvider>();
    return dataProtectionProvider.CreateProtector("WebMail.Auth.PasswordProtector");
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login"; // مسیری که کاربر لاگین‌نشده به آن پاس داده می‌شود
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/Login"; // در صورت نداشتن دسترسی
        options.ExpireTimeSpan = TimeSpan.FromHours(2); // مدت زمان اعتبار لاگین
    });

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.AddControllersWithViews()
    .AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix)
    .AddDataAnnotationsLocalization();

builder.Services.AddMemoryCache();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

var supportedCultures = new[] { "fa", "en" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("fa") // زبان پیش‌فرض
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

app.UseRequestLocalization(localizationOptions);

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.UseMiddleware<SetupMiddleware>();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
