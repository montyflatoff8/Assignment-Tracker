using AssignmentTracker.Services;
using Azure.Core;
using Azure.Identity;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Logging: one JSON object per line on stdout, so Docker, Azure Container Apps and
// Log Analytics can index each named property (DbHost, ElapsedMs, SqlState...).
// Minimum levels per category are set in appsettings*.json ("Logging:LogLevel").
// ---------------------------------------------------------------------------
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.UseUtcTimestamp = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
    options.IncludeScopes = false;
});

// ---------------------------------------------------------------------------
// Services (dependency injection)
// ---------------------------------------------------------------------------

builder.Services.AddControllers();   // endpoints live in Controllers/AssignmentsController.cs

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

// ---------------------------------------------------------------------------
// PostgreSQL
// Settings come from the "Database" section (appsettings*.json) and can be overridden by
// environment variables such as Database__Host=db (that's how compose.yaml configures it).
//
// Two ways to authenticate:
//   Local (Docker):  Database__Password - a plain password for the local dev database.
//   Azure:           Database__UseManagedIdentity=true - no password anywhere. The app asks
//                    its User-Assigned Managed Identity for a short-lived Microsoft Entra token
//                    and sends that as the password, over TLS (SslMode=Require).
// ---------------------------------------------------------------------------
var db = builder.Configuration.GetSection("Database");
var useManagedIdentity = db.GetValue("UseManagedIdentity", false);

var connectionString = new NpgsqlConnectionStringBuilder
{
    Host = db["Host"] ?? "localhost",
    Port = db.GetValue("Port", 5432),
    Database = db["Name"] ?? "student_db",
    Username = db["Username"],
    Password = useManagedIdentity ? null : db["Password"],
    SslMode = useManagedIdentity ? SslMode.Require : db.GetValue("SslMode", SslMode.Prefer)
}.ConnectionString;

// One NpgsqlDataSource for the whole app = one shared connection pool.
builder.Services.AddSingleton(serviceProvider =>
{
    var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString)
        .UseLoggerFactory(serviceProvider.GetRequiredService<ILoggerFactory>());

    if (useManagedIdentity)
    {
        // ManagedIdentityClientId picks which identity to use (the app can have several).
        // Locally, DefaultAzureCredential falls back to your "az login" account.
        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            ManagedIdentityClientId = db["ManagedIdentityClientId"]
        });
        var tokenRequest = new TokenRequestContext(["https://ossrdbms-aad.database.windows.net/.default"]);

        // Entra tokens expire after about an hour, so fetch a fresh one every 55 minutes.
        // The token is used as the password and is never logged.
        dataSourceBuilder.UsePeriodicPasswordProvider(
            async (_, ct) => (await credential.GetTokenAsync(tokenRequest, ct)).Token,
            successRefreshInterval: TimeSpan.FromMinutes(55),
            failureRefreshInterval: TimeSpan.FromSeconds(10));
    }

    return dataSourceBuilder.Build();
});

builder.Services.AddScoped<AssignmentRepository>();

// The consumer app runs on a different port (a different "origin"), so the browser
// will only let it call this API if we explicitly allow that origin with CORS.
var clientOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                    ?? ["http://localhost:5300"];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy
        .WithOrigins(clientOrigins)
        .AllowAnyHeader()
        .WithMethods("GET", "POST", "PUT", "DELETE"));
});

var app = builder.Build();

// ---------------------------------------------------------------------------
// HTTP request pipeline
// ---------------------------------------------------------------------------

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();   // OpenAPI document at /openapi/v1.json
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();

app.MapControllers();

app.Run();
