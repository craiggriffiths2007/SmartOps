using Microsoft.EntityFrameworkCore;
using SmartOps.Web.Data;
using SmartOps.Web.Models;
using System.Collections.Concurrent;
using SkiaSharp;

namespace SmartOps.Web
{
    public static partial class EndpointMappings
    {


        private static bool IsDrawingFile(string filename)
        {
            // Example: 00000123_dDT00100.jpg
            // Only process recognised drawing types.

            if (filename.Length < 12 || filename[8] != '_')
                return false;

            string code = filename.Substring(9, 3);

            return code.Equals("dDT", StringComparison.OrdinalIgnoreCase)
                || code.Equals("dBT", StringComparison.OrdinalIgnoreCase)
                || code.Equals("dET", StringComparison.OrdinalIgnoreCase)
                || code.Equals("dRG", StringComparison.OrdinalIgnoreCase)
                || code.Equals("dAU", StringComparison.OrdinalIgnoreCase);
        }

        private static void CropDrawingWhitespace(string path)
        {
            if (!IsDrawingFile(Path.GetFileName(path)))
                return;

            string extension = Path.GetExtension(path).ToLowerInvariant();

            if (extension != ".jpg" &&
                extension != ".jpeg" &&
                extension != ".png")
                return;

            using var original = SKBitmap.Decode(path);

            if (original == null)
                return;

            int minX = original.Width;
            int minY = original.Height;
            int maxX = -1;
            int maxY = -1;

            // Find the bounding rectangle of all non-white pixels.
            // The threshold allows for minor JPEG artefacts.

            for (int y = 0; y < original.Height; y++)
            {
                for (int x = 0; x < original.Width; x++)
                {
                    SKColor pixel = original.GetPixel(x, y);

                    bool isBackground =
                        pixel.Alpha < 16 ||
                        (pixel.Red >= 245 &&
                         pixel.Green >= 245 &&
                         pixel.Blue >= 245);

                    if (isBackground)
                        continue;

                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x);
                    maxY = Math.Max(maxY, y);
                }
            }

            // Empty image: nothing to crop.
            if (maxX < minX || maxY < minY)
                return;

            // Retain a small white margin around the drawing.
            const int padding = 12;

            int left = Math.Max(0, minX - padding);
            int top = Math.Max(0, minY - padding);
            int right = Math.Min(original.Width, maxX + padding + 1);
            int bottom = Math.Min(original.Height, maxY + padding + 1);

            int width = right - left;
            int height = bottom - top;

            // Skip if the drawing already fills the image.
            if (left == 0 &&
                top == 0 &&
                width == original.Width &&
                height == original.Height)
                return;

            using var cropped = new SKBitmap(width, height);

            using (var canvas = new SKCanvas(cropped))
            {
                canvas.Clear(SKColors.White);

                var source = new SKRect(
                    left, top, right, bottom);

                var destination = new SKRect(
                    0, 0, width, height);

                canvas.DrawBitmap(original, source, destination);
                canvas.Flush();
            }

            // Preserve the original image format.
            SKEncodedImageFormat format =
                extension == ".png"
                    ? SKEncodedImageFormat.Png
                    : SKEncodedImageFormat.Jpeg;

            using var image = SKImage.FromBitmap(cropped);
            using var data = image.Encode(format, 95);

            if (data == null)
                throw new IOException("Could not encode cropped drawing.");

            // Save separately before replacing the original.
            string tempPath = path + ".cropped";

