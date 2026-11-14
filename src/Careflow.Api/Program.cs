using System.Net;
using System.Threading.RateLimiting;
using Careflow.Api;
using Careflow.Application;
using Careflow.Domain;
using Careflow.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
var configuredMode = builder.Configuration["Careflow:Mode"] ?? "SqlServer";
var demo = configuredMode.Equals("Demo", StringComparison.OrdinalIgnoreCase);
var mode = demo ? "Demo" : configuredMode.Equals("SqlServer", StringComparison.OrdinalIgnoreCase) ? "SqlServer" : throw new InvalidOperationException("Careflow:Mode must be Demo or SqlServer.");
if (demo && builder.Environment.IsProduction()) throw new InvalidOperationException("The synthetic adapter cannot run in Production. Use SQL Server mode.");
if (!demo && builder.Environment.IsProduction())
{
    var publicOrigin = builder.Configuration["Careflow:PublicOrigin"];
    if (!Uri.TryCreate(publicOrigin, UriKind.Absolute, out var originUri) || originUri.Scheme != "https" || originUri.AbsolutePath != "/" || originUri.Query.Length > 0 || originUri.Fragment.Length > 0 || originUri.UserInfo.Length > 0) throw new InvalidOperationException("Production requires an exact HTTPS origin without path, query, fragment or credentials.");
    if (!Guid.TryParse(builder.Configuration["Careflow:HospitalId"], out var configuredHospital) || configuredHospital == Guid.Empty || !Guid.TryParse(builder.Configuration["Careflow:BranchId"], out var configuredBranch) || configuredBranch == Guid.Empty) throw new InvalidOperationException("Production requires explicit hospital and branch identifiers.");
}
var connectionString = builder.Configuration.GetConnectionString("Careflow") ?? "";
if (!demo && string.IsNullOrWhiteSpace(connectionString)) throw new InvalidOperationException("ConnectionStrings__Careflow is required in SQL Server mode.");
SqlDatabase? database = demo ? null : new SqlDatabase(connectionString, builder.Configuration["Careflow:ProviderFactoryType"] ?? "Microsoft.Data.SqlClient.SqlClientFactory, Microsoft.Data.SqlClient");
if (args.FirstOrDefault() == "migrate")
{
    if (database is null) throw new InvalidOperationException("Migrations require SQL Server mode and a connection string.");
    await database.MigrateAsync(Path.Combine(AppContext.BaseDirectory, "migrations"), CancellationToken.None);
    Console.WriteLine("SQL migrations applied successfully."); return;
}
if (args.FirstOrDefault() is "provision-admin" or "provision-user") { await AdministratorProvisioner.Run(database ?? throw new InvalidOperationException("SQL Server mode required.")); return; }

