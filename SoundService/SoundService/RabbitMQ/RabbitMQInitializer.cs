using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using System.Threading;
using System.Threading.Tasks;


namespace SoundService.RabbitMQ;

// RabbitMqConstants.cs
public static class RabbitMqConstants
{
    public const string DemucsQueueName = "demucs_tasks_queue";
    public const string DemucsTasksRoutingKey = "demucs.task";
    public const string AudioProcessingExchange = "audio_processing_exchange";

    public const string ResultsExchangeName = "results_exchange"; // Для результатов
    public const string TaskResultsQueueName = "task_results_queue"; // Для результатов
    public const string TaskResultsRoutingKey = "task.result"; // Для результатов
    
}


public class RabbitMQInitializer : IHostedService
{
    private readonly IConnectionFactory _connectionFactory;
    private IConnection _connection;
    private IChannel _channel;
    private readonly ILogger<RabbitMQInitializer> _logger;

    public RabbitMQInitializer(IConfiguration configuration, ILogger<RabbitMQInitializer> logger)
    {
        _logger = logger;
        _connectionFactory = new ConnectionFactory()
        {
            HostName = configuration["RABBITMQ_HOST"],
            UserName = configuration["RABBITMQ_USER"],
            Password = configuration["RABBITMQ_PASS"],
            Port = configuration.GetValue<int>("RABBITMQ_PORT"),
        };
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("RabbitMQ Initializer: Declaring RabbitMQ infrastructure...");
        try
        {
            // TODO: Добавить политику retry (например, с Polly) для создания соединения
            _connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

            // Exchange для задач
            await _channel.ExchangeDeclareAsync(
                exchange: RabbitMqConstants.AudioProcessingExchange,
                type: ExchangeType.Direct,
                durable: true);

            // Очередь для Demucs задач
            await _channel.QueueDeclareAsync(
                queue: RabbitMqConstants.DemucsQueueName,
                durable: true, exclusive: false, autoDelete: false, arguments: null);

            await _channel.QueueBindAsync(
                queue: RabbitMqConstants.DemucsQueueName,
                exchange: RabbitMqConstants.AudioProcessingExchange,
                routingKey: RabbitMqConstants.DemucsTasksRoutingKey);

            // Exchange для результатов
            await _channel.ExchangeDeclareAsync(
                exchange: RabbitMqConstants.ResultsExchangeName,
                type: ExchangeType.Direct, // или Topic, если нужна более сложная маршрутизация результатов
                durable: true);

            // Очередь для результатов
            await _channel.QueueDeclareAsync(
                queue: RabbitMqConstants.TaskResultsQueueName,
                durable: true, exclusive: false, autoDelete: false, arguments: null);

            await _channel.QueueBindAsync(
                queue: RabbitMqConstants.TaskResultsQueueName,
                exchange: RabbitMqConstants.ResultsExchangeName,
                routingKey: RabbitMqConstants.TaskResultsRoutingKey); // или "#" если ResultsExchange типа Topic

            _logger.LogInformation("RabbitMQ infrastructure declared successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to declare RabbitMQ infrastructure.");
            // В зависимости от критичности, можно остановить приложение или реализовать логику ожидания
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("RabbitMQ Initializer stopping.");
        _channel?.Dispose();
        _connection?.Dispose();
        return Task.CompletedTask;
    }
}