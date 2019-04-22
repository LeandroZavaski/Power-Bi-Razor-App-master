using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using PowerBiRazorApp.Authentication;
using PowerBiRazorApp.Authentication.AuthenticationHandler;
using PowerBiRazorApp.DataAccess;
using PowerBiRazorApp.Models;

namespace PowerBiRazorApp

{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<CookiePolicyOptions>(options =>
            {
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
                })
                .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
                {
                    options.Events = new CookieAuthenticationEvents
                    {
                        OnSignedIn = context => Task.CompletedTask,
                        OnSigningOut = context => Task.CompletedTask,
                        OnValidatePrincipal = context => Task.CompletedTask,
                    };
                    options.SlidingExpiration = true;
                })
                .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
                {
                    options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.Authority = Configuration["OpenId:Authority"];
                    options.RequireHttpsMetadata = false;
                    options.ClientId = Configuration["OpenId:ClientId"];
                    options.ResponseType = OpenIdConnectResponseType.IdToken;
                    options.SaveTokens = true;
                    options.TokenValidationParameters.ValidateIssuer = false;
                    options.GetClaimsFromUserInfoEndpoint = true;
                });

            services.AddMvc();
            services.AddDistributedMemoryCache();
            services.AddSession();

            var azureConfig = Configuration.GetSection("AzureAd");
            services.Configure<AzureAdSettings>(azureConfig);

            var powerbiConfig = Configuration.GetSection("PowerBi");
            services.Configure<PowerBiSettings>(powerbiConfig);

            services.AddScoped<AuthenticationHandler>();
            services.AddScoped<ReportRepository>();
        }


        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseBrowserLink();
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
            }

            app.UseStaticFiles();
            app.UseCookiePolicy();
            app.UseSession();

            app.UseAuthentication();
            app.UseMiddleware(typeof(AuthenticationPingMiddleware));

            app.UseExceptionHandler(errorApp =>
            {
                errorApp.Run(async context =>
                {
                    context.Response.StatusCode = 500;
                    context.Response.ContentType = "application/json";

                    var error = context.Features.Get<IExceptionHandlerFeature>();
                    if (error != null)
                    {
                        var ex = error.Error;

                        await context.Response.WriteAsync(new ErrorViewModel()
                        {
                            Code = ex.HResult,
                            Message = ex.Message
                        }.ToString(), Encoding.UTF8);
                    }
                });
            });

            app.UseMvc();
        }
    }
}
