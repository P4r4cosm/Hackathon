// SoundService/RabbitMQ/RabbitMQInitializer.cs
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using System.Threading;
using System.Threading.Tasks;
using SoundService.RabbitMQ;

public class RabbitMQInitializer : IHostedService
{
    private readonly IConnectionFactory _connectionFactory;
    private IConnection _connection;
    private IChannel _channel;
    private readonly ILogger<RabbitMQInitializer> _logger;
    private readonly RabbitMqConf _conf;

    public RabbitMQInitializer(IConfiguration configuration, ILogger<RabbitMQInitializer> logger, RabbitMqConf conf)
    {
        _logger = logger;
        _conf = conf;
        _connectionFactory = new ConnectionFactory()
        {
            HostName = configuration["RABBITMQ_HOST"],
            UserName = configuration["RABBITMQ_USER"],
            Password = configuration["RABBITMQ_PASS"],
            Port = configuration.GetValue<int>("RABBITMQ_PORT"),
            VirtualHost = configuration["RABBITMQ_VHOST"]
        };
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("RabbitMQ Initializer: Declaring RabbitMQ infrastructure...");
        try
        {
            _connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

            // --- ОБМЕННИК ДЛЯ ЗАДАЧ (TASKS EXCHANGE) ---
            await _channel.ExchangeDeclareAsync(
                exchange: _conf.TasksExchange,
                type: ExchangeType.Topic, // Topic более гибкий, чем Direct
                durable: true);

            // --- ОБМЕННИК ДЛЯ РЕЗУЛЬТАТОВ (RESULTS EXCHANGE) ---
            await _channel.ExchangeDeclareAsync(
                exchange: _conf.ResultsExchange,
                type: ExchangeType.Topic, // Topic более гибкий
                durable: true);

            // --- ТОПОЛОГИЯ ДЛЯ DEMUCS ---
            // Очередь для задач Demucs
            await _channel.QueueDeclareAsync(queue: _conf.DemucsTaskQueue, durable: true, exclusive: false, autoDelete: false);
            await _channel.QueueBindAsync(queue: _conf.DemucsTaskQueue, exchange: _conf.TasksExchange, routingKey: _conf.DemucsTaskRoutingKey);
            
            // Очередь для результатов Demucs
            await _channel.QueueDeclareAsync(queue: _conf.DemucsResultQueue, durable: true, exclusive: false, autoDelete: false);
            await _channel.QueueBindAsync(queue: _conf.DemucsResultQueue, exchange: _conf.ResultsExchange, routingKey: _conf.DemucsResultRoutingKey);
            
            // --- ТОПОЛОГИЯ ДЛЯ WHISPER ---
            // Очередь для задач Whisper
            await _channel.QueueDeclareAsync(queue: _conf.WhisperTaskQueue, durable: true, exclusive: false, autoDelete: false);
            await _channel.QueueBindAsync(queue: _conf.WhisperTaskQueue, exchange: _conf.TasksExchange, routingKey: _conf.WhisperTaskRoutingKey);

            // Очередь для результатов Whisper
            await _channel.QueueDeclareAsync(queue: _conf.WhisperResultQueue, durable: true, exclusive: false, autoDelete: false);
            await _channel.QueueBindAsync(queue: _conf.WhisperResultQueue, exchange: _conf.ResultsExchange, routingKey: _conf.WhisperResultRoutingKey);
            
            _logger.LogInformation("RabbitMQ infrastructure declared successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to declare RabbitMQ infrastructure.");
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