            try
            {
                using (var stream = File.Create(tempPath))
                {
                    data.SaveTo(stream);
                }

                File.Move(tempPath, path, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }


        public static void MapMediaEndpoints(this IEndpointRouteBuilder app)
        {
            var config = app.ServiceProvider.GetRequiredService<IConfiguration>();
            string imageDirectory = config["ImageStoragePath"] ?? @"C:\SmartOpsImages";

            app.MapPost("/GetImage", async (GetDataDTO gs, AppDBContext db) =>
            {
                // Find tenant
                var tenant = await db.Tenant
                    .FirstOrDefaultAsync(t =>
                        t.Code == gs.TenantCode &&
                        t.IsActive);

                if (tenant == null)
                {
                    return Results.BadRequest(
                        new { ReasonPhrase = "Tenant Code Not Found : " + gs.TenantCode });
                }

                int tenantId = tenant.Id;

                // Authenticate agent within this tenant
                var agent = await db.Agent
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(a =>
                        a.TenantId == tenantId &&
                        a.Code == gs.AgentCode &&
                        a.AuthenticationString == gs.AuthenticationString);

                if (agent == null)
                {
                    return Results.BadRequest(
                        new { ReasonPhrase = "Authentication Failed" });
                }

                // Sanitize filename
                string safeFilename = Path.GetFileName(gs.Filename);

                if (string.IsNullOrWhiteSpace(safeFilename))
                {
                    return Results.BadRequest(
                        new { ReasonPhrase = "Invalid filename" });
                }

                // Find image belonging to this tenant
                var image = await db.Images
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(i =>
                        i.TenantId == tenantId &&
                        i.Filename == safeFilename);

                if (image == null)
                    return Results.NotFound();

                byte[] bytes;

                try
                {
                    string filePath = Path.Combine(
                        imageDirectory,
                        tenantId.ToString(),
                        safeFilename);

                    if (File.Exists(filePath))
                    {
                        bytes = await File.ReadAllBytesAsync(filePath);
                    }
                    else if (!string.IsNullOrEmpty(image.Data))
                    {
                        // Legacy database-stored image
                        bytes = Convert.FromBase64String(image.Data);
                    }
                    else
                    {
                        return Results.NotFound();
                    }
                }
                catch
                {
                    return Results.StatusCode(500);
                }

                string ext =
                    Path.GetExtension(safeFilename)
                        .ToLowerInvariant();

                string contentType = ext switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".gif" => "image/gif",
                    ".bmp" => "image/bmp",
                    ".webp" => "image/webp",
                    ".mp4" => "video/mp4",
                    ".mov" => "video/quicktime",
                    _ => "application/octet-stream"
                };

                return Results.File(
                    bytes,
                    contentType,
                    safeFilename);
            });






            app.MapPost("/SendMedia", async (ImageDTO imageDTO, AppDBContext db) =>
            {
                OKRecordDTO return_record = new OKRecordDTO();

                try
                {
                    if (string.IsNullOrWhiteSpace(imageDTO.TenantCode))
                    {
                        return_record.comments = "Tenant code missing";
                        return Results.Ok(return_record);
                    }

                    var tenant = await db.Tenant.FirstOrDefaultAsync(t =>
                        t.Code == imageDTO.TenantCode && t.IsActive);

                    if (tenant == null)
                    {
                        return_record.comments = "Invalid tenant";
                        return Results.Ok(return_record);
                    }

                    int tenantId = tenant.Id;

                    var agent = await db.Agent
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(x =>
                        x.TenantId == tenantId &&
                        x.Code == imageDTO.AgentCode &&
                        x.AuthenticationString == imageDTO.AuthenticationString);

                    if (agent == null)
                    {
                        return Results.BadRequest(
                            new { ReasonPhrase = "Authentication Failed" });
                    }

                    string safeFilename = Path.GetFileName(
                        imageDTO.Filename ?? string.Empty);

                    if (string.IsNullOrWhiteSpace(safeFilename))
                    {
                        return_record.comments = "Invalid filename";
                        return Results.Ok(return_record);
                    }

                    var allowed = new[] { ".mp4", ".mov", ".jpg", ".jpeg", ".png" };

                    string ext = Path.GetExtension(safeFilename).ToLowerInvariant();

                    if (!allowed.Contains(ext))
                    {
                        return_record.comments = "File type not allowed";
                        return Results.Ok(return_record);
                    }

                    string tenantImageDirectory = Path.Combine(
                        imageDirectory, tenantId.ToString());

                    Directory.CreateDirectory(tenantImageDirectory);

                    string finalPath = Path.Combine(
                        tenantImageDirectory, safeFilename);

                    string tempPath = finalPath + ".part";

                    byte[] chunkBytes = Convert.FromBase64String(
                        imageDTO.Data ?? string.Empty);

                    // ---------------------------------------------------------
                    // CHUNKED UPLOAD
                    // ---------------------------------------------------------

                    if (imageDTO.ChunkIndex.HasValue && imageDTO.TotalChunks.HasValue)
                    {
                        int chunkIndex = imageDTO.ChunkIndex.Value;
                        int totalChunks = imageDTO.TotalChunks.Value;

                        // Include TenantId because two tenants can have
                        // exactly the same filename.
                        string fileKey = $"{tenantId}:{safeFilename}";

                        var lockObj = _uploadLocks.GetOrAdd(
                            fileKey, _ => new object());

                        try
                        {
                            lock (lockObj)
                            {
                                using (var fs = new FileStream(
                                    tempPath,
                                    FileMode.OpenOrCreate,
                                    FileAccess.Write,
                                    FileShare.None))
                                {
                                    fs.Seek(0, SeekOrigin.End);
                                    fs.Write(chunkBytes, 0, chunkBytes.Length);
                                    fs.Flush(true);
                                }

                                // Final chunk
                                if (chunkIndex == totalChunks)
                                {
                                    if (File.Exists(finalPath))
                                        File.Delete(finalPath);

                                    File.Move(tempPath, finalPath);

                                    // Crop white margins once all chunks have arrived.
                                    //CropDrawingWhitespace(finalPath);

                                    string contractCode = safeFilename.Length >= 8
                                        ? safeFilename.Substring(0, 8)
                                        : string.Empty;

                                    var existing = db.Images
                                        .IgnoreQueryFilters()
                                        .FirstOrDefault(i =>
                                            i.TenantId == tenantId &&
                                            i.Filename == safeFilename);

                                    if (existing != null)
                                    {
                                        existing.DateTime = DateTime.Now;
                                        existing.ContractCode = contractCode;
                                        existing.Data = string.Empty;

                                        db.Update(existing);
                                    }
                                    else
                                    {
                                        PhotoImage image = new PhotoImage
                                        {
                                            TenantId = tenantId,
                                            Filename = safeFilename,
                                            Data = string.Empty,
                                            DateTime = DateTime.Now,
                                            ContractCode = contractCode
                                        };

                                        db.Add(image);
                                    }

                                    db.SaveChanges();
                                }
                            }
                        }
                        finally
                        {
                            _uploadLocks.TryRemove(fileKey, out _);
                        }
                    }

                    // ---------------------------------------------------------
                    // NORMAL NON-CHUNKED UPLOAD
                    // ---------------------------------------------------------

                    else
                    {
                        await File.WriteAllBytesAsync(finalPath, chunkBytes);

                        // Crop white margins for drawing images only.
                        //CropDrawingWhitespace(finalPath);

                        string contractCode = safeFilename.Length >= 8
                            ? safeFilename.Substring(0, 8)
                            : string.Empty;

                        var existing = await db.Images
                            .IgnoreQueryFilters()
                            .FirstOrDefaultAsync(i =>
                                i.TenantId == tenantId &&
                                i.Filename == safeFilename);

                        if (existing != null)
                        {
                            existing.DateTime = DateTime.Now;
                            existing.ContractCode = contractCode;
                            existing.Data = string.Empty;

                            db.Update(existing);
                        }
                        else
                        {
                            PhotoImage image = new PhotoImage
                            {
                                TenantId = tenantId,
                                Filename = safeFilename,
                                Data = string.Empty,
                                DateTime = DateTime.Now,
                                ContractCode = contractCode
                            };

                            db.Add(image);
                        }

                        await db.SaveChangesAsync();
                    }

                    return_record.comments = "Success";
                    return Results.Ok(return_record);
                }
                catch (Exception ex)
                {
                    return_record.comments = $"File Save Failed: {ex.Message}";
                    return Results.Ok(return_record);
                }
            });

            app.MapGet("/debug-paths", (IWebHostEnvironment env) =>
            {
                return Results.Ok(new
                {
                    contentRoot = env.ContentRootPath,
                    webRoot = env.WebRootPath,
                    currentDirectory = Directory.GetCurrentDirectory()
                });
            });

            // Serve images by filename via GET so <img src="/images/filename"> works
            app.MapGet(
                    "/images/{filename}",
                    (string filename, ITenantContext tenantContext) =>
                    {
                        try
                        {
                            if (!tenantContext.TenantId.HasValue)
                            {
                                return Results.NotFound(new
                                {
                                    reason = "No TenantId"
                                });
                            }

                            int tenantId = tenantContext.TenantId.Value;

                            string safe = Path.GetFileName(filename);

                            string filePath = Path.Combine(
                                imageDirectory,
                                tenantId.ToString(),
                                safe);

                            if (!File.Exists(filePath))
                            {
                                return Results.NotFound(new
                                {
                                    reason = "File does not exist",
                                    tenantId = tenantId,
                                    imageDirectory = imageDirectory,
                                    filename = safe,
                                    filePath = filePath
                                });
                            }

                            var ext = Path.GetExtension(safe)?.ToLowerInvariant();

                            var contentType = ext switch
                            {
                                ".jpg" or ".jpeg" => "image/jpeg",
                                ".png" => "image/png",
                                ".gif" => "image/gif",
                                ".bmp" => "image/bmp",
                                ".webp" => "image/webp",
                                ".mp4" => "video/mp4",
                                _ => "application/octet-stream"
                            };

                            var bytes = File.ReadAllBytes(filePath);

                            return Results.File(
                                bytes,
                                contentType
                                );
                        }
                        catch
                        {
                            return Results.StatusCode(500);
                        }
                    });

            app.MapPost(
                    "/Customer/GetImage",
                    async (CustomerImageRequestDTO request, AppDBContext db) =>
                    {
                        try
                        {
                            if (string.IsNullOrWhiteSpace(request.AuthenticationString))
                                return Results.Unauthorized();

                            string safeFilename =
                                Path.GetFileName(request.Filename);

                            if (string.IsNullOrWhiteSpace(safeFilename))
                                return Results.BadRequest();

                            // Authenticate the customer
                            var customer = await db.Customer
                                .IgnoreQueryFilters()
                                .FirstOrDefaultAsync(c =>
                                    c.AuthenticationString ==
                                        request.AuthenticationString);

                            if (customer == null)
                                return Results.Unauthorized();

                            // Make sure the requested contract belongs
                            // to this customer and tenant.
                            var contract = await db.Contract
                                .IgnoreQueryFilters()
                                .FirstOrDefaultAsync(c =>
                                    c.Id == request.ContractId &&
                                    c.CustomerId == customer.Id &&
                                    c.TenantId == customer.TenantId);

                            if (contract == null)
                                return Results.Unauthorized();

                            // ContractCode is NotMapped, so calculate it here
                            // rather than using it inside an EF query.
                            string contractCode =
                                contract.ContractNumber.ToString("D8");

                            // Find only a customer-visible image belonging
                            // to this contract and tenant.
                            var image = await db.Images
                                .IgnoreQueryFilters()
                                .FirstOrDefaultAsync(i =>
                                    i.TenantId == contract.TenantId &&
                                    i.ContractCode == contractCode &&
                                    i.Filename == safeFilename &&
                                    i.CustomerVisible);

                            if (image == null)
                                return Results.NotFound();

                            string filePath = Path.Combine(
                                imageDirectory,
                                contract.TenantId.ToString(),
                                safeFilename);

                            if (!File.Exists(filePath))
                                return Results.NotFound();

                            string ext =
                                Path.GetExtension(safeFilename)
                                    .ToLowerInvariant();

                            string contentType = ext switch
                            {
                                ".jpg" or ".jpeg" => "image/jpeg",
                                ".png" => "image/png",
                                ".gif" => "image/gif",
                                ".bmp" => "image/bmp",
                                ".webp" => "image/webp",
                                _ => "application/octet-stream"
                            };

                            byte[] bytes =
                                await File.ReadAllBytesAsync(filePath);

                            return Results.File(
                                bytes,
                                contentType);
                        }
                        catch
                        {
                            return Results.StatusCode(500);
                        }
                    });

            // Export images stored as base64 in DB to files and clear Data column
            app.MapPost("/ExportImages", async (AppDBContext db) =>
            {
                var imagesWithData = db.Images.Where(i => !string.IsNullOrEmpty(i.Data)).ToList();
                var errors = new List<string>();
                int written = 0;

                Directory.CreateDirectory(imageDirectory);

                foreach (var img in imagesWithData)
                {
                    try
                    {
                        string safe = Path.GetFileName(img.Filename);
                        string filePath = Path.Combine(imageDirectory, safe);
                        byte[] bytes = Convert.FromBase64String(img.Data);
                        await File.WriteAllBytesAsync(filePath, bytes);
                        img.Data = string.Empty;
                        db.Update(img);
                        written++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{img.Filename}: {ex.Message}");
                    }
                }

                await db.SaveChangesAsync();

                return Results.Ok(new { written, errors });
            });
        }
    }
}
