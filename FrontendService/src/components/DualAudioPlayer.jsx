import React, { useState, useRef, useEffect } from 'react';
import { FaPlay, FaPause, FaVolumeUp, FaVolumeMute } from 'react-icons/fa';
import { BiSkipPrevious, BiSkipNext } from 'react-icons/bi';

const DualAudioPlayer = ({ 
  originalUrl, 
  restoredUrl, 
  title,
  onTimeUpdate,
  syncPlayers = true
}) => {
  // Преобразование URL для прохождения через API Gateway
  const getProperAudioUrl = (url) => {
    if (!url) return '';
    return url.startsWith('http') 
      ? url 
      : `http://localhost:8000/download?path=${encodeURIComponent(url)}`;
  };

  const processedOriginalUrl = getProperAudioUrl(originalUrl);
  const processedRestoredUrl = getProperAudioUrl(restoredUrl);

  const [isPlaying, setIsPlaying] = useState(false);
  const [currentTime, setCurrentTime] = useState(0);
  const [duration, setDuration] = useState(0);
  const [volume, setVolume] = useState(0.7);
  const [muted, setMuted] = useState(false);
  const [activePlayer, setActivePlayer] = useState('restored'); // 'original' или 'restored'
  
  const originalAudioRef = useRef(null);
  const restoredAudioRef = useRef(null);
  
  // Ссылка на активный плеер
  const activeAudioRef = activePlayer === 'original' ? originalAudioRef : restoredAudioRef;
  
  useEffect(() => {
    // Синхронизация плееров при переключении
    if (syncPlayers && isPlaying) {
      const inactivePlayer = activePlayer === 'original' ? restoredAudioRef.current : originalAudioRef.current;
      
      if (inactivePlayer) {
        inactivePlayer.currentTime = activeAudioRef.current.currentTime;
        inactivePlayer.pause();
      }
    }
  }, [activePlayer, syncPlayers, isPlaying]);
  
  const togglePlayPause = () => {
    if (isPlaying) {
      activeAudioRef.current.pause();
    } else {
      activeAudioRef.current.play();
    }
    setIsPlaying(!isPlaying);
  };
  
  const toggleMute = () => {
    activeAudioRef.current.muted = !muted;
    setMuted(!muted);
  };
  
  const handleVolumeChange = (e) => {
    const value = e.target.value;
    activeAudioRef.current.volume = value;
    setVolume(value);
    
    if (value === 0) {
      setMuted(true);
    } else if (muted) {
      setMuted(false);
    }
  };
  
  const handleTimeUpdate = () => {
    setCurrentTime(activeAudioRef.current.currentTime);
    
    if (onTimeUpdate) {
      onTimeUpdate(activeAudioRef.current.currentTime);
    }
    
    // Синхронизация времени воспроизведения между плеерами
    if (syncPlayers && isPlaying) {
      const inactivePlayer = activePlayer === 'original' ? restoredAudioRef.current : originalAudioRef.current;
      
      if (inactivePlayer && Math.abs(inactivePlayer.currentTime - activeAudioRef.current.currentTime) > 0.5) {
        inactivePlayer.currentTime = activeAudioRef.current.currentTime;
      }
    }
  };
  
  const handleSeek = (e) => {
    const seekTime = parseFloat(e.target.value);
    activeAudioRef.current.currentTime = seekTime;
    setCurrentTime(seekTime);
    
    // Синхронизация времени при перемотке
    if (syncPlayers) {
      const inactivePlayer = activePlayer === 'original' ? restoredAudioRef.current : originalAudioRef.current;
      if (inactivePlayer) {
        inactivePlayer.currentTime = seekTime;
      }
    }
  };
  
  const handleLoadedMetadata = () => {
    setDuration(activeAudioRef.current.duration);
  };
  
  const formatTime = (time) => {
    const minutes = Math.floor(time / 60);
    const seconds = Math.floor(time % 60);
    return `${minutes}:${seconds < 10 ? '0' : ''}${seconds}`;
  };
  
  const handlePlayerSelect = (player) => {
    if (player !== activePlayer) {
      const wasPlaying = isPlaying;
      const currentSeekTime = activeAudioRef.current.currentTime;
      
      setActivePlayer(player);
      
      // После переключения, установим то же время и состояние воспроизведения
      setTimeout(() => {
        const newActivePlayer = player === 'original' ? originalAudioRef.current : restoredAudioRef.current;
        
        if (newActivePlayer) {
          newActivePlayer.currentTime = currentSeekTime;
          
          if (wasPlaying) {
            newActivePlayer.play();
          } else {
            newActivePlayer.pause();
          }
        }
      }, 50);
    }
  };
  
  return (
    <div className="flex flex-col p-4 bg-black/30 backdrop-blur-lg rounded-lg">
      <div className="flex items-center justify-between mb-4">
        <h3 className="text-white font-bold truncate">{title}</h3>
        
        {/* Переключатель версий */}
        <div className="flex bg-black/20 rounded-full p-1">
          <button
            onClick={() => handlePlayerSelect('original')}
            className={`px-3 py-1 text-sm rounded-full transition-all ${
              activePlayer === 'original' 
                ? 'bg-blue-600 text-white' 
                : 'text-gray-300'
            }`}
          >
            Оригинал
          </button>
          <button
            onClick={() => handlePlayerSelect('restored')}
            className={`px-3 py-1 text-sm rounded-full transition-all ${
              activePlayer === 'restored' 
                ? 'bg-blue-600 text-white' 
                : 'text-gray-300'
            }`}
          >
            Восстановленная
          </button>
        </div>
      </div>
      
      {/* Таймлайн */}
      <div className="flex items-center mb-4">
        <span className="text-gray-300 text-sm mr-2">{formatTime(currentTime)}</span>
        <input
          type="range"
          min="0"
          max={duration || 0}
          step="0.01"
          value={currentTime}
          onChange={handleSeek}
          className="flex-1 h-2 bg-gray-700 rounded-lg appearance-none cursor-pointer"
        />
        <span className="text-gray-300 text-sm ml-2">{formatTime(duration)}</span>
      </div>
      
      {/* Контроллы */}
      <div className="flex items-center justify-between">
        <div className="flex items-center">
          <button 
            onClick={() => { activeAudioRef.current.currentTime -= 10; }}
            className="text-gray-300 hover:text-white mr-4"
          >
            <BiSkipPrevious size={24} />
          </button>
          
          <button 
            onClick={togglePlayPause}
            className="bg-white p-2 rounded-full hover:bg-gray-200"
          >
            {isPlaying ? <FaPause className="text-gray-900" /> : <FaPlay className="text-gray-900" />}
          </button>
          
          <button 
            onClick={() => { activeAudioRef.current.currentTime += 10; }}
            className="text-gray-300 hover:text-white ml-4"
          >
            <BiSkipNext size={24} />
          </button>
        </div>
        
        {/* Громкость */}
        <div className="flex items-center">
          <button 
            onClick={toggleMute}
            className="text-gray-300 hover:text-white mr-2"
          >
            {muted ? <FaVolumeMute /> : <FaVolumeUp />}
          </button>
          
          <input
            type="range"
            min="0"
            max="1"
            step="0.01"
            value={volume}
            onChange={handleVolumeChange}
            className="w-20 h-2 bg-gray-700 rounded-lg appearance-none cursor-pointer"
          />
        </div>
      </div>
      
      {/* Скрытые аудио-элементы */}
      <audio
        ref={originalAudioRef}
        src={processedOriginalUrl}
        onTimeUpdate={handleTimeUpdate}
        onLoadedMetadata={handleLoadedMetadata}
        onEnded={() => setIsPlaying(false)}
        className="hidden"
      />
      
      <audio
        ref={restoredAudioRef}
        src={processedRestoredUrl}
        onTimeUpdate={handleTimeUpdate}
        onLoadedMetadata={handleLoadedMetadata}
        onEnded={() => setIsPlaying(false)}
        className="hidden"
      />
    </div>
  );
};

export default DualAudioPlayer; 