using Object_Detection.Api.Infrastructure.ErrorHandling;
using Object_Detection.Api.Options;
using Object_Detection.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<ObjectDetectionApiOptions>(
    builder.Configuration.GetSection(ObjectDetectionApiOptions.SectionName));
builder.Services.AddSingleton<IObjectDetectionService, ObjectDetectionService>();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseSwagger();
app.UseSwaggerUI();

// app.UseHttpsRedirection();

app.MapControllers();

app.Run();
