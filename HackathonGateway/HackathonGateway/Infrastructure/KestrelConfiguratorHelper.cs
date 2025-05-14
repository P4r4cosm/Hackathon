

public class KestrelConfiguratorHelper
{
    public static void ConfigureKestrel(WebApplicationBuilder builder)
    {
        var logger = LoggerFactory.Create(logBuilder => logBuilder.AddConsole()).CreateLogger<Program>();
    
        var certificatePath = Environment.GetEnvironmentVariable("KESTREL_CERTIFICATE_PATH");
        var certificatePassword = Environment.GetEnvironmentVariable("KESTREL_CERTIFICATE_PASSWORD");
        var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS")?.Split(';') ?? Array.Empty<string>();

        if (urls.Any(url => url.StartsWith("https://")) && 
            !string.IsNullOrEmpty(certificatePath) && 
            !string.IsNullOrEmpty(certificatePassword))
        {
            builder.WebHost.ConfigureKestrel(serverOptions =>
            {
                foreach (var url in urls)
                {
                    var uri = new Uri(url.Replace("+", "0.0.0.0"));
                    if (uri.Scheme == "https")
                    {
                        serverOptions.ListenAnyIP(uri.Port, options =>
                        {
                            options.UseHttps(certificatePath, certificatePassword);
                        });
                    }
                    else if (uri.Scheme == "http")
                    {
                        serverOptions.ListenAnyIP(uri.Port);
                    }
                }
            });
        }
        else
        {
            logger.LogWarning("HTTPS не настроен: отсутствуют переменные окружения или сертификат");
        }
    }
}