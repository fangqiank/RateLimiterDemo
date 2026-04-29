# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
# Restore and build
dotnet build

# Run with Aspire (starts Redis container + 3 API replicas + Redis Commander)
dotnet run --project RateLimiterDemo.AppHost

# Run API standalone (requires Redis at localhost:6379)
dotnet run --project RateLimiterDemo
```

Target framework: .NET 10.0. Aspire SDK 13.1.2.

## Architecture

This is a .NET Aspire distributed application demonstrating Redis-based rate limiting algorithms.

**Three projects:**
- `RateLimiterDemo.AppHost` — Aspire orchestrator. Provisions Redis container (`ratelimit-redis`), launches 3 API replicas on port 5001, includes Redis Commander UI.
- `RateLimiterDemo.ServiceDefaults` — Shared Aspire service configuration (OpenTelemetry, health checks, service discovery, HTTP resilience).
- `RateLimiterDemo` — The API project implementing rate limiting algorithms.

**Rate limiting algorithms (all Redis-backed):**

| Class | Algorithm | Redis Data Structure | API Endpoint |
|---|---|---|---|
| `FixedWindowRateLimiter` | Fixed Window | String counter with TTL | `GET /api/demo/fixed-window` (via middleware) |
| `SlideWindowRateLimiter` | Sliding Window | Sorted Set (timestamp+guid members) | `GET /api/demo/sliding-window-test` (direct call) |
| `LuaScriptRateLimiter` | Token Bucket | Hash + Lua script (atomic) | `GET /api/demo/token-bucket-test` (direct call) |

**Key flow:**
- `RateLimitingMiddleware` intercepts requests and applies `FixedWindowRateLimiter` based on `[RateLimitPolicy]` attribute on endpoints. Policies: `strict` (10 req/min), `relaxed` (1000 req/min), `default` (100 req/min).
- Sliding Window and Token Bucket are called directly from `DemoController`, bypassing middleware.
- All limiters depend on `RedisConnectionManager` (singleton wrapping `IConnectionMultiplexer`).
- `LuaScriptRateLimiter` pre-loads and prepares the Lua script at construction time via `LuaScript.Prepare().Load(server)`.

**Client identification:** Authenticated user name > X-Forwarded-For header > Remote IP > "unknown".

**Redis key patterns:** `ratelimit:fixed:{clientId}:{window}`, `ratelimit:sliding:{clientId}`, `ratelimit:tokenbucket:{clientId}`.

## API Docs

Development mode exposes OpenAPI spec + Scalar UI at the root path. Health endpoints: `/health` and `/alive`.
