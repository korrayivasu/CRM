using System;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Web.Data;
using AcxiomCRM.Web.Models.Entities;
using AcxiomCRM.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Database Configuration with SQLite
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Data Source=acxiom_crm.db";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

// 2. ASP.NET Core Identity Configuration
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password policy per specification (Section 6.2 & 17.4)
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;

    // Account lockout policy per specification (Section 6.3 & 17.4)
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User policy
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// 3. Cookie Configuration
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.SlidingExpiration = true;
});

// 4. Anti-Forgery Protection (Section 17.9)
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
});

// 5. Application Services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditService, AuditService>();

// 6. MVC & API Controllers
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        // Prevent JSON cyclical references in REST API
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

// 7. Swagger Documentation for REST API (Section 10 & 17.14)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "AcxiomCRM REST API",
        Version = "v1",
        Description = "Secure RESTful API for AcxiomCRM Customers, Leads, Opportunities, Follow-ups, and Reporting"
    });
});

var app = builder.Build();

// 8. Seed Database on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        await DbInitializer.SeedAsync(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

// 9. HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Swagger API Documentation UI
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "AcxiomCRM API v1");
    c.RoutePrefix = "swagger";
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();
