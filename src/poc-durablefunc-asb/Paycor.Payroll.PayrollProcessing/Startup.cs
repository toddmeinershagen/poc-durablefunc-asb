using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using System;

[assembly: FunctionsStartup(typeof(Paycor.Payroll.PayrollProcessing.Startup))]
namespace Paycor.Payroll.PayrollProcessing
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            ConfigureServices(builder.Services);
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<ISettings, Settings>();
        }

        public bool IsDevelopmentEnvironment()
        {
            return "Development".Equals(Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT"), StringComparison.OrdinalIgnoreCase);
        }
    }

    public interface ISettings
    {
        string ServiceBusConnectionString { get; }
    }

    public class Settings : ISettings
    {
        public string ServiceBusConnectionString => Environment.GetEnvironmentVariable("ServiceBusConnectionString");
    }
}