builder.WebHost.ConfigureKestrel(options => { options.Limits.MaxRequestBodySize = 262_144; options.AddServerHeader = false; });
builder.Services.ConfigureHttpJsonOptions(options => { options.SerializerOptions.RespectNullableAnnotations = true; options.SerializerOptions.RespectRequiredConstructorParameters = true; });
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear(); options.KnownProxies.Clear();
    var trusted = builder.Configuration["Careflow:TrustedProxies"];
    if (string.IsNullOrWhiteSpace(trusted)) options.KnownProxies.Add(IPAddress.Loopback);
    else foreach (var entry in trusted.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        if (IPAddress.TryParse(entry, out var proxy)) options.KnownProxies.Add(proxy);
        else if (System.Net.IPNetwork.TryParse(entry, out var network)) options.KnownIPNetworks.Add(network);
        else throw new InvalidOperationException($"Invalid Careflow:TrustedProxies entry: {entry}");
    }
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<CareflowExceptionHandler>();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<IPasswordService, IdentityPasswordService>();
if (demo)
{
    builder.Services.AddSingleton(sp => DemoData.Create(sp.GetRequiredService<IClock>(), sp.GetRequiredService<IPasswordService>()));
    builder.Services.AddSingleton<IUnitOfWork>(sp => new MemoryUnitOfWork(sp.GetRequiredService<DemoData>()));
}
else builder.Services.AddSingleton<IUnitOfWork>(new SqlUnitOfWork(database!));
builder.Services.AddSingleton<HospitalService>();
builder.Services.AddSingleton<InpatientService>();
builder.Services.AddSingleton<PharmacyService>();
builder.Services.AddSingleton<LaboratoryService>();
builder.Services.AddSingleton<RadiologyService>();
builder.Services.AddSingleton<EmergencyService>();
builder.Services.AddSingleton<OtService>();
builder.Services.AddSingleton<BloodBankService>();
builder.Services.AddSingleton<InsuranceService>();
builder.Services.AddSingleton<IncentiveService>();
builder.Services.AddSingleton<TelemedicineService>();
builder.Services.AddSingleton<InventoryService>();
builder.Services.AddSingleton<CssdService>();
builder.Services.AddSingleton<QualityService>();
builder.Services.AddSingleton<PortalService>();
builder.Services.AddSingleton<ReportingService>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
    if (demo) options.AddPolicy("demo", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
});
var app = builder.Build();
app.UseForwardedHeaders();
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.Headers.CacheControl = "no-store";
    }
    else
    {
        context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; font-src 'self'; connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";
    }
    await next();
});
app.UseRateLimiter();
// The compiled React workspace is served by this ASP.NET Core host so the product
// runs as a single .NET process in production (see docs/adr/0007). The file provider
// resolves wwwroot next to the published binaries first, then the project directory.
var webRoot = new[] { Path.Combine(AppContext.BaseDirectory, "wwwroot"), Path.Combine(builder.Environment.ContentRootPath, "wwwroot") }.FirstOrDefault(Directory.Exists);
if (webRoot is not null)
{
    var files = new PhysicalFileProvider(webRoot);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = files,
        OnPrepareResponse = context =>
        {
            if (context.Context.Request.Path.StartsWithSegments("/assets")) context.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        }
    });
}
app.MapGet("/health/live", () => Results.Ok(new { status = "alive" }));
app.MapGet("/health/ready", async (CancellationToken ct) => database is null || await database.Ready(ct) ? Results.Ok(new { status = "ready", mode }) : Results.Problem(statusCode: 503, title: "Database is unavailable."));

var root = app.MapGroup("/api/v1");
root.MapGet("/config", async (HttpContext context, HospitalService service, CancellationToken ct) =>
{
    var token = context.Request.Cookies["careflow.session"];
    var principal = token is null ? null : await service.Authenticate(token, HospitalId(), BranchId(), ct);
    return Results.Ok(new { demo, product = "Careflow HIS", phase = 5, principal });
});
Guid HospitalId() => Guid.TryParse(builder.Configuration["Careflow:HospitalId"], out var id) ? id : SeedIds.Hospital;
Guid BranchId() => Guid.TryParse(builder.Configuration["Careflow:BranchId"], out var id) ? id : SeedIds.Branch;
void SetCookie(HttpContext context, AuthResult auth) => context.Response.Cookies.Append("careflow.session", auth.Token, new CookieOptions { HttpOnly = true, Secure = !demo, SameSite = SameSiteMode.Strict, Expires = auth.ExpiresAt, Path = "/api", IsEssential = true });
root.MapPost("/auth/login", async (LoginInput input, HttpContext context, HospitalService service, CancellationToken ct) =>
{
    var auth = await service.Login(input, HospitalId(), BranchId(), ct, context.Connection.RemoteIpAddress?.ToString()); SetCookie(context, auth); return Results.Ok(auth.Principal);
}).RequireRateLimiting("login");
root.MapPost("/auth/demo", async (DemoInput? input, HttpContext context, HospitalService service, CancellationToken ct) =>
{
    if (!demo) return Results.NotFound();
    var role = input?.Role ?? "Hospital Admin";
    var username = role switch { "Doctor" => "doctor", "Receptionist" => "reception", "Billing Staff" => "billing", "Nurse" => "nurse", "Nurse 2" => "nurse2", "Pharmacist" => "pharmacist", "Lab Technician" => "labtech", "Pathologist" => "pathologist", "Hospital Admin" => "admin", "Radiologist" => "radiologist", "Blood Bank Technician" => "bloodtech", "Insurance Coordinator" => "insurance", "Accountant" => "account", "Store Manager" => "store", "CSSD Technician" => "cssdtech", _ => "" };
    if (username == "") return Results.BadRequest(new { title = "Unknown preview role." });
    var old = context.Request.Cookies["careflow.session"]; if (old is not null) await service.Logout(old, HospitalId(), BranchId(), ct);
    var auth = await service.Login(new LoginInput(username, "Preview!2026"), SeedIds.Hospital, SeedIds.Branch, ct, context.Connection.RemoteIpAddress?.ToString()); SetCookie(context, auth); return Results.Ok(auth.Principal);
}).RequireRateLimiting("demo");

