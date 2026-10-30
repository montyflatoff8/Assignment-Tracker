using AssignmentTracker.Services;
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
// ---------------------------------------------------------------------------
var db = builder.Configuration.GetSection("Database");
var connectionString = new NpgsqlConnectionStringBuilder
{
    Host = db["Host"] ?? "localhost",
    Port = db.GetValue("Port", 5432),
    Database = db["Name"] ?? "student_db",
    Username = db["Username"],
    Password = db["Password"],
    SslMode = db.GetValue("SslMode", SslMode.Prefer)
}.ConnectionString;

// One NpgsqlDataSource for the whole app = one shared connection pool.
builder.Services.AddSingleton(serviceProvider =>
    new NpgsqlDataSourceBuilder(connectionString)
        .UseLoggerFactory(serviceProvider.GetRequiredService<ILoggerFactory>())
        .Build());

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
