using Microsoft.EntityFrameworkCore;
using SmartOps.Web.Data;
using SmartOps.Web.Services;
using SmartOps.Web.Models;
using System.Reflection;
using System.Text.Json;
using SmartOps.Shared.Models;

namespace SmartOps.Web
{
    public static partial class EndpointMappings
    {
        public static void MapChecksEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapPost("/SendChecks", async (ChecksDTO checks, AppDBContext db) =>
            {
                var tenant = await db.Tenant.FirstOrDefaultAsync(t =>
                    t.Code == checks.TenantCode && t.IsActive);

                if (tenant == null)
                    return Results.BadRequest(new
                    {
                        ReasonPhrase = "Tenant Code Not Found : " + checks.TenantCode
                    });

                int tenantId = tenant.Id;

                var agent = await db.Agent.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(a =>
                        a.TenantId == tenantId &&
                        a.Code == checks.AgentCode &&
                        a.AuthenticationString == checks.AuthenticationString);

                if (agent == null)
                    return Results.BadRequest(new { ReasonPhrase = "Authentication Failed" });

                // Dynamic accident answers and their saved definition travel together.
                foreach (var dto in checks.AccidentsVehicleDTOs)
                {
                    if (dto.Accident == null || string.IsNullOrWhiteSpace(dto.Accident.AccidentQuestionsSnapshotJson)) continue;
                    try
                    {
                        var definition = VehicleAccidentQuestions.Definition(dto.Accident.AccidentQuestionsSnapshotJson);
                        var answers = VehicleAccidentQuestions.Answers(dto.Accident.AccidentAnswersJson);
                        var issues = VehicleAccidentQuestions.Validate(definition, answers);
                        if (issues.Count > 0)
                            return Results.BadRequest(new { ReasonPhrase = "Incomplete vehicle accident: " + string.Join(", ", issues.Select(i => i.Message)) });
                    }
                    catch (Exception ex) when (ex is JsonException or InvalidOperationException)
                    {
                        return Results.BadRequest(new { ReasonPhrase = "Invalid vehicle accident questionnaire." });
                    }
                }

                foreach (var accident in checks.FAccidents)
                {
                    if (string.IsNullOrWhiteSpace(accident.AccidentQuestionsSnapshotJson)) continue;
                    try
                    {
                        var definition = WorkAccidentQuestions.Definition(accident.AccidentQuestionsSnapshotJson, accident.Type);
                        var answers = WorkAccidentQuestions.Answers(accident.AccidentAnswersJson);
                        var issues = WorkAccidentQuestions.Validate(definition, answers);
                        if (issues.Count > 0)
                            return Results.BadRequest(new { ReasonPhrase = "Incomplete work accident: " + string.Join(", ", issues.Select(i => i.Message)) });
                    }
                    catch (JsonException)
                    {
                        return Results.BadRequest(new { ReasonPhrase = "Invalid work accident questionnaire." });
                    }
                }

                foreach (var check in checks.SpotChecks)
                {
                    if (string.IsNullOrWhiteSpace(check.SpotCheckQuestionsSnapshotJson)) continue;
                    try
                    {
                        var definition = SpotCheckQuestions.Definition(check.SpotCheckQuestionsSnapshotJson);
                        var answers = SpotCheckQuestions.Answers(check.SpotCheckAnswersJson);
                        var issues = SpotCheckQuestions.Validate(definition, answers, check.bCorrectContract, check.uspot_signed, check.no_of_photos);
                        if (issues.Count > 0)
                            return Results.BadRequest(new { ReasonPhrase = "Incomplete spot check: " + string.Join(", ", issues.Select(i => i.Message)) });
                    }
                    catch (JsonException)
                    {
                        return Results.BadRequest(new { ReasonPhrase = "Invalid spot check questionnaire." });
                    }
                }

                // A failed upload rolls back every change made by this request.
                await using var transaction = await db.Database.BeginTransactionAsync();

                try
                {
                    foreach (var check in checks.LadderChecks)
                        await UpsertCheckRecordAsync(db, check, tenantId);

                    foreach (var check in checks.ToolChecks)
                        await UpsertCheckRecordAsync(db, check, tenantId);

                    foreach (var check in checks.SpotChecks)
                        await UpsertCheckRecordAsync(db, check, tenantId);

                    foreach (var sheet in checks.MileageSheets)
                        await UpsertCheckRecordAsync(db, sheet, tenantId);

                    foreach (var accident in checks.FAccidents)
                        await UpsertCheckRecordAsync(db, accident, tenantId);

                    foreach (var dto in checks.AccidentsVehicleDTOs)
                    {
                        if (dto.Accident == null)
                            continue;

                        await UpsertCheckRecordAsync(
                            db, dto.Accident, tenantId);

                        await db.SaveChangesAsync();

                    }

                    foreach (var dto in checks.VehicleCheckDTOs)
                    {
                        if (dto.VehicleCheckHeader == null)
                            continue;

                        var header = await UpsertCheckRecordAsync(
                            db, dto.VehicleCheckHeader, tenantId);

                        await db.SaveChangesAsync();
                        int headerId = header.Id;

                        // Null means no vehicle-item snapshot was supplied.
                        if (dto.Vehicles != null)
                        {
                            await SyncCheckChildrenAsync(
                                db,
                                dto.Vehicles,
                                tenantId,
                                "VehicleCheckHeaderId",
                                headerId);
                        }
                    }

                    await db.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Results.Ok(new { status = "success" });
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });

            app.MapPost("/GetSpotCheckJobInfo", async (GetDataDTO gs, AppDBContext db) =>
            {
                var tenant = await db.Tenant.FirstOrDefaultAsync(t =>
                    t.Code == gs.TenantCode && t.IsActive);

                if (tenant == null)
                    return Results.BadRequest(new
                    {
                        ReasonPhrase = "Tenant Code Not Found : " + gs.TenantCode
                    });

                int tenantId = tenant.Id;

                var agent = await db.Agent.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(a =>
                        a.TenantId == tenantId &&
                        a.Code == gs.AgentCode &&
                        a.AuthenticationString == gs.AuthenticationString);

                if (agent == null)
                    return Results.BadRequest(new { ReasonPhrase = "Authentication Failed" });

                var job = await db.Job.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(x =>
                        x.TenantId == tenantId &&
                        x.ContractCode == gs.ContractCode &&
                        x.DiaryDate == DateTime.Today);

                if (job == null)
                    return Results.BadRequest(new { ReasonPhrase = "Job not found" });

                var customer = await db.Customer.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(x =>
                        x.TenantId == tenantId &&
                        x.Id == job.CustomerId);

                return Results.Ok(new JobContractDTO(job, customer ?? new Customer()));
            });

            app.MapPost("/GetVehicles", async (GetDataDTO gs, AppDBContext db, TenantQuestionSetService questionSets, HttpContext http) =>
            {
                var tenant = await db.Tenant.FirstOrDefaultAsync(t =>
                    t.Code == gs.TenantCode && t.IsActive);

                if (tenant == null)
                    return Results.BadRequest(new
                    {
                        ReasonPhrase = "Tenant Code Not Found : " + gs.TenantCode
                    });

                int tenantId = tenant.Id;

                var agent = await db.Agent.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(a =>
                        a.TenantId == tenantId &&
                        a.Code == gs.AgentCode &&
                        a.AuthenticationString == gs.AuthenticationString);

                if (agent == null)
                    return Results.BadRequest(new { ReasonPhrase = "Authentication Failed" });

                if (agent.BranchId == null)
                    return Results.BadRequest(new { ReasonPhrase = "Branch code not assigned" });

                var branch = await db.Branches.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(x =>
                        x.TenantId == tenantId && x.Id == agent.BranchId);

                if (branch == null)
                    return Results.BadRequest(new { ReasonPhrase = "Branch Not Found : " });

                // Match GetJobs: advertise the question version without changing the vehicle JSON.
                http.Response.Headers["X-Dynamic-Questions-Version"] =
                    (await questionSets.GetVersionAsync(tenantId)).ToString(
                        System.Globalization.CultureInfo.InvariantCulture);

                var vehicles = await db.Vehicles.IgnoreQueryFilters()
                    .Include(x => x.Branch)
                    .Where(x => x.TenantId == tenantId && x.BranchId == branch.Id)
                    .ToListAsync();

                return Results.Ok(vehicles.Select(v => new VehicleDTO(v)).ToList());
            });
        }

