using Microsoft.AspNetCore.Mvc;
using RestSharp;
using StackExchange.Redis;
using ThrottlingTroll;

namespace ThrottlingTrollSampleWeb.Controllers
{
    [ApiController]
    public class TestController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConnectionMultiplexer _redis;

        public TestController(IHttpClientFactory httpClientFactory, IConnectionMultiplexer redis = null)
        {
            this._httpClientFactory = httpClientFactory;
            this._redis = redis;
        }

        public const string FixedWindow3RequestsPer10SecondsConfiguredViaAppSettingsRoute = "fixed-window-3-requests-per-10-seconds-configured-via-appsettings";
        /// <summary>
        /// Rate limited to 3 requests per a fixed window of 10 seconds. Configured via appsettings.json.
        /// </summary>
        /// <response code="200">OK</response>
        /// <response code="429">TooManyRequests</response>
        [HttpGet]
        [Route(FixedWindow3RequestsPer10SecondsConfiguredViaAppSettingsRoute)]
        public string Test1()
        {
            // Here is how to set a custom header with the number of remaining requests

            // Obtaining the current list of limit check results from HttpContext.Items
            // Note that the result might be null, if ThrottlingTroll had an internal failure (e.g. got disconnected from Redis)
            var limitCheckResults = this.HttpContext.GetThrottlingTrollLimitCheckResults();

            // Now finding the minimal RequestsRemaining number (since there can be multiple rules matched)
            var minRequestsRemaining = limitCheckResults?.OrderByDescending(r => r.RequestsRemaining).FirstOrDefault();
            if (minRequestsRemaining != null)
            {
                // Now setting the custom header
                this.Response.Headers.Add("X-Requests-Remaining", minRequestsRemaining.RequestsRemaining.ToString());
            }

            return "OK";
        }

        public const string SlidingWindow5RequestsPer15SecondsWith5BucketsConfiguredViaAppSettingsRoute = "sliding-window-5-requests-per-15-seconds-with-5-buckets-configured-via-appsettings";
        /// <summary>
        /// Rate limited to 5 requests per a sliding window of 15 seconds split into 5 buckets. Configured via appsettings.json.
        /// </summary>
        /// <response code="200">OK</response>
        /// <response code="429">TooManyRequests</response>
        [HttpGet]
        [Route(SlidingWindow5RequestsPer15SecondsWith5BucketsConfiguredViaAppSettingsRoute)]
        public string Test2()
        {
            return "OK";
        }

        public const string FixedWindow1RequestPer2SecondsConfiguredProgrammaticallyRoute = "fixed-window-1-request-per-2-seconds-configured-programmatically";
        /// <summary>
        /// Rate limited to 1 request per a fixed window of 2 seconds. Configured programmatically.
        /// </summary>
        /// <response code="200">OK</response>
        /// <response code="429">TooManyRequests</response>
        [HttpGet]
        [Route(FixedWindow1RequestPer2SecondsConfiguredProgrammaticallyRoute)]
        public string Test3()
        {
            return "OK";
        }

        public const string SlidingWindow7RequestsPer20SecondsWith4BucketsConfiguredReactivelyRoute = "sliding-window-7-requests-per-20-seconds-with-4-buckets-configured-reactively";
        /// <summary>
        /// Rate limited to 7 requests per a sliding window of 20 seconds split into 4 buckets. Configured reactively (via a callback, that's being re-executed periodically).
        /// </summary>
        /// <response code="200">OK</response>
        /// <response code="429">TooManyRequests</response>
        [HttpGet]
        [Route(SlidingWindow7RequestsPer20SecondsWith4BucketsConfiguredReactivelyRoute)]
        public string Test4()
        {
            return "OK";
        }

        public const string FixedWindow3RequestsPer15SecondsPerEachApiKeyRoute = "fixed-window-3-requests-per-15-seconds-per-each-api-key";
        /// <summary>
        /// Rate limited to 3 requests per a fixed window of 15 seconds per each identity.
        /// Query string's 'api-key' parameter is used as identityId.
        /// Demonstrates how to use identity extractors.
        /// </summary>
        /// <response code="200">OK</response>
        /// <response code="429">TooManyRequests</response>
        [HttpGet]
        [Route(FixedWindow3RequestsPer15SecondsPerEachApiKeyRoute)]
        public string Test5([FromQuery(Name = "api-key")] string apiKey)
        {
            return "OK";
        }

