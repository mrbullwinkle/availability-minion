using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights;
using System.Diagnostics;
using System.Net.Http;
using Microsoft.ApplicationInsights.DataContracts;
using System.IO;
using System.Collections.Generic;


namespace Availability_Watcher
{

    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            TelemetryConfiguration configuration = TelemetryConfiguration.CreateDefault();
            configuration.InstrumentationKey = "afc62271-f6c9-49cf-af07-f573727e0b3f";
            var telemetryClient = new TelemetryClient(configuration);

            HttpClient HttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

            string configPath = System.IO.Directory.GetCurrentDirectory();
            string[] lines = File.ReadAllLines($"{configPath}/config.txt");

            List<string> uri = new List<string>();
            foreach (string line in lines)
            {
                uri.Add(line);
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);


                if (telemetryClient != null)
                {
                    foreach (string address in uri)
                    {
                        await TestAvailability(telemetryClient, HttpClient, address, _logger);
                    }
                }

                await Task.Delay(10000, stoppingToken);
            }
        }

        private static async Task TestAvailability(TelemetryClient telemetryClient, HttpClient HttpClient, String address, ILogger _logger)
        {
            var availability = new AvailabilityTelemetry
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"Availability Test: {address}",
                RunLocation = System.Environment.MachineName,
                Success = false
            };

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            bool isMonitoringFailure = false;

            try
            {
                using (var httpResponse = await HttpClient.GetAsync(address))
                {
                    // add test results to availability telemetry property
                    availability.Properties.Add("HttpResponseStatusCode", Convert.ToInt32(httpResponse.StatusCode).ToString());

                    // check if response content contains specific text
                    string content = httpResponse.Content != null ? await httpResponse.Content.ReadAsStringAsync() : "";
                    if (httpResponse.IsSuccessStatusCode)
                    {
                        availability.Success = true;
                        availability.Message = $"Test succeeded with response: {httpResponse.StatusCode}";
                        _logger.LogTrace($"[Verbose]: {availability.Message}");
                    }
                    else if (!httpResponse.IsSuccessStatusCode)
                    {
                        availability.Message = $"Test failed with response: {httpResponse.StatusCode}";
                        _logger.LogWarning($"[Warning]: {availability.Message}");
                    }
                }
            }
            catch (TaskCanceledException e)
            {
                availability.Message = $"Test timed out: {e.Message}";
                _logger.LogDebug($"[Warning]: {availability.Message}");
            }
            catch (Exception ex)
            {
                // track exception when unable to determine the state of web app
                isMonitoringFailure = true;
                var exceptionTelemetry = new ExceptionTelemetry(ex);
                exceptionTelemetry.Context.Operation.Id = "test";
                exceptionTelemetry.Properties.Add("TestName", "test");
                exceptionTelemetry.Properties.Add("TestLocation", "test");
                exceptionTelemetry.Properties.Add("TestUri", "test");
                telemetryClient.TrackException(exceptionTelemetry);
                _logger.LogError($"[Error]: {ex.Message}");

                // optional - throw to fail the function
                throw;
            }
            finally
            {
                stopwatch.Stop();
                availability.Duration = stopwatch.Elapsed;
                availability.Timestamp = DateTimeOffset.UtcNow;

                // do not make an assumption as to the state of the web app when monitoring failures occur
                if (!isMonitoringFailure)
                {
                    telemetryClient.TrackAvailability(availability);
                    _logger.LogInformation($"Availability telemetry for {availability.Name} is sent.");
                }

                // call flush to ensure telemetries are sent
                telemetryClient.Flush();

            }
        }
    }
}
