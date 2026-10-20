using AssignmentTracker.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Services (dependency injection)
// ---------------------------------------------------------------------------

builder.Services.AddControllers();   // endpoints live in Controllers/AssignmentsController.cs

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

builder.Services.AddSingleton<AssignmentStore>();

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
