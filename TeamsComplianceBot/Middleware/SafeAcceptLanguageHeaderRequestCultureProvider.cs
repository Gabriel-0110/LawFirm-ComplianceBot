using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using System.Linq;
using System.Collections.Generic;

namespace TeamsComplianceBot.Middleware
{
    /// <summary>
    /// Safe implementation of AcceptLanguageHeaderRequestCultureProvider that never throws CultureNotFoundException
    /// </summary>
    public class SafeAcceptLanguageHeaderRequestCultureProvider : RequestCultureProvider
    {
        /// <summary>
        /// The default fallback culture
        /// </summary>
        public static readonly CultureInfo DefaultFallbackCulture = CultureInfo.InvariantCulture;

        /// <inheritdoc />
        public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
        {
            if (httpContext == null)
                return Task.FromResult<ProviderCultureResult?>(null);

            var acceptLanguageHeader = httpContext.Request.Headers["Accept-Language"].ToString();
            
            if (string.IsNullOrEmpty(acceptLanguageHeader))
                return Task.FromResult<ProviderCultureResult?>(null);

            var cultureCodes = acceptLanguageHeader.Split(',')
                .Select(s => s.Split(';').First().Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            if (cultureCodes.Count == 0)
                return Task.FromResult<ProviderCultureResult?>(null);

            // Filter out any invalid culture codes that would cause exceptions
            var validCultures = new List<string>();
            foreach (var code in cultureCodes)
            {
                try
                {
                    // Only add cultures that can be successfully created
                    if (code.Length <= 10 && (code.Contains('-') || code.All(c => char.IsLetter(c))))
                    {
                        CultureInfo.CreateSpecificCulture(code);
                        validCultures.Add(code);
                    }
                }
                catch (CultureNotFoundException)
                {
                    // Skip invalid cultures
                    continue;
                }
            }

            if (validCultures.Count == 0)
                return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(DefaultFallbackCulture.Name));

            return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(validCultures[0], validCultures.Count > 1 ? validCultures[1] : validCultures[0]));
        }
    }
}
