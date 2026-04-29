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

Target framework: .NET 10.0. Aspire SDK 13.2.4.

## Why multi-instance + Redis

In-memory rate limiting only works within a single process. When deployed with multiple instances, each instance maintains its own counter, resulting in 3x the allowed traffic. Redis provides a shared counter across all instances for true global rate limiting.

```
Multi-instance (in-memory fails):
  Instance 1: 80/100    Instance 2: 90/100    Instance 3: 70/100
  Total: 240 requests passed, but global limit should be 100!

Multi-instance + Redis (correct):
  Instance 1 ─┐   Instance 2 ─┤   Instance 3 ─┘
               ↓
         Redis counter: 100/100 (shared across all instances)
```

**Scenario comparison:**

| Scenario | If rate limiting fails | With Redis working |
|---|---|---|
| 10 instances, each receives 10 requests | All pass (100 total) | Returns 429 from the 6th request onward |
| Load balancer random distribution | Unpredictable, frequently over limit | Precise control, all blocked after 100 |
| High concurrency burst | May exceed limit 2-3x | Atomic operations ensure precision |

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

**Algorithm comparison:**

| | Fixed Window | Sliding Window | Token Bucket |
|---|---|---|---|
| Burst control | Poor (boundary doubling) | Good (smooth) | Good (allows reasonable bursts) |
| Memory | Low (1 key) | High (1 entry per request) | Low (1 key) |
| Complexity | Simple | Medium | Complex (Lua script) |
| Atomicity | Single INCREMENT | Pipeline batch | Lua script guaranteed |
| Use case | Simple rate limiting | Precise rate limiting | API quotas, traffic shaping |

- **Fixed Window**: Counts requests in fixed time intervals. Simplest but suffers from boundary bursts (2x requests at window edges).
- **Sliding Window**: Window slides continuously over time. No boundary issue but stores one Sorted Set member per request.
- **Token Bucket**: Tokens refill at a fixed rate, each request consumes one. Allows short bursts while keeping long-term rate controlled. Requires Lua script for atomic operations.

**Key flow:**
- `RateLimitingMiddleware` intercepts requests and applies `FixedWindowRateLimiter` based on `[RateLimitPolicy]` attribute on endpoints. Policies: `strict` (10 req/min), `relaxed` (1000 req/min), `default` (100 req/min).
- Sliding Window and Token Bucket are called directly from `DemoController`, bypassing middleware.
- All limiters depend on `RedisConnectionManager` (singleton wrapping `IConnectionMultiplexer`).
- `LuaScriptRateLimiter` pre-loads and prepares the Lua script at construction time via `LuaScript.Prepare().Load(server)`.

**Client identification:** Authenticated user name > X-Forwarded-For header > Remote IP > "unknown".

**Redis key patterns:** `ratelimit:fixed:{clientId}:{window}`, `ratelimit:sliding:{clientId}`, `ratelimit:tokenbucket:{clientId}`.

## API Docs

Development mode exposes OpenAPI spec + Scalar UI at the root path. Health endpoints: `/health` and `/alive`.
