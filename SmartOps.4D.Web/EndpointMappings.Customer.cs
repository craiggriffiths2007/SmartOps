using SmartOps.Web.Data;
using SmartOps.Web.Helpers;
using SmartOps.Web.Models;
using SmartOps.Web.Models.CustomerApp;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace SmartOps.Web
{
    public static partial class EndpointMappings
    {
        public static void MapCustomerEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapPost("/Customer/CreateTicket", async (
                        CustomerCreateTicketDTO request,
                        AppDBContext db) =>
            {
                var customer = await db.Customer
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(x =>
                        x.AuthenticationString == request.AuthenticationString &&
                        x.IsActive);

                if (customer == null)
                    return Results.Unauthorized();

                if (string.IsNullOrWhiteSpace(request.Subject))
                {
                    return Results.BadRequest(new
                    {
                        ReasonPhrase = "Please enter a subject."
                    });
                }

                if (string.IsNullOrWhiteSpace(request.Message))
                {
                    return Results.BadRequest(new
                    {
                        ReasonPhrase = "Please enter a message."
                    });
                }

                // Very important:
                // Confirm this contract actually belongs to this customer.
                var contract = await db.Contract
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(x =>
                        x.Id == request.ContractId &&
                        x.CustomerId == customer.Id);

                if (contract == null)
                    return Results.Unauthorized();

                var ticket = new SupportTicket
                {
                    TenantId = customer.TenantId,

                    ContractId = contract.Id,
                    CustomerId = customer.Id,

                    Subject = request.Subject.Trim(),

                    Status = TicketStatus.AwaitingOffice,

                    Created = DateTime.UtcNow
                };

                ticket.Messages.Add(new SupportTicketMessage
                {
                    TenantId = customer.TenantId,

                    Message = request.Message.Trim(),

                    Sender = TicketMessageSender.Customer,

                    Created = DateTime.UtcNow,

                    ReadByCustomer = true,
                    ReadByOffice = false
                });

                db.SupportTickets.Add(ticket);

                await db.SaveChangesAsync();

                return Results.Ok(new
                {
                    TicketId = ticket.Id
                });
            });

            app.MapPost("/Customer/Tickets", async (
                                CustomerRequestDTO request,
                                AppDBContext db) =>
            {
                var customer = await db.Customer
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(x =>
                        x.AuthenticationString == request.AuthenticationString &&
                        x.IsActive);

                if (customer == null)
                    return Results.Unauthorized();

                var tickets = await db.SupportTickets
                    .IgnoreQueryFilters()
                    .Where(x =>
                        x.CustomerId == customer.Id)
                    .Select(x => new CustomerTicketListDTO
                    {
                        Id = x.Id,

                        Subject = x.Subject,

                        Status = x.Status,

                        Created = x.Created,

                        MessageCount = x.Messages.Count,

                        LastMessageDate = x.Messages
                            .OrderByDescending(m => m.Created)
                            .Select(m => (DateTime?)m.Created)
                            .FirstOrDefault(),

                        HasUnreadMessages = x.Messages
                            .Any(m =>
                                m.Sender == TicketMessageSender.HeadOffice &&
                                !m.ReadByCustomer)
                    })
                    .OrderByDescending(x =>
                        x.LastMessageDate ?? x.Created)
                    .ToListAsync();

                return Results.Ok(tickets);
            });


            app.MapPost("/Customer/Ticket", async (
                        CustomerTicketRequestDTO request,
                        AppDBContext db) =>
            {
                var customer = await db.Customer
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(x =>
                        x.AuthenticationString == request.AuthenticationString &&
                        x.IsActive);

                if (customer == null)
                    return Results.Unauthorized();

                var ticket = await db.SupportTickets
                    .IgnoreQueryFilters()
                    .Include(x => x.Messages)
                        .ThenInclude(x => x.SentByUser)
                    .FirstOrDefaultAsync(x =>
                        x.Id == request.TicketId &&
                        x.CustomerId == customer.Id);

                if (ticket == null)
                    return Results.NotFound();

                // Customer has now opened the conversation.
                foreach (var message in ticket.Messages
                    .Where(x =>
                        x.Sender == TicketMessageSender.HeadOffice &&
                        !x.ReadByCustomer))
                {
                    message.ReadByCustomer = true;
                }

                await db.SaveChangesAsync();

                var dto = new CustomerTicketDTO
                {
                    Id = ticket.Id,
                    ContractId = ticket.ContractId,
                    Subject = ticket.Subject,
                    Status = ticket.Status,
                    Created = ticket.Created,

                    Messages = ticket.Messages
                        .OrderBy(x => x.Created)
                        .Select(x => new CustomerTicketMessageDTO
                        {
                            Id = x.Id,
                            Message = x.Message,
                            Sender = x.Sender,
                            Created = x.Created,

                            SenderName = x.Sender == TicketMessageSender.Customer
                                ? customer.Name
                                : x.SentByUser != null
                                    ? $"{x.SentByUser.FirstName} {x.SentByUser.LastName}"
                                    : "Office"
                        })
                        .ToList()
                };

                return Results.Ok(dto);
            });


            app.MapPost("/Customer/ReplyToTicket", async (
                        CustomerTicketReplyDTO request,
                        AppDBContext db) =>
            {
                var customer = await db.Customer
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(x =>
                        x.AuthenticationString == request.AuthenticationString &&
                        x.IsActive);

                if (customer == null)
                    return Results.Unauthorized();

                if (string.IsNullOrWhiteSpace(request.Message))
                {
                    return Results.BadRequest(new
                    {
                        ReasonPhrase = "Please enter a message."
                    });
                }

                var ticket = await db.SupportTickets
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(x =>
                        x.Id == request.TicketId &&
                        x.CustomerId == customer.Id);

                if (ticket == null)
                    return Results.NotFound();

                if (ticket.Status == TicketStatus.Closed)
                {
                    return Results.BadRequest(new
                    {
                        ReasonPhrase = "This ticket has been closed."
                    });
                }

                var message = new SupportTicketMessage
                {
                    TenantId = customer.TenantId,

                    SupportTicketId = ticket.Id,

                    Message = request.Message.Trim(),

                    Sender = TicketMessageSender.Customer,

                    Created = DateTime.UtcNow,

                    ReadByCustomer = true,
                    ReadByOffice = false
                };

                db.SupportTicketMessages.Add(message);

                ticket.Status = TicketStatus.AwaitingOffice;

                await db.SaveChangesAsync();

                return Results.Ok();
            });



            app.MapPost("/Customer/Login", async (
                        CustomerLoginRequestDTO login,
                        AppDBContext db) =>
            {
                string reference = login.Reference.Trim();
                string email = login.Email.Trim().ToLowerInvariant();

                var contract = await db.Contract
                    .IgnoreQueryFilters()
                    .Include(x => x.Customer)
                    .FirstOrDefaultAsync(x =>
                        x.CustomerReference == reference);

                if (contract?.Customer == null)
                {
                    return Results.BadRequest(
                        new
                        {
                            ReasonPhrase =
                                "Reference, email address or password is incorrect."
                        });
                }

                var customer = contract.Customer;

                if (!customer.IsActive ||
                    !string.Equals(
                        customer.Email?.Trim(),
                        email,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return Results.BadRequest(
                        new
                        {
                            ReasonPhrase =
                                "Reference, email address or password is incorrect."
                        });
                }

                var hasher = new PasswordHasher<Customer>();

                var verify = hasher.VerifyHashedPassword(
                    customer,
                    customer.PasswordHash,
                    login.Password);

                if (verify == PasswordVerificationResult.Failed)
                {
                    return Results.BadRequest(
                        new
                        {
                            ReasonPhrase =
                                "Reference, email address or password is incorrect."
                        });
                }

                customer.AuthenticationString =
                    Guid.NewGuid().ToString("N");

                customer.LoggedInDateTime =
                    DateTime.UtcNow;

                await db.SaveChangesAsync();

                return Results.Ok(new CustomerLoginResultDTO
                {
                    AuthenticationString =
                        customer.AuthenticationString,

                    CustomerId =
                        customer.Id,

                    ContractId =
                        contract.Id,

                    CustomerName =
                        customer.Name
                });
            });

            app.MapPost("/Customer/Account", async (
                                        CustomerRequestDTO request,
                                        AppDBContext db) =>
            {
                var customer = await db.Customer
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(x =>
                        x.AuthenticationString ==
                            request.AuthenticationString &&
                        x.IsActive);

                if (customer == null)
                    return Results.Unauthorized();

                return Results.Ok(new CustomerAccountDTO
                {
                    Name = customer.Name,
                    Email = customer.Email,
                    AddressLine1 = customer.Add1,
                    AddressLine2 = customer.Add2,
                    Town = customer.Add3,
                    Postcode = customer.Postcode,
                    Phone = customer.Phone1
                });
            });

            app.MapPost("/Customer/ChangePassword", async (
                                        CustomerChangePasswordDTO request,
                                        AppDBContext db) =>
            {
                var customer = await db.Customer
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(x =>
                        x.AuthenticationString ==
                            request.AuthenticationString &&
                        x.IsActive);

                if (customer == null)
                    return Results.Unauthorized();

                var hasher = new PasswordHasher<Customer>();

                var verify = hasher.VerifyHashedPassword(
                    customer,
                    customer.PasswordHash,
                    request.CurrentPassword);

                if (verify == PasswordVerificationResult.Failed)
                {
                    return Results.BadRequest(new
                    {
                        ReasonPhrase = "Current password is incorrect."
                    });
                }

                customer.PasswordHash =
                    hasher.HashPassword(
                        customer,
                        request.NewPassword);

                await db.SaveChangesAsync();

                return Results.Ok();
            });

            app.MapPost("/Customer/CurrentRepair", async (
                CustomerRequestDTO request,
                AppDBContext db,
                HttpContext http) =>
            {
               // var tenant = await db.Tenant
               //     .IgnoreQueryFilters()
                //    .FirstOrDefaultAsync(t =>
                //        t.Code == request.TenantCode &&
                //        t.IsActive);
            
                //if (tenant == null)
                //    return Results.Unauthorized();

                var account = await db.Customer
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(x =>
                        x.AuthenticationString == request.AuthenticationString &&
                        x.IsActive);

                if (account == null)
                    return Results.Unauthorized();

                var contract = await db.Contract
                    .IgnoreQueryFilters()
                    .Include(c => c.Customer)
                    .FirstOrDefaultAsync(c =>
                        c.Id == request.ContractId &&
                        c.CustomerId == account.Id);

                if (contract == null)
                    return Results.NotFound();

                var updates = await db.ContractUpdates
                    .IgnoreQueryFilters()
                    .Where(x =>
                        //x.TenantId == tenant.Id &&
                        x.ContractId == contract.Id &&
                        x.CustomerVisible)
                    .OrderByDescending(x => x.DateTime)
                    .Select(x => new CustomerRepairUpdateDTO
                    {
                        Title = x.Title,
                        Description = x.Description,
                        DateTime = x.DateTime,
                        IsCurrent = x.Status == contract.Status
                    })
                    .ToListAsync();

                var nextJob = await db.Job
                    .IgnoreQueryFilters()
                    .Include(j => j.Agent)
                    .Where(j =>
                       // j.TenantId == tenant.Id &&
                        j.ContractId == contract.Id &&
                        j.DiaryDate >= DateTime.Today)
                    .OrderBy(j => j.DiaryDate)
                    .ThenBy(j => j.Time)
                    .FirstOrDefaultAsync();

                CustomerAppointmentDTO? appointment = null;
                if (nextJob != null)
                {
                    var appointmentDateTime = nextJob.DiaryDate.Date
                        .Add(nextJob.Time.TimeOfDay);

                    appointment = new CustomerAppointmentDTO
                    {
                        Type = nextJob.JobType == enum_job_type.Survey
                            ? "Survey appointment"
                            : nextJob.JobType == enum_job_type.Fitting
                                ? "Fitting appointment"
                                : "Repair appointment",
                        DateTime = appointmentDateTime,
                        EngineerName = nextJob.Agent?.Name ?? "To be confirmed",
                        Notes = "Your appointment details will be updated here if anything changes."
                    };
                }

                var baseUrl = $"{http.Request.Scheme}://{http.Request.Host}";
                var photoRows = await db.Images
                    .IgnoreQueryFilters()
                    .Where(x =>
                       // x.TenantId == tenant.Id &&
                        x.ContractCode == contract.ContractCode &&
                        x.CustomerVisible)
                    .OrderByDescending(x => x.DateTime)
                    .ToListAsync();

                var photos = photoRows
                    .Select(x => new CustomerPhotoDTO
                    {
                        Url = baseUrl + "/images/" + Uri.EscapeDataString(x.Filename),
                        Category = GetPhotoCategory(x.Filename),
                        Title = "Repair photo",
                        Description = "Photo shared by your repair team.",
                        DateTime = x.DateTime
                    })
                    .ToList();

                var dto = new CustomerRepairDTO
                {
                    ContractId = contract.Id,
                    Reference = contract.ContractCode,
                    CustomerName = contract.Customer?.Name ?? "",
                    AddressLine1 = contract.Customer?.Add1 ?? "",
                    Town = FirstNonEmpty(contract.Customer?.Add3, contract.Customer?.Add2),
                    Postcode = contract.Customer?.Postcode ?? "",
                    DamageDescription = contract.DamageDescription,
                    CauseOfDamage = contract.CauseOfDamage,
                    Status = contract.Status,
                    StatusTitle = ContractStatusHelper.GetTitle(contract.Status),
                    StatusDescription = ContractStatusHelper.GetDescription(contract.Status),
                    StatusEta = GetStatusEta(contract.Status, appointment),
                    NextAppointment = appointment,
                    Updates = updates,
                    Photos = photos,
                    Items =
                    [
                        new CustomerRepairItemDTO
                        {
                            Location = "Property",
                            Title = contract.DamageDescription,
                            Description = contract.CauseOfDamage
                        }
                    ]
                };

                return Results.Ok(dto);
            });

            // Initial testing helper. Call this while signed into the MVC site.
            // It creates or resets a customer portal login for the current tenant.
            app.MapPost("/Customer/CreateTestAccount", async (
                CreateCustomerPortalAccountDTO request,
                AppDBContext db,
                ITenantContext tenantContext) =>
            {
                if (!tenantContext.TenantId.HasValue)
                    return Results.Unauthorized();

                int tenantId = tenantContext.TenantId.Value;

                var customer = await db.Customer
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(x =>
                        x.TenantId == tenantId &&
                        x.Id == request.CustomerId);

                if (customer == null)
                    return Results.NotFound(new { ReasonPhrase = "Customer not found." });

                string email = request.Email.Trim().ToLowerInvariant();

                var account = await db.Customer
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(x =>
                        x.TenantId == tenantId &&
                        x.Id == request.CustomerId);

                if (account == null)
                {
                    account = new Customer
                    {
                        //TenantId = tenantId,
                        Id = customer.Id,
                        Email = email,
                        IsActive = true
                    };
                    db.Customer.Add(account);
                }
                else
                {
                    account.Email = email;
                    account.IsActive = true;
                }

                var hasher = new PasswordHasher<Customer>();
                account.PasswordHash = hasher.HashPassword(account, request.Password);
                account.AuthenticationString = "";

                await db.SaveChangesAsync();

                return Results.Ok(new
                {
                    customer.Id,
                    customer.Name,
                    account.Email
                });
            }).RequireAuthorization();
        }

      

        private static string GetStatusEta(
            ContractStatus status,
            CustomerAppointmentDTO? appointment)
        {
            if (appointment != null)
                return $"Next appointment {appointment.DateTime:d MMMM} at {appointment.DateTime:HH:mm}";

            return status switch
            {
                ContractStatus.FittingBooked => "We'll update you when the fitting is ready.",
                ContractStatus.SurveyCompleted => "We'll update you when the next step is arranged.",
                _ => ""
            };
        }

        private static string GetPhotoCategory(string filename)
        {
            if (filename.Contains("fAZ", StringComparison.OrdinalIgnoreCase)) return "Fitting";
            if (filename.Contains("cAZ", StringComparison.OrdinalIgnoreCase)) return "Survey";
            return "Repair";
        }

        private static string FirstNonEmpty(params string?[] values) =>
            values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "";
    }
}
