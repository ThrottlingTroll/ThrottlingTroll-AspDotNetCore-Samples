# ThrottlingTroll-AspDotNetCore-Samples

Sample code demonstrating how to use [ThrottlingTroll](https://github.com/ThrottlingTroll/ThrottlingTroll) with ASP.NET Core.

## How to run locally

As a prerequisite, you will need minimum [.NET 6.0](https://dotnet.microsoft.com/en-us/download/dotnet/6.0) installed.

Then either open [ThrottlingTroll-AspDotNetCore-Samples.sln](https://github.com/ThrottlingTroll/ThrottlingTroll-AspDotNetCore-Samples/blob/main/ThrottlingTroll-AspDotNetCore-Samples.sln) in your Visual Studio, or go to `ThrottlingTrollSampleWeb` folder and type `dotnet run` in your terminal window.

By default the default **MemoryCacheCounterStore** is used. If you want to use [RedisCounterStore](https://github.com/ThrottlingTroll/ThrottlingTroll/tree/main/ThrottlingTroll.CounterStores.Redis), add `RedisConnectionString` setting to [appsettings.json](https://github.com/ThrottlingTroll/ThrottlingTroll-AspDotNetCore-Samples/blob/main/ThrottlingTrollSampleWeb/appsettings.json) file. For a local Redis server that connection string usually looks like `localhost:6379`. 
