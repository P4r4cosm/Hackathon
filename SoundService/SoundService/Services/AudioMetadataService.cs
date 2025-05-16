using SoundService.Models;
using TagLib;
using File = TagLib.File;

namespace SoundService.Services;

public class AudioMetadataService
{
    public AudioMetadata ExtractMetadata(string filePath, string fileName)
    {
        var file = File.Create(filePath);
        return new AudioMetadata
        {
            Title = file.Tag.Title ?? Path.GetFileNameWithoutExtension(fileName),
            Artist = file.Tag.FirstPerformer,
            Year = file.Tag.Year > 0 ? (int?)file.Tag.Year : null,
            Album = file.Tag.Album,
            Genre = file.Tag.FirstGenre,
            Duration = file.Properties.Duration,
            Bitrate = file.Properties.AudioBitrate,
            SampleRate = file.Properties.AudioSampleRate,
            Channels = file.Properties.AudioChannels,

            // Эти поля будут заполнены позже через ASR / NLP
            Transcript = "", // Нужен Speech-to-Text сервис
            Keywords = new List<string>(),
            ThematicTags = new List<string>(),
            ModerationStatus = "pending"
        };
    }
}