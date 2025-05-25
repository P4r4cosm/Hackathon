/* eslint-disable import/no-unresolved */
import React, { useEffect, useRef } from 'react';
import { Link } from 'react-router-dom';
import { useSelector, useDispatch } from 'react-redux';
import { Swiper, SwiperSlide } from 'swiper/react';
import { FreeMode } from 'swiper';

import PlayPause from './PlayPause';
import { playPause, setActiveSong } from '../redux/features/playerSlice';
import { useGetAllRecordingsQuery, useGetAuthorsQuery } from '../redux/services/audioArchiveApi';

import 'swiper/css';
import 'swiper/css/free-mode';

const TopChartCard = ({ recording, i, isPlaying, activeSong, handlePauseClick, handlePlayClick }) => (
  <div className={`w-full flex flex-row items-center hover:bg-[#4c426e] ${activeSong?.title === recording?.title ? 'bg-[#4c426e]' : 'bg-transparent'} py-2 p-4 rounded-lg cursor-pointer mb-2`}>
    <h3 className="font-bold text-base text-white mr-3">{i + 1}.</h3>
    <div className="flex-1 flex flex-row justify-between items-center">
      <img className="w-20 h-20 rounded-lg" src={recording?.coverImage || '/assets/default-cover.png'} alt={recording?.title} />
      <div className="flex-1 flex flex-col justify-center mx-3">
        <Link to={`/track/${recording.id}`}>
          <p className="text-xl font-bold text-white">
            {recording?.title}
          </p>
        </Link>
        <Link to={recording?.authorId ? `/authors/${recording.authorId}` : '#'}>
          <p className="text-base text-gray-300 mt-1">
            {recording?.author || 'Неизвестный автор'}
          </p>
        </Link>
      </div>
    </div>
    <PlayPause
      isPlaying={isPlaying}
      activeSong={activeSong}
      song={recording}
      handlePause={handlePauseClick}
      handlePlay={handlePlayClick}
    />
  </div>
);

const TopPlay = () => {
  const dispatch = useDispatch();
  const { activeSong, isPlaying } = useSelector((state) => state.player);
  const { data: recordings = [], isError: recordingsError } = useGetAllRecordingsQuery();
  const { data: authors = [], isError: authorsError } = useGetAuthorsQuery();
  const divRef = useRef(null);

  useEffect(() => {
    if (divRef.current) {
      divRef.current.scrollIntoView({ behavior: 'smooth' });
    }
  }, []);

  // Безопасное получение топ-5 записей
  const topRecordings = recordings && recordings.length > 0 ? recordings.slice(0, 5) : [];

  const handlePauseClick = () => {
    dispatch(playPause(false));
  };

  const handlePlayClick = (recording, i) => {
    // Проверка на существование объекта recording
    if (!recording) return;
    
    // Используем путь к файлу из нового API
    const audioPath = recording.filePath || recording.originalAudioUrl;
    dispatch(setActiveSong({ 
      song: {
        ...recording,
        audioPath,
        hub: { actions: [{ type: 'applemusicplay' }, { uri: audioPath }] }
      }, 
      data: recordings, 
      i 
    }));
    dispatch(playPause(true));
  };

  return (
    <div ref={divRef} className="xl:ml-6 ml-0 xl:mb-0 mb-6 flex-1 xl:max-w-[500px] max-w-full flex flex-col">
      <div className="w-full flex flex-col">
        <div className="flex flex-row justify-between items-center">
          <h2 className="text-white font-bold text-2xl">Популярные записи</h2>
          <Link to="/top-charts">
            <p className="text-gray-300 text-base cursor-pointer">Больше</p>
          </Link>
        </div>

        <div className="mt-4 flex flex-col gap-1">
          {topRecordings?.length > 0 ? (
            topRecordings.map((recording, i) => (
              <TopChartCard
                key={recording.id || i}
                recording={recording}
                i={i}
                isPlaying={isPlaying}
                activeSong={activeSong}
                handlePauseClick={handlePauseClick}
                handlePlayClick={() => handlePlayClick(recording, i)}
              />
            ))
          ) : (
            <p className="text-gray-400 text-sm py-2">Записи не найдены</p>
          )}
        </div>
      </div>
    </div>
  );
};

export default TopPlay; 