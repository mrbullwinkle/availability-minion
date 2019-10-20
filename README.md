# availability-watcher
Proof of concept side-project using .NET Core 3.0 Worker Service for Azure Monitor - Application Insights Availability monitoring in hybrid/on prem environments running as Windows Service.

## Pre-requisites:

- To work with the Visual Studio project files you need at least Visual Studio 2019 16.3.5
- To run Availability-Watcher.exe standalone or as a windows service you need at least Microsoft.NETCore.App' [version 3.0.0 Runtime](https://dotnet.microsoft.com/download/dotnet-core/3.0). 
- An instrumentation key + a test Application Insights resource to send avaiability telemetry to. If you don't have an Application Insights resource you an create one by following [these instructions](https://docs.microsoft.com/azure/azure-monitor/app/create-new-resource). 