        // A tablet ID is never used to identify a server record.
        // Both GUID and tenant must match before updating an existing row.
        private static async Task<T> UpsertCheckRecordAsync<T>(
            AppDBContext db, T incoming, int tenantId)
            where T : TenantEntity
        {
            var guidProperty = typeof(T).GetProperty("Guid")
                ?? throw new InvalidOperationException(
                    $"{typeof(T).Name} requires a Guid property.");

            var idProperty = typeof(T).GetProperty("Id")
                ?? throw new InvalidOperationException(
                    $"{typeof(T).Name} requires an Id property.");

            if (guidProperty.PropertyType != typeof(Guid))
                throw new InvalidOperationException(
                    $"{typeof(T).Name}.Guid must be a non-nullable Guid.");

            var guid = (Guid)guidProperty.GetValue(incoming)!;
            if (guid == Guid.Empty)
                throw new InvalidOperationException(
                    $"{typeof(T).Name} has an empty GUID.");

            incoming.TenantId = tenantId;

            var existing = await db.Set<T>().IgnoreQueryFilters()
                .FirstOrDefaultAsync(x =>
                    x.TenantId == tenantId &&
                    EF.Property<Guid>(x, "Guid") == guid);

            if (existing != null)
            {
                // Set the tablet object's ID to the server ID BEFORE SetValues.
                idProperty.SetValue(incoming, idProperty.GetValue(existing));
                db.Entry(existing).CurrentValues.SetValues(incoming);
                return existing;
            }

            idProperty.SetValue(incoming, 0);
            db.Set<T>().Add(incoming);
            return incoming;
        }

