using System.Net.Mail;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using SmartOps.Web.Services;

namespace SmartOps.Web;

// Separate persistent tracking store: the bridge's normal SQLite workspace is
// intentionally in-memory and MUST NOT be used for journey state.
public sealed class SurveyTrackingStore
{
    private readonly string connectionString;

    public SurveyTrackingStore(IConfiguration config)
    {
        var file = config["SurveyTracking:DatabasePath"];
        if (string.IsNullOrWhiteSpace(file))
            file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SmartOps", "survey-tracking.db");
        file = Path.GetFullPath(file);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = file,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = true
        }.ToString();
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS SurveyJourneys (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                TenantCode TEXT NOT NULL, AgentCode TEXT NOT NULL, ContractCode TEXT NOT NULL,
                DiaryDate TEXT NOT NULL, ViewerTokenHash TEXT NOT NULL UNIQUE,
                UploadTokenHash TEXT NOT NULL, StartedUtc TEXT NOT NULL,
                ExpiresUtc TEXT NOT NULL, FinishedUtc TEXT,
                Latitude REAL, Longitude REAL, UpdatedUtc TEXT
            );
            CREATE INDEX IF NOT EXISTS IX_SurveyJourneyVisit
              ON SurveyJourneys(TenantCode, AgentCode, ContractCode, DiaryDate);
            """;
        cmd.ExecuteNonQuery();
    }

    public SqliteConnection Open()
    {
        var db = new SqliteConnection(connectionString);
        db.Open();
        return db;
    }

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public void CleanupExpired()
    {
        using var db = Open();
        using var clean = db.CreateCommand();
        clean.CommandText = """
            UPDATE SurveyJourneys SET Latitude=NULL, Longitude=NULL, UpdatedUtc=NULL
            WHERE ExpiresUtc<=@now AND (Latitude IS NOT NULL OR Longitude IS NOT NULL);
            DELETE FROM SurveyJourneys WHERE ExpiresUtc<@purge;
            """;
        clean.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));
        clean.Parameters.AddWithValue("@purge", DateTime.UtcNow.AddDays(-1).ToString("O"));
        clean.ExecuteNonQuery();
    }
}

public sealed class SurveyTrackingCleanupService(SurveyTrackingStore store, ILogger<SurveyTrackingCleanupService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { store.CleanupExpired(); }
            catch (Exception error) { logger.LogError(error, "Survey location cleanup failed"); }

            try { await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}

public static class SurveyTrackingEndpoints
{
    public sealed record StartRequest(string TenantCode, string AgentCode,
        string AuthenticationString, string ContractCode, DateTime DiaryDate);
    public sealed record UpdateRequest(int JourneyId, string UploadToken,
        double Latitude, double Longitude);
    public sealed record FinishRequest(int JourneyId, string UploadToken);

    public static void MapSurveyTracking(this WebApplication app)
    {
        app.MapPost("/api/survey-tracking/start", async (
            StartRequest request, SurveyTrackingStore store, IHttpClientFactory clients,
            IConfiguration config, EmailSender mail, ILoggerFactory log, HttpContext context) =>
        {
            var publicUrl = config["SurveyTracking:PublicBaseUrl"]?.TrimEnd('/');
            var emailConfig = config.GetSection("EmailSettings").Get<EmailSettings>();
            if (string.IsNullOrEmpty(publicUrl) || !Uri.TryCreate(publicUrl, UriKind.Absolute, out var parsed) ||
                parsed.Scheme != Uri.UriSchemeHttps || emailConfig is null ||
                string.IsNullOrWhiteSpace(emailConfig.SmtpServer) ||
                string.IsNullOrWhiteSpace(emailConfig.SenderEmail))
                return Results.Problem("Tracking host requires an HTTPS PublicBaseUrl and SMTP EmailSettings.", statusCode: 503);
            if (string.IsNullOrWhiteSpace(request.TenantCode) ||
                string.IsNullOrWhiteSpace(request.AgentCode) ||
                string.IsNullOrWhiteSpace(request.AuthenticationString) ||
                string.IsNullOrWhiteSpace(request.ContractCode) || request.DiaryDate == default)
                return Results.BadRequest();

            // Re-check job assignment and the active 4D login token on EVERY start.
            // Never trust a client-provided customer email.
            string? recipient;
            try
            {
                using var client = clients.CreateClient("4D");
                using var verify = await client.PostAsJsonAsync("SurveyTrackingVerify", request,
                    context.RequestAborted);
                if (!verify.IsSuccessStatusCode)
                    return Results.StatusCode((int)verify.StatusCode);
                using var document = JsonDocument.Parse(await verify.Content.ReadAsStringAsync(context.RequestAborted));
                recipient = document.RootElement.GetProperty("email").GetString()?.Trim();
            }
            catch (Exception error)
            {
                log.CreateLogger("SurveyTracking").LogWarning(error, "4D survey verification failed");
                return Results.StatusCode(502);
            }
            if (string.IsNullOrWhiteSpace(recipient) || !MailAddress.TryCreate(recipient, out _))
                return Results.BadRequest(new { reasonPhrase = "Customer email is missing or invalid" });

            var viewerToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var uploadToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var started = DateTime.UtcNow;
            var expiry = started.AddHours(1);
            int id;
            using (var db = store.Open())
            {
                using var transaction = db.BeginTransaction();
                using var add = db.CreateCommand();
                add.Transaction = transaction;
                add.CommandText = """
                    INSERT INTO SurveyJourneys
                      (TenantCode, AgentCode, ContractCode, DiaryDate, ViewerTokenHash,
                       UploadTokenHash, StartedUtc, ExpiresUtc)
                    VALUES (@tenant, @agent, @contract, @date, @view, @upload, @start, @expires);
                    """;
                add.Parameters.AddWithValue("@tenant", request.TenantCode);
                add.Parameters.AddWithValue("@agent", request.AgentCode);
                add.Parameters.AddWithValue("@contract", request.ContractCode);
                add.Parameters.AddWithValue("@date", request.DiaryDate.ToString("yyyy-MM-dd"));
                add.Parameters.AddWithValue("@view", SurveyTrackingStore.Hash(viewerToken));
                add.Parameters.AddWithValue("@upload", SurveyTrackingStore.Hash(uploadToken));
                add.Parameters.AddWithValue("@start", started.ToString("O"));
                add.Parameters.AddWithValue("@expires", expiry.ToString("O"));
                add.ExecuteNonQuery();
                using var lastId = db.CreateCommand();
                lastId.Transaction = transaction;
                lastId.CommandText = "SELECT last_insert_rowid()";
                id = Convert.ToInt32(lastId.ExecuteScalar());
                transaction.Commit();
            }

            // The viewer token is never saved in plaintext.
            var link = publicUrl + "/Track/" + viewerToken;
            var body = $$"""
                <!doctype html><html lang="en"><head><meta charset="utf-8"></head>
                <body style="margin:0;background:#f1f5f9;font-family:Arial,sans-serif">
                <table role="presentation" width="100%" cellspacing="0" cellpadding="0"><tr><td align="center" style="padding:30px 16px">
                <table role="presentation" width="100%" style="max-width:560px;background:white;border-radius:12px">
                <tr><td style="background:#073d72;color:white;text-align:center;padding:26px;font-size:28px;font-weight:bold">SmartOps</td></tr>
                <tr><td style="padding:30px;color:#153b61">
                <h1 style="font-size:24px">Your surveyor is on the way</h1>
                <p>Your surveyor is travelling to your appointment.</p>
                <p>You can follow their progress using the link below.</p>
                <p style="text-align:center;padding:20px 0">
                <a href="{{System.Net.WebUtility.HtmlEncode(link)}}" style="background:#1376be;color:white;text-decoration:none;border-radius:7px;padding:15px 24px;font-weight:bold">View your surveyor's location</a></p>
                <p style="font-size:13px;color:#637487">This is an automated appointment notification.</p>
                </td></tr></table></td></tr></table></body></html>
                """;
            try
            {
                await mail.SendEmailAsync(recipient, "Your surveyor is on the way", body);
            }
            catch (Exception error)
            {
                log.CreateLogger("SurveyTracking").LogError(error, "Failed to send tracking link email");
                using var db = store.Open();
                using var remove = db.CreateCommand();
                remove.CommandText = "DELETE FROM SurveyJourneys WHERE Id = @id";
                remove.Parameters.AddWithValue("@id", id);
                remove.ExecuteNonQuery();
                return Results.StatusCode(502);
            }

            // Invalidate previous customer links only after new email was sent.
            using (var db = store.Open())
            {
                using var close = db.CreateCommand();
                close.CommandText = """
                    UPDATE SurveyJourneys SET FinishedUtc=@now, Latitude=NULL, Longitude=NULL
                    WHERE TenantCode=@tenant AND AgentCode=@agent AND ContractCode=@contract
                      AND DiaryDate=@date AND Id<>@id AND FinishedUtc IS NULL;
                    """;
                close.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));
                close.Parameters.AddWithValue("@tenant", request.TenantCode);
                close.Parameters.AddWithValue("@agent", request.AgentCode);
                close.Parameters.AddWithValue("@contract", request.ContractCode);
                close.Parameters.AddWithValue("@date", request.DiaryDate.ToString("yyyy-MM-dd"));
                close.Parameters.AddWithValue("@id", id);
                close.ExecuteNonQuery();
            }
            return Results.Ok(new { JourneyId = id, UploadToken = uploadToken, JourneyExpiresAtUtc = expiry });
        });

        app.MapPost("/api/survey-tracking/position", (UpdateRequest request, SurveyTrackingStore store) =>
        {
            if (request.JourneyId <= 0 || request.UploadToken?.Length != 64 ||
                !double.IsFinite(request.Latitude) || !double.IsFinite(request.Longitude) ||
                request.Latitude is < -90 or > 90 || request.Longitude is < -180 or > 180)
                return Results.BadRequest();

            using var db = store.Open();
            using var update = db.CreateCommand();
            update.CommandText = """
                UPDATE SurveyJourneys SET Latitude=@lat, Longitude=@lon, UpdatedUtc=@now
                WHERE Id=@id AND UploadTokenHash=@token AND FinishedUtc IS NULL
                  AND ExpiresUtc>@now;
                """;
            update.Parameters.AddWithValue("@lat", request.Latitude);
            update.Parameters.AddWithValue("@lon", request.Longitude);
            update.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));
            update.Parameters.AddWithValue("@id", request.JourneyId);
            update.Parameters.AddWithValue("@token", SurveyTrackingStore.Hash(request.UploadToken));
            return update.ExecuteNonQuery() == 1 ? Results.NoContent() : Results.StatusCode(410);
        });

        app.MapPost("/api/survey-tracking/arrived", (FinishRequest request, SurveyTrackingStore store) =>
        {
            if (request.JourneyId <= 0 || request.UploadToken?.Length != 64) return Results.BadRequest();
            using var db = store.Open();
            using var close = db.CreateCommand();
            close.CommandText = """
                UPDATE SurveyJourneys SET FinishedUtc=@now, Latitude=NULL, Longitude=NULL
                WHERE Id=@id AND UploadTokenHash=@token AND FinishedUtc IS NULL;
                """;
            close.Parameters.AddWithValue("@id", request.JourneyId);
            close.Parameters.AddWithValue("@token", SurveyTrackingStore.Hash(request.UploadToken));
            close.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));
            close.ExecuteNonQuery();
            return Results.NoContent();
        });

        app.MapGet("/api/survey-tracking/view/{token}", (string token, SurveyTrackingStore store, HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (token.Length != 64 || !token.All(Uri.IsHexDigit)) return Results.NotFound();
            using var db = store.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                SELECT ExpiresUtc, FinishedUtc, Latitude, Longitude, UpdatedUtc
                FROM SurveyJourneys WHERE ViewerTokenHash=@token LIMIT 1;
                """;
            cmd.Parameters.AddWithValue("@token", SurveyTrackingStore.Hash(token));
            using var data = cmd.ExecuteReader();
            if (!data.Read()) return Results.NotFound();
            var expired = DateTime.Parse(data.GetString(0)).ToUniversalTime() <= DateTime.UtcNow;
            var arrived = !data.IsDBNull(1);
            var active = !arrived && !expired;
            return Results.Ok(new
            {
                active, arrived, expired,
                latitude = active && !data.IsDBNull(2) ? (double?)data.GetDouble(2) : null,
                longitude = active && !data.IsDBNull(3) ? (double?)data.GetDouble(3) : null,
                updatedUtc = active && !data.IsDBNull(4) ? data.GetString(4) : null
            });
        });

        app.MapGet("/Track/{token}", (string token, HttpContext context) =>
        {
            if (token.Length != 64 || !token.All(Uri.IsHexDigit)) return Results.NotFound();
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            context.Response.Headers["X-Robots-Tag"] = "noindex,nofollow";
            context.Response.Headers["Content-Security-Policy"] =
                "default-src 'none'; img-src 'self' https://tile.openstreetmap.org data:; style-src 'self' https://unpkg.com 'unsafe-inline'; script-src https://unpkg.com 'unsafe-inline'; connect-src 'self';";
            return Results.Content(TrackingPage, "text/html; charset=utf-8");
        });
    }

    private const string TrackingPage = """
<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<meta name="robots" content="noindex,nofollow"><title>SmartOps | Live surveyor tracking</title>
<link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css">
<style>
body{margin:0;background:#f3f6fb;color:#183351;font:16px system-ui,Arial,sans-serif}
header{background:#073d72;color:#fff;padding:20px;text-align:center}h1{margin:0;font-size:22px}
main{max-width:900px;margin:20px auto;padding:0 16px}.panel{background:white;border-radius:12px;padding:18px}
#map{height:min(65vh,560px);margin-top:16px;border-radius:9px}
.vehicle-marker{border:0;background:transparent;filter:drop-shadow(0 2px 3px #193c6550)}
</style></head><body>
<header><h1>SmartOps · Surveyor tracking</h1></header>
<main><div class="panel"><strong id="status">Checking your surveyor's location…</strong>
<p id="updated">This page refreshes automatically while your surveyor is travelling.</p>
<div id="map" aria-label="Map showing the surveyor's last reported location"></div>
<p><small>Locations are approximate and can be delayed. Tracking ends on arrival or after one hour.</small></p>
</div></main>
<script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"></script>
<script>
const token=location.pathname.split('/').pop();
const map=L.map('map').setView([54,-2],6);
L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png',
{attribution:'&copy; OpenStreetMap contributors',maxZoom:19}).addTo(map);
const vehicleIcon=L.icon({
 iconUrl:'/images/survey-vehicle-pin.png',
 iconSize:[32,40],iconAnchor:[16,39],
 className:'vehicle-marker'
});
let marker=null,finished=false;
async function refresh(){
 if(finished)return;
 try{
  const res=await fetch('/api/survey-tracking/view/'+encodeURIComponent(token),{cache:'no-store'});
  if(!res.ok){document.getElementById('status').textContent='Tracking link unavailable.';finished=true;return;}
  const d=await res.json();
  if(!d.active){
   document.getElementById('status').textContent=d.arrived?'Your surveyor has arrived.':'This tracking link has expired.';
   document.getElementById('updated').textContent='Location sharing has ended.';
   if(marker)map.removeLayer(marker);
   finished=true;return;
  }
  document.getElementById('status').textContent='Your surveyor is on the way';
  if(d.latitude!=null&&d.longitude!=null){
   const p=[d.latitude,d.longitude];
   if(marker)marker.setLatLng(p);
   else{marker=L.marker(p,{icon:vehicleIcon,title:'Surveyor vehicle location'}).addTo(map);map.setView(p,14);}
   const age=d.updatedUtc?Math.max(0,Math.floor((Date.now()-new Date(d.updatedUtc).getTime())/1000)):0;
   document.getElementById('updated').textContent=age>90
    ?'Last reported '+Math.floor(age/60)+' minute(s) ago — location may be outdated.'
    :'Location last updated '+new Date(d.updatedUtc).toLocaleTimeString();
  }else document.getElementById('updated').textContent='Waiting for the first GPS update…';
 }catch(e){document.getElementById('status').textContent='Unable to refresh location.';
 document.getElementById('updated').textContent='Trying again shortly…';}
}
refresh();setInterval(refresh,20000);
</script></body></html>
""";
}
