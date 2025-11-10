using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

using Tomlyn.Extensions.Configuration;

namespace Cyberboss.OidcReverseProxy
{
	public class Program
	{
		public static async Task Main(string[] args)
		{
			var builder = WebApplication.CreateBuilder(args);

			const string AppSettings = "appsettings";
			const string TomlExtension = ".toml";
			builder
				.Configuration
				.AddTomlFile($"{AppSettings}{TomlExtension}")
				.AddTomlFile($"{AppSettings}.{builder.Environment.EnvironmentName}{TomlExtension}")
				.AddEnvironmentVariables();

			builder
				.Services
				.AddAuthentication(options =>
				{
					options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
					options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
				})
				.AddCookie(options =>
				{
					options.ExpireTimeSpan = TimeSpan.FromMinutes(builder.Configuration.GetRequiredSection("CookieDurationMinutes").Get<uint>()); // Set desired session duration
					options.SlidingExpiration = true; // Extend session on activity
				})
				.AddOpenIdConnect(options =>
				{
					var oidcConfig = builder.Configuration.GetRequiredSection("OpenIDConnectSettings");

					options.Authority = oidcConfig["Authority"];
					options.ClientId = oidcConfig["ClientId"];
					options.ClientSecret = oidcConfig["ClientSecret"];

					options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
					options.ResponseType = OpenIdConnectResponseType.Code;

					options.SaveTokens = true;
				});

			builder.Host.UseConsoleLifetime();

			using var app = builder.Build();
			var forwardedHeaderOptions = new ForwardedHeadersOptions
			{
				ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost,
			};

			forwardedHeaderOptions.KnownNetworks.Clear();
			forwardedHeaderOptions.KnownNetworks.Add(
				new IPNetwork(
					global::System.Net.IPAddress.Any,
					0));

			app
				.UseForwardedHeaders(forwardedHeaderOptions)
				.UseRouting()
				.UseAuthentication()
				.UseMiddleware<ReverseProxyMiddleware>()
				.UseEndpoints(endpoints =>
				{
					endpoints.MapGet(
						"/",
						context => context.ChallengeAsync(
							new AuthenticationProperties
							{
								RedirectUri = "/oidc-landing",
							}));
					endpoints.MapGet(
						"/oidc-landing",
						context =>
						{
							context.Response.Redirect("/");
							return Task.CompletedTask;
						});
				});

			await app.RunAsync();
		}
	}
}
