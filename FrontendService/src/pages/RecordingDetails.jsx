import React, { useState } from 'react';
import { useParams } from 'react-router-dom';
import { useSelector, useDispatch } from 'react-redux';
import { DetailsHeader, Error, Loader, RelatedSongs, DualAudioPlayer } from '../components';
import { TranscriptionEditor } from '../components/Admin';

import { setActiveSong, playPause } from '../redux/features/playerSlice';
import { 
  useGetRecordingDetailsQuery, 
  useGetRelatedRecordingsQuery 
} from '../redux/services/audioArchiveApi';

const RecordingDetails = () => {
  const dispatch = useDispatch();
  const { recordingId } = useParams();
  const { activeSong, isPlaying } = useSelector((state) => state.player);
  const [isAdmin, setIsAdmin] = useState(false); // В реальном приложении нужно проверять роль пользователя
  const [currentTime, setCurrentTime] = useState(0);

  const { 
    data: recording, 
    isFetching: isFetchingDetails,
    error: detailsError
  } = useGetRecordingDetailsQuery(recordingId);
  
  const { 
    data: relatedRecordings, 
    isFetching: isFetchingRelated, 
    error: relatedError 
  } = useGetRelatedRecordingsQuery(recordingId);

  if (isFetchingDetails || isFetchingRelated) {
    return <Loader title="Загрузка данных о записи..." />;
  }

  if (detailsError || relatedError) {
    return <Error />;
  }

  const handlePauseClick = () => {
    dispatch(playPause(false));
  };

  const handlePlayClick = (song, i) => {
    dispatch(setActiveSong({ song, data: relatedRecordings, i }));
    dispatch(playPause(true));
  };

  const handleTimeUpdate = (time) => {
    setCurrentTime(time);
  };

  // Функция для поиска текущей строки текста на основе времени воспроизведения
  const getCurrentLyricIndex = () => {
    if (!recording.timestamps || recording.timestamps.length === 0) return -1;
    
    for (let i = recording.timestamps.length - 1; i >= 0; i--) {
      const timestamp = recording.timestamps[i];
      const time = parseFloat(timestamp.time.split(':').reduce((acc, val) => (60 * acc) + parseFloat(val)));
      
      if (time <= currentTime) {
        return i;
      }
    }
    
    return -1;
  };

  const currentLyricIndex = getCurrentLyricIndex();

  return (
    <div className="flex flex-col">
      <DetailsHeader
        artistId={recording.authorId}
        songData={recording}
      />

      {/* Двойной плеер для аудио */}
      <div className="mb-10">
        <DualAudioPlayer 
          originalUrl={recording.originalAudioUrl} 
          restoredUrl={recording.restoredAudioUrl} 
          title={recording.title}
          onTimeUpdate={handleTimeUpdate}
        />
      </div>

      {/* Информация о записи */}
      <div className="mb-10">
        <h2 className="text-white text-3xl font-bold">{recording.title}</h2>
        <p className="text-gray-400 mt-2">Автор: {recording.author}</p>
        <p className="text-gray-400">Год: {recording.year}</p>
        
        <div className="mt-4 flex flex-wrap">
          {recording.tags?.map((tag) => (
            <span 
              key={`tag-${tag.id}`} 
              className="text-sm mr-2 mb-1 py-1 px-3 bg-blue-800/30 text-blue-200 rounded-full"
            >
              {tag.name}
            </span>
          ))}
        </div>
        
        {recording.description && (
          <div className="mt-4">
            <h3 className="text-white text-xl font-bold">Описание:</h3>
            <p className="text-gray-300 mt-2">{recording.description}</p>
          </div>
        )}
      </div>

      {/* Компонент для текста песни, с возможностью редактирования для админов */}
      {isAdmin ? (
        <TranscriptionEditor 
          recordingId={recordingId}
          initialText={recording.transcription}
          timestamps={recording.timestamps}
        />
      ) : (
        <div className="mb-10">
          <h3 className="text-white text-xl font-bold mb-4">Текст записи:</h3>
          
          {recording.timestamps ? (
            <div className="text-gray-300 space-y-2">
              {recording.timestamps.map((item, index) => (
                <div 
                  key={`timestamp-${index}`} 
                  className={`flex p-1 rounded ${index === currentLyricIndex ? 'bg-blue-900/30' : ''}`}
                >
                  <span className="text-gray-500 w-16">[{item.time}]</span>
                  <span>{item.text}</span>
                </div>
              ))}
            </div>
          ) : recording.transcription ? (
            <p className="text-gray-300 whitespace-pre-line">{recording.transcription}</p>
          ) : (
            <p className="text-gray-400">Текст для этой записи отсутствует.</p>
          )}
        </div>
      )}

      {/* Похожие записи */}
      <RelatedSongs
        data={relatedRecordings}
        isPlaying={isPlaying}
        activeSong={activeSong}
        handlePauseClick={handlePauseClick}
        handlePlayClick={handlePlayClick}
      />
    </div>
  );
};

export default RecordingDetails; 