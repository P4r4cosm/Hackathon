namespace SoundService.Models;

public class WhisperTaskData
{
    public string TaskId { get; set; }
    public string input_bucket_name { get; set; }
    public string input_object_name { get; set; }
    public string output_minio_folder { get; set; }
}