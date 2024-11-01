using Microsoft.Net.Http.Headers;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using StackExchange.Redis;
using System.Text.Json;
using ThrottlingTroll;
using ThrottlingTroll.CounterStores.Redis;
using ThrottlingTrollSampleWeb.Controllers;

namespace ThrottlingTrollSampleWeb
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllersWithViews();
            builder.Services.AddRazorPages();

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
                options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "ThrottlingTrollSampleWeb.xml"));
            });

            // If RedisConnectionString is specified, then using RedisCounterStore.
            // Otherwise the default MemoryCacheCounterStore will be used.
            var redisConnString = builder.Configuration["RedisConnectionString"];
            if (!string.IsNullOrEmpty(redisConnString))
            {
                builder.Services.AddSingleton<ICounterStore>(
                    new RedisCounterStore(ConnectionMultiplexer.Connect(redisConnString))
                );
            }

            // OpenTelemetry

            builder.Services
                .AddOpenTelemetry()
                .WithMetrics(metrics =>
                {
                    metrics.AddAspNetCoreInstrumentation();

                    metrics.AddMeter("Microsoft.AspNetCore.Hosting");
                    metrics.AddMeter("Microsoft.AspNetCore.Server.Kestrel");

                    metrics.AddMeter("ThrottlingTroll");
                })
                .WithTracing(tracing =>
                {
                    tracing.AddAspNetCoreInstrumentation();
                    tracing.AddHttpClientInstrumentation();
                    tracing.AddSource("ThrottlingTroll");

                    tracing.AddZipkinExporter();
                });


            // <ThrottlingTroll Egress Configuration>

            // Configuring a named HttpClient for egress throttling. Rules and limits taken from appsettings.json
            builder.Services.AddHttpClient(TestController.MyThrottledHttpClientName).AddThrottlingTrollMessageHandler();

            // Configuring a named HttpClient that does automatic retries with respect to Retry-After response header.
            // Note that egress limits from appsettings.json will also apply to this one.
            builder.Services.AddHttpClient(TestController.MyRetryingHttpClientName).AddThrottlingTrollMessageHandler(options =>
            {
                options.ResponseFabric = async (checkResults, requestProxy, responseProxy, cancelToken) =>
                {
                    var egressResponse = (IEgressHttpResponseProxy)responseProxy;

                    egressResponse.ShouldRetry = true;
                };
            });

            // </ThrottlingTroll Egress Configuration>


            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseAuthorization();

            app.MapControllers();

            app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

            app.MapRazorPages();

            // <ThrottlingTroll Ingress Configuration>

            // This method will read static configuration from appsettings.json and merge it with all the programmatically configured rules below
            app.UseThrottlingTroll(options =>
            {
                // Here is how to enable storing rate counters in Redis. You'll also need to add a singleton IConnectionMultiplexer instance beforehand.
                // options.CounterStore = new RedisCounterStore(app.Services.GetRequiredService<IConnectionMultiplexer>());

                options.Config = new ThrottlingTrollConfig
                {
                    // Specifying UniqueName is needed when multiple services store their
                    // rate limit counters in the same cache instance, to prevent those services
                    // from corrupting each other's counters. Otherwise you can skip it.
                    UniqueName = "MyThrottledService1",


                    Rules = new[]
                    {
                        // Static programmatic configuration
                        new ThrottlingTrollRule
                        {
                            UriPattern = $"/{TestController.FixedWindow1RequestPer2SecondsConfiguredProgrammaticallyRoute}",
                            LimitMethod = new FixedWindowRateLimitMethod
                            {
                                PermitLimit = 1,
                                IntervalInSeconds = 2
                            }
                        },


                        // Demonstrates how to use custom response fabrics
                        new ThrottlingTrollRule
                        {
                            UriPattern = $"/{TestController.FixedWindow1RequestPer2SecondsResponseFabricRoute}",
                            LimitMethod = new FixedWindowRateLimitMethod
                            {
                                PermitLimit = 1,
                                IntervalInSeconds = 2
                            },

                            // Custom response fabric, returns 400 BadRequest + some custom content
                            ResponseFabric = async (checkResults, requestProxy, responseProxy, requestAborted) =>
                            {
                                // Getting the rule that was exceeded and with the biggest RetryAfter value
                                var limitExceededResult = checkResults.OrderByDescending(r => r.RetryAfterInSeconds).FirstOrDefault(r => r.RequestsRemaining < 0);
                                if (limitExceededResult == null)
                                {
                                    return;
                                }

                                responseProxy.StatusCode = StatusCodes.Status400BadRequest;

                                responseProxy.SetHttpHeader(HeaderNames.RetryAfter, limitExceededResult.RetryAfterHeaderValue);

                                await responseProxy.WriteAsync("Too many requests. Try again later.");
                            }
                        },


                        // Demonstrates how to delay the response instead of returning 429
                        new ThrottlingTrollRule
                        {
                            UriPattern = $"/{TestController.FixedWindow1RequestPer2SecondsDelayedResponseRoute}",
                            LimitMethod = new FixedWindowRateLimitMethod
                            {
                                PermitLimit = 1,
                                IntervalInSeconds = 2
                            },

                            // Custom response fabric, impedes the normal response for 3 seconds
                            ResponseFabric = async (checkResults, requestProxy, responseProxy, requestAborted) =>
                            {
                                await Task.Delay(TimeSpan.FromSeconds(3));

                                var ingressResponse = (IIngressHttpResponseProxy)responseProxy;
                                ingressResponse.ShouldContinueAsNormal = true;
                            }
                        },


                        // Demonstrates how to use identity extractors
                        new ThrottlingTrollRule
                        {
                            UriPattern = $"/{TestController.FixedWindow3RequestsPer15SecondsPerEachApiKeyRoute}",
                            LimitMethod = new FixedWindowRateLimitMethod
                            {
                                PermitLimit = 3,
                                IntervalInSeconds = 15
                            },

                            IdentityIdExtractor = request =>
                            {
                                // Identifying clients by their api-key
                                return ((IIncomingHttpRequestProxy)request).Request.Query["api-key"];
                            }
                        },


                        // Demonstrates Semaphore (Concurrency) rate limiter
                        // DON'T TEST IT IN BROWSER, because browsers themselves limit the number of concurrent requests to the same URL.
                        new ThrottlingTrollRule
                        {
                            UriPattern = $"/{TestController.Semaphore2ConcurrentRequestsRoute}",
                            LimitMethod = new SemaphoreRateLimitMethod
                            {
                                PermitLimit = 2
                            }
                        },


                        // Demonstrates how to make a named distributed critical section with Semaphore (Concurrency) rate limiter and Identity Extractor.
                        // Query string's 'id' parameter is used as identityId.
                        // DON'T TEST IT IN BROWSER, because browsers themselves limit the number of concurrent requests to the same URL.
                        new ThrottlingTrollRule
                        {
                            UriPattern = $"/{TestController.NamedCriticalSectionRoute}",
                            LimitMethod = new SemaphoreRateLimitMethod
                            {
                                PermitLimit = 1
                            },

                            // This must be set to something > 0 for responses to be automatically delayed
                            MaxDelayInSeconds = 120,

                            IdentityIdExtractor = request =>
                            {
                                // Identifying clients by their id
                                return ((IIncomingHttpRequestProxy)request).Request.Query["id"];
                            }
                        },


                        // Demonstrates how to make a distributed counter with SemaphoreRateLimitMethod
                        new ThrottlingTrollRule
                        {
                            UriPattern = $"/{TestController.DistributedCounterRoute}",
                            LimitMethod = new SemaphoreRateLimitMethod
                            {
                                PermitLimit = 1
                            },

                            // This must be set to something > 0 for responses to be automatically delayed
                            MaxDelayInSeconds = 120,

                            IdentityIdExtractor = request =>
                            {
                                // Identifying counters by their id
                                return ((IIncomingHttpRequestProxy)request).Request.Query["id"];
                            }
                        },


                        // Demonstrates how to use cost extractors
                        new ThrottlingTrollRule
                        {
                            UriPattern = $"/{TestController.FixedWindowBalanceOf10Per20Seconds}",

                            LimitMethod = new FixedWindowRateLimitMethod
                            {
                                PermitLimit = 10,
                                IntervalInSeconds = 20
                            },

                            // Specifying a routine to calculate the cost (weight) of each request
                            CostExtractor = request =>
                            {
                                // Cost comes as a 'cost' query string parameter
                                string? cost = ((IIncomingHttpRequestProxy)request).Request.Query["cost"];

                                return long.TryParse(cost, out long val) ? val : 1;
                            }
                        },


                        // Demonstrates how to use request deduplication
                        new ThrottlingTrollRule
                        {
                            UriPattern = $"/{TestController.RequestDeduplicationRoute}",

                            LimitMethod = new SemaphoreRateLimitMethod
                            {
                                PermitLimit = 1,
                                ReleaseAfterSeconds = 10
                            },

                            // Using "id" query string param to identify requests
                            IdentityIdExtractor = request =>
                            {
                                return ((IIncomingHttpRequestProxy)request).Request.Query["id"];
                            },

                            // Returning 409 Conflict for duplicate requests
                            ResponseFabric = async (checkResults, requestProxy, responseProxy, requestAborted) =>
                            {
                                responseProxy.StatusCode = StatusCodes.Status409Conflict;

                                await responseProxy.WriteAsync("Duplicate request detected");
                            }
                        },


                        // Demonstrates how to use circuit breaker
                        new ThrottlingTrollRule
                        {
                            UriPattern = $"/{TestController.CircuitBreaker2ErrorsPer10SecondsRoute}",

                            LimitMethod = new CircuitBreakerRateLimitMethod
                            {
                                PermitLimit = 2,
                                IntervalInSeconds = 10,
                                TrialIntervalInSeconds = 20
                            }
                        }
                    },
                };


                // Reactive programmatic configuration. Allows to adjust rules and limits without restarting the service.
                options.GetConfigFunc = async () =>
                {
                    // Loading settings from a custom file. You can instead load them from a database
                    // or from anywhere else.

                    string ruleFileName = Path.Combine(AppContext.BaseDirectory, "my-dynamic-throttling-rule.json");

                    string ruleJson = await File.ReadAllTextAsync(ruleFileName);

                    var rule = JsonSerializer.Deserialize<ThrottlingTrollRule>(ruleJson);

                    return new ThrottlingTrollConfig
                    {
                        Rules = new[] { rule }
                    };
                };

                // The above function will be periodically called every 5 seconds
                options.IntervalToReloadConfigInSeconds = 5;

            });

            // </ThrottlingTroll Ingress Configuration>


            app.Run();
        }
    }
}