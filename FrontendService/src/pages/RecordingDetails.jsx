import React, { useState, useEffect } from 'react';
import { useParams, Link } from 'react-router-dom';
import { useSelector, useDispatch } from 'react-redux';
import { DetailsHeader, Error, Loader, DualAudioPlayer } from '../components';
import { TranscriptionEditor } from '../components/Admin';

import { setActiveSong, playPause } from '../redux/features/playerSlice';
import { 
  useGetAllRecordingsQuery,
  useDownloadAudioQuery
} from '../redux/services/audioArchiveApi';

const RecordingDetails = () => {
  const dispatch = useDispatch();
  const { recordingId } = useParams();
  const { activeSong, isPlaying } = useSelector((state) => state.player);
  const [isAdmin, setIsAdmin] = useState(false); // В реальном приложении нужно проверять роль пользователя
  const [currentTime, setCurrentTime] = useState(0);
  const [audioPath, setAudioPath] = useState(null);

  // Получаем все записи
  const { 
    data: recordings,
    isFetching: isFetchingRecordings,
    error: recordingsError
  } = useGetAllRecordingsQuery({});
  
  // Находим текущую запись по ID
  const recording = recordings?.find(rec => rec.id === parseInt(recordingId));
  
  // Получаем аудиофайл по пути
  const {
    data: audioBlob,
    isFetching: isFetchingAudio
  } = useDownloadAudioQuery(
    recording?.path,
    { skip: !recording?.path }
  );
  
  // Создаем URL для воспроизведения из blob
  useEffect(() => {
    if (audioBlob) {
      const audioURL = URL.createObjectURL(audioBlob);
      setAudioPath(audioURL);
      
      return () => {
        URL.revokeObjectURL(audioURL);
      };
    }
  }, [audioBlob]);

  // Функции для работы с плеером
  const handlePauseClick = () => {
    dispatch(playPause(false));
  };

  const handlePlayClick = () => {
    if (recording) {
      const adaptedRecording = {
        ...recording,
        title: recording.title,
        author: recording.authorName,
        authorId: recording.authorId,
        audio: audioPath
      };
      
      dispatch(setActiveSong({ song: adaptedRecording, data: recordings, i: 0 }));
      dispatch(playPause(true));
    }
  };

  const handleTimeUpdate = (time) => {
    setCurrentTime(time);
  };

  // Функция для поиска текущей строки текста на основе времени воспроизведения
  const getCurrentLyricIndex = () => {
    if (!recording?.transcriptSegments || recording.transcriptSegments.length === 0) return -1;
    
    for (let i = recording.transcriptSegments.length - 1; i >= 0; i--) {
      const segment = recording.transcriptSegments[i];
      if (segment.start <= currentTime) {
        return i;
      }
    }
    
    return -1;
  };

  const currentLyricIndex = getCurrentLyricIndex();

  if (isFetchingRecordings || (isFetchingAudio && !audioPath)) {
    return <Loader title="Загрузка данных о записи..." />;
  }

  if (recordingsError || !recording) {
    return <Error message="Ошибка загрузки записи или запись не найдена" />;
  }

  return (
    <div className="flex flex-col">
      <div className="mt-4 flex flex-col md:flex-row items-start">
        {/* Обложка и основная информация */}
        <div className="flex-shrink-0 md:mr-8 mb-6 md:mb-0">
          <div className="relative w-full max-w-[300px] h-[300px]">
            <img 
              className="w-full h-full object-cover rounded-lg"
              src="https://via.placeholder.com/400?text=Военная+запись" 
              alt={recording.title} 
            />
            <button 
              onClick={isPlaying && activeSong?.id === recording.id ? handlePauseClick : handlePlayClick}
              className="absolute bottom-2 right-2 w-12 h-12 rounded-full bg-blue-600 text-white flex items-center justify-center"
            >
              <i className={`fa fa-${isPlaying && activeSong?.id === recording.id ? 'pause' : 'play'}`} />
            </button>
          </div>
        </div>

        <div className="flex-1">
          {/* Информация о записи */}
          <h2 className="text-white text-3xl font-bold">{recording.title}</h2>
          
          <Link to={`/authors/${recording.authorId}`} className="block text-gray-300 hover:text-cyan-400 mt-2">
            <span className="text-gray-400">Автор: </span>{recording.authorName}
          </Link>
          
          {recording.albumTitle && (
            <p className="text-gray-300 mt-1">
              <span className="text-gray-400">Альбом: </span>{recording.albumTitle}
            </p>
          )}
          
          <p className="text-gray-300 mt-1">
            <span className="text-gray-400">Год: </span>{recording.year || "Не указан"}
          </p>

          {recording.duration && (
            <p className="text-gray-300 mt-1">
              <span className="text-gray-400">Длительность: </span>{recording.duration}
            </p>
          )}
          
          <div className="mt-4 flex flex-wrap">
            {recording.thematicTags?.map((tag, index) => (
              <span 
                key={`tag-${index}`} 
                className="text-sm mr-2 mb-1 py-1 px-3 bg-blue-800/30 text-blue-200 rounded-full"
              >
                {tag}
              </span>
            ))}
          </div>

          {/* Жанры */}
          {recording.genres && recording.genres.length > 0 && (
            <div className="mt-2">
              <p className="text-gray-400">Жанры:</p>
              <div className="flex flex-wrap mt-1">
                {recording.genres.map((genre, index) => (
                  <span 
                    key={`genre-${index}`} 
                    className="text-sm mr-2 mb-1 py-1 px-3 bg-purple-800/30 text-purple-200 rounded-full"
                  >
                    {genre.name}
                  </span>
                ))}
              </div>
            </div>
          )}
          
          {/* Ключевые слова */}
          {recording.keywords && recording.keywords.length > 0 && (
            <div className="mt-2">
              <p className="text-gray-400">Ключевые слова:</p>
              <div className="flex flex-wrap mt-1">
                {recording.keywords.map((keyword, index) => (
                  <span 
                    key={`keyword-${index}`} 
                    className="text-sm mr-2 mb-1 py-1 px-3 bg-green-800/30 text-green-200 rounded-full"
                  >
                    {keyword}
                  </span>
                ))}
              </div>
            </div>
          )}
        </div>
      </div>

      {/* Плеер для аудио */}
      {audioPath && (
        <div className="my-8">
          <h3 className="text-white text-xl font-bold mb-4">Аудиозапись:</h3>
          <audio 
            controls 
            className="w-full" 
            onTimeUpdate={(e) => handleTimeUpdate(e.target.currentTime)}
          >
            <source src={audioPath} type="audio/mpeg" />
            Ваш браузер не поддерживает аудиоэлемент.
          </audio>
        </div>
      )}

      {/* Компонент для текста песни, с возможностью редактирования для админов */}
      {isAdmin ? (
        <TranscriptionEditor 
          recordingId={recordingId}
          initialText={recording.fullText}
          segments={recording.transcriptSegments}
        />
      ) : (
        <div className="mb-10">
          <h3 className="text-white text-xl font-bold mb-4">Текст записи:</h3>
          
          {recording.transcriptSegments && recording.transcriptSegments.length > 0 ? (
            <div className="text-gray-300 space-y-2">
              {recording.transcriptSegments.map((segment, index) => (
                <div 
                  key={`segment-${index}`} 
                  className={`flex p-1 rounded ${index === currentLyricIndex ? 'bg-blue-900/30' : ''}`}
                >
                  <span className="text-gray-500 w-16">[{segment.start.toFixed(1)}s]</span>
                  <span>{segment.text}</span>
                </div>
              ))}
            </div>
          ) : recording.fullText ? (
            <p className="text-gray-300 whitespace-pre-line">{recording.fullText}</p>
          ) : (
            <p className="text-gray-400">Текст для этой записи отсутствует.</p>
          )}
        </div>
      )}

      {/* Статус модерации (для админов) */}
      {isAdmin && recording.moderationStatus && (
        <div className="mb-10 p-4 bg-gray-800/50 rounded-lg">
          <h3 className="text-white text-xl font-bold mb-2">Статус модерации:</h3>
          <p className="text-gray-300">
            Статус: <span className={`font-semibold ${
              recording.moderationStatus === "Approved" ? "text-green-400" :
              recording.moderationStatus === "Pending" ? "text-yellow-400" :
              "text-red-400"
            }`}>
              {recording.moderationStatus}
            </span>
          </p>
        </div>
      )}
    </div>
  );
};

export default RecordingDetails; 