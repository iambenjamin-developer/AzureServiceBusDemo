using Shared.Messaging;

namespace Inventory.Worker
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);

            builder.Services.AddServiceBus(builder.Configuration);
            builder.Services.AddSingleton<InMemoryStock>();
            builder.Services.AddHostedService<OrderEventsConsumer>();

            var host = builder.Build();
            host.Run();
        }
    }
}