        // Synchronise ONLY children belonging to this specific server header.
        // An empty supplied list removes its children; null is handled by the caller.
        private static async Task SyncCheckChildrenAsync<T>(
            AppDBContext db,
            IEnumerable<T> incomingItems,
            int tenantId,
            string foreignKeyName,
            int serverHeaderId)
            where T : TenantEntity
        {
            var type = typeof(T);
            var idProperty = type.GetProperty("Id")
                ?? throw new InvalidOperationException($"{type.Name} requires Id.");

            var foreignKeyProperty = type.GetProperty(foreignKeyName)
                ?? throw new InvalidOperationException(
                    $"{type.Name} requires {foreignKeyName}.");

            // Read the existing children for this header only.
            var existing = await db.Set<T>().IgnoreQueryFilters()
                .Where(x =>
                    x.TenantId == tenantId &&
                    EF.Property<int>(x, foreignKeyName) == serverHeaderId)
                .ToListAsync();

            var supplied = incomingItems.ToList();
            var guidProperty = type.GetProperty("Guid");

            // Without a stable child GUID there is no safe way to match an
            // incoming tablet item to an existing SQL Server item. Fail rather
            // than silently deleting and recreating historical records.
            if (guidProperty == null || guidProperty.PropertyType != typeof(Guid))
                throw new InvalidOperationException(
                    $"{type.Name} requires a non-nullable Guid for child synchronisation.");

            var incomingGuids = new HashSet<Guid>();

            foreach (var item in supplied)
            {
                var guid = (Guid)guidProperty.GetValue(item)!;

                if (guid == Guid.Empty || !incomingGuids.Add(guid))
                    throw new InvalidOperationException(
                        $"{type.Name} has an empty or duplicate GUID.");

                item.TenantId = tenantId;
                foreignKeyProperty.SetValue(item, serverHeaderId);

                // Only match a child within THIS header, not a different
                // historical accident or vehicle check.
                var matching = existing.FirstOrDefault(x =>
                    (Guid)guidProperty.GetValue(x)! == guid);

                if (matching != null)
                {
                    idProperty.SetValue(item, idProperty.GetValue(matching));
                    db.Entry(matching).CurrentValues.SetValues(item);
                }
                else
                {
                    // Detect a GUID reused elsewhere in this tenant.
                    bool guidUsedElsewhere = await db.Set<T>()
                        .IgnoreQueryFilters()
                        .AnyAsync(x =>
                            x.TenantId == tenantId &&
                            EF.Property<Guid>(x, "Guid") == guid);

                    if (guidUsedElsewhere)
                        throw new InvalidOperationException(
                            $"{type.Name} GUID belongs to another header.");

                    idProperty.SetValue(item, 0);
                    db.Set<T>().Add(item);
                }
            }

            foreach (var oldItem in existing)
            {
                var guid = (Guid)guidProperty.GetValue(oldItem)!;
                if (!incomingGuids.Contains(guid))
                    db.Set<T>().Remove(oldItem);
            }
        }
    }
}
