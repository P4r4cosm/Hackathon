namespace SoundService.RabbitMQ;

public class RabbitMqConf
{
    // --- ОБЩИЕ ОБМЕННИКИ (EXCHANGES) ---
    public string TasksExchange { get; }
    public string ResultsExchange { get; }

    // --- ТОПОЛОГИЯ ДЛЯ DEMUCS ---
    public string DemucsTaskQueue { get; }
    public string DemucsTaskRoutingKey { get; }
    public string DemucsResultQueue { get; }
    public string DemucsResultRoutingKey { get; }

    // --- ТОПОЛОГИЯ ДЛЯ WHISPER ---
    public string WhisperTaskQueue { get; }
    public string WhisperTaskRoutingKey { get; }
    public string WhisperResultQueue { get; }
    public string WhisperResultRoutingKey { get; }

    public RabbitMqConf(IConfiguration configuration)
    {
        // Читаем имена обменников
        TasksExchange = configuration["RABBITMQ_TASKS_EXCHANGE"];
        ResultsExchange = configuration["RABBITMQ_RESULTS_EXCHANGE"];

        // Читаем настройки для Demucs
        DemucsTaskQueue = configuration["RABBITMQ_DEMUCS_TASK_QUEUE"];
        DemucsTaskRoutingKey = configuration["RABBITMQ_DEMUCS_TASK_ROUTING_KEY"];
        DemucsResultQueue = configuration["RABBITMQ_DEMUCS_RESULT_QUEUE"];
        DemucsResultRoutingKey = configuration["RABBITMQ_DEMUCS_RESULT_ROUTING_KEY"];

        // Читаем настройки для Whisper
        WhisperTaskQueue = configuration["RABBITMQ_WHISPER_TASK_QUEUE"];
        WhisperTaskRoutingKey = configuration["RABBITMQ_WHISPER_TASK_ROUTING_KEY"];
        WhisperResultQueue = configuration["RABBITMQ_WHISPER_RESULT_QUEUE"];
        WhisperResultRoutingKey = configuration["RABBITMQ_WHISPER_RESULT_ROUTING_KEY"];
    }
}