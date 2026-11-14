using SmartOps.Web.Data;
using SmartOps.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace SmartOps.Web.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly AppDBContext _context;
        private readonly IWebHostEnvironment _environment;

        public HomeController(
            ILogger<HomeController> logger,
            AppDBContext context,
            IWebHostEnvironment environment)
        {
            _logger = logger;
            _context = context;
            _environment = environment;
        }

        private async Task RecordPageHit(string page)
        {
            try
            {
                _context.PageHits.Add(new PageHit
                {
                    Page = page,
                    HitDate = DateTime.UtcNow,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    Referrer = Request.Headers.Referer.ToString(),
                    UserAgent = Request.Headers.UserAgent.ToString()
                });

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to record page hit for {Page}",
                    page);
            }
        }




        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> SurveyAppVersion(
            [FromBody] SurveyAppVersionRequestDto request,
            [FromServices] IHttpClientFactory clients)
        {
            using var client = clients.CreateClient("4D");
            try
            {
                using var response = await client.PostAsJsonAsync("Home/SurveyAppVersion", request);
                return new ContentResult
                {
                    Content = await response.Content.ReadAsStringAsync(),
                    ContentType = "application/json; charset=utf-8",
                    StatusCode = (int)response.StatusCode
                };
            }
            catch (HttpRequestException error)
            {
                _logger.LogWarning(error, "Unable to reach 4D for the Survey app version check");
                return StatusCode(502, new { message = "Unable to check app version." });
            }
        }
        [AllowAnonymous]
        public IActionResult Index()
        {
            return User.Identity?.IsAuthenticated == true
                ? Redirect("/Jobs")
                : Redirect("/Account/Login");
        }

        [HttpGet]
        [Authorize(Roles = "SuperAdmin")]
        public IActionResult CheckSmartOpsImagePaths()
        {
            const string source =
                @"C:\SmartOpsImages";

            const string destination =
                @"D:\Sites\site87469\private\SmartOpsImages";

            return Json(new
            {
                source,
                sourceExists = Directory.Exists(source),

                sourceFiles = Directory.Exists(source)
                    ? Directory.GetFiles(
                        source,
                        "*",
                        SearchOption.AllDirectories).Length
                    : 0,

                destination,
                destinationExists =
                    Directory.Exists(destination)
            });
        }

        [HttpGet]

        [Authorize(Roles = "SuperAdmin")]
        public IActionResult MoveSmartOpsImages()
        {
            const string sourceRoot = @"C:\SmartOpsImages";
            const string destinationRoot =
                @"D:\Sites\site87469\private\SmartOpsImages";

            try
            {
                if (!Directory.Exists(sourceRoot))
                {
                    return Content(
                        $"Source directory does not exist:\r\n{sourceRoot}");
                }

                Directory.CreateDirectory(destinationRoot);

                int filesMoved = 0;
                int filesSkipped = 0;
                int directoriesCreated = 0;

                var sourceFiles = Directory.GetFiles(
                    sourceRoot,
                    "*",
                    SearchOption.AllDirectories);

                foreach (var sourceFile in sourceFiles)
                {
                    // Preserve the path underneath C:\SmartOpsImages
                    var relativePath =
                        Path.GetRelativePath(
                            sourceRoot,
                            sourceFile);

                    var destinationFile =
                        Path.Combine(
                            destinationRoot,
                            relativePath);

                    var destinationDirectory =
                        Path.GetDirectoryName(destinationFile);

                    if (!string.IsNullOrWhiteSpace(destinationDirectory) &&
                        !Directory.Exists(destinationDirectory))
                    {
                        Directory.CreateDirectory(destinationDirectory);
                        directoriesCreated++;
                    }

                    /*
                     * Safety:
                     * Don't overwrite an existing file.
                     *
                     * This is useful if you've already moved some files
                     * or the destination contains newer uploads.
                     */
                    if (System.IO.File.Exists(destinationFile))
                    {
                        filesSkipped++;
                        continue;
                    }

                    System.IO.File.Move(
                        sourceFile,
                        destinationFile);

                    filesMoved++;
                }

                /*
                 * Delete empty folders left behind.
                 * Work from the deepest directory upwards.
                 */
                var sourceDirectories = Directory
                    .GetDirectories(
                        sourceRoot,
                        "*",
                        SearchOption.AllDirectories)
                    .OrderByDescending(x => x.Length)
                    .ToList();

                foreach (var directory in sourceDirectories)
                {
                    if (!Directory.EnumerateFileSystemEntries(directory).Any())
                    {
                        Directory.Delete(directory);
                    }
                }

                /*
                 * Delete C:\SmartOpsImages itself if everything
                 * was successfully moved and nothing remains.
                 */
                if (Directory.Exists(sourceRoot) &&
                    !Directory.EnumerateFileSystemEntries(sourceRoot).Any())
                {
                    Directory.Delete(sourceRoot);
                }

                return Content(
                    "SmartOps image migration complete.\r\n\r\n" +
                    $"Source: {sourceRoot}\r\n" +
                    $"Destination: {destinationRoot}\r\n\r\n" +
                    $"Files moved: {filesMoved}\r\n" +
                    $"Files skipped because they already existed: {filesSkipped}\r\n" +
                    $"Directories created: {directoriesCreated}");
            }
            catch (Exception ex)
            {
                return Content(
                    "SmartOps image migration FAILED.\r\n\r\n" +
                    ex.ToString());
            }
        }


        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
