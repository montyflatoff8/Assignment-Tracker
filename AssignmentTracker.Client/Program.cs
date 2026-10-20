// The consumer app: a small ASP.NET Core host that serves the static web page in wwwroot.
// The page itself (wwwroot/app.js) calls the Assignment Tracker API from the browser with fetch().

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Tell the page where the API lives. Change "ApiBaseUrl" in appsettings.json if your API runs elsewhere.
var apiBaseUrl = (builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5204").TrimEnd('/');

app.MapGet("/config.js", () => Results.Text(
    $"window.APP_CONFIG = {System.Text.Json.JsonSerializer.Serialize(new { apiBaseUrl })};",
    "text/javascript"));

app.UseDefaultFiles();   // serves index.html for "/"
app.UseStaticFiles();

app.Run();
