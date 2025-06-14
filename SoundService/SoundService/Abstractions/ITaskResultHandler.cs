using SoundService.Models;

namespace SoundService.Abstractions;

public interface ITaskResultHandler
{
    Task HandleDemucsResultAsync(DemucsResultData result);
    Task HandleWhisperResultAsync(WhisperResultData result);
    Task HandleFailedResultAsync(TaskResultBase result, string originalMessage);
}