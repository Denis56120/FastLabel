using FastLabel.Service;

var builder = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options =>
    {
        options.ServiceName = "FastLabel";
    })
    .ConfigureServices((hostContext, services) =>
    {
        services.AddHostedService<ServiceHostWorker>();
    });

var host = builder.Build();
host.Run();