        public const string FixedWindow1RequestPer2SecondsResponseFabricRoute = "fixed-window-1-request-per-2-seconds-response-fabric";
        /// <summary>
        /// Rate limited to 1 request per a fixed window of 2 seconds.
        /// Custom throttled response is returned, with 400 BadRequest status code and custom body.
        /// Demonstrates how to use custom response fabrics.
        /// </summary>
        /// <response code="200">OK</response>
        /// <response code="400">BadRequest</response>
        [HttpGet]
        [Route(FixedWindow1RequestPer2SecondsResponseFabricRoute)]
        public string Test6()
        {
            return "OK";
        }

        public const string FixedWindow1RequestPer2SecondsDelayedResponseRoute = "fixed-window-1-request-per-2-seconds-delayed-response";
        /// <summary>
        /// Rate limited to 1 request per a fixed window of 2 seconds.
        /// Throttled response is delayed for 3 seconds (instead of returning an error).
        /// Demonstrates how to implement a delay with a custom response fabric.
        /// </summary>
        /// <response code="200">OK</response>
        [HttpGet]
        [Route(FixedWindow1RequestPer2SecondsDelayedResponseRoute)]
        public string Test7()
        {
            return "OK";
        }

        public const string Semaphore2ConcurrentRequestsRoute = "semaphore-2-concurrent-requests";
        /// <summary>
        /// Rate limited to 2 concurrent requests.
        /// Demonstrates Semaphore (Concurrency) rate limiter.
        /// DON'T TEST IT IN BROWSER, because browsers themselves limit the number of concurrent requests to the same URL.
        /// </summary>
        /// <response code="200">OK</response>
        [HttpGet]
        [Route(Semaphore2ConcurrentRequestsRoute)]
        public async Task<string> Test8()
        {
            await Task.Delay(TimeSpan.FromSeconds(10));

            return "OK";
        }

        public const string NamedCriticalSectionRoute = "named-critical-section";
        /// <summary>
        /// Rate limited to 1 concurrent request per each identity, other requests are delayed.
        /// Demonstrates how to make a named distributed critical section with Semaphore (Concurrency) rate limiter and Identity Extractor.
        /// Query string's 'id' parameter is used as identityId.
        /// DON'T TEST IT IN BROWSER, because browsers themselves limit the number of concurrent requests to the same URL.
        /// </summary>
        /// <response code="200">OK</response>
        [HttpGet]
        [Route(NamedCriticalSectionRoute)]
        public async Task<string> Test9()
        {
            await Task.Delay(TimeSpan.FromSeconds(10));

            return "OK";
        }

        private static Dictionary<string, long> Counters = new Dictionary<string, long>();

        public const string DistributedCounterRoute = "distributed-counter";
        /// <summary>
        /// Endpoint for testing Semaphores. Increments a counter value, but NOT atomically.
        /// </summary>
        /// <response code="200">OK</response>
        [HttpGet]
        [Route(DistributedCounterRoute)]
        public async Task<long> Test10([FromQuery] string? id)
        {
            long counter = 1;
            string counterId = $"TestCounter{id}";

            // The below code is intentionally not thread-safe

            if (this._redis != null)
            {
                var db = this._redis.GetDatabase();

                counter = (long)await db.StringGetAsync(counterId);

                counter++;

                await db.StringSetAsync(counterId, counter);
            }
            else
            {
                if (Counters.ContainsKey(counterId))
                {
                    counter = Counters[counterId];

                    counter++;
                }

                Counters[counterId] = counter;
            }

            return counter;
        }

        public const string FixedWindowBalanceOf10Per20Seconds = "fixed-window-balance-of-10-per-20-seconds";
        /// <summary>
        /// A balance of 10 coins per a fixed window of 20 seconds. Cost of a request is taken from 'cost' query string parameter. 
        /// </summary>
        /// <response code="200">OK</response>
        /// <response code="429">TooManyRequests</response>
        [HttpGet]
        [Route(FixedWindowBalanceOf10Per20Seconds)]
        public string Test11()
        {
            return "OK";
        }

