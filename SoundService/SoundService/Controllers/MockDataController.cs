using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using SoundService.Models;

namespace SoundService.Controllers;

[ApiController]
public class MockDataController : ControllerBase
{
    private readonly ILogger<MockDataController> _logger;

    public MockDataController(ILogger<MockDataController> logger)
    {
        _logger = logger;
    }

    [HttpGet("api/tags")]
    public IActionResult GetTags()
    {
        _logger.LogInformation("Запрос к заглушке api/tags");
        
        var tags = new[]
        {
            new { id = "1", name = "Патриотические" },
            new { id = "2", name = "Победа" },
            new { id = "3", name = "Фронт" },
            new { id = "4", name = "Подвиг" },
            new { id = "5", name = "Родина" }
        };
        
        return Ok(tags);
    }

    [HttpGet("api/recordings")]
    public IActionResult GetRecordings()
    {
        _logger.LogInformation("Запрос к заглушке api/recordings");
        
        var recordings = new[]
        {
            new {
                id = "1",
                title = "Священная война",
                author = "Александров А. В.",
                authorId = "1",
                year = 1941,
                originalAudioUrl = "original_tracks/war_songs/sacred_war.mp3",
                restoredAudioUrl = "restored_tracks/war_songs/sacred_war.mp3",
                coverImage = "https://via.placeholder.com/300?text=Священная+война",
                tags = new[] { 
                    new { id = "1", name = "Патриотические" },
                    new { id = "3", name = "Фронт" }
                }
            },
            new {
                id = "2",
                title = "Катюша",
                author = "Блантер М. И.",
                authorId = "2",
                year = 1943,
                originalAudioUrl = "original_tracks/war_songs/katyusha.mp3",
                restoredAudioUrl = "restored_tracks/war_songs/katyusha.mp3",
                coverImage = "https://via.placeholder.com/300?text=Катюша",
                tags = new[] { 
                    new { id = "1", name = "Патриотические" },
                    new { id = "5", name = "Родина" }
                }
            },
            new {
                id = "3",
                title = "День Победы",
                author = "Тухманов Д. Ф.",
                authorId = "3",
                year = 1945,
                originalAudioUrl = "original_tracks/war_songs/victory_day.mp3",
                restoredAudioUrl = "restored_tracks/war_songs/victory_day.mp3",
                coverImage = "https://via.placeholder.com/300?text=День+Победы",
                tags = new[] { 
                    new { id = "2", name = "Победа" },
                    new { id = "4", name = "Подвиг" }
                }
            }
        };
        
        return Ok(recordings);
    }

    [HttpGet("api/authors")]
    public IActionResult GetAuthors()
    {
        _logger.LogInformation("Запрос к заглушке api/authors");
        
        var authors = new[]
        {
            new {
                id = "1",
                name = "Александров А. В.",
                imageUrl = "https://via.placeholder.com/300?text=Александров",
                biography = "Александр Васильевич Александров (1883-1946) - советский композитор, хоровой дирижёр, народный артист СССР. Автор музыки гимна СССР и России, а также песни «Священная война»."
            },
            new {
                id = "2",
                name = "Блантер М. И.",
                imageUrl = "https://via.placeholder.com/300?text=Блантер",
                biography = "Матвей Исаакович Блантер (1903-1990) - советский композитор. Народный артист СССР. Автор множества популярных песен, в том числе «Катюша»."
            },
            new {
                id = "3",
                name = "Тухманов Д. Ф.",
                imageUrl = "https://via.placeholder.com/300?text=Тухманов",
                biography = "Давид Фёдорович Тухманов (род. 1940) - советский и российский композитор. Народный артист Российской Федерации. Автор песни «День Победы»."
            }
        };
        
        return Ok(authors);
    }

    [HttpGet("api/recordings/{id}")]
    public IActionResult GetRecordingDetails(string id)
    {
        _logger.LogInformation($"Запрос к заглушке api/recordings/{id}");
        
        var recording = new
        {
            id = id,
            title = id == "1" ? "Священная война" : id == "2" ? "Катюша" : "День Победы",
            author = id == "1" ? "Александров А. В." : id == "2" ? "Блантер М. И." : "Тухманов Д. Ф.",
            authorId = id,
            year = id == "1" ? 1941 : id == "2" ? 1943 : 1945,
            originalAudioUrl = $"original_tracks/war_songs/{(id == "1" ? "sacred_war" : id == "2" ? "katyusha" : "victory_day")}.mp3",
            restoredAudioUrl = $"restored_tracks/war_songs/{(id == "1" ? "sacred_war" : id == "2" ? "katyusha" : "victory_day")}.mp3",
            coverImage = $"https://via.placeholder.com/300?text={(id == "1" ? "Священная+война" : id == "2" ? "Катюша" : "День+Победы")}",
            description = "Одна из самых известных песен военных лет, ставшая своеобразным гимном защиты Отечества.",
            transcription = "Полный текст песни...",
            tags = new[] { 
                new { id = "1", name = "Патриотические" },
                new { id = id == "1" ? "3" : id == "2" ? "5" : "2", name = id == "1" ? "Фронт" : id == "2" ? "Родина" : "Победа" }
            }
        };
        
        return Ok(recording);
    }

