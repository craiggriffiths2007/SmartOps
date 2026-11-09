using SmartOps.Shared.Models;
using SmartOps.Web.Services;

using Microsoft.EntityFrameworkCore;
using SmartOps.Web.Data;
using SmartOps.Web.Models;
using SmartOps.Web.Helpers;

namespace SmartOps.Web
{
    public static partial class EndpointMappings
    {
        public static void MapJobsEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapSurveyEnRouteEndpoint();

            app.MapPost("/SendJobs", async (JobSendDTO send, AppDBContext db) =>
            {
                if (send.JobHeaders == null || send.JobHeaders.Count == 0)
                {
                    return Results.BadRequest(new
                    {
                        status = "error",
                        message = "No jobs supplied"
                    });
                }

                // ---------------------------------------------------------
                // Find tenant
                // ---------------------------------------------------------

                var tenant = await db.Tenant
                    .FirstOrDefaultAsync(t =>
                        t.Code == send.TenantCode &&
                        t.IsActive);

                if (tenant == null)
                {
                    return Results.BadRequest(new
                    {
                        status = "error",
                        message = "Invalid tenant"
                    });
                }

                int tenantId = tenant.Id;

                // ---------------------------------------------------------
                // Authenticate agent
                // ---------------------------------------------------------

                var agent = await db.Agent
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(x =>
                        x.TenantId == tenantId &&
                        x.Code == send.AgentCode &&
                        x.AuthenticationString == send.AuthenticationString);

                if (agent == null)
                {
                    return Results.BadRequest(new
                    {
                        ReasonPhrase = "Authentication Failed"
                    });
                }

                // ---------------------------------------------------------
                // Process returned jobs
                // ---------------------------------------------------------

                foreach (var job in send.JobHeaders)
                {
                    if (job.Head == null)
                        continue;

                    // -----------------------------------------------------
                    // Resolve contract BEFORE changing any data
                    // -----------------------------------------------------

                    if (!int.TryParse(
                        job.Head.ContractCode,
                        out int contractNumber))
                    {
                        return Results.BadRequest(new
                        {
                            status = "error",
                            message = $"Invalid contract code: {job.Head.ContractCode}"
                        });
                    }

                    var contract = await db.Contract
                        .IgnoreQueryFilters()
                        .FirstOrDefaultAsync(c =>
                            c.TenantId == tenantId &&
                            c.ContractNumber == contractNumber);

                    if (contract == null)
                    {
                        return Results.BadRequest(new
                        {
                            status = "error",
                            message = $"Contract {job.Head.ContractCode} was not found."
                        });
                    }

                    // -----------------------------------------------------
                    // Everything below here is one transaction.
                    //
                    // If anything fails, the old JobHeader/items remain
                    // exactly as they were before this send.
                    // -----------------------------------------------------

                    await using var transaction =
                        await db.Database.BeginTransactionAsync();

                    try
                    {
                        // The tablet's primary keys are local SQLite IDs.
                        // Match server records ONLY by tenant and GUID.
                        if (job.Head.Guid == Guid.Empty)
                            return Results.BadRequest(new { status = "error", message = "Job header GUID is empty" });

                        job.Head.TenantId = tenantId;
                        job.Head.ContractId = contract.Id;
                        job.Head.CustomerId = contract.CustomerId;
                        job.Head.AgentId = agent.Id;

                        var existingHeader = await db.JobHeader
                            .IgnoreQueryFilters()
                            .Include(h => h.GarageDoorMotor)
                            .FirstOrDefaultAsync(h =>
                                h.TenantId == tenantId && h.Guid == job.Head.Guid);

                        int headerId;
                        if (existingHeader != null)
                        {
                            // Do not attach the incoming header or copy its SQLite ID.
                            var incomingMotor = job.Head.GarageDoorMotor;
                            int serverId = existingHeader.Id;
                            job.Head.Id = serverId;
                            db.Entry(existingHeader).CurrentValues.SetValues(job.Head);
                            headerId = serverId;

                            if (incomingMotor != null)
                            {
                                if (existingHeader.GarageDoorMotor != null)
                                {
                                    incomingMotor.Id = existingHeader.GarageDoorMotor.Id;
                                    db.Entry(existingHeader.GarageDoorMotor)
                                        .CurrentValues.SetValues(incomingMotor);
                                }
                                else
                                {
                                    incomingMotor.Id = 0;
                                    existingHeader.GarageDoorMotor = incomingMotor;
                                }
                            }
                        }
                        else
                        {
                            job.Head.Id = 0;
                            if (job.Head.GarageDoorMotor != null)
                                job.Head.GarageDoorMotor.Id = 0;
                            db.JobHeader.Add(job.Head);
                            await db.SaveChangesAsync(); // Obtain server header ID.
                            headerId = job.Head.Id;
                        }

                        // Update or insert each child by GUID within THIS header.
                        // An empty list is treated as a complete, authoritative list:
                        // children omitted by the tablet are deleted on the server.
                        async Task SyncItems<T>(IEnumerable<T> incoming)
                            where T : TenantEntity
                        {
                            var items = incoming?.ToList() ?? new List<T>();
                            var existing = await db.Set<T>()
                                .IgnoreQueryFilters()
                                .Where(e => e.TenantId == tenantId &&
                                    EF.Property<int>(e, "HeaderId") == headerId)
                                .ToListAsync();

                            var guidProperty = typeof(T).GetProperty("Guid")
                                ?? throw new InvalidOperationException($"{typeof(T).Name} has no Guid");
                            var headerProperty = typeof(T).GetProperty("HeaderId")
                                ?? throw new InvalidOperationException($"{typeof(T).Name} has no HeaderId");
                            var idProperty = typeof(T).GetProperty("Id")
                                ?? throw new InvalidOperationException($"{typeof(T).Name} has no Id");

                            var existingByGuid = existing.ToDictionary(e =>
                                (Guid)guidProperty.GetValue(e)!);
                            var received = new HashSet<Guid>();

                            foreach (var item in items)
                            {
                                var guid = (Guid)guidProperty.GetValue(item)!;
                                if (guid == Guid.Empty || !received.Add(guid))
                                    throw new InvalidOperationException(
                                        $"Missing or duplicate {typeof(T).Name} GUID: {guid}");

                                item.TenantId = tenantId;
                                headerProperty.SetValue(item, headerId);

                                if (existingByGuid.TryGetValue(guid, out var serverItem))
                                {
                                    // Replace the local PK with the server PK before SetValues.
                                    idProperty.SetValue(item, idProperty.GetValue(serverItem));
                                    db.Entry(serverItem).CurrentValues.SetValues(item);
                                }
                                else
                                {
                                    // The same item keeps its GUID across visits. Insert a
                                    // separate row for this header to preserve earlier surveys.
                                    idProperty.SetValue(item, 0);
                                    db.Set<T>().Add(item);
                                }
                            }

                            foreach (var oldItem in existing)
                            {
                                if (!received.Contains((Guid)guidProperty.GetValue(oldItem)!))
                                    db.Set<T>().Remove(oldItem);
                            }
                        }

                        await SyncItems(job.CustomItems);

                        // -------------------------------------------------
                        // Make sure diary job exists
                        // -------------------------------------------------

                        var diaryJob = await db.Job
                            .IgnoreQueryFilters()
                            .FirstOrDefaultAsync(x =>
                                x.TenantId == tenantId &&
                                x.DiaryDate == job.Head.DiaryDate &&
                                x.ContractId == contract.Id); //&&
                                                              //x.AgentId == agent.Id &&
                                                              //x.JobType == job.Head.JobType);

                        if (diaryJob == null)
                        {
                            diaryJob = new Job
                            {
                                TenantId = tenantId,

                                DiaryDate =
                                    job.Head.DiaryDate,

                                Time =
                                    job.Head.DateTimeCompleted
                                        .ToLocalTime(),

                                ContractCode =
                                    contract.ContractCode,

                                ContractId =
                                    contract.Id,

                                CustomerId =
                                    contract.CustomerId,

                                AgentId =
                                    agent.Id,

                                JobType =
                                    job.Head.JobType,

                                DamageDesc =
                                    job.Head.DamageDescription,

                                Instructions =
                                    job.Head.JobInstructions,

                                bRequestRepudiation =
                                    job.Head.bRequestRepudiation
                            };

                            db.Job.Add(diaryJob);
                        }

                        // -------------------------------------------------
                        // Update contract status
                        // -------------------------------------------------

                        ContractStatus? newStatus =
                            job.Head.JobType switch
                            {
                                enum_job_type.Survey =>
                                    ContractStatus.SurveyCompleted,

                                enum_job_type.Fitting
                                    or enum_job_type.Remedial =>
                                    ContractStatus.FittingCompleted,

                                _ => null
                            };

                        if (newStatus.HasValue &&
                            contract.Status != newStatus.Value)
                        {
                            db.ContractUpdates.Add(
                                ContractUpdateHelper.Create(
                                    contract,
                                    newStatus.Value));
                        }

                        // -------------------------------------------------
                        // Save everything
                        // -------------------------------------------------

                        await db.SaveChangesAsync();

                        // Everything succeeded - make it permanent
                        await transaction.CommitAsync();
                    }
                    catch
                    {
                        // Something failed.
                        // Restore the previous JobHeader/items/etc.
                        await transaction.RollbackAsync();

                        throw;
                    }
                }

                return Results.Ok(new
                {
                    status = "success"
                });
            });

