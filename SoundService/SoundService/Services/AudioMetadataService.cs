using Microsoft.EntityFrameworkCore;
using SoundService.Data;
using SoundService.Models;
using TagLib;

namespace SoundService.Services;

public class AudioMetadataService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AudioMetadataService>? _logger;

    public AudioMetadataService(ApplicationDbContext context, ILogger<AudioMetadataService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<AudioRecord?> CreateAudioRecordFromMetadata(string filePath, string fileName, string pathInMinio,
        UploadAudioDto data)
    {
        TagLib.File? file = null;
        try
        {
            if (System.IO.File.Exists(filePath)) // Убедимся, что файл существует перед попыткой чтения
            {
                file = TagLib.File.Create(filePath);
            }
            else
            {
                _logger?.LogWarning($"Файл не найден по пути: {filePath}. Обработка без метаданных файла.");
            }
        }
        catch (TagLib.CorruptFileException ex)
        {
            _logger?.LogWarning(ex,
                $"Не удалось прочитать метаданные из поврежденного файла: {filePath}. Используются данные из DTO.");
        }
        catch (TagLib.UnsupportedFormatException ex)
        {
            _logger?.LogWarning(ex,
                $"Не удалось прочитать метаданные из файла неподдерживаемого формата: {filePath}. Используются данные из DTO.");
        }
        catch (Exception ex) // Перехват других исключений при чтении метаданных
        {
            _logger?.LogError(ex,
                $"Непредвиденная ошибка при чтении метаданных из: {filePath}. Используются данные из DTO.");
        }

        string authorName;
        // Поиск или создание автора
        if (!string.IsNullOrWhiteSpace(data.ArtistName))
        {
            authorName = data.ArtistName;
        }
        else
        {
            authorName = file?.Tag?.FirstPerformer ?? "Unknown Artist";
        }

        var author = await _context.Authors
                         .FirstOrDefaultAsync(a => a.Name == authorName)
                     ?? new Author { Name = authorName };
        // Проверка, добавлен ли автор в контекст (если он новый)
        if (author.Id == 0)
        {
            _context.Authors.Add(author);
            await _context.SaveChangesAsync(); // Убедимся, что у нового автора есть ID
        }

        // Поиск или создание альбома 
        string albumTitle;
        if (!string.IsNullOrWhiteSpace(data.AlbumName))
        {
            albumTitle = data.AlbumName;
        }
        else
        {
            albumTitle = file?.Tag?.Album ?? "Unknown Album";
        }


        int albumYear = data.Year ?? (int)(file?.Tag?.Year ?? 0);


        if (albumYear == 0 &&
            file?.Tag?.Year > 0) // Отдаем предпочтение году из метаданных, если год из DTO не задан или 0
        {
            albumYear = (int)file.Tag.Year;
        }


        var album = await _context.Albums
                    .FirstOrDefaultAsync(a => a.Title == albumTitle && a.AuthorId == author.Id)
                ?? new Album
                {
                    Title = albumTitle,
                    AuthorId = author.Id, // Явно устанавливаем внешний ключ
                    Author = author, // Навигационное свойство
                    Year = albumYear
                };

        if (album.Id == 0) // Если альбом новый
        {
            _context.Albums.Add(album);
            await _context.SaveChangesAsync(); // Сохранение нового альбома для получения ID
        }


        string[] rawGenresInput;
        if (data.GenreNames != null && data.GenreNames.Any(gn => !string.IsNullOrWhiteSpace(gn)))
        {
            rawGenresInput = data.GenreNames;
        }
        else
        {
            rawGenresInput = file?.Tag?.Genres ?? new string[] { "Unknown Genre" };
        }

        var processedGenreNames = new List<string>();
        foreach (var genreStr in rawGenresInput)
        {
            if (string.IsNullOrWhiteSpace(genreStr)) continue;

            processedGenreNames.AddRange(
                genreStr
                    .Split(new[] { ',', ';' },
                        StringSplitOptions.RemoveEmptyEntries) // Разделяем по запятой или точке с запятой
                    .Select(g => g.Trim())
                    .Where(g => !string.IsNullOrWhiteSpace(g))
            );
        }

        if (!processedGenreNames.Any())
        {
            processedGenreNames.Add("Unknown Genre");
        }

        // Убираем дубликаты, не учитывая регистр
        var distinctGenreNames = processedGenreNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var audioGenres = new List<AudioGenre>();

        foreach (var genreName in distinctGenreNames)
        {
            var genre = await _context.Genres.FirstOrDefaultAsync(g => g.Name == genreName)
                        ?? new Genre { Name = genreName };

            if (genre.Id == 0) // Если жанр новый
            {
                _context.Genres.Add(genre);
                await _context.SaveChangesAsync(); // Сохраняем сразу, чтобы получить Id
            }

            // Создаем связь AudioGenre
            audioGenres.Add(new AudioGenre
            {
                GenreId = genre.Id,
                Genre = genre
            });
        }

        string recordTitle;
        if (!string.IsNullOrWhiteSpace(data.Title)) // Сначала проверяем DTO
        {
            recordTitle = data.Title;
        }
        else
        {
            recordTitle = file?.Tag?.Title ?? Path.GetFileNameWithoutExtension(fileName);
        }

        TimeSpan duration = file?.Properties?.Duration ?? TimeSpan.Zero;

        var audioRecord = new AudioRecord
        {
            Title = recordTitle,
            FilePath = pathInMinio,
            ModerationStatus = new ModerationStatus()
                { State = ModerationState.Pending }, // Предполагаем, что новый статус всегда Pending
            Year = albumYear,
            AuthorId = author.Id, // Явно устанавливаем FK
            Author = author, // Навигационное свойство
            AlbumId = album?.Id, // Явно устанавливаем FK (может быть null)
            Album = album, // Навигационное свойство (может быть null)
            AudioGenres = audioGenres, // Заполняем список связей
            Duration = duration
            //AudioKeywords = new List<AudioKeyword>(),
            //AudioThematicTags = new List<AudioThematicTag>()
        };

        return audioRecord;
    }
}