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
        public string[] configFile;
        public int testFrequency = 300000; //5 minutes in milliseconds (Edit this value to change the frequency of your tests)

        public Worker(ILogger<Worker> logger, TelemetryClient tc)
        {
            _logger = logger;
            telemetryClient = tc;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            string configPath = System.IO.Directory.GetCurrentDirectory();

            if (File.Exists($"{configPath}/config.txt"))
            {
                configFile = File.ReadAllLines($"{configPath}/config.txt");
            }

            else
            {
                configFile = File.ReadAllLines($"C:/Program Files/Minion/config.txt");
            }

            HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; Trident/5.0; MinionBot)");


            List<string> testAddressList = new List<string>();
            foreach (string line in configFile)
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


                        if (!testSchedule.ContainsKey(address))
                        {
                            // Run only once for each address, generate initial random start time
                            // between 0 and testFrequency. Default= 300,000 milliseconds (5 minutes)
                            int resultRandom = rand.Next(0, testFrequency);
                            DateTime randStartTime = currentTime.AddMilliseconds(resultRandom);
                            testSchedule.Add(address, randStartTime);
                        }

                        DateTime checkPrevScheduledTime = testSchedule[address];

                        // Prevent execution of test until scheduled time occurs
                        if (checkPrevScheduledTime <= currentTime) 
                        {
                            _ = TestAvailability(telemetryClient, client, address, _logger);

                            // Next scheduled execution is set to 5 minutes from now
                            DateTime scheduledRunTime = currentTime.AddMilliseconds(testFrequency);
                            testSchedule[address] = scheduledRunTime;                 
                        }
                    }
                }
                await Task.Delay(100, stoppingToken).ConfigureAwait(false);
            }
        }

        private static async Task TestAvailability(TelemetryClient telemetryClient, HttpClient client, String address, ILogger _logger)

        {
            var availability = new AvailabilityTelemetry
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = address,
                RunLocation = System.Environment.MachineName,
                Success = false
            };

            string testRunId = availability.Id;
            availability.Context.Operation.Id = availability.Id;

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            DateTimeOffset startTimeTest = DateTimeOffset.UtcNow;

            try
            {
                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri(address),
                    Method = HttpMethod.Get
                };
                request.Headers.Add("SyntheticTest-RunId", testRunId);
                request.Headers.Add("Request-Id", "|" + testRunId);

                using (var httpResponse = await client.SendAsync(request).ConfigureAwait(false))
                {
                    // add test results to availability telemetry property
                    availability.Properties.Add("HttpResponseStatusCode", Convert.ToInt32(httpResponse.StatusCode).ToString());

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
            catch (Exception ex)
            {
                // track exception when unable to determine the state of web app
                availability.Message = ex.Message;
                var exceptionTelemetry = new ExceptionTelemetry(ex);
                exceptionTelemetry.Context.Operation.Id = availability.Id;
                exceptionTelemetry.Properties.Add("TestAddress", address);
                exceptionTelemetry.Properties.Add("RunLocation", availability.RunLocation);
                telemetryClient.TrackException(exceptionTelemetry);
                _logger.LogError($"[Error]: {ex.Message}");
            }
            finally
            {
                stopwatch.Stop();

                availability.Duration = stopwatch.Elapsed;
                availability.Timestamp = startTimeTest;

                telemetryClient.TrackAvailability(availability);
                _logger.LogInformation($"Availability telemetry for {availability.Name} is sent.");
            }

        }
    }
}
