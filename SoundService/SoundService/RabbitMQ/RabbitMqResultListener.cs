using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SoundService.Abstractions;
using SoundService.Models;

namespace SoundService.RabbitMQ;

public class RabbitMqResultListener: BackgroundService
{
    private readonly ILogger<RabbitMqResultListener> _logger;
    private readonly IConnectionFactory _connectionFactory;
    private readonly RabbitMqConf _conf;
    private readonly IServiceScopeFactory _scopeFactory;
    private IConnection _connection;
    private IChannel _channel;

    public RabbitMqResultListener(ILogger<RabbitMqResultListener> logger, IConfiguration configuration, RabbitMqConf conf, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _conf = conf;
        _scopeFactory = scopeFactory;
        _connectionFactory = new ConnectionFactory()
        {
            HostName = configuration["RABBITMQ_HOST"],
            UserName = configuration["RABBITMQ_USER"],
            Password = configuration["RABBITMQ_PASS"],
            Port = configuration.GetValue<int>("RABBITMQ_PORT"),
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
        };
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stoppingToken.Register(() => _logger.LogInformation("RabbitMQ Result Listener is stopping."));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("RabbitMQ Result Listener: Connecting...");
                _connection = await _connectionFactory.CreateConnectionAsync(stoppingToken);
                _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);
                
                _logger.LogInformation("RabbitMQ Result Listener: Connection established. Setting up consumers.");

                await SetupConsumer(_conf.DemucsResultQueue, _conf.DemucsResultRoutingKey, stoppingToken);
                await SetupConsumer(_conf.WhisperResultQueue, _conf.WhisperResultRoutingKey, stoppingToken);

                // Держим ExecuteAsync живым, пока не придет сигнал отмены
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Это нормально, происходит при остановке сервиса.
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RabbitMQ Result Listener encountered an error. Reconnecting in 5 seconds...");
                await Task.Delay(5000, stoppingToken);
            }
            finally
            {
                _channel?.Dispose();
                _connection?.Dispose();
            }
        }
        
        _logger.LogInformation("RabbitMQ Result Listener has stopped.");
    }
    
    private async Task SetupConsumer(string queueName, string expectedRoutingKey, CancellationToken stoppingToken)
    {
        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            _logger.LogDebug("Received message from queue '{QueueName}' with routing key '{RoutingKey}'.", queueName, ea.RoutingKey);

            try
            {
                // Создаем новый scope для обработки сообщения. ЭТО ВАЖНО!
                // Позволяет использовать scoped-сервисы (например, DbContext) внутри обработчика.
                using var scope = _scopeFactory.CreateScope();
                var resultHandler = scope.ServiceProvider.GetRequiredService<ITaskResultHandler>();
                
                var baseResult = JsonSerializer.Deserialize<TaskResultBase>(message);

                if (baseResult?.Status?.ToLower() == "error" || baseResult?.Status?.ToLower() == "critical_error")
                {
                    await resultHandler.HandleFailedResultAsync(baseResult, message);
                }
                else if (ea.RoutingKey == _conf.DemucsResultRoutingKey)
                {
                    var demucsResult = JsonSerializer.Deserialize<DemucsResultData>(message);
                    await resultHandler.HandleDemucsResultAsync(demucsResult);
                }
                else if (ea.RoutingKey == _conf.WhisperResultRoutingKey)
                {
                    var whisperResult = JsonSerializer.Deserialize<WhisperResultData>(message);
                    await resultHandler.HandleWhisperResultAsync(whisperResult);
                }
                else
                {
                    _logger.LogWarning("Unknown routing key '{RoutingKey}' for message: {Message}", ea.RoutingKey, message);
                }
                
                await _channel.BasicAckAsync(ea.DeliveryTag, false);
            }
            catch (JsonException jsonEx)
            {
                 _logger.LogError(jsonEx, "Failed to deserialize message. Moving to dead-letter queue if configured. Message: {Message}", message);
                 // Не подтверждаем (nack), но и не ставим в очередь повторно (requeue=false),
                 // чтобы избежать "ядовитых" сообщений.
                 await _channel.BasicNackAsync(ea.DeliveryTag, false, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message. Message will be requeued. Message: {Message}", message);
                 // Ставим в очередь повторно, так как это может быть временная ошибка (например, БД недоступна)
                await _channel.BasicNackAsync(ea.DeliveryTag, false, true);
            }
        };

        await _channel.BasicConsumeAsync(queue: queueName, autoAck: false, consumer: consumer);
        _logger.LogInformation("Consumer set up for queue: {QueueName}", queueName);
    }
}