using System.ComponentModel.DataAnnotations;

namespace SoundService.Models;

public class UploadAudioDto
{
    [Required] 
    public IFormFile File { get; set; }

    [Required] 
    public string FolderToUpload { get; set; } // Возможно, это должно определяться на бэкенде? Или это часть пути пользователя?

    // Опциональные метаданные от пользователя
    public string? Title { get; set; } // Пользователь может указать название, даже если в тегах другое
    public string? AlbumName { get; set; }
    public string? ArtistName { get; set; }
    public int? Year { get; set; }
}