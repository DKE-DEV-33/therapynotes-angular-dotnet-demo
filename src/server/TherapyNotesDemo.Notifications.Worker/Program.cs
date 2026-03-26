using TherapyNotesDemo.Notifications.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<SchedulingApiOptions>(builder.Configuration.GetSection("SchedulingApi"));

builder.Services.AddSingleton(new HttpClient());
builder.Services.AddSingleton<SchedulingOutboxClient>();
builder.Services.AddSingleton<IdempotencyStore>();
builder.Services.AddSingleton<NotificationLog>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