app.Use(async (context, next) =>
{
    if (!context.Request.Path.StartsWithSegments("/api/v1")) { await next(); return; }
    var path = context.Request.Path.Value ?? "";
    if (path is "/api/v1/config" or "/api/v1/auth/login" or "/api/v1/auth/demo") { await next(); return; }
    var token = context.Request.Cookies["careflow.session"];
    var principal = token is null ? null : await context.RequestServices.GetRequiredService<HospitalService>().Authenticate(token, HospitalId(), BranchId(), context.RequestAborted);
    if (principal is null) { context.Response.StatusCode = 401; await context.Response.WriteAsJsonAsync(new { title = "Sign in to access the hospital workspace.", code = "unauthenticated" }); return; }
    context.Items["principal"] = principal;
    if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
    {
        if (context.Request.Headers["X-Requested-With"] != "Careflow") { context.Response.StatusCode = 403; await context.Response.WriteAsJsonAsync(new { title = "A same-origin application header is required.", code = "csrf" }); return; }
        var origin = context.Request.Headers.Origin.ToString();
        var allowed = builder.Configuration["Careflow:PublicOrigin"];
        var requestAuthority = context.Request.Host.Port.HasValue && context.Request.Host.Port.Value == (context.Request.IsHttps ? 443 : 80) ? context.Request.Host.Host : context.Request.Host.Value;
        var originPermitted = Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.AbsolutePath == "/" && uri.Query.Length == 0 && uri.Fragment.Length == 0 && uri.UserInfo.Length == 0 &&
            (!string.IsNullOrEmpty(allowed) ? origin.TrimEnd('/') == allowed.TrimEnd('/') : demo && uri.Scheme == context.Request.Scheme && uri.Authority.Equals(requestAuthority, StringComparison.OrdinalIgnoreCase));
        if (!originPermitted)
        { context.Response.StatusCode = 403; await context.Response.WriteAsJsonAsync(new { title = "Request origin is not permitted.", code = "origin" }); return; }
    }
    await next();
});
Scope Scope(HttpContext context) => Careflow.Application.Scope.For((Principal)context.Items["principal"]!, context.RequestServices.GetRequiredService<IClock>().UtcNow);
root.MapGet("/auth/me", (HttpContext context) => Results.Ok(Scope(context).Actor));
root.MapPost("/auth/logout", async (HttpContext context, HospitalService service, CancellationToken ct) =>
{
    var token = context.Request.Cookies["careflow.session"]; if (token is not null) await service.Logout(token, HospitalId(), BranchId(), ct);
    context.Response.Cookies.Delete("careflow.session", new CookieOptions { Path = "/api", HttpOnly = true, Secure = !demo, SameSite = SameSiteMode.Strict }); return Results.NoContent();
});
root.MapGet("/workspace", async (HttpContext context, HospitalService service, CancellationToken ct) => Results.Ok(await service.GetWorkspace(Scope(context), ct)));
root.MapGet("/patients/{id:guid}/chart", async (Guid id, HttpContext context, HospitalService service, CancellationToken ct) => Results.Ok(await service.GetChart(Scope(context), id, ct)));
root.MapPost("/patients/duplicate-check", async (PatientInput input, HttpContext context, HospitalService service, CancellationToken ct) =>
{
    var scope = Scope(context); scope.Require(Permissions.PatientRead);
    return Results.Ok(await service.FindDuplicates(scope, input, ct));
});
root.MapPost("/patients", async (PatientInput input, HttpContext context, HospitalService service, CancellationToken ct) => Results.Ok(await service.Register(Scope(context), input, ct)));
root.MapPut("/patients/{id:guid}", async (Guid id, long version, PatientInput input, HttpContext context, HospitalService service, CancellationToken ct) => Results.Ok(await service.UpdatePatient(Scope(context), id, input, version, ct)));
root.MapPost("/appointments", async (BookingInput input, HttpContext context, HospitalService service, CancellationToken ct) => Results.Ok(await service.Book(Scope(context), input, ct)));
root.MapPost("/appointments/{id:guid}/reschedule", async (Guid id, RescheduleInput input, HttpContext context, HospitalService service, CancellationToken ct) => Results.Ok(await service.Reschedule(Scope(context), id, input.StartsAt, input.Version, input.Reason, ct)));
root.MapPost("/appointments/{id:guid}/encounter", async (Guid id, HttpContext context, HospitalService service, CancellationToken ct) => Results.Ok(await service.StartEncounter(Scope(context), id, ct)));
foreach (var action in new[] { "check-in", "start", "complete", "cancel" })
    root.MapPost($"/appointments/{{id:guid}}/{action}", async (Guid id, VersionInput input, HttpContext context, HospitalService service, CancellationToken ct) => Results.Ok(await service.Transition(Scope(context), id, action, input.Version, input.Reason, ct)));
