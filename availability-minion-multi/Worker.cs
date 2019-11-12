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


namespace availability_minion_multi
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        public string[] configFile;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            string configPath = System.IO.Directory.GetCurrentDirectory();
            List<string>testAddressList = new List<string>();
            List<string> ikeys = new List<string>();
         

            if (File.Exists($"{configPath}/config.txt"))
            {
                configFile = File.ReadAllLines($"{configPath}/config.txt");
            }

            else
            {
                configFile = File.ReadAllLines($"C:/Program Files/Minion/config.txt");
            }
            
            foreach (string line in configFile)
            {
                var textProcessing = line.Replace(" ", String.Empty);
                var items = textProcessing.Split(',');

                testAddressList.Add(items[0]);
                ikeys.Add(items[1]);
            }

            TelemetryConfiguration configuration = TelemetryConfiguration.CreateDefault();
            var telemetryClient = new TelemetryClient(configuration);

            HttpClient HttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            HttpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (compatible; minionbot/1.0)");

            var testSchedule = new Dictionary<string, DateTime>();
            Random rand = new Random();


            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);


                if (telemetryClient != null)
                {
                    for (int i =0; i < testAddressList.Count; i++)
                    {
                        {
                            DateTime currentTime = DateTime.Now;
                            DateTime scheduledRunTime = currentTime.AddMilliseconds(60000);
                            int resultRandom = rand.Next(0, 60000);
                            DateTime randStartTime = currentTime.AddMilliseconds(resultRandom);

                            if (!testSchedule.ContainsKey(testAddressList[i]))
                            {
                                testSchedule.Add(testAddressList[i], randStartTime);
                            }

                            DateTime checkPrevScheduledTime = testSchedule[testAddressList[i]];

                            if (checkPrevScheduledTime <= currentTime)
                            {
                                _ = TestAvailability(telemetryClient, HttpClient, testAddressList[i], ikeys[i], _logger);
                                testSchedule[testAddressList[i]] = scheduledRunTime;
                            }
                        }
                    }
                }

                await Task.Delay(1000, stoppingToken);
            }
        }

        private static async Task TestAvailability(TelemetryClient telemetryClient, HttpClient HttpClient, String address, string ikey, ILogger _logger)
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

            availability.Context.InstrumentationKey = ikey;
            availability.Context.Cloud.RoleName = "minion";
            

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
                exceptionTelemetry.Context.InstrumentationKey = ikey;
                exceptionTelemetry.Context.Cloud.RoleName = "minion";
                exceptionTelemetry.Context.Operation.Id = "test";
                exceptionTelemetry.Properties.Add("TestName", "test");
                exceptionTelemetry.Properties.Add("TestLocation", "test");
                exceptionTelemetry.Properties.Add("TestUri", "test");
                telemetryClient.TrackException(exceptionTelemetry);
                _logger.LogError($"[Error]: {ex.Message}");

                // optional - throw to fail the function
                //throw;
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
                }

                // call flush to ensure telemetry is sent
                telemetryClient.Flush();

            }
        }

    }
}
