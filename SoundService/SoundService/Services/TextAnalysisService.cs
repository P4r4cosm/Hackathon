using SoundService.Data;
using SoundService.Models;

namespace SoundService.Services;

public class TextAnalysisService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TextAnalysisService> _logger;

    // Словарь захардкоженных тематических тегов и ключевых слов для их определения 
    private readonly Dictionary<string, List<string>> _thematicTagKeywords = new()
    {
        { "патриотические", new List<string> { "родина", "отечество", "страна", "россия", "победа", "слава", "герой", "подвиг", "знамя", "отвага" } },
        { "фронтовые", new List<string> { "бой", "атака", "битва", "сражение", "война", "фронт", "огонь", "пуля", "враг", "солдат", "офицер" } },
        { "о любви", new List<string> { "любовь", "любимая", "любимый", "сердце", "письмо", "разлука", "ждать", "встреча", "поцелуй", "объятие" } },
        { "о потерях", new List<string> { "потеря", "гибель", "смерть", "память", "могила", "похоронка", "слеза", "скорбь", "боль", "утрата" } },
        { "о героизме", new List<string> { "герой", "подвиг", "орден", "медаль", "награда", "отвага", "мужество", "храбрость", "честь", "доблесть" } },
        { "о тоске по дому", new List<string> { "дом", "родные", "семья", "мать", "отец", "сын", "дочь", "ребенок", "изба", "хата", "родная сторона" } },
        { "о победе", new List<string> { "победа", "май", "знамя", "салют", "радость", "окончание", "возвращение", "мир", "праздник", "освобождение" } }
    };

    // Захардкоженные ключевые слова для популярных песен
    private readonly Dictionary<string, List<string>> _songKeywords = new()
    {
        { "Священная война", new List<string> { "фашизм", "враг", "орда", "ярость", "смертный бой", "народная война" } },
        { "Катюша", new List<string> { "берег", "ожидание", "письма", "орел", "любовь", "расставание" } },
        { "Темная ночь", new List<string> { "пули", "степь", "ветер", "звезды", "любимая", "ребенок", "разлука" } },
        { "В землянке", new List<string> { "огонь", "печурка", "смола", "гармонь", "улыбка", "глаза", "кусты", "поля", "тоска" } },
        { "Офицеры", new List<string> { "герои", "память", "смертный бой", "земля", "трава", "доблесть", "вечный огонь" } }
    };

    public TextAnalysisService(ApplicationDbContext context, ILogger<TextAnalysisService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Анализирует текст песни и возвращает тематические теги и ключевые слова
    /// </summary>
    /// <param name="text">Полный текст песни</param>
    /// <param name="title">Название песни</param>
    /// <returns>Кортеж с коллекциями тегов и ключевых слов</returns>
    public async Task<(List<ThematicTag> Tags, List<Keyword> Keywords)> AnalyzeTextAsync(string text, string title)
    {
        _logger.LogInformation("Начало анализа текста песни: {Title}", title);
        
        var thematicTags = new List<ThematicTag>();
        var keywords = new List<Keyword>();
        
        try
        {
            // Имитация задержки обработки
            await Task.Delay(1000);
            
            // Приводим текст к нижнему регистру для лучшего сопоставления
            var lowerText = text.ToLower();
            
            // Поиск соответствия тематическим тегам
            foreach (var tagEntry in _thematicTagKeywords)
            {
                foreach (var keyword in tagEntry.Value)
                {
                    if (lowerText.Contains(keyword))
                    {
                        // Ищем тег в базе данных или создаем новый
                        var tag = await FindOrCreateThematicTagAsync(tagEntry.Key);
                        if (tag != null && !thematicTags.Any(t => t.Id == tag.Id))
                        {
                            thematicTags.Add(tag);
                        }
                        break; // Если нашли хотя бы одно ключевое слово - добавляем тег и прекращаем поиск
                    }
                }
            }
            
            // Если не нашли ни одного тега, добавляем базовый тег "военные"
            if (thematicTags.Count == 0)
            {
                var defaultTag = await FindOrCreateThematicTagAsync("военные");
                if (defaultTag != null)
                {
                    thematicTags.Add(defaultTag);
                }
            }
            
            // Поиск ключевых слов из захардкоженного списка по названию песни
            foreach (var songEntry in _songKeywords)
            {
                if (title.Contains(songEntry.Key, StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var word in songEntry.Value)
                    {
                        var keyword = await FindOrCreateKeywordAsync(word);
                        if (keyword != null && !keywords.Any(k => k.Id == keyword.Id))
                        {
                            keywords.Add(keyword);
                        }
                    }
                    break; // Если нашли подходящую песню, добавляем её ключевые слова и прекращаем поиск
                }
            }
            
            // Если не нашли ключевых слов, добавляем некоторые общие для военных песен
            if (keywords.Count == 0)
            {
                var defaultKeywords = new List<string> { "война", "песня", "военные годы" };
                foreach (var word in defaultKeywords)
                {
                    var keyword = await FindOrCreateKeywordAsync(word);
                    if (keyword != null)
                    {
                        keywords.Add(keyword);
                    }
                }
            }
            
            return (thematicTags, keywords);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при анализе текста песни");
            return (new List<ThematicTag>(), new List<Keyword>());
        }
    }
    
    /// <summary>
    /// Находит тематический тег в БД или создает новый
    /// </summary>
    private async Task<ThematicTag?> FindOrCreateThematicTagAsync(string tagName)
    {
        // Получим все теги один раз
        var allTags = _context.Set<ThematicTag>().ToList();
        var tag = allTags.FirstOrDefault(t => t.Name == tagName);
        
        if (tag == null)
        {
            tag = new ThematicTag { Name = tagName };
            _context.Set<ThematicTag>().Add(tag);
            await _context.SaveChangesAsync();
        }
        return tag;
    }
    
    /// <summary>
    /// Находит ключевое слово в БД или создает новое
    /// </summary>
    private async Task<Keyword?> FindOrCreateKeywordAsync(string word)
    {
        // Получим все ключевые слова один раз
        var allKeywords = _context.Set<Keyword>().ToList();
        var keyword = allKeywords.FirstOrDefault(k => k.Text == word);
        
        if (keyword == null)
        {
            keyword = new Keyword { Text = word };
            _context.Set<Keyword>().Add(keyword);
            await _context.SaveChangesAsync();
        }
        return keyword;
    }
} 