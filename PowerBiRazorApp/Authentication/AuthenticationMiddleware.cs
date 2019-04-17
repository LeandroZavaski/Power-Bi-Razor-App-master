using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace PowerBiRazorApp.Authentication
{
    public class AuthenticationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IMemoryCache _cache;


        public AuthenticationMiddleware(RequestDelegate next, IConfiguration configuration, IMemoryCache memoryCache)
        {
            _next = next;
            _cache = memoryCache;
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public async Task Invoke(HttpContext context)
        {

            if (context.User.Identity.IsAuthenticated)
            {
                await _next.Invoke(context);
                return;
            }

            await ProcessAsync(context);
        }

        public async Task ProcessAsync(HttpContext context)
        {
            try
            {
                var count = Convert.ToInt32(_cache.Get("count") ?? 0);

                if (context.Request.Query.Keys.Count == 0)
                {
                    if (count < 1)
                    {
                        count += 1;
                        _cache.Set("count", count);
                        await context.Response.WriteAsync(
                        "<html>" +
                        "<body>" +
                        "<script>" +
                        "window.location = window.location.origin + window.location.pathname + " +
                        "'?' + window.location.hash.substr(1)" +
                        "</script>" +
                        "</body>" +
                        "</html>");

                        return;
                    }
                    else
                    {
                        _cache.Remove("count");
                        context.Response.Redirect(Configuration["OpenId:redirectUri"]);
                    }
                }
                var items = context.Request.Query.ToDictionary(field => field.Key, field => field.Value.First());

                if (items.Count == 0)
                    return;

                if (!items.ContainsKey("access_token"))
                    return;

                var accessToken = items["access_token"];

                var expiresIn = items["expires_in"];

                await ValidateAsyncToken(context, accessToken, expiresIn);
            }
            catch (Exception)
            {
                context.Response.Redirect(Configuration["OpenId:redirectUri"]);
            }
        }

        private async Task ValidateAsyncToken(HttpContext context, string accessToken, string expiresIn)
        {
            var store = new X509Store(StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);
            var certCollection = store.Certificates.Find(X509FindType.FindByThumbprint, Configuration["OpenId:CertificateValue"], false);
            var issuerSigningKey = new X509SecurityKey(new X509Certificate2(new X509Certificate(certCollection[0])));

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = false,
                IssuerSigningKey = issuerSigningKey
            };

            var validator = new JwtSecurityTokenHandler();

            if (!validator.CanReadToken(accessToken))
                throw new UnauthorizedAccessException("token is not readable");

            var validate = validator.ValidateToken(accessToken, validationParameters, out var validatedToken);

            var claimsIdentity = new ClaimsIdentity(validate.Claims, CookieAuthenticationDefaults.AuthenticationScheme);

            var principal = new ClaimsPrincipal(claimsIdentity);

            var authProperties = new AuthenticationProperties
            {
                ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(double.Parse(expiresIn)),
                IsPersistent = true
            };

            if (principal.HasClaim(c => c.Type == ClaimTypes.Email))
            {
                await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);

                context.User.AddIdentity((ClaimsIdentity)principal.Identity);

                context.Response.Headers.Add("Bearer", accessToken);
            }

            await _next.Invoke(context);
        }
    }
}
