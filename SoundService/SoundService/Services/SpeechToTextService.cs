using SoundService.Models;

namespace SoundService.Services;

public class SpeechToTextService
{
    private readonly ILogger<SpeechToTextService> _logger;

    // Словарь с захардкоженными текстами для популярных военных песен
    private readonly Dictionary<string, (string FullText, List<TranscriptSegment> Segments)> _knownSongs = new()
    {
        {
            "Священная война", (
                "Вставай, страна огромная,\nВставай на смертный бой\nС фашистской силой тёмною,\nС проклятою ордой.\n\nПусть ярость благородная\nВскипает, как волна,\nИдёт война народная,\nСвященная война!",
                new List<TranscriptSegment>
                {
                    new() { Start = 15, End = 20, Text = "Вставай, страна огромная," },
                    new() { Start = 21, End = 26, Text = "Вставай на смертный бой" },
                    new() { Start = 27, End = 32, Text = "С фашистской силой тёмною," },
                    new() { Start = 33, End = 38, Text = "С проклятою ордой." },
                    new() { Start = 45, End = 50, Text = "Пусть ярость благородная" },
                    new() { Start = 51, End = 56, Text = "Вскипает, как волна," },
                    new() { Start = 57, End = 62, Text = "Идёт война народная," },
                    new() { Start = 63, End = 68, Text = "Священная война!" }
                }
            )
        },
        {
            "Катюша", (
                "Расцветали яблони и груши,\nПоплыли туманы над рекой.\nВыходила на берег Катюша,\nНа высокий берег на крутой.\n\nВыходила, песню заводила\nПро степного сизого орла,\nПро того, которого любила,\nПро того, чьи письма берегла.",
                new List<TranscriptSegment>
                {
                    new() { Start = 10, End = 15, Text = "Расцветали яблони и груши," },
                    new() { Start = 16, End = 21, Text = "Поплыли туманы над рекой." },
                    new() { Start = 22, End = 27, Text = "Выходила на берег Катюша," },
                    new() { Start = 28, End = 33, Text = "На высокий берег на крутой." },
                    new() { Start = 39, End = 44, Text = "Выходила, песню заводила" },
                    new() { Start = 45, End = 50, Text = "Про степного сизого орла," },
                    new() { Start = 51, End = 56, Text = "Про того, которого любила," },
                    new() { Start = 57, End = 62, Text = "Про того, чьи письма берегла." }
                }
            )
        },
        {
            "Темная ночь", (
                "Тёмная ночь, только пули свистят по степи,\nТолько ветер гудит в проводах, тускло звёзды мерцают.\nВ тёмную ночь ты, любимая, знаю, не спишь,\nИ у детской кроватки тайком ты слезу утираешь.",
                new List<TranscriptSegment>
                {
                    new() { Start = 12, End = 18, Text = "Тёмная ночь, только пули свистят по степи," },
                    new() { Start = 19, End = 24, Text = "Только ветер гудит в проводах, тускло звёзды мерцают." },
                    new() { Start = 25, End = 31, Text = "В тёмную ночь ты, любимая, знаю, не спишь," },
                    new() { Start = 32, End = 38, Text = "И у детской кроватки тайком ты слезу утираешь." }
                }
            )
        },
        {
            "В землянке", (
                "Бьётся в тесной печурке огонь,\nНа поленьях смола, как слеза.\nИ поёт мне в землянке гармонь\nПро улыбку твою и глаза.\n\nПро тебя мне шептали кусты\nВ белоснежных полях под Москвой.\nЯ хочу, чтобы слышала ты,\nКак тоскует мой голос живой.",
                new List<TranscriptSegment>
                {
                    new() { Start = 8, End = 13, Text = "Бьётся в тесной печурке огонь," },
                    new() { Start = 14, End = 19, Text = "На поленьях смола, как слеза." },
                    new() { Start = 20, End = 25, Text = "И поёт мне в землянке гармонь" },
                    new() { Start = 26, End = 31, Text = "Про улыбку твою и глаза." },
                    new() { Start = 37, End = 42, Text = "Про тебя мне шептали кусты" },
                    new() { Start = 43, End = 48, Text = "В белоснежных полях под Москвой." },
                    new() { Start = 49, End = 54, Text = "Я хочу, чтобы слышала ты," },
                    new() { Start = 55, End = 60, Text = "Как тоскует мой голос живой." }
                }
            )
        },
        {
            "Офицеры", (
                "От героев былых времён\nНе осталось порой имён.\nТе, кто приняли смертный бой,\nСтали просто землёй и травой.\nТолько грозная доблесть их\nПоселилась в сердцах живых.\nЭтот вечный огонь, нам завещанный одним,\nМы в груди храним.",
                new List<TranscriptSegment>
                {
                    new() { Start = 8, End = 13, Text = "От героев былых времён" },
                    new() { Start = 14, End = 19, Text = "Не осталось порой имён." },
                    new() { Start = 20, End = 25, Text = "Те, кто приняли смертный бой," },
                    new() { Start = 26, End = 31, Text = "Стали просто землёй и травой." },
                    new() { Start = 32, End = 37, Text = "Только грозная доблесть их" },
                    new() { Start = 38, End = 43, Text = "Поселилась в сердцах живых." },
                    new() { Start = 44, End = 49, Text = "Этот вечный огонь, нам завещанный одним," },
                    new() { Start = 50, End = 55, Text = "Мы в груди храним." }
                }
            )
        }
    };

    // Default text if the song title is not found
    private readonly (string FullText, List<TranscriptSegment> Segments) _defaultText = (
        "Это автоматическая транскрипция военной песни.\nКаждая строка представляет собой фрагмент текста с соответствующим временным интервалом.\nЭти строки могут быть использованы для синхронизации текста с аудио при воспроизведении.",
        new List<TranscriptSegment>
        {
            new() { Start = 5, End = 10, Text = "Это автоматическая транскрипция военной песни." },
            new() { Start = 15, End = 20, Text = "Каждая строка представляет собой фрагмент текста с соответствующим временным интервалом." },
            new() { Start = 25, End = 30, Text = "Эти строки могут быть использованы для синхронизации текста с аудио при воспроизведении." }
        }
    );

    public SpeechToTextService(ILogger<SpeechToTextService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Имитирует процесс распознавания речи из аудиофайла
    /// </summary>
    /// <param name="filePath">Путь к аудиофайлу</param>
    /// <param name="title">Название песни или трека</param>
    /// <returns>Объект с полным текстом и сегментами с временными метками</returns>
    public async Task<(string FullText, List<TranscriptSegment> Segments)> RecognizeTextAsync(string filePath, string title)
    {
        _logger.LogInformation("Начало распознавания текста песни: {Title}", title);
        
        try
        {
            // Симуляция процесса распознавания речи
            await Task.Delay(3000); // В реальности это может занять много времени
            
            // Ищем по заголовку совпадение в нашем словаре захардкоженных текстов
            foreach (var songTitle in _knownSongs.Keys)
            {
                if (title.Contains(songTitle, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Найдена песня в базе захардкоженных текстов: {SongTitle}", songTitle);
                    return _knownSongs[songTitle];
                }
            }
            
            // Если не нашли совпадение, возвращаем дефолтный текст
            _logger.LogInformation("Песня не найдена в базе, возвращаем стандартный текст");
            return _defaultText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при распознавании текста");
            return _defaultText; // В случае ошибки возвращаем дефолтный текст
        }
    }
} 