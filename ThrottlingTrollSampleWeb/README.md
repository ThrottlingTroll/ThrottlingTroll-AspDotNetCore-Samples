# ThrottlingTrollSampleWeb

This ASP.NET Core project demonstrates all the features of [ThrottlingTroll](https://www.nuget.org/packages/ThrottlingTroll).

## How to run locally

As a prerequisite, you will need minimum [.NET 6.0](https://dotnet.microsoft.com/en-us/download/dotnet/6.0) installed.

1. (Optional, if you want to use [RedisCounterStore](https://github.com/ThrottlingTroll/ThrottlingTroll/tree/main/ThrottlingTroll.CounterStores.Redis)) Add `RedisConnectionString` setting to [appsettings.json](https://github.com/ThrottlingTroll/ThrottlingTroll-AspDotNetCore-Samples/blob/main/ThrottlingTrollSampleWeb/appsettings.json) file. For a local Redis server that connection string usually looks like `localhost:6379`. 

2. Type `dotnet run` in a terminal window.

3. Navigate to `https://localhost:7085/swagger`.
