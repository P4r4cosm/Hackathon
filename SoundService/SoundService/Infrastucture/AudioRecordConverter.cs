using SoundService.Models;

namespace SoundService.Infrastucture;

public static class AudioRecordConverter
{
    public static AudioRecordForElastic ToAudioRecordForElastic(AudioRecord audioRecord)
    {
        var audioRecordForElastic = new AudioRecordForElastic()
        {
            Id = audioRecord.Id,
            Title = audioRecord.Title,
            AuthorName = audioRecord.Author.Name,
            AuthorId = audioRecord.AuthorId,
            Genres = audioRecord.AudioGenres.Select(ag => ag.Genre.ToGenreDto()).ToList(),
            Year = audioRecord.Year,
            AlbumTitle = audioRecord.Album.Title,
            AlbumId = audioRecord.AlbumId,
            //ThematicTagIds = audioRecord.AudioThematicTags.Select(at => at.ThematicTagId).ToList(),
            //ThematicTags = audioRecord.AudioThematicTags.Select(at => at.ThematicTag.Name).ToList(),
            //Keywords = audioRecord.AudioKeywords.Select(ak => ak.Keyword.Text).ToList(),
            UploadedAt = audioRecord.UploadedAt,
            Duration = audioRecord.Duration,
            Path=audioRecord.FilePath,
            ModerationStatus = audioRecord.ModerationStatus.State,
            TranscriptSegments = new List<TranscriptSegment>()
        };
        return audioRecordForElastic;
    }

    public static GenreDto ToGenreDto(this Genre genre)
    {
        var genreDto = new GenreDto()
        {
            Id = genre.Id,
            Name = genre.Name
        };
        return genreDto;
    }
}