    [HttpGet("api/recordings/{id}/related")]
    public IActionResult GetRelatedRecordings(string id)
    {
        _logger.LogInformation($"Запрос к заглушке api/recordings/{id}/related");
        
        var relatedRecordings = new[]
        {
            new {
                id = id == "1" ? "2" : "1",
                title = id == "1" ? "Катюша" : "Священная война",
                author = id == "1" ? "Блантер М. И." : "Александров А. В.",
                authorId = id == "1" ? "2" : "1",
                year = id == "1" ? 1943 : 1941,
                originalAudioUrl = $"original_tracks/war_songs/{(id == "1" ? "katyusha" : "sacred_war")}.mp3",
                restoredAudioUrl = $"restored_tracks/war_songs/{(id == "1" ? "katyusha" : "sacred_war")}.mp3",
                coverImage = $"https://via.placeholder.com/300?text={(id == "1" ? "Катюша" : "Священная+война")}",
                tags = new[] { 
                    new { id = "1", name = "Патриотические" }
                }
            },
            new {
                id = "3",
                title = "День Победы",
                author = "Тухманов Д. Ф.",
                authorId = "3",
                year = 1945,
                originalAudioUrl = "original_tracks/war_songs/victory_day.mp3",
                restoredAudioUrl = "restored_tracks/war_songs/victory_day.mp3",
                coverImage = "https://via.placeholder.com/300?text=День+Победы",
                tags = new[] { 
                    new { id = "2", name = "Победа" }
                }
            }
        };
        
        return Ok(relatedRecordings);
    }

    [HttpGet("api/authors/{id}")]
    public IActionResult GetAuthorDetails(string id)
    {
        _logger.LogInformation($"Запрос к заглушке api/authors/{id}");
        
        var author = new
        {
            id = id,
            name = id == "1" ? "Александров А. В." : id == "2" ? "Блантер М. И." : "Тухманов Д. Ф.",
            imageUrl = $"https://via.placeholder.com/300?text={(id == "1" ? "Александров" : id == "2" ? "Блантер" : "Тухманов")}",
            biography = id == "1" 
                ? "Александр Васильевич Александров (1883-1946) - советский композитор, хоровой дирижёр, народный артист СССР. Автор музыки гимна СССР и России, а также песни «Священная война»."
                : id == "2" 
                ? "Матвей Исаакович Блантер (1903-1990) - советский композитор. Народный артист СССР. Автор множества популярных песен, в том числе «Катюша»."
                : "Давид Фёдорович Тухманов (род. 1940) - советский и российский композитор. Народный артист Российской Федерации. Автор песни «День Победы»."
        };
        
        return Ok(author);
    }

    [HttpGet("api/recordings/author/{authorId}")]
    public IActionResult GetRecordingsByAuthor(string authorId)
    {
        _logger.LogInformation($"Запрос к заглушке api/recordings/author/{authorId}");
        
        var recordings = new[]
        {
            new {
                id = authorId,
                title = authorId == "1" ? "Священная война" : authorId == "2" ? "Катюша" : "День Победы",
                author = authorId == "1" ? "Александров А. В." : authorId == "2" ? "Блантер М. И." : "Тухманов Д. Ф.",
                authorId = authorId,
                year = authorId == "1" ? 1941 : authorId == "2" ? 1943 : 1945,
                originalAudioUrl = $"original_tracks/war_songs/{(authorId == "1" ? "sacred_war" : authorId == "2" ? "katyusha" : "victory_day")}.mp3",
                restoredAudioUrl = $"restored_tracks/war_songs/{(authorId == "1" ? "sacred_war" : authorId == "2" ? "katyusha" : "victory_day")}.mp3",
                coverImage = $"https://via.placeholder.com/300?text={(authorId == "1" ? "Священная+война" : authorId == "2" ? "Катюша" : "День+Победы")}",
                tags = new[] { 
                    new { id = "1", name = "Патриотические" }
                }
            }
        };
        
        return Ok(recordings);
    }

