namespace SoundService.Models.Settings;

public class MinioSettings
{
    public string Endpoint { get; set; }
    public int Port { get; set; }
    public string AccessKey { get; set; }
    public string SecretKey { get; set; }
    public string BucketName { get; set; }
}