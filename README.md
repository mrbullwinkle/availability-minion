# availability-minion
Proof of concept side-project using a .NET Core 3.0 Worker Service for Azure Monitor - Application Insights Availability monitoring in hybrid/on prem environments running as a Windows Service.

## Overview

When monitoring an application you always want to have a combination of internal instrumentation coupled with external monitoring/synthetic transactions that give you visibility into the overall health of your app. If you only have internal instrumentation you can't detect when the server or underlying infrastructure running your app is completely down. When there is a complete outage your app won't send any exceptions, traces, or errors, it's just dead. So to counter this you add external monitoring that is in no way dependent on your application or its core infrastructure.

In the Azure Monitor world this type of external syntethtic transaction based monitoring comes in the form of [Application Insights Availabilty tests](https://docs.microsoft.com/azure/azure-monitor/app/monitor-web-app-availability). The default availability tests are built via the portal and allow you to spin up external test agents all over the world to check on the health of you application or endpoints. These tests are incredibly powerful, and if you can I highly recommend using the availability monitoring service built-into Application Insights in the Azure portal. Handling availability monitoring well reliably at scale is a tough problem and Microsoft has solved it taking away all the hard work for you. There are however a few scenarious where availability monitoring via the portal with the tests being initiated in Azure won't work:
  - You want to do some sort of custom availability check, and need to be able to run your own code.
  - You have systems you want to monitor on-premises that you can't allow any incoming traffic from the public internet even if it means only opening up your firewall for specific Microsoft owned IP addresses.
  
If you are one of these situations your primary option is to use the TrackAvailability() method call and send your own custom availability telemetry. Microsoft put together an example of running TrackAvailability() from within an Azure Function in [this article](https://docs.microsoft.com/azure/azure-monitor/app/availability-azure-functions). This provides a good starter example of the basic mechnics of how to make a custom TrackAvailability() call, but it leaves the implementation of any HTTP/test code as an exercise to the reader. 

The purpose of this project is to take that initial example Azure Functions code and build on it to show you how to create fully functional on premises/hybrid monitoring solutions.

## Please note:

These examples have not been extensively tested for production use. Variations of this code are currently being used by some large organizations. But there is no official support for this code, and updates/improvements will be dependent on how much free time I have. In some cases this code might be able to help unblock you, but do so at your own risk, and please test carefully.

## [availability-minion](https://github.com/mrbullwinkle/availability-watcher/tree/master/availability-minion)

I call it a "minion", because I like to think of these type of small programs as little minions out gathering useful monitoring data. In similar spirt to Netflix's "Chaos Monkey" though slightly more helpful and less destructive. So "monitoring minion" or in this case "availability minion. 

Features:
- Adds the ability for the published .exe file to be installed/run as a standard windows service controllable via services.msc.
- Improveed try/catch logic to properly handle certain exceptions like incorrect hostnames so that they are registered within the availability UI blade rather than just be captured as exceptions.
- Uses the new [Application Insights for Worker Services](https://docs.microsoft.com/azure/azure-monitor/app/worker-service) packages. This is the new preferred method of using App Insights + Worker Services. This automatically lights up:
  * Live Metrics
  * Application Map
  * Dependencies Performance/Failures view for all TrackAvailability calls.
  * Heartbeat
  * ikey is set via appsettings.json file
- Distributed tracing support
- Randomized scheduler to distribute the individual test execution so that tests don't all run at the same time, but still run at your desired test interval.This is to help avoid too many tests being executed at the same time. If you were to scale up to running thousands of tests from a single minion you want the tests to be staggered to allow for accurate response time results while maintaining low overhead/performance impact for the system/s running the minion service.
  
## [availability-minion-multi](https://github.com/mrbullwinkle/availability-watcher/tree/master/availability-minion-multi)

This is an alternative to the standard availability minion. The standard minion's config file only allows you to set the sites/addresses you want to test and all the data is sent to a single instrumentaiton key. The multi-minion version takes a config.txt file that expects each line to consist of a desired test address followed by a comma followed by an instrumentation key. So the config.txt file's contents would look like:

```
https://opsconfig.com, a48b65d8-bbbb-cccc-a452-8b5294f633e6
https://microsoft.com, 83117d2a-dddd-eeee-8cd1-dc2a3040715c
https://bing.com, 78247d22-e097-ffff-gggg-f255974b2bcf
https://visualstudio.microsoft.com/, a48b65d8-bbbb-cccc-a452-8b5294f633e6
https://azure.microsoft.com/, 83117d2a-dddd-eeee-8cd1-dc2a3040715c
```

Each test will send data to whatever key you assign on the corresponding line of the file. To make this work I am unable to use the same SDK that the standard availability minion uses so this makes this option a stripped down version. It has all the perf efficiences of the standard minion but does not have the automatic light up features: Live Metrics, App Map, etc.

## [Availability.Minion.Multi.WithPing](https://github.com/mrbullwinkle/availability-minion/tree/master/Availability.Minion.Multi.WithPing)

New experimental minion that supports swapping between HttpClient based tests and Ping based tests.

- Adds .NET Core 3.1 support
- Uses `System.Net.Ping` for Ping tests
- Updated to use latest stable release of the Application Insights Base API 2.14.0

I still need to put detailed instructions together for this one, but they are fairly similar to the standard multi-minion instructions other than that the config file requires additional options, and you need .NET Core 3.1 whereas the other projects are still on 3.0 though they should upgrade without requiring any code changes.

Config.txt format:

`<target-address>,<ikey>,<https or ping>`

```
https://opsconfig.com, a48b65d8-bbbb-cccc-a452-8b5294f633e6, https
https://microsoft.com, 83117d2a-dddd-eeee-8cd1-dc2a3040715c, https
https://bing.com, 78247d22-e097-ffff-gggg-f255974b2bcf, https
https://visualstudio.microsoft.com/, a48b65d8-bbbb-cccc-a452-8b5294f633e6, https
https://azure.microsoft.com/, 83117d2a-dddd-eeee-8cd1-dc2a3040715c, https
https://docs.microsoft.com, 78247d22-e097-ffff-gggg-f255974b2bcf, https
https://xbox.com/en-US, a48b65d8-bbbb-cccc-a452-8b5294f633e6, https
https://www.microsoft.com/en-us/surface, 83117d2a-f8b9-483a-8cd1-dc2a3040715c, https
https://azure.microsoft.com/en-us/, 78247d22-e097-ffff-gggg-f255974b2bcf, https
https://www.office.com/, a48b65d8-bbbb-cccc-a452-8b5294f633e6, https
https://outlook.live.com/owa/, 83117d2a-dddd-eeee-8cd1-dc2a3040715c, https
https://mixer.com, 78247d22-e097-ffff-gggg-f255974b2bcf, https
https://onedrive.live.com/about/en-us/, a48b65d8-bbbb-cccc-a452-8b5294f633e6, https
www.microsoft.com, 83117d2a-dddd-eeee-8cd1-dc2a3040715c, ping
www.bing.com, a48b65d8-bbbb-cccc-a452-8b5294f633e6, ping
products.office.com, 78247d22-e097-ffff-gggg-f255974b2bcf, ping
www.linkedin.com, 78247d22-e097-ffff-gggg-f255974b2bcf, ping
github.com, a48b65d8-bbbb-cccc-a452-8b5294f633e6, ping
partner.microsoft.com, 78247d22-e097-ffff-gggg-f255974b2bcf, ping
dotnet.microsoft.com, a48b65d8-bbbb-cccc-a452-8b5294f633e6, ping
www.minecraft.net, 78247d22-e097-ffff-gggg-f255974b2bcf, ping
192.168.1.150, 78247d22-e097-ffff-gggg-f255974b2bcf, ping
```

## Getting up and running

* [availability-minion instructions](availability-minion/instructions.md) 
* [Availability-watcher-simplefile instructions](Availability-Watcher-simplefile/instructions.md) (This was an early version, it has less features and is here just for reference.)

## Video

If you want a step-by-step how to get started guide, I recorded a video of the basic process when I was working on the initial availability-watcher code: (The new availability-minion has a lot more functionality than the simple example I cover in the video, but if you want to get a sense of the basic mechanics particularly in how to turn a .NET Core 3.0 worker service into a windows service this will provide you the details you need)

[Video of creating .NET Core 3.0 Worker Service for Hybrid/on-prem availability testing](https://www.youtube.com/watch?v=nAt1NbDLalQ&feature=youtu.be)
