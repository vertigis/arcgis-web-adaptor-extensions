using System.Security.Claims;
using System.Security.Principal;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

[assembly: HostingStartup(typeof(VertiGIS.WebAdaptorExtensions.Startup))]

namespace VertiGIS.WebAdaptorExtensions
{
    /// <summary>
    /// Injects some behavior into portal/server web adaptors.
    /// </summary>
    public sealed class Startup : IHostingStartup, IStartupFilter
    {
        void IHostingStartup.Configure(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.AddTransient<IStartupFilter>(_ => this);
            });
        }

        Action<IApplicationBuilder> IStartupFilter.Configure(Action<IApplicationBuilder> next)
        {
            return builder =>
            {
                builder.Use(x => ctx => Invoke(ctx, x));
                next(builder);
            };
        }

        private static readonly Lazy<Task<ConfigFile>> _hints = new(ConfigFile.InitializeAsync);

        private static async Task<bool> Detect(HttpContext ctx)
        {
            var config = await _hints.Value;
            var resolved = config.TrustedServiceAccountsResolutions;
            foreach (var claim in ctx.User.Claims)
            {
                if (claim.Type == ClaimTypes.PrimarySid && resolved.Contains(claim.Value))
                {
                    return true;
                }

                if (claim.Type == ClaimTypes.GroupSid && resolved.Contains(claim.Value))
                {
                    return true;
                }
            }

            return false;
        }

        private static async Task Invoke(HttpContext ctx, RequestDelegate next)
        {
            if (await Detect(ctx))
            {
                ctx.User = new GenericPrincipal(new GenericIdentity(string.Empty), null);
            }

            await next(ctx);
        }
    }
}