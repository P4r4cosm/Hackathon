using Minio;
using Minio.DataModel.Args;

namespace SoundService.Services;

public class MinIOService
{
    private readonly IMinioClient _minioClient;
    private readonly string _bucketName;

    public MinIOService(IMinioClient minioClient, string bucketName)
    {
        _minioClient = minioClient;
        _bucketName = bucketName;
    }

    public async Task UploadTrackAsync(string filePath, string objectName, string contentType,
        CancellationToken ct = default)
    {
        // Проверяем существование бакета
        bool found = await _minioClient.BucketExistsAsync(new BucketExistsArgs().WithBucket(_bucketName), ct);
        if (!found)
        {
            await _minioClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(_bucketName), ct);
        }

        // Открываем поток к файлу явно и используем 'using'
        using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var putObjectArgs = new PutObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(objectName)
                .WithStreamData(fileStream) // <--- Используем поток
                .WithObjectSize(fileStream.Length) // <--- Указываем размер
                .WithContentType(contentType); // <--- Указываем тип контента (важно!)
            await _minioClient.PutObjectAsync(putObjectArgs, ct).ConfigureAwait(false);
        }
    }

    public async Task<(Stream, string, long)> GetTrackAsync(string objectName, CancellationToken ct = default)
    {
        try
        {
            var statArgs = new StatObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(objectName);
            var objectStat = await _minioClient.StatObjectAsync(statArgs, ct).ConfigureAwait(false);

            if (objectStat == null || objectStat.Size == 0)
            {
                throw new FileNotFoundException($"Object '{objectName}' not found in bucket '{_bucketName}'.");
            }

            Stream stream = new MemoryStream();
            var getArgs = new GetObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(objectName)
                .WithCallbackStream(async (s, token) =>
                {
                    await s.CopyToAsync(stream, 81920, token); // 81920 - размер буфера по умолчанию
                });

            await _minioClient.GetObjectAsync(getArgs, ct).ConfigureAwait(false);
            stream.Seek(0, SeekOrigin.Begin);

            return (stream, objectStat.ContentType, objectStat.Size);
        }
        catch (Minio.Exceptions.ObjectNotFoundException)
        {
            throw new FileNotFoundException($"Object '{objectName}' not found in bucket '{_bucketName}'.");
        }
    }
}