using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace SmartOps.Web.Helpers
{
    public class ReplaceRedirectResult : IActionResult
    {
        private readonly string _url;

        public ReplaceRedirectResult(string url)
        {
            _url = url;
        }

        public async Task ExecuteResultAsync(ActionContext context)
        {
            var response = context.HttpContext.Response;

            response.ContentType = "text/html; charset=utf-8";

            // Safely encode the URL as a JavaScript string.
            var jsonUrl = JsonSerializer.Serialize(_url);

            var html = $"""
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset="utf-8" />
                    <title>Redirecting...</title>
                </head>
                <body>
                    <script>
                        window.location.replace({jsonUrl});
                    </script>
                </body>
                </html>
                """;

            await response.WriteAsync(html);
        }
    }
}