        public const string RequestDeduplicationRoute = "request-deduplication";
        /// <summary>
        /// Demonstrates how to use request deduplication. First request with a given id will be processed, other requests with the same id will be rejected with 409 Conflict.
        /// Duplicate detection window is set to 10 seconds.
        /// </summary>
        /// <response code="200">OK</response>
        /// <response code="409">Conflict</response>
        [HttpGet]
        [Route(RequestDeduplicationRoute)]
        public string Test12([FromQuery] string? id)
        {
            return "OK";
        }

        public const string CircuitBreaker2ErrorsPer10SecondsRoute = "circuit-breaker-2-errors-per-10-seconds";
        /// <summary>
        /// Demonstrates how to use circuit breaker. The method itself throws 50% of times.
        /// Once the limit of 2 errors per a 10 seconds interval is exceeded, the endpoint goes into Trial state.
        /// While in Trial state, only 1 request per 20 seconds is allowed to go through
        /// (the rest will be handled by ThrottlingTroll, which will return 503 Service Unavailable).
        /// Once a probe request succeeds, the endpoint goes back to normal state.
        /// </summary>
        /// <response code="200">OK</response>
        /// <response code="500">Internal Server Error</response>
        /// <response code="503">Service Unavailable</response>
        [HttpGet]
        [Route(CircuitBreaker2ErrorsPer10SecondsRoute)]
        public IActionResult Test13()
        {
            if (Random.Shared.Next(0, 2) == 1)
            {
                Console.WriteLine($"{CircuitBreaker2ErrorsPer10SecondsRoute} succeeded");

                return this.Ok("OK");
            }
            else
            {
                Console.WriteLine($"{CircuitBreaker2ErrorsPer10SecondsRoute} failed");

                throw new Exception("Oops, I am broken");
            }
        }

        public const string MyThrottledHttpClientName = "my-throttled-httpclient";
        public const string EgressFixedWindow2RequestsPer5SecondsConfiguredViaAppSettingsRoute = "egress-fixed-window-2-requests-per-5-seconds-configured-via-appsettings";
        /// <summary>
        /// Uses a rate-limited HttpClient to make calls to a dummy endpoint. Rate limited to 2 requests per a fixed window of 5 seconds.
        /// </summary>
        /// <response code="200">OK</response>
        [HttpGet]
        [Route(EgressFixedWindow2RequestsPer5SecondsConfiguredViaAppSettingsRoute)]
        public async Task<string> EgressTest1()
        {
            using var client = this._httpClientFactory.CreateClient(MyThrottledHttpClientName);

            string url = $"{this.Request.Scheme}://{this.Request.Host}/{DummyRoute}";

            var response = await client.GetAsync(url);

            return $"Dummy endpoint returned {response.StatusCode}";
        }

        public const string EgressFixedWindow3RequestsPer10SecondsConfiguredProgrammaticallyRoute = "egress-fixed-window-3-requests-per-10-seconds-configured-programmatically";
        /// <summary>
        /// Calls /fixed-window-3-requests-per-10-seconds-configured-via-appsettings endpoint 
        /// using an HttpClient that is configured to propagate 429 responses.  
        /// HttpClient configured in-place programmatically.
        /// </summary>
        /// <response code="200">OK</response>
        /// <response code="429">TooManyRequests</response>
        [HttpGet]
        [Route(EgressFixedWindow3RequestsPer10SecondsConfiguredProgrammaticallyRoute)]
        public async Task<string> EgressTest2()
        {
            // NOTE: HttpClient instances should normally be reused. Here we're creating separate instances only for the sake of simplicity.
            using var client = new HttpClient
            (
                new ThrottlingTrollHandler
                (
                    new ThrottlingTrollEgressConfig
                    {
                        PropagateToIngress = true
                    }
                )
            );

            string url = $"{this.Request.Scheme}://{this.Request.Host}/{FixedWindow3RequestsPer10SecondsConfiguredViaAppSettingsRoute}";

            await client.GetAsync(url);

            return "OK";
        }