root.MapPut("/encounters/{id:guid}/draft", async (Guid id, DraftInput input, HttpContext context, HospitalService service, CancellationToken ct) => Results.Ok(await service.SaveDraft(Scope(context), id, input, ct)));
root.MapPost("/encounters/{id:guid}/sign", async (Guid id, VersionInput input, HttpContext context, HospitalService service, CancellationToken ct) => Results.Ok(await service.Sign(Scope(context), id, input.Version, ct)));
root.MapPost("/invoices", async (InvoiceInput input, HttpContext context, HospitalService service, CancellationToken ct) => Results.Ok(await service.CreateInvoice(Scope(context), input, ct)));
root.MapPost("/invoices/{id:guid}/issue", async (Guid id, VersionInput input, HttpContext context, HospitalService service, CancellationToken ct) => Results.Ok(await service.IssueInvoice(Scope(context), id, input.Version, ct)));
root.MapPost("/invoices/{id:guid}/payments", async (Guid id, PaymentInput input, HttpContext context, HospitalService service, CancellationToken ct) => Results.Ok(await service.Pay(Scope(context), id, input, ct)));
root.MapPut("/modules/{key}", async (string key, ModuleInput input, HttpContext context, HospitalService service, CancellationToken ct) => Results.Ok(await service.SetModule(Scope(context), key, input.Enabled, input.Reason, ct)));
CareEndpoints.Map(root);
Phase3Endpoints.Map(root);
Phase4Endpoints.Map(root);
Phase5Endpoints.Map(root);
root.MapGet("/admin/roles", (HttpContext context) => { Scope(context).Require(Permissions.Admin); return Results.Ok(Permissions.Roles.Select(r => new { role = r.Key, permissions = r.Value })); });
if (webRoot is not null)
{
    var indexFile = Path.Combine(webRoot, "index.html");
    app.MapFallback((RequestDelegate)(async context =>
    {
        var path = context.Request.Path.Value ?? "";
        var isPage = HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method);
        if (isPage && !path.StartsWith("/api", StringComparison.OrdinalIgnoreCase) && !path.StartsWith("/health", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.Headers.CacheControl = "no-cache";
            await context.Response.SendFileAsync(indexFile);
            return;
        }
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsJsonAsync(new { title = "Endpoint not found.", code = "not-found" });
    }));
}
app.Run();
public sealed record DemoInput(string Role = "Hospital Admin");
public partial class Program { }
