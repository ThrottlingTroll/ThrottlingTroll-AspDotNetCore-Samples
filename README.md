# ThrottlingTroll-AspDotNetCore-Samples

Sample code demonstrating how to use [ThrottlingTroll](https://github.com/ThrottlingTroll/ThrottlingTroll) with ASP.NET Core.

## How to run locally

As a prerequisite, you will need minimum [.NET 6.0](https://dotnet.microsoft.com/en-us/download/dotnet/6.0) installed.

1. Either open [ThrottlingTroll-AspDotNetCore-Samples.sln](https://github.com/ThrottlingTroll/ThrottlingTroll-AspDotNetCore-Samples/blob/main/ThrottlingTroll-AspDotNetCore-Samples.sln) in your Visual Studio and press F5, or go to `ThrottlingTrollSampleWeb` folder and type `dotnet run` in your terminal window.
2. Open `http://localhost:5085/swagger` in your browser and try making calls.

By default the default **MemoryCacheCounterStore** is used. If you want to use [RedisCounterStore](https://github.com/ThrottlingTroll/ThrottlingTroll/tree/main/ThrottlingTroll.CounterStores.Redis), add `RedisConnectionString` setting to [appsettings.json](https://github.com/ThrottlingTroll/ThrottlingTroll-AspDotNetCore-Samples/blob/main/ThrottlingTrollSampleWeb/appsettings.json) file. For a local Redis server that connection string usually looks like `localhost:6379`. 

## How to see OpenTelemetry

The project is already instrumented with [OpenTelemetry](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/observability-with-otel) and [Zipkin exporter](https://zipkin.io/pages/quickstart.html), so all you have to do is:

1. [Run Zipkin locally](https://zipkin.io/pages/quickstart.html), e.g.:

    ```
    curl -sSL https://zipkin.io/quickstart.sh | bash -s
    java -jar zipkin.jar
    ```
2. Open Zipkin UI (http://localhost:9411/zipkin).
3. Observe traces, e.g.:

    ![image](https://github.com/user-attachments/assets/b480764e-5114-4ca3-9c78-133575926a26)
    
