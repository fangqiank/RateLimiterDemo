var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("ratelimit-redis")
    .WithRedisCommander()
    .WithLifetime(ContainerLifetime.Persistent) ;

var api = builder.AddProject<Projects.RateLimiterDemo>("api")
    .WithReference(redis)
    .WaitFor(redis)
    .WithReplicas(3);

builder.Build().Run();