        public const string EgressFixedWindow3RequestsPer10SecondsViaRestsharpRoute = "egress-fixed-window-3-requests-per-10-seconds-via-restsharp";
        /// <summary>
        /// Uses a rate-limited <see href="https://restsharp.dev">RestSharp</see>'s RestClient to make calls to a dummy endpoint. 
        /// Rate limited to 3 requests per a fixed window of 10 seconds.
        /// </summary>
        /// <response code="200">OK</response>
        [HttpGet]
        [Route(EgressFixedWindow3RequestsPer10SecondsViaRestsharpRoute)]
        public async Task<string> EgressTest3()
        {
            string url = $"{this.Request.Scheme}://{this.Request.Host}/{DummyRoute}";

            var restClientOptions = new RestClientOptions(url)
            {
                ConfigureMessageHandler = unused =>

                    new ThrottlingTrollHandler
                    (
                        new ThrottlingTrollEgressConfig
                        {
                            Rules = new[]
                            {
                                new ThrottlingTrollRule
                                {
                                    LimitMethod = new FixedWindowRateLimitMethod
                                    {
                                        PermitLimit = 3,
                                        IntervalInSeconds = 10,
                                    }
                                }
                            }
                        }
                    )
            };

            // NOTE: RestClient instances should normally be reused. Here we're creating separate instances only for the sake of simplicity.
            using var restClient = new RestClient(restClientOptions);

            try
            {
                var res = await restClient.GetAsync(new RestRequest());
            }
            catch (HttpRequestException ex)
            {
                return $"Dummy endpoint returned {ex.StatusCode}";
            }

            return $"Dummy endpoint returned OK";
        }

        public const string MyRetryingHttpClientName = "my-retrying-httpclient";
        public const string EgressFixedWindow3RequestsPer10SecondsWithRetriesRoute = "egress-fixed-window-3-requests-per-10-seconds-with-retries";
        /// <summary>
        /// Calls /fixed-window-3-requests-per-10-seconds-configured-via-appsettings endpoint 
        /// using an HttpClient that is configured to do retries.
        /// </summary>
        /// <response code="200">OK</response>
        [HttpGet]
        [Route(EgressFixedWindow3RequestsPer10SecondsWithRetriesRoute)]
        public async Task<string> EgressTest4()
        {
            using var client = this._httpClientFactory.CreateClient(MyRetryingHttpClientName);

            string url = $"{this.Request.Scheme}://{this.Request.Host}/{FixedWindow3RequestsPer10SecondsConfiguredViaAppSettingsRoute}";

            var response = await client.GetAsync(url);

            return $"Dummy endpoint returned {response.StatusCode}";
        }

        public const string EgressFixedWindow3RequestsPer5SecondsWithDelaysRoute = "egress-fixed-window-3-requests-per-5-seconds-with-delays";
        /// <summary>
        /// Calls /dummy endpoint 
        /// using an HttpClient that is limited to 3 requests per 5 seconds and does automatic delays and retries.
        /// HttpClient configured in-place programmatically.
        /// </summary>
        /// <response code="200">OK</response>
        [HttpGet]
        [Route(EgressFixedWindow3RequestsPer5SecondsWithDelaysRoute)]
        public async Task<string> EgressTest5()
        {
            // NOTE: HttpClient instances should normally be reused. Here we're creating separate instances only for the sake of simplicity.
            using var client = new HttpClient
            (
                new ThrottlingTrollHandler
                (
                    async (limitExceededResult, httpRequestProxy, httpResponseProxy, cancellationToken) => 
                    {
                        var egressResponse = (IEgressHttpResponseProxy)httpResponseProxy;

                        egressResponse.ShouldRetry = true;
                    },

                    counterStore: null,

                    new ThrottlingTrollEgressConfig
                    {
                        Rules = new[]
                        {
                            new ThrottlingTrollRule
                            {
                                LimitMethod = new FixedWindowRateLimitMethod
                                {
                                    PermitLimit = 3,
                                    IntervalInSeconds = 5
                                }
                            }
                        }
                    }
                )
            );

            string url = $"{this.Request.Scheme}://{this.Request.Host}/{DummyRoute}";

            await client.GetAsync(url);

            return "OK";
        }

        public const string EgressSemaphore2ConcurrentRequestsRoute = "egress-semaphore-2-concurrent-requests";
        /// <summary>
        /// Calls /lazy-dummy endpoint 
        /// using an HttpClient that is limited to 2 concurrent requests.
        /// Demonstrates Semaphore (Concurrency) rate limiter.
        /// DON'T TEST IT IN BROWSER, because browsers themselves limit the number of concurrent requests to the same URL.
        /// </summary>
        /// <response code="200">OK</response>
        [HttpGet]
        [Route(EgressSemaphore2ConcurrentRequestsRoute)]
        public async Task<string> EgressTest6()
        {
            // NOTE: HttpClient instances should normally be reused. Here we're creating separate instances only for the sake of simplicity.
            using var client = new HttpClient
            (
                new ThrottlingTrollHandler
                (
                    new ThrottlingTrollEgressConfig
                    {
                        Rules = new[]
                        {
                            new ThrottlingTrollRule
                            {
                                LimitMethod = new SemaphoreRateLimitMethod
                                {
                                    PermitLimit = 2
                                }
                            }
                        }
                    }
                )
            );

            string url = $"{this.Request.Scheme}://{this.Request.Host}/{LazyDummyRoute}";

            var response = await client.GetAsync(url);

            return $"Dummy endpoint returned {response.StatusCode}";
        }

