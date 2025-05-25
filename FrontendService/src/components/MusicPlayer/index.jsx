import React, { useState, useEffect } from 'react';
import { useSelector, useDispatch } from 'react-redux';
import { MdFullscreen, MdFullscreenExit } from 'react-icons/md';

import { nextSong, prevSong, playPause } from '../../redux/features/playerSlice';
import Controls from './Controls';
import Player from './Player';
import Seekbar from './Seekbar';
import Track from './Track';
import VolumeBar from './VolumeBar';

const MusicPlayer = () => {
  const { activeSong, currentSongs, currentIndex, isActive, isPlaying } = useSelector((state) => state.player);
  const [duration, setDuration] = useState(0);
  const [seekTime, setSeekTime] = useState(0);
  const [appTime, setAppTime] = useState(0);
  const [volume, setVolume] = useState(0.3);
  const [repeat, setRepeat] = useState(false);
  const [shuffle, setShuffle] = useState(false);
  const [expanded, setExpanded] = useState(false);
  const dispatch = useDispatch();

  useEffect(() => {
    if (currentSongs.length) dispatch(playPause(true));
  }, [currentIndex]);

  const handlePlayPause = () => {
    if (!isActive) return;

    if (isPlaying) {
      dispatch(playPause(false));
    } else {
      dispatch(playPause(true));
    }
  };

  const handleNextSong = () => {
    dispatch(playPause(false));

    if (!shuffle) {
      dispatch(nextSong((currentIndex + 1) % currentSongs.length));
    } else {
      dispatch(nextSong(Math.floor(Math.random() * currentSongs.length)));
    }
  };

  const handlePrevSong = () => {
    if (currentIndex === 0) {
      dispatch(prevSong(currentSongs.length - 1));
    } else if (shuffle) {
      dispatch(prevSong(Math.floor(Math.random() * currentSongs.length)));
    } else {
      dispatch(prevSong(currentIndex - 1));
    }
  };
  
  const toggleExpanded = () => {
    setExpanded(!expanded);
  };
  
  // Получаем и форматируем текст песни для отображения
  const getLyrics = () => {
    if (activeSong?.transcription) {
      return activeSong.transcription;
    } else if (activeSong?.text) {
      return activeSong.text;
    }
    return 'Текст песни отсутствует';
  };
  
  // Определяем текущую строку текста на основе времени воспроизведения
  const getCurrentLyricIndex = () => {
    if (!activeSong?.timestamps || activeSong.timestamps.length === 0) return -1;
    
    for (let i = activeSong.timestamps.length - 1; i >= 0; i--) {
      const timestamp = activeSong.timestamps[i];
      const time = parseFloat(timestamp.time.split(':').reduce((acc, val) => (60 * acc) + parseFloat(val)));
      
      if (time <= appTime) {
        return i;
      }
    }
    
    return -1;
  };
  
  const currentLyricIndex = getCurrentLyricIndex();

  return (
    <div className={`fixed bottom-0 left-0 right-0 animate-slideup bg-gradient-to-br from-black/90 to-[#121286] backdrop-blur-lg rounded-t-3xl z-10 transition-all duration-500 ${expanded ? 'h-[400px]' : 'h-28'}`}>
      <div className={`relative sm:px-12 px-8 w-full h-full flex flex-col`}>
        {/* Кнопка разворачивания */}
        <button 
          onClick={toggleExpanded}
          className="absolute right-4 top-2 text-white p-1 z-20"
          title={expanded ? "Свернуть плеер" : "Развернуть плеер с текстом"}
        >
          {expanded ? <MdFullscreenExit size={24} /> : <MdFullscreen size={24} />}
        </button>
        
        {/* Текст песни (отображается только в развернутом режиме) */}
        {expanded && (
          <div className="flex-1 overflow-y-auto px-4 py-8 mb-28 scrollbar-thin scrollbar-thumb-gray-600">
            <h2 className="text-white text-xl font-bold mb-2">{activeSong?.title || 'Название неизвестно'}</h2>
            <h3 className="text-gray-300 text-sm mb-4">{activeSong?.author || activeSong?.subtitle || 'Исполнитель неизвестен'}</h3>
            
            {activeSong?.timestamps && activeSong.timestamps.length > 0 ? (
              <div className="text-gray-300 space-y-1 text-sm">
                {activeSong.timestamps.map((item, index) => (
                  <div 
                    key={`timestamp-${index}`} 
                    className={`flex p-1 rounded ${index === currentLyricIndex ? 'bg-blue-900/30 text-white' : ''}`}
                  >
                    <span className="text-gray-500 w-12">[{item.time}]</span>
                    <span>{item.text}</span>
                  </div>
                ))}
              </div>
            ) : (
              <p className="text-gray-300 whitespace-pre-line text-sm">{getLyrics()}</p>
            )}
          </div>
        )}
        
        {/* Основной плеер (всегда виден) */}
        <div className={`${expanded ? 'absolute bottom-0 left-0 right-0 bg-black/50 h-28 px-8' : ''} flex items-center justify-between w-full`}>
          <Track isPlaying={isPlaying} isActive={isActive} activeSong={activeSong} />
          <div className="flex-1 flex flex-col items-center justify-center">
            <Controls
              isPlaying={isPlaying}
              isActive={isActive}
              repeat={repeat}
              setRepeat={setRepeat}
              shuffle={shuffle}
              setShuffle={setShuffle}
              currentSongs={currentSongs}
              handlePlayPause={handlePlayPause}
              handlePrevSong={handlePrevSong}
              handleNextSong={handleNextSong}
            />
            <Seekbar
              value={appTime}
              min="0"
              max={duration}
              onInput={(event) => setSeekTime(event.target.value)}
              setSeekTime={setSeekTime}
              appTime={appTime}
            />
            <Player
              activeSong={activeSong}
              volume={volume}
              isPlaying={isPlaying}
              seekTime={seekTime}
              repeat={repeat}
              currentIndex={currentIndex}
              onEnded={handleNextSong}
              onTimeUpdate={(event) => setAppTime(event.target.currentTime)}
              onLoadedData={(event) => setDuration(event.target.duration)}
            />
          </div>
          <VolumeBar value={volume} min="0" max="1" onChange={(event) => setVolume(event.target.value)} setVolume={setVolume} />
        </div>
      </div>
    </div>
  );
};

export default MusicPlayer; 