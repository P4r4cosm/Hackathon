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

    public async Task<AudioRecord> CreateAudioRecordFromMetadata(string filePath, string fileName)
    {
        var file = TagLib.File.Create(filePath);

        // Поиск или создание автора
        var authorName = file.Tag.FirstPerformer ?? "Unknown Artist";
        var author = await _context.Authors
                         .FirstOrDefaultAsync(a => a.Name == authorName) 
                     ?? new Author { Name = authorName };

        // Проверка, добавлен ли автор в контекст (если он новый)
        if (author.Id == Guid.Empty)
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
                        Author = author       // Навигационное свойство
                    };

        // Проверка, добавлен ли альбом в контекст (если он новый)
        if (album.Id == Guid.Empty)
        {
            _context.Albums.Add(album);
            await _context.SaveChangesAsync(); // Сохранение нового альбома
        }

        // Поиск или создание жанра
        var genreName = file.Tag.FirstGenre ?? "Unknown Genre";
        var genre = await _context.Genres
                        .FirstOrDefaultAsync(g => g.Name == genreName)
                    ?? new Genre { Name = genreName };

        if (genre.Id == Guid.Empty)
        {
            _context.Genres.Add(genre);
            await _context.SaveChangesAsync();
        }

        var audioRecord = new AudioRecord
        {
            Title = file.Tag.Title ?? Path.GetFileNameWithoutExtension(fileName),
            FilePath = filePath,
            ModerationStatus = new ModerationStatus(){State = ModerationState.Pending},
            Author = author,
            Album = album,
            Genre = genre,
            AudioKeywords = new List<AudioKeyword>(), 
            AudioThematicTags = new List<AudioThematicTag>()
        };

        return audioRecord;
    }
}