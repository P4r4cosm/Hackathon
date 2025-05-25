using Microsoft.AspNetCore.Mvc;
using SoundService.Repositories;
using SoundService.Services;

namespace SoundService.Controllers;

[Route("api/[controller]")]
[ApiController]
public class EditController : ControllerBase
{
    private readonly ILogger<EditController> _logger;

    private readonly AudioRecordRepository _audioRecordRepository;

    private MinIOService _minIOService;

    private readonly AuthorRepository _authorRepository;

    private readonly GenreRepository _genreRepository;

    public EditController(ILogger<EditController> logger, AudioRecordRepository audioRecordRepository
        , MinIOService minIOService, AuthorRepository authorRepository, GenreRepository genreRepository)
    {
        _logger = logger;
        _audioRecordRepository = audioRecordRepository;
        _minIOService = minIOService;
        _authorRepository = authorRepository;
        _genreRepository = genreRepository;
    }


    /// <summary>
    /// изменяет полностью все записи с автором на новое указанное имя
    /// </summary>
    /// <param name="authorId"></param>
    /// <param name="authorName"></param>
    /// <returns></returns>
    [HttpPatch("author")]
    public async Task<IActionResult> EditAuthorName(int authorId, string authorName)
    {
        await _authorRepository.EditAuthorName(authorId, authorName);
        return Ok();
    }

    /// <summary>
    /// изменяет название жанра, все записи с этим жанром на новое указанное имя
    /// </summary>
    /// <param name="genreId"></param>
    /// <param name="genreName"></param>
    /// <returns></returns>
    [HttpPatch("genre")]
    public async Task<IActionResult> EditGenreName(int genreId, string genreName)
    {
        await _genreRepository.EditGenreName(genreId, genreName);
        return Ok();
    }


    /// <summary>
    /// изменяет Track Title
    /// </summary>
    /// <param name="id"></param>
    /// <param name="title"></param>
    /// <returns></returns>
    [HttpPatch("audio_title")]
    public async Task<IActionResult> EditAudioTitle(int id, string title)
    {
        await _audioRecordRepository.EditTitleAsync(id, title);
        return Ok();
    }
}