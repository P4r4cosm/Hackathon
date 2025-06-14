using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Microsoft.EntityFrameworkCore;
using SoundService.Data;
using SoundService.Models;

namespace SoundService.Repositories;

public class AudioRecordRepository
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ElasticsearchClient _elasticClient;
    private readonly ILogger<AudioRecordRepository> _logger;

    public AudioRecordRepository(ApplicationDbContext dbContext, ElasticsearchClient elasticClient,
        ILogger<AudioRecordRepository> logger)
    {
        _dbContext = dbContext;
        _elasticClient = elasticClient;
        _logger = logger;
    }

    public async Task EditTitleAsync(int id, string title)
    {
        var track = await _dbContext.AudioRecords.FirstOrDefaultAsync(a => a.Id == id);
        if (track != null)
        {
            var oldName =
                track.Title; // Сохраняем старое имя на случай, если оно понадобится для логирования или сложной логики
            track.Title = title;
            if (oldName == title)
            {
                _logger.LogInformation(
                    "Название трека ID {TrackId} не изменилось ('{Title}'). Обновление Elasticsearch пропущено.",
                    id, title);
                return; // Выходим, если имя не изменилось
            }

            await _dbContext.SaveChangesAsync();
            // Получаем аудиозапись из Elasticsearch (для проверки существования)
            var elasticRecord = await GetTrackTextByIdAsync(id);
            if (elasticRecord == null)
            {
                _logger.LogWarning(
                    "Документ с ID {Id} не найден в Elasticsearch. Продолжаем обновление только в базе данных.", id);
            }

            // Обновляем поле Title в Elasticsearch
            elasticRecord.Title = title;

            // Сохраняем изменения обратно в Elasticsearch
            await SaveAsync(elasticRecord);

            _logger.LogInformation(
                "Название аудиозаписи с ID {Id} успешно обновлено в базе данных и Elasticsearch на '{Title}'.", id,
                title);
        }
    }


    /// <summary>
    /// Обно
    /// </summary>
    /// <param name="audioRecordDTO"></param>
    /// <exception cref="Exception"></exception>
    public async Task EditAudioRecordAsync(AudioRecordEditDTO audioRecordDTO)
    {
        //получаем реальную запись
        var audio = await _dbContext.AudioRecords.Include(a => a.AudioGenres)
            .Include(a => a.Author)
            .Include(a => a.Album)
            .FirstOrDefaultAsync(a => a.Id == audioRecordDTO.Id);
        if (audio == null)
            throw new Exception($"Audio record with id {audioRecordDTO.Id} not found");

        // если указано имя создаём автора
        if (!string.IsNullOrEmpty(audioRecordDTO.AuthorName))
        {
            var author = await _dbContext.Authors.FirstOrDefaultAsync(a => a.Name == audioRecordDTO.AuthorName);
            if (author == null)
            {
                author = new Author { Name = audioRecordDTO.AuthorName };
                _dbContext.Authors.Add(author);
                //await _dbContext.SaveChangesAsync();
            }

            audio.Author = author;
        }
        //если указан id проверяем существует ли данный автор и отличается ли он от id изначального
        else if (audioRecordDTO.AuthorId.HasValue)
        {
            if (audio.AuthorId == audioRecordDTO.AuthorId.Value)
            {
                _logger.LogInformation("The author does not change");
            }
            else
            {
                if (await _dbContext.Authors.AnyAsync(a => a.Id == audioRecordDTO.AuthorId.Value) == false)
                {
                    _logger.LogInformation("The author does not exist");
                }
                else
                {
                    audio.AuthorId = audioRecordDTO.AuthorId.Value;
                }
            }
        }

        // Check and create album if needed
        if (!string.IsNullOrEmpty(audioRecordDTO.AlbumTitle))
        {
            var album = await _dbContext.Albums.FirstOrDefaultAsync(a => a.Title == audioRecordDTO.AlbumTitle);
            if (album == null)
            {
                album = new Album()
                {
                    Title = audioRecordDTO.AlbumTitle,
                    AuthorId = audio.AuthorId,
                    Year = audioRecordDTO.Year ?? audio.Year,
                };
                _dbContext.Albums.Add(album);
                //await _dbContext.SaveChangesAsync();
            }

            audio.Album = album; // Присваиваем навигационное свойство. EF Core обработает AlbumId.
        }
        //если указан id проверяем существует ли данный автор и отличается ли он от id изначального
        else
        {
            if (audioRecordDTO.AuthorId.HasValue)
            {
                if (audio.AlbumId == audioRecordDTO.AlbumId.Value)
                {
                    _logger.LogInformation("The album does not change");
                }
                else
                {
                    if (await _dbContext.Albums.AnyAsync(a => a.Id == audioRecordDTO.AlbumId.Value) == false)
                    {
                        _logger.LogInformation("The album does not exist");
                    }
                    else
                    {
                        audio.AlbumId = audioRecordDTO.AlbumId.Value;
                    }
                }
            }
        }

        // Update genres in PostgreSQL
        if (audioRecordDTO.Genres != null && audioRecordDTO.Genres.Any())
        {
            // Удаляем существующие жанры
            audio.AudioGenres.Clear();
            // Дедупликация DTO по имени жанра (регистронезависимо)
            var distinctGenreDtos = audioRecordDTO.Genres
                .Where(dto => dto != null && !string.IsNullOrWhiteSpace(dto.Name)) // Пропускаем некорректные DTO
                .GroupBy(dto => dto.Name.Trim(),
                    StringComparer.OrdinalIgnoreCase) // Группируем по имени (убираем пробелы, игнорируем регистр)
                .Select(group => group.First()) // Берем первый уникальный DTO из каждой группы
                .ToList();

            if (distinctGenreDtos.Any())
            {
                foreach (var genreDto in distinctGenreDtos) // Итерируемся по уникальным DTO жанров
                {
                    var genreName = genreDto.Name.Trim(); // Используем очищенное имя
                    var genre = await _dbContext.Genres.FirstOrDefaultAsync(g => g.Name == genreName);
                    if (genre == null)
                    {
                        genre = new Genre { Name = genreName };
                        _dbContext.Genres.Add(genre); // EF отследит и добавит новый жанр
                    }

                    // Добавляем новую связь. EF Core должен сам установить AudioRecordId из audio.Id
                    // и GenreId из genre.Id (или запланировать это, если genre новый).
                    audio.AudioGenres.Add(new AudioGenre
                    {
                        // AudioRecord = audio, // Можно указать явно, но EF обычно справляется сам при добавлении в коллекцию
                        Genre = genre
                    });
                }
            }
        }


        // Update other fields
        audio.Title = audioRecordDTO.Title ?? audio.Title;
        audio.Year = audioRecordDTO.Year ?? audio.Year;
        audio.ModerationStatus.State = audioRecordDTO.ModerationStatus ?? audio.ModerationStatus.State;

        // Get corresponding elastic document
        var elasticRecord = await GetTrackTextByIdAsync(audio.Id);
        if (elasticRecord != null)
        {
            // Update elastic document fields
            elasticRecord.Title = audio.Title;
            elasticRecord.AuthorId = audio.AuthorId;
            elasticRecord.AuthorName = audio.Author.Name;
            elasticRecord.AlbumId = audio.AlbumId;
            elasticRecord.AlbumTitle = audio.Album.Title;
            elasticRecord.Year = audio.Year;
            elasticRecord.ModerationStatus = audio.ModerationStatus.State;
            if (audioRecordDTO.TranscriptSegments != null)
                elasticRecord.TranscriptSegments = audioRecordDTO.TranscriptSegments;
            if (audioRecordDTO.FullText != null)
                elasticRecord.FullText = audioRecordDTO.FullText;
            if (audio.AudioGenres.Any())
            {
                // Убедимся, что у каждого ag.Genre не null, если Genre не был включен явно при загрузке ag.Genre
                // В данном случае, мы создаем или находим Genre, так что он должен быть.
                elasticRecord.Genres = audio.AudioGenres
                    .Where(ag => ag.Genre != null) // Дополнительная проверка на всякий случай
                    .Select(ag => new GenreDto() { Id = ag.GenreId, Name = ag.Genre.Name }).ToList();
            }

            if (audioRecordDTO.ThematicTags != null)
                elasticRecord.ThematicTags = audioRecordDTO.ThematicTags;
            if (audioRecordDTO.Keywords != null)
                elasticRecord.Keywords = audioRecordDTO.Keywords;
            await SaveAsync(elasticRecord);
        }

        await _dbContext.SaveChangesAsync();
    }

    public async Task<AudioRecord> SaveAsync(AudioRecord audioRecord)
    {
        _dbContext.AudioRecords.Add(audioRecord); // Добавляем аудиозапись в контекст
        await _dbContext.SaveChangesAsync(); // Сохраняем изменения в базу
        return audioRecord;
    }

    public async Task SaveAsync(AudioRecordForElastic audioRecord)
    {
        var bulkResponse = await _elasticClient.BulkAsync(b => b
                .Index(audioRecord) // Имя индекса(audioRecord)
        );
        if (!bulkResponse.IsValidResponse)
        {
            _logger.LogError("Error saving to Elasticsearch: {ErrorReason}", bulkResponse.DebugInformation);
        }
        else
        {
            _logger.LogInformation("Elasticsearch save complete.");
        }
    }

    public async Task SaveAsync(IEnumerable<AudioRecordForElastic> audioRecords)
    {
        var bulkResponse = await _elasticClient.BulkAsync(b => b
            .IndexMany(audioRecords)
        );
        if (!bulkResponse.IsValidResponse)
        {
            _logger.LogError("Error saving multiple documents to Elasticsearch: {ErrorReason}",
                bulkResponse.DebugInformation);
        }
        else
        {
            _logger.LogInformation("Elasticsearch bulk save complete. Saved {count} documents",
                audioRecords.Count());
        }
    }

    public async Task<AudioRecord?> GetTrackByIdAsync(int id)
    {
        return await _dbContext.AudioRecords.FirstOrDefaultAsync(ar => ar!.Id == id);
    }

    public async Task<List<AudioRecordForElastic>> GetAllAsync(int from = 0, int size = 20)
    {
        var response = await _elasticClient.SearchAsync<AudioRecordForElastic>(s => s
            .Index("audio_records")
            .From(from)
            .Size(size)
            .Query(q => q.MatchAll(new MatchAllQuery()))
        );

        if (!response.IsValidResponse)
        {
            _logger.LogError("Error getting documents from Elasticsearch: {ErrorReason}",
                response.DebugInformation);
            return new List<AudioRecordForElastic>();
        }

        return response.Documents.ToList();
    }

    public async Task<List<Author>> GetAllAuthorsAsync()
    {
        return await _dbContext.Authors.ToListAsync();
    }

    public async Task<List<Genre>> GetAllGenresAsync()
    {
        return await _dbContext.Genres.ToListAsync();
    }

    public async Task<List<ThematicTag>> GetAllThematicTagsAsync()
    {
        return await _dbContext.ThematicTags.ToListAsync();
    }

    public async Task<List<Keyword>> GetAllKeywordsAsync()
    {
        return await _dbContext.Keywords.ToListAsync();
    }

    public async Task<AudioRecordForElastic?> GetTrackTextByIdAsync(int id)
    {
        var response = await _elasticClient.GetAsync<AudioRecordForElastic>(id);
        if (!response.IsValidResponse)
        {
            _logger.LogError("Error getting document from Elasticsearch: {ErrorReason}", response.DebugInformation);
            return null;
        }

        if (!response.Found)
        {
            _logger.LogWarning("Document with id {Id} not found in Elasticsearch", id);
            return null;
        }

        return response.Source;
    }

    public async Task<List<AudioRecordForElastic?>> GetTracksByAuthor(int id, int from, int size)
    {
        var response = await _elasticClient.SearchAsync<AudioRecordForElastic>(s => s
            .Index("audio_records")
            .From(from)
            .Size(size)
            .Query(q => q.Term(t => t.Field(f => f.AuthorId).Value(id)))
        );

        if (!response.IsValidResponse)
        {
            _logger.LogError("Error getting documents from Elasticsearch: {ErrorReason}",
                response.DebugInformation);
            return new List<AudioRecordForElastic?>();
        }

        return response.Documents.ToList();
    }

    public async Task<List<AudioRecordForElastic?>> GetTracksByYear(int year, int from, int size)
    {
        var response = await _elasticClient.SearchAsync<AudioRecordForElastic>(s => s
            .Index("audio_records")
            .From(from)
            .Size(size)
            .Query(q => q.Term(t => t.Field(f => f.Year.ToString()).Value(year.ToString())))
        );

        if (!response.IsValidResponse)
        {
            _logger.LogError("Error getting documents from Elasticsearch: {ErrorReason}",
                response.DebugInformation);
            return new List<AudioRecordForElastic?>();
        }

        return response.Documents.ToList();
    }

    public async Task<List<AudioRecordForElastic>> GetTracksByGenreId(int genreId, int from, int size)
    {
        var response = await _elasticClient.SearchAsync<AudioRecordForElastic>(s => s
            .Index("audio_records")
            .From(from)
            .Size(size)
            .Query(q => q
                .Nested(n => n
                    .Path(p => p.Genres) // Указываем путь до nested-объектов
                    .Query(nq => nq
                        .Term(t => t
                                .Field(f => f.Genres[0].Id) // Поле Id внутри Genre
                                .Value(genreId.ToString()) // TermQuery требует строку
                        )
                    )
                )
            )
        );

        if (!response.IsValidResponse)
        {
            _logger.LogError("Error fetching tracks by genre from Elasticsearch: {DebugInfo}",
                response.DebugInformation);
            return new List<AudioRecordForElastic>();
        }

        return response.Documents.ToList();
    }

    public async Task<List<AudioRecordForElastic>> GetTracksByKeywords(IEnumerable<string> keywords, int from,
        int size)
    {
        var response = await _elasticClient.SearchAsync<AudioRecordForElastic>(s => s
            .Index("audio_records") // Указываем индекс
            .From(from) // Для пагинации
            .Size(size) // Для пагинации
            .Query(q => q
                .Terms(t => t // Используем Terms query
                        .Field(f => f.Keywords) // Указываем поле для поиска

                        //.Terms(new TermsQueryField(keywords.Cast<object>().ToList())) // Для NEST 7.x
                        .Terms(new TermsQueryField(keywords.Select(name => FieldValue.String(name))
                            .ToArray())) // Для Elastic.Clients.Elasticsearch 8.x
                )
            )
        );
        if (!response.IsValidResponse)
        {
            _logger.LogError("Error fetching tracks by genre from Elasticsearch: {DebugInfo}",
                response.DebugInformation);
            return new List<AudioRecordForElastic>();
        }

        return response.Documents.ToList();
    }

    public async Task EditAudioRecordAsyncByPath(string path, string fulltext, List<TranscriptSegment> segments)
    {
        var response = await _elasticClient.UpdateByQueryAsync<AudioRecordForElastic>("audio_records", req => req
            .Query(q => q
                .Term(t => t.Field("path.keyword").Value(path))
            )
            // 2. SCRIPT: Описать, какие поля и как нужно обновить.
            .Script(s => s
                // Исходный код скрипта на языке Painless
                .Source(
                    "ctx._source.fullText = params.newFullText; ctx._source.transcriptSegments = params.newSegments;")
                // Передача параметров в скрипт. Это безопасно и эффективно.
                .Params(p => p
                    .Add("newFullText", fulltext)
                    .Add("newSegments", segments)
                )
            )
            // Опционально: не останавливаться при конфликтах версий
            .Conflicts(Conflicts.Proceed)
            // Опционально, но рекомендуется: дождаться завершения операции
            .WaitForCompletion(true)
        );

        // 3. ПРОВЕРКА РЕЗУЛЬТАТА
        if (response.IsValidResponse)
        {
            Console.WriteLine(
                $"Операция завершена. Найдено документов: {response.Total}. Обновлено: {response.Updated}.");
            if (response.Updated == 0 && response.Total > 0)
            {
                Console.WriteLine("Документ был найден, но не обновлен. Возможно, данные уже были идентичны.");
            }
            else if (response.Total == 0)
            {
                Console.WriteLine($"Внимание: Документ с путем '{path}' не найден.");
            }
        }
        else
        {
            // Если что-то пошло не так, выводим отладочную информацию
            Console.WriteLine("Ошибка при выполнении UpdateByQuery:");
            Console.WriteLine(response.DebugInformation);
            if (response.ElasticsearchServerError != null)
            {
                Console.WriteLine($"Ошибка от сервера Elasticsearch: {response.ElasticsearchServerError.Error.Reason}");
            }
        }
    }

    public async Task<List<AudioRecordForElastic>> GetTracksByThematicTags(IEnumerable<string> tags, int from,
        int size)
    {
        var response = await _elasticClient.SearchAsync<AudioRecordForElastic>(s => s
            .Index("audio_records") // Указываем индекс
            .From(from) // Для пагинации
            .Size(size) // Для пагинации
            .Query(q => q
                .Terms(t => t // Используем Terms query
                        .Field(f => f.ThematicTags) // Указываем поле для поиска

                        //.Terms(new TermsQueryField(keywords.Cast<object>().ToList())) // Для NEST 7.x
                        .Terms(new TermsQueryField(tags.Select(name => FieldValue.String(name))
                            .ToArray())) // Для Elastic.Clients.Elasticsearch 8.x
                )
            )
        );
        if (!response.IsValidResponse)
        {
            _logger.LogError("Error fetching tracks by genre from Elasticsearch: {DebugInfo}",
                response.DebugInformation);
            return new List<AudioRecordForElastic>();
        }

        return response.Documents.ToList();
    }
}