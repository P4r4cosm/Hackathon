using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Metadata;
using RabbitMQ.Client;
using SoundService.Models;

namespace SoundService.RabbitMQ;

public class RabbitMqService : IDisposable
{
    private IConnection _connection;
    private IChannel _channel; // Канал может потребоваться пересоздать
    private ConnectionFactory _connectionFactory;
    private ILogger<RabbitMqService> _logger;
    private object _channelLock = new object(); // Для безопасного пересоздания канала


    public RabbitMqService(IConfiguration configuration, ILogger<RabbitMqService> logger)
    {
        _logger = logger;
        _connectionFactory = new ConnectionFactory()
        {
            HostName = configuration["RABBITMQ_HOST"],
            UserName = configuration["RABBITMQ_USER"],
            Password = configuration["RABBITMQ_PASS"],
            Port = configuration.GetValue<int>("RABBITMQ_PORT"),
            // Рекомендуется включить автоматическое восстановление
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10) // Интервал попыток восстановления
        };

        try
        {
            // Соединение создается один раз для Singleton сервиса
            _connection = _connectionFactory.CreateConnectionAsync().Result;
            EnsureChannel(); // Создаем канал
            _logger.LogInformation("RabbitMQ Service initialized, connection established.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to RabbitMQ during service initialization.");
            throw;
        }
    }

    private void EnsureChannel()
    {
        // Блокировка для потокобезопасного создания/проверки канала
        lock (_channelLock)
        {
            if (_channel == null || _channel.IsClosed)
            {
                if (_connection == null || !_connection.IsOpen)
                {
                    _logger.LogWarning("RabbitMQ connection is not open. Attempting to recreate connection...");
                    try
                    {
                        _connection?.Dispose(); // Закрыть старое, если есть
                        _connection = _connectionFactory.CreateConnectionAsync().Result;
                        _logger.LogInformation("RabbitMQ connection re-established.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to re-establish RabbitMQ connection.");
                        throw; // Перебросить, чтобы вызывающий код знал о проблеме
                    }
                }

                _channel = _connection.CreateChannelAsync().Result;
                _logger.LogInformation("RabbitMQ channel (re)created.");
            }
        }
    }


    public async Task PublishDemucsTask(DemucsTaskData data)
    {
        try
        {
            EnsureChannel(); // Убедимся, что канал открыт

            var messageBody = JsonSerializer.Serialize(data);
            var body = Encoding.UTF8.GetBytes(messageBody);

            var properties = new BasicProperties();
            properties.Persistent = true; // Делаем сообщение персистентным
            properties.ContentType = "application/json";
            if (!string.IsNullOrEmpty(data.TaskId)) // Предполагаем, что у DemucsTaskData есть TaskId
            {
                properties.MessageId = data.TaskId;
                properties.CorrelationId = data.TaskId; // Часто используется для связи запроса и ответа
            }

            // Публикуем в ваш AudioProcessingExchange с правильным routingKey
            await _channel.BasicPublishAsync(
                exchange: RabbitMqConstants.AudioProcessingExchange,
                routingKey: RabbitMqConstants.DemucsTasksRoutingKey,
                mandatory: false, // Если true и сообщение не может быть смашрутизировано, оно вернется (событие BasicReturn)
                basicProperties: properties,
                body: body);

            _logger.LogInformation("Sent Demucs Task to exchange '{ExchangeName}' with key '{RoutingKey}'. TaskId: {TaskId}, Message: {MessageBody}",
                RabbitMqConstants.AudioProcessingExchange,
                RabbitMqConstants.DemucsTasksRoutingKey,
                data.TaskId, // Предполагаем, что у DemucsTaskData есть TaskId
                messageBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish Demucs task. TaskId: {TaskId}", data.TaskId);
            // Здесь можно добавить логику retry с Polly или отправку в DLQ (Dead Letter Queue)
            throw; // Перебрасываем, чтобы вызывающий код мог обработать
        }
    }

    public async void Dispose()
    {
        try
        {
            await _channel.CloseAsync();
            _channel?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing RabbitMQ channel.");
        }
        try
        {
            await _connection.CloseAsync();
            _connection?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing RabbitMQ connection.");
        }

        _logger.LogInformation("RabbitMQ Service disposed.");
    }
}