            // This endpoint is authenticated with the same agent credentials as /GetJobs.
            app.MapPost("/GetDynamicQuestions", async (
                GetDataDTO gs, AppDBContext db, TenantQuestionSetService questionSets) =>
            {
                var tenant = await db.Tenant.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(x => x.Code == gs.TenantCode && x.IsActive);
                if (tenant is null) return Results.Unauthorized();

                bool authorized = await db.Agent.IgnoreQueryFilters().AnyAsync(x =>
                    x.TenantId == tenant.Id && x.Code == gs.AgentCode &&
                    x.AuthenticationString == gs.AuthenticationString);
                if (!authorized) return Results.Unauthorized();

                return Results.Json(await questionSets.GetAsync(tenant.Id), DynamicQuestionLoader.JsonOptions);
            });

            app.MapPost("/GetJobs", async (GetDataDTO gs, AppDBContext db, TenantQuestionSetService questionSets, HttpContext http) =>
            {
                var tenant = await db.Tenant
                    .FirstOrDefaultAsync(t =>
                        t.Code == gs.TenantCode &&
                        t.IsActive);

                if (tenant == null)
                {
                    return Results.BadRequest(
                        new { ReasonPhrase = "Invalid tenant" });
                }

                int tenantId = tenant.Id;

                var agent = await db.Agent
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(x =>
                        x.TenantId == tenantId &&
                        x.Code == gs.AgentCode &&
                        x.AuthenticationString == gs.AuthenticationString);

                if (agent == null)
                {
                    return Results.BadRequest(
                        new { ReasonPhrase = "Authentication Failed" });
                }

                // The existing JSON response remains a plain array of jobs.
                // The app compares this header against its cached question version.
                http.Response.Headers["X-Dynamic-Questions-Version"] =
                    (await questionSets.GetVersionAsync(tenantId)).ToString(
                        System.Globalization.CultureInfo.InvariantCulture);

                List<Job> jobs = new List<Job>();

                if (gs.AgentCode == "ADMIN")
                {
                    jobs = await db.Job
                                    .IgnoreQueryFilters()
                                    .Where(x =>
                                        x.TenantId == tenant.Id &&
                                        (
                                            x.DiaryDate == DateTime.Today ||
                                            x.DiaryDate == DateTime.Today.AddDays(1)
                                        ))
                                    .Include(x => x.Customer)
                                    .Include(x => x.Contract)
                                        .ThenInclude(c => c.Insurer)
                                    .ToListAsync();
                }
                else
                {
                    jobs = await db.Job
                                    .IgnoreQueryFilters()
                                    .Where(x =>
                                        x.TenantId == tenant.Id &&
                                        x.AgentId == agent.Id &&
                                        (
                                            x.DiaryDate == DateTime.Today ||
                                            x.DiaryDate == DateTime.Today.AddDays(1)
                                        ))
                                    .Include(x => x.Customer)
                                    .Include(x => x.Contract)
                                        .ThenInclude(c => c.Insurer)
                                    .ToListAsync();
                }

                var results = new List<JobHeaderDTO>();

                foreach (var job in jobs)
                {
                    JobHeader jobHeader = new JobHeader();

                    if (job.bIncludeSurvey)
                    {
                        jobHeader = await db.JobHeader
                                            .IgnoreQueryFilters()
                                            .Where(h =>
                                                h.TenantId == tenantId &&
                                                h.ContractCode == job.ContractCode &&
                                                h.JobType == enum_job_type.Survey)
                                            .Include(g => g.GarageDoorMotor)
                                            .OrderByDescending(h => h.DateTimeCompleted)
                                            .FirstOrDefaultAsync()
                                            ?? new JobHeader();

                        if (jobHeader.Id > 0) //
                        {
                            jobHeader.bSurvey = true;
                        }
                    }

                    jobHeader.Guid = Guid.NewGuid();
                    jobHeader.JobType = job.JobType;
                    jobHeader.ContractCode = job.ContractCode;
                    jobHeader.bRequestRepudiation = job.bRequestRepudiation;

                    jobHeader.JobInstructions = job.Instructions;

                    jobHeader.StartTime = job.Time.ToShortTimeString();
                    jobHeader.FinishTime = job.Time.Add(job.JobTime).ToString("HH:mm");

                    jobHeader.DiaryDate = job.DiaryDate;
                    jobHeader.EnRouteNotificationSent = job.EnRouteNotificationSent;
                    jobHeader.EnRoutePressedAtUtc = job.EnRoutePressedAtUtc;
                    jobHeader.CustomerId = job.CustomerId;
                    jobHeader.ContractId = job.ContractId;
                    jobHeader.AgentId = job.AgentId;
                    //jobHeader.BranchId = job.BranchId;
                    if (job.Customer != null)
                    {

                        jobHeader.CustomerName = job.Customer.Name;
                        jobHeader.CustomerAddressLine1 = job.Customer.Add1;
                        jobHeader.CustomerAddressLine2 = job.Customer.Add2;
                        jobHeader.CustomerAddressLine3 = job.Customer.Add3 ?? "";
                        jobHeader.CustomerPostcode = job.Customer.Postcode;
                        jobHeader.CustomerPhoneNumber = job.Customer.Phone1;
                        jobHeader.CustomerPhoneNumber2 = job.Customer.Phone2 ?? "";
                        jobHeader.CustomerPhoneNumber3 = job.Customer.Phone3 ?? "";
                    }
                    if (job.Contract != null)
                    {
                        jobHeader.TrafficLight = job.Contract.TrafficLight;
                        jobHeader.InsuranceCompanyName = job.Contract.Insurer?.Name ?? "";
                        jobHeader.IncidentDate = job.Contract.IncidentDate.ToShortDateString();
                        jobHeader.CauseOfDamage = job.Contract.CauseOfDamage;
                        jobHeader.PolicyNumber = job.Contract.PolicyNumber;
                        jobHeader.Excess = job.Contract.Excess;
                        jobHeader.DamageDescription = job.Contract.DamageDescription;
                    }
                    jobHeader.bComplete = false;
                    jobHeader.bSent = false;

                    var images = new List<string>();

                    if (jobHeader.bSurvey == true)
                    {
                        string pattern = $"{jobHeader.ContractCode:00000000}_______%"; // using _ as a wildcard ( would have been cAZ and dAZ )

                        List<string> imagesRange = await db.Images
                                                            .IgnoreQueryFilters()
                                                            .Where(x =>
                                                                x.TenantId == tenantId &&
                                                                EF.Functions.Like(x.Filename, pattern))
                                                            .Select(f => f.Filename)
                                                            .ToListAsync();

                        foreach (var im in imagesRange)
                        {
                            if (im != null)
                            {
                                images.Add(im);
                            }
                        }
                    }

                    if (true)
                    { // add videos of job
                        string pattern = $"{jobHeader.ContractCode:00000000}_Videos%";

                        List<string> imagesRange = await db.Images
                                                            .IgnoreQueryFilters()
                                                            .Where(x =>
                                                                x.TenantId == tenantId &&
                                                                EF.Functions.Like(x.Filename, pattern))
                                                            .Select(f => f.Filename)
                                                            .ToListAsync();

                        foreach (var im in imagesRange)
                        {
                            if (im != null)
                            {
                                images.Add(im);
                            }
                        }
                    }
                    if (job.bIncludeSurvey)
                    {
                        results.Add(new JobHeaderDTO
                        {
                            Head = jobHeader,
                            CustomItems = await db.CustomItems.IgnoreQueryFilters().AsNoTracking()
                                .Where(f => f.TenantId == tenantId && f.HeaderId == jobHeader.Id).ToListAsync(),

                            Images = images
                        });
                    }
                    else
                    {
                        results.Add(new JobHeaderDTO
                        {
                            Head = jobHeader,
                            Images = images
                        });
                    }
                    var downloaded = results[^1];
                    // Historical database records remain untouched. Only a new resurvey receives the latest definitions.
                    if (jobHeader.JobType == enum_job_type.Survey && jobHeader.bSurvey)
                    {
                        var published = await questionSets.GetAsync(tenantId);
                        foreach (var item in downloaded.CustomItems)
                        {
                            string? setName = DynamicQuestionSnapshots.SetName(item);
                            if (setName != null && published.Sets.TryGetValue(setName, out var current))
                                DynamicQuestionSnapshots.UpgradeResurvey(item, current, published.Version);
                            else if (item is not CustomItemBase)
                            {
                                // No dynamic questions for this item; don't carry an
                                // unrelated old snapshot into a resurvey.
                                item.SurveyQuestionsSnapshotJson = null;
                                item.SurveyQuestionSetVersion = null;
                            }
                        }
                    }
                }

                return Results.Ok(results);
            });
        }
    }
}
