var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddControllers().AddJsonOptions(opts =>
{
    opts.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});

// Windows Authentication (Negotiate)
builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Negotiate.NegotiateDefaults.AuthenticationScheme)
    .AddNegotiate();

builder.Services.AddAuthorization();

// Add CORS policy for frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy => policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

var app = builder.Build();

// Use CORS in development
if (app.Environment.IsDevelopment())
{
    app.UseCors("AllowFrontend");
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

// Minimal API to get current Windows user display name
app.MapGet("/api/auth/current-user", (HttpContext http) =>
{
    var identity = http.User?.Identity;
    if (identity is { IsAuthenticated: true, Name: not null })
    {
        // identity.Name usually in the form DOMAIN\\username
        return Results.Ok(new { user = identity.Name });
    }
    return Results.Ok(new { user = "Guest" });
}).RequireAuthorization();

app.MapControllers();
app.Run();
