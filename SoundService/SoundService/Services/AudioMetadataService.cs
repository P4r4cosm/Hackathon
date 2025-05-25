using Microsoft.EntityFrameworkCore;
using SoundService.Data;
using SoundService.Models;
using TagLib;

namespace SoundService.Services;

public class AudioMetadataService
{
    private readonly ApplicationDbContext _context;

    public AudioMetadataService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AudioRecord?> CreateAudioRecordFromMetadata(string filePath, string fileName, string pathInMinio)
    {
        var file = TagLib.File.Create(filePath);

        // Поиск или создание автора
        var authorName = file.Tag.FirstPerformer ?? "Unknown Artist";
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
        var albumTitle = file.Tag.Album ?? "Unknown Album";
        var album = await _context.Albums
                        .FirstOrDefaultAsync(a => a.Title == albumTitle && a.AuthorId == author.Id)
                    ?? new Album
                    {
                        Title = albumTitle,
                        AuthorId = author.Id, // Привязка по внешнему ключу
                        Author = author,
                        Year = (int)file.Tag.Year// Навигационное свойство
                    };

        // Проверка, добавлен ли альбом в контекст (если он новый)
        if (album.Id == 0)
        {
            _context.Albums.Add(album);
            await _context.SaveChangesAsync(); // Сохранение нового альбома
        }

        // Поиск или создание жанра
        var rawGenres = file.Tag.Genres ?? new string[] { "Unknown Genre" };

        var genreNames = new List<string>();

        foreach (var genre in rawGenres)
        {
            // Разделяем жанры по запятой и убираем лишние пробелы
            var splitGenres = genre.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(g => g.Trim())
                .Where(g => !string.IsNullOrWhiteSpace(g));

            genreNames.AddRange(splitGenres);
        }

        // Убираем дубликаты 
        genreNames = genreNames.Distinct().ToList();
        var audioGenres = new List<AudioGenre>();

        foreach (var genreName in genreNames)
        {
            // Поиск существующего жанра или создание нового
            var genre = await _context.Genres
                            .FirstOrDefaultAsync(g => g.Name == genreName) 
                        ?? new Genre { Name = genreName };

            if (genre.Id == 0)
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
        
        var audioRecord = new AudioRecord
        {
            Title = file.Tag.Title ?? Path.GetFileNameWithoutExtension(fileName),
            FilePath = pathInMinio,
            ModerationStatus = new ModerationStatus(){State = ModerationState.Pending},
            Year = (int)file.Tag.Year,
            Author = author,
            Album = album,
            AudioGenres = audioGenres,
            Duration = file.Properties.Duration,
            AudioKeywords = new List<AudioKeyword>(), 
            AudioThematicTags = new List<AudioThematicTag>()
        };

        return audioRecord;
    }
}