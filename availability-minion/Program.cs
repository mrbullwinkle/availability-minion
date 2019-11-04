using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace availability_minion
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseWindowsService()
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<Worker>();
                    services.AddLogging();
                    services.AddApplicationInsightsTelemetryWorkerService();
                });
    }
}
