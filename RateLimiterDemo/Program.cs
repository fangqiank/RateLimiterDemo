using RateLimiterDemo.Middleware;
using RateLimiterDemo.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.AddRedisClient("ratelimit-redis");

builder.Services.AddSingleton<RedisConnectionManager>();
builder.Services.AddSingleton<SlideWindowRateLimiter>();
builder.Services.AddSingleton<FixedWindowRateLimiter>();
builder.Services.AddSingleton<LuaScriptRateLimiter>();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseMiddleware<RateLimitingMiddleware>();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