    [HttpGet("api/recordings/tag/{tagId}")]
    public IActionResult GetRecordingsByTag(string tagId)
    {
        _logger.LogInformation($"Запрос к заглушке api/recordings/tag/{tagId}");
        
        var recordings = new[]
        {
            new {
                id = tagId == "1" || tagId == "3" ? "1" : "3",
                title = tagId == "1" || tagId == "3" ? "Священная война" : "День Победы",
                author = tagId == "1" || tagId == "3" ? "Александров А. В." : "Тухманов Д. Ф.",
                authorId = tagId == "1" || tagId == "3" ? "1" : "3",
                year = tagId == "1" || tagId == "3" ? 1941 : 1945,
                originalAudioUrl = tagId == "1" || tagId == "3" ? "original_tracks/war_songs/sacred_war.mp3" : "original_tracks/war_songs/victory_day.mp3",
                restoredAudioUrl = tagId == "1" || tagId == "3" ? "restored_tracks/war_songs/sacred_war.mp3" : "restored_tracks/war_songs/victory_day.mp3",
                coverImage = tagId == "1" || tagId == "3" ? "https://via.placeholder.com/300?text=Священная+война" : "https://via.placeholder.com/300?text=День+Победы",
                tags = new[] { 
                    new { id = tagId, name = tagId == "1" ? "Патриотические" : tagId == "2" ? "Победа" : tagId == "3" ? "Фронт" : tagId == "4" ? "Подвиг" : "Родина" }
                }
            }
        };
        
        return Ok(recordings);
    }

    [HttpGet("api/recordings/search")]
    public IActionResult SearchRecordings([FromQuery] string query)
    {
        _logger.LogInformation($"Запрос к заглушке api/recordings/search?query={query}");
        
        // Возвращаем все записи для любого поискового запроса
        var recordings = new[]
        {
            new {
                id = "1",
                title = "Священная война",
                author = "Александров А. В.",
                authorId = "1",
                year = 1941,
                originalAudioUrl = "original_tracks/war_songs/sacred_war.mp3",
                restoredAudioUrl = "restored_tracks/war_songs/sacred_war.mp3",
                coverImage = "https://via.placeholder.com/300?text=Священная+война",
                tags = new[] { 
                    new { id = "1", name = "Патриотические" },
                    new { id = "3", name = "Фронт" }
                }
            },
            new {
                id = "2",
                title = "Катюша",
                author = "Блантер М. И.",
                authorId = "2",
                year = 1943,
                originalAudioUrl = "original_tracks/war_songs/katyusha.mp3",
                restoredAudioUrl = "restored_tracks/war_songs/katyusha.mp3",
                coverImage = "https://via.placeholder.com/300?text=Катюша",
                tags = new[] { 
                    new { id = "1", name = "Патриотические" },
                    new { id = "5", name = "Родина" }
                }
            },
            new {
                id = "3",
                title = "День Победы",
                author = "Тухманов Д. Ф.",
                authorId = "3",
                year = 1945,
                originalAudioUrl = "original_tracks/war_songs/victory_day.mp3",
                restoredAudioUrl = "restored_tracks/war_songs/victory_day.mp3",
                coverImage = "https://via.placeholder.com/300?text=День+Победы",
                tags = new[] { 
                    new { id = "2", name = "Победа" },
                    new { id = "4", name = "Подвиг" }
                }
            }
        };
        
        return Ok(recordings);
    }

    [HttpGet("api/statistics")]
    public IActionResult GetStatistics()
    {
        _logger.LogInformation("Запрос к заглушке api/statistics");
        
        var statistics = new
        {
            totalRecordings = 3,
            totalAuthors = 3,
            recordingsByYear = new[]
            {
                new { year = 1941, count = 1 },
                new { year = 1943, count = 1 },
                new { year = 1945, count = 1 }
            },
            recordingsByTag = new[]
            {
                new { tag = "Патриотические", count = 2 },
                new { tag = "Победа", count = 1 },
                new { tag = "Фронт", count = 1 },
                new { tag = "Подвиг", count = 1 },
                new { tag = "Родина", count = 1 }
            },
            popularAuthors = new[]
            {
                new { name = "Александров А. В.", count = 1 },
                new { name = "Блантер М. И.", count = 1 },
                new { name = "Тухманов Д. Ф.", count = 1 }
            }
        };
        
        return Ok(statistics);
    }
} 