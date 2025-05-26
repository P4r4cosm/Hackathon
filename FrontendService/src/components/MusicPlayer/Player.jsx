/* eslint-disable jsx-a11y/media-has-caption */
import React, { useRef, useEffect, useState } from 'react';
import { useDownloadAudioQuery } from '../../redux/services/audioArchiveApi';

const Player = ({ activeSong, isPlaying, volume, seekTime, onEnded, onTimeUpdate, onLoadedData, repeat }) => {
  const ref = useRef(null);
  const [audioUrl, setAudioUrl] = useState(null);
  
  // Получаем аудио файл по пути
  const {
    data: audioBlob,
    isFetching: isFetchingAudio,
    error: audioError
  } = useDownloadAudioQuery(
    activeSong?.path,
    { skip: !activeSong?.path }
  );
  
  // Отладочная информация
  useEffect(() => {
    console.log('Путь к аудио:', activeSong?.path);
    console.log('Получен блоб:', !!audioBlob);
    console.log('Создан URL:', audioUrl);
    if (audioError) {
      console.error('Ошибка загрузки аудио:', audioError);
    }
  }, [activeSong?.path, audioBlob, audioUrl, audioError]);
  
  // Создаем URL для воспроизведения из blob
  useEffect(() => {
    if (audioBlob) {
      const url = URL.createObjectURL(audioBlob);
      setAudioUrl(url);
      
      return () => {
        URL.revokeObjectURL(url);
      };
    }
  }, [audioBlob]);
  
  // eslint-disable-next-line no-unused-expressions
  if (ref.current) {
    if (isPlaying) {
      ref.current.play().catch(error => {
        console.error('Ошибка воспроизведения:', error);
      });
    } else {
      ref.current.pause();
    }
  }

  useEffect(() => {
    if (ref.current) {
      ref.current.volume = volume;
    }
  }, [volume]);
  
  // updates audio element only on seekTime change (and not on each rerender):
  useEffect(() => {
    if (ref.current && !isNaN(seekTime)) {
      ref.current.currentTime = seekTime;
    }
  }, [seekTime]);

  return (
    <audio
      src={audioUrl || activeSong?.path}
      ref={ref}
      loop={repeat}
      onEnded={onEnded}
      onTimeUpdate={onTimeUpdate}
      onLoadedData={onLoadedData}
    />
  );
};

export default Player;