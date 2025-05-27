﻿using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using System.Threading;
using System.Threading.Tasks;


namespace SoundService.RabbitMQ;

// RabbitMqConstants.cs
public class RabbitMqConf
{
    public RabbitMqConf(IConfiguration configuration)
    {
        DemucsQueueName = configuration["RABBITMQ_DEMUCS_QUEUE_NAME"];
        DemucsTasksRoutingKey = configuration["RABBITMQ_DEMUCS_TASKS_ROUTING_KEY"];
        AudioProcessingExchange = configuration["RABBITMQ_AUDIO_PROCESSING_EXCHANGE"];
        ResultsExchangeName = configuration["RABBITMQ_RESULTS_EXCHANGE_NAME"];
        TaskResultsDemucsName = configuration["RABBITMQ_TASK_RESULTS_QUEUE_NAME"];
        TaskResultsDemucsRoutingKey = configuration["RABBITMQ_TASK_RESULTS_ROUTING_KEY"];
        WhisperQueueName = configuration["RABBITMQ_WHISPER_QUEUE_NAME"];
        TaskResultsWhisperRoutingKey = configuration["RABBITMQ_WHISPER_RESULT_ROUTING_KEY"];
        WhisperResultQueueName = configuration["RABBITMQ_WHISPER_RESULTS_QUEUE_NAME"];
        WhisperTasksRoutingKey = configuration["RABBITMQ_WHISPER_TASK_ROUTING_KEY"];

    }

    public readonly string DemucsQueueName;
    public readonly string DemucsTasksRoutingKey;
    public readonly string AudioProcessingExchange;

    public readonly string ResultsExchangeName;
    public readonly string TaskResultsDemucsName;
    public readonly string TaskResultsDemucsRoutingKey;

    public readonly string WhisperQueueName; //очередь для задач
    
    public readonly string TaskResultsWhisperRoutingKey; //ключ для очереди с результатами

    public readonly string WhisperResultQueueName;

    public readonly string WhisperTasksRoutingKey;
}

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
        };
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("RabbitMQ Initializer: Declaring RabbitMQ infrastructure...");
        try
        {
            
            _connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

            // Exchange для задач
            await _channel.ExchangeDeclareAsync(
                exchange: _conf.AudioProcessingExchange,
                type: ExchangeType.Direct,
                durable: true);

            // Очередь для Demucs задач
            await _channel.QueueDeclareAsync(
                queue: _conf.DemucsQueueName,
                durable: true, exclusive: false, autoDelete: false, arguments: null);

            await _channel.QueueBindAsync(
                queue: _conf.DemucsQueueName,
                exchange: _conf.AudioProcessingExchange,
                routingKey: _conf.DemucsTasksRoutingKey);
            
            // Очередь для Whisper задач
            await _channel.QueueDeclareAsync(
                queue: _conf.WhisperQueueName,
                durable: true, exclusive: false, autoDelete: false, arguments: null);

            await _channel.QueueBindAsync(
                queue: _conf.WhisperQueueName,
                exchange: _conf.AudioProcessingExchange,
                routingKey: _conf.TaskResultsWhisperRoutingKey);

            // Exchange для результатов
            await _channel.ExchangeDeclareAsync(
                exchange: _conf.ResultsExchangeName,
                type: ExchangeType.Direct, // или Topic, если нужна более сложная маршрутизация результатов
                durable: true);

            // Очередь для Demucs результатов
            await _channel.QueueDeclareAsync(
                queue: _conf.TaskResultsDemucsName,
                durable: true, exclusive: false, autoDelete: false, arguments: null);

            await _channel.QueueBindAsync(
                queue: _conf.TaskResultsDemucsName,
                exchange: _conf.ResultsExchangeName,
                routingKey: _conf.WhisperTasksRoutingKey); // или "#" если ResultsExchange типа Topic
            
            //Очередь для результатов Whisper
            
            await _channel.QueueDeclareAsync(
                queue: _conf.WhisperResultQueueName,
                durable:true, exclusive:false, autoDelete:false, arguments:null);

            await _channel.QueueBindAsync(
                queue: _conf.WhisperResultQueueName,
                exchange: _conf.ResultsExchangeName,
                routingKey: _conf.TaskResultsWhisperRoutingKey);

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