        public const string EgressCircuitBreaker2ErrorsPer10SecondsRoute = "egress-circuit-breaker-2-errors-per-10-seconds";
        /// <summary>
        /// Calls /semi-failing-dummy endpoint 
        /// using an HttpClient that is configured to break the circuit after receiving 2 errors within 10 seconds interval.
        /// Once broken, will execute no more than 1 request per 20 seconds, other requests will shortcut to 429.
        /// Once a request succceeds, will return to normal.
        /// </summary>
        /// <response code="200">OK</response>
        [HttpGet]
        [Route(EgressCircuitBreaker2ErrorsPer10SecondsRoute)]
        public async Task<string> EgressTest7()
        {
            // NOTE: HttpClient instances should normally be reused. Here we're creating separate instances only for the sake of simplicity.
            using var client = new HttpClient
            (
                new ThrottlingTrollHandler
                (
                    new ThrottlingTrollEgressConfig
                    {
                        Rules = new[]
                        {
                            new ThrottlingTrollRule
                            {
                                LimitMethod = new CircuitBreakerRateLimitMethod
                                {
                                    PermitLimit = 2,
                                    IntervalInSeconds = 10,
                                    TrialIntervalInSeconds = 20
                                }
                            }
                        }
                    }
                )
            );

            string url = $"{this.Request.Scheme}://{this.Request.Host}/{SemiFailingDummy}";

            var response = await client.GetAsync(url);

            return $"Dummy endpoint returned {response.StatusCode}";
        }

        public const string DummyRoute = "dummy";
        /// <summary>
        /// Dummy endpoint for testing HttpClient. Isn't throttled.
        /// </summary>
        /// <response code="200">OK</response>
        [HttpGet]
        [Route(DummyRoute)]
        public string Dummy()
        {
            return "OK";
        }

        public const string LazyDummyRoute = "lazy-dummy";
        /// <summary>
        /// Dummy endpoint for testing HttpClient. Sleeps for 10 seconds. Isn't throttled.
        /// </summary>
        /// <response code="200">OK</response>
        [HttpGet]
        [Route(LazyDummyRoute)]
        public async Task<string> LazyDummy()
        {
            await Task.Delay(TimeSpan.FromSeconds(10));

            return "OK";
        }

        public const string SemiFailingDummyRoute = "semi-failing-dummy";
        /// <summary>
        /// Dummy endpoint for testing Circuit Breaker. Fails 50% of times.
        /// </summary>
        /// <response code="200">OK</response>
        /// <response code="500">Internal Server Error</response>
        [HttpGet]
        [Route(SemiFailingDummyRoute)]
        public IActionResult SemiFailingDummy()
        {
            if (Random.Shared.Next(0, 2) == 1)
            {
                Console.WriteLine($"{SemiFailingDummyRoute} succeeded");

                return this.Ok("OK");
            }
            else
            {
                Console.WriteLine($"{SemiFailingDummyRoute} failed");

                return this.StatusCode(500);
            }
        }

        public const string ThrottlingTrollConfigDebugDumpRoute = "throttling-troll-config-debug-dump";
        /// <summary>
        /// Dumps all the current effective ThrottlingTroll configuration for debugging purposes.
        /// Never do this in a real service.
        /// </summary>
        [HttpGet]
        [Route(ThrottlingTrollConfigDebugDumpRoute)]
        public List<ThrottlingTrollConfig> ThrottlingTrollConfigDebugDump()
        {
            // ThrottlingTroll places a list of ThrottlingTrollConfigs into request's context under the "ThrottlingTrollConfigsContextKey" key
            // The value is a list, because there might be multiple instances of ThrottlingTrollMiddleware configured
            return this.HttpContext.GetThrottlingTrollConfig();
        }
    }
}