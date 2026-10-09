using Shared.Messaging;

namespace Notifications.Worker
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);

            builder.Services.AddServiceBus(builder.Configuration);
            builder.Services.AddHostedService<OrderEventsConsumer>();

            var host = builder.Build();
            host.Run();
        }
    }
}
