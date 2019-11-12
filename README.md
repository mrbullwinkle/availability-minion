# availability-watcher + availability-minion
Proof of concept side-project using a .NET Core 3.0 Worker Service for Azure Monitor - Application Insights Availability monitoring in hybrid/on prem environments running as a Windows Service.

## Overview

When monitoring an application you always want to have a combination of internal instrumentation coupled with external monitoring/synthetic transactions that give you visibility into the overall health of your app. If you only have internal instrumentation you can't detect when the server or underlying infrastructure running your app is completely down, because it isn't sending any exceptions, traces, or errors, it's just dead. So to counter this you add external monitoring that is in no way dependent on your application or its core infrastructure which can provide an outside in view.

In the Azure Monitor world this type of external syntethtic transaction based monitoring comes in the form of [Application Insights Availabilty tests](https://docs.microsoft.com/azure/azure-monitor/app/monitor-web-app-availability). The default availability tests are built via the portal and allow you to spin up external test agents all over the world to check on the health of you application or endpoints. These tests are incredibly powerful, and if you can I highly recommend using the availability monitoring service built-into Application Insights in the Azure portal. Handling availability monitoring well reliably at scale is a tough problem and Microsoft has solved it taking away all the hard work for you. There are however a few scenarious where availability monitoring via the portal with the tests being initiated in Azure won't work:
  - You want to do some sort of custom availability check, and need to be able to run your own code.
  - You have systems your want to monitor on premises that you can't allow any incoming traffic from the public internet even if it means only opening up your firewall for specific Microsoft owned IP addresses.
  
If you are one of these situations your primary option is to use the TrackAvailability() method call and send your own custom availability telemetry. Microsoft put together an example of running TrackAvailability() from within an Azure Function in [this article](https://docs.microsoft.com/azure/azure-monitor/app/availability-azure-functions). This provides a good starter example of the basic mechnics of how to make a custom TrackAvailability() call, but it leaves the implementation of any HTTP/test code as an exercise to the reader. The first version of the Microsoft doc did show some basic HTTP test code in conjunction with the trackavailability code for testing a single page, but this will soon (or by the time you read this) will have been replaced with a more generic example that removes the HTTP code.

The purpose of this project is to take that initial example Azure Functions code and modify it so it can be used to create an on premises based availability monitoring which can run from anywhere as a windows service. I am working on this and iterating as I have time and so far have created two different versions of this solution.

## Please note:

These are not intended/tested for production use, and are just samples to help give you ideas/get started. There is no official support for this code, and updates/improvements will be dependent on how much free time I have. I think a production ready solution could be built by expanding on these examples, but it would require additional work/testing. 


## [Availability-Watcher-simplefile](https://github.com/mrbullwinkle/availability-watcher/tree/master/Availability-Watcher-simplefile)

- Removes the Azure Function specific code, and makes it so it can run standalone in a .NET Core 3.0 Worker Service.
- Removes the content match (Can be added back later as a modifiable parameter)
- Reads the ikey from a simple .txt based config file.
- Reads a separate .txt based config file and generates a list of URLs/Endpoints to test based on the contents of the file. (The old Microsoft Azure Function example took a single hardcoded url)
- Adds the ability for the published .exe file to be installed/run as a standard windows service controllable via services.msc.
- Improves the try/catch logic to properly handle certain exceptions like incorrect hostnames so that they are registered within the availability UI blade rather than just be captured as exceptions.
- runs tests one at a time and waits before each test is finished before executing the next test. (Good for small scale learning about availability tests, really bad for at scale monitoring when you want to run large number of tests at a regular interval. 

## [availability-minion](https://github.com/mrbullwinkle/availability-watcher/tree/master/availability-minion)

This is the latest version. New name, because I like to think of these type of small programs as little minions out gathering useful monitoring data. So "monitoring minion" or in this case "availability minion. It does most of what the first version did, and a whole bunch more. 

Here's what's the same:
- Removes the Azure Function specific code, and makes it so it can run standalone in a .NET Core 3.0 Worker Service
- Removes the content match (Can be added back later as a modifiable parameter)
- Reads a separate .txt based config file and generates a list of URLs/Endpoints to test based on the contents of the file. (The old Microsoft Azure Function example took a single hardcoded url)
- Adds the ability for the published .exe file to be installed/run as a standard windows service controllable via services.msc.
- Improves the try/catch logic to properly handle certain exceptions like incorrect hostnames so that they are registered within the availability UI blade rather than just be captured as exceptions.

Here's whats new:
- Uses the new [Application Insights for Worker Services](https://docs.microsoft.com/azure/azure-monitor/app/worker-service) packages. This is the new preferred method of using App Insights + Worker Services. This automatically lights up:
  * Live Metrics
  * Application Map
  * Dependencies Performance/Failures view for all TrackAvailability calls.
  * Heartbeat
  * ikey is now set via appsettings.json file

  I also added a:
  - Pseduo random number based functionality to distribute the individual test execution schedule across a range from 1 to 60000 milliseconds with tests being run for each url/endpoint approximately every 60 seconds. This is to help avoid too many tests being executed at the same time. This should allow for more accurate test response time results, as well as greatly increase the number of tests that a single availability-minion can run.
  
## [availability-minion-multi](https://github.com/mrbullwinkle/availability-watcher/tree/master/availability-minion-multi)

This is an alternative to the standard availability minion. The standard minion's config file only allows you to set the sites/addresses you want to test and all the data is sent to a single ikey. The multi-minion version takes a config.txt file that expects each line to consist of a desired test address followed by a comma followed by an instrumentation key. So the config.txt file's contents would look like:

```
https://opsconfig.com, a48b65d8-bbbb-cccc-a452-8b5294f633e6
https://microsoft.com, 83117d2a-dddd-eeee-8cd1-dc2a3040715c
https://bing.com, 78247d22-e097-ffff-gggg-f255974b2bcf
https://visualstudio.microsoft.com/, a48b65d8-bbbb-cccc-a452-8b5294f633e6
https://azure.microsoft.com/, 83117d2a-dddd-eeee-8cd1-dc2a3040715c
```

Each test will send data to whatever key you assign on the corresponding line of the file. To make this work I am unable to use the same SDK that the standard availability minion uses so this makes this option a stripped down version. It has all the perf efficiences of the standard minion but does not have the automatic light up features: Live Metrics, App Map, etc.

## Getting up and running

* [availability-minion instructions](availability-minion/instructions.md) (This is the latest version)
* [Availability-watcher-simplefile instructions](Availability-Watcher-simplefile/instructions.md) (This was the first version, it has less features and is here just for reference.)

## Video

If you want a step-by-step how to get started guide, I recorded a video of the basic process when I was working on the initial availability-watcher code: (The new availability-minion has a lot more functionality than the simple example I cover in the video, but if you want to get a sense of the basic mechanics particularly in how to turn a .NET Core 3.0 worker service into a windows service this will provide you the details you need)

[Video of creating .NET Core 3.0 Worker Service for Hybrid/on-prem availability testing](https://www.youtube.com/watch?v=nAt1NbDLalQ&feature=youtu.be)
