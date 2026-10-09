using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using KitApi.Api.Authorization;
using KitApi.Application.Abstractions.Services;
using KitApi.Application.Features.Auth;
using KitApi.Infrastructure.Identity;
using KitApi.Infrastructure.Persistence;

namespace KitApi.Extensions;

public static class SecurityExtensions
{
    /// <summary>Identity local + JWT + permisos (standards/05 y 06).</summary>
    public static IServiceCollection AddSecurity(this IServiceCollection services)
    {
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        services.AddOptions<JwtOptions>().BindConfiguration(JwtOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<AppUrlOptions>().BindConfiguration(AppUrlOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<SeedOptions>().BindConfiguration(SeedOptions.SectionName);

        // Identity local: sin cookies, sin proveedores externos.
        services.AddIdentityCore<AppUser>(o =>
            {
                o.User.RequireUniqueEmail = true;
                o.SignIn.RequireConfirmedEmail = true;

                o.Password.RequiredLength = 8;
                o.Password.RequireDigit = true;
                o.Password.RequireLowercase = true;
                o.Password.RequireUppercase = true;
                o.Password.RequireNonAlphanumeric = true;
                o.Password.RequiredUniqueChars = 4;

                o.Lockout.AllowedForNewUsers = true;
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<AppRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();   // email confirm, reset password, change email, authenticator (TOTP)

        // Vigencia de los tokens de email (confirmación, reset, cambio de email).
        services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromHours(3));

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((o, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                o.MapInboundClaims = false;   // claims con su nombre JWT ("sub", "role", "permission")
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,   // el token de 2FA usa otra audiencia y es rechazado
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = ClaimNames.Subject,
                    RoleClaimType = ClaimNames.Role
                };
            });

        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddAuthorization();

        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<SessionManager>();
        services.AddScoped<AccountEmails>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddScoped<IRoleService, RoleService>();

        return services;
    }
}
