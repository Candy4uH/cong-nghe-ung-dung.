using Object_Detection_ASP.NETMVC.Models.Api;
using Object_Detection_ASP.NETMVC.Services;

namespace Object_Detection_ASP.NETMVC
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllersWithViews();
            builder.Services
                .AddOptions<ObjectDetectionApiOptions>()
                .Bind(builder.Configuration.GetSection(ObjectDetectionApiOptions.SectionName))
                .ValidateDataAnnotations()
                .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _), "ObjectDetectionApi:BaseUrl must be a valid absolute URL.")
                .ValidateOnStart();

            builder.Services.AddHttpClient<IObjectDetectionApiClient, ObjectDetectionApiClient>((serviceProvider, client) =>
            {
                var apiOptions = serviceProvider
                    .GetRequiredService<Microsoft.Extensions.Options.IOptions<ObjectDetectionApiOptions>>()
                    .Value;

                client.BaseAddress = new Uri(apiOptions.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(apiOptions.TimeoutSeconds);
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseRouting();

            app.UseAuthorization();

            app.MapStaticAssets();
            app.MapControllers();
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}")
                .WithStaticAssets();

            app.Run();
        }
    }
}
