using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using System.Net.Http;
using System.IO;
using System.Diagnostics;


namespace availability_minion
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        public TelemetryClient telemetryClient;

        public Worker(ILogger<Worker> logger, TelemetryClient tc)
        {
            _logger = logger;
            telemetryClient = tc;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            string configPath = System.IO.Directory.GetCurrentDirectory();
            string[] endpointAddresses = File.ReadAllLines($"{configPath}/config.txt");

            HttpClient HttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

            //Add User-Agent info with word "bot" so analytics programs can understand that these are synthethic transactions and filter them out as needed
            HttpClient.DefaultRequestHeaders.Add("User-Agent", "App Insights (availablity-minion) bot");

            List<string> testAddressList = new List<string>();
            foreach (string line in endpointAddresses)
            {
                testAddressList.Add(line);
            }

            var testSchedule = new Dictionary <string, DateTime>();
            Random rand = new Random();

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                //for testing only if someone is running direct from .exe in cmd prompt
               Console.WriteLine("availability minion running at: {0}", DateTimeOffset.Now);

                if (telemetryClient != null)
                {
                    foreach (string address in testAddressList)
                    {
                        DateTime currentTime = DateTime.Now;
                        DateTime scheduledRunTime = currentTime.AddMilliseconds(60000);
                        int resultRandom = rand.Next(0, 60000);
                        DateTime randStartTime = currentTime.AddMilliseconds(resultRandom);

                        if (!testSchedule.ContainsKey(address))
                        {
                            testSchedule.Add(address, randStartTime);
                        }

                        DateTime checkPrevScheduledTime = testSchedule[address];

                        if (checkPrevScheduledTime <= currentTime) 
                        {
                            _ = TestAvailability(telemetryClient, HttpClient, address, _logger);
                            testSchedule[address] = scheduledRunTime;                 
                        }
                    }
                }
                await Task.Delay(1000, stoppingToken);
            }
        }

        private static async Task TestAvailability(TelemetryClient telemetryClient, HttpClient HttpClient, String address, ILogger _logger)

        {
            var availability = new AvailabilityTelemetry
            {
                Id = Guid.NewGuid().ToString(),
                Name = address,
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

                    //if HttpStatusCode is in the successful range 200-299
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
            catch (System.Net.Sockets.SocketException se)
            {
                availability.Message = $"Test failed with socket exception, response: {se.Message}";
                _logger.LogWarning($"[Warning]: {availability.Message}");
            }

            catch (TaskCanceledException e)
            {
                availability.Message = $"Test failed due to monitoring interruption: {e.Message}";
                _logger.LogWarning($"[Warning]: {availability.Message}");
            }
            catch (System.Net.Http.HttpRequestException hre)
            {
                availability.Message = $"Test failed with an HTTP request exception, response: {hre.Message}";
                _logger.LogWarning($"[Warning]: {availability.Message}");
            }
            catch (Exception ex)
            {
                // track exception when unable to determine the state of web app
                isMonitoringFailure = true;
                var exceptionTelemetry = new ExceptionTelemetry(ex);
                //  exceptionTelemetry.Context.Operation.Id = "test";
                exceptionTelemetry.Properties.Add("Message", ex.Message);
                exceptionTelemetry.Properties.Add("Source", ex.Source);
                exceptionTelemetry.Properties.Add("Test site", address);
                //exceptionTelemetry.Properties.Add("StackTrace", ex.StackTrace);
                telemetryClient.TrackException(exceptionTelemetry);
                _logger.LogError($"[Error]: {ex.Message}");
            }
            finally
            {
                stopwatch.Stop();
                availability.Duration = stopwatch.Elapsed;
                availability.Timestamp = DateTimeOffset.UtcNow;

                if (!isMonitoringFailure)
                {
                    telemetryClient.TrackAvailability(availability);
                    _logger.LogInformation($"Availability telemetry for {availability.Name} is sent.");
                    Console.WriteLine($"Availability telemetry for {availability.Name} is sent.");
                }

                // call flush to ensure all telemetry is sent
                telemetryClient.Flush();            
            }
        }
    }
}
