/* eslint-disable import/no-unresolved */
import React, { useEffect, useRef } from 'react';
import { Link } from 'react-router-dom';
import { useSelector, useDispatch } from 'react-redux';

import PlayPause from './PlayPause';
import { playPause, setActiveSong } from '../redux/features/playerSlice';
import { useGetAllRecordingsQuery } from '../redux/services/audioArchiveApi';
import Loader from './Loader';
import Error from './Error';

const RecordingCard = ({ recording, i, isPlaying, activeSong, handlePauseClick, handlePlayClick }) => {
  // Адаптируем запись для плеера
  const adaptedRecording = {
    ...recording,
    title: recording.title,
    author: recording.authorName,
    authorId: recording.authorId,
    audio: recording.path,
  };

  return (
    <div className={`w-full flex flex-row items-center hover:bg-[#4c426e] ${activeSong?.title === recording?.title ? 'bg-[#4c426e]' : 'bg-transparent'} py-2 p-4 rounded-lg cursor-pointer mb-2`}>
      <h3 className="font-bold text-base text-white mr-3">{i + 1}.</h3>
      <div className="flex-1 flex flex-row justify-between items-center">
        <div className="w-16 h-16 rounded-lg bg-gray-800 flex items-center justify-center">
          <i className="fa fa-music text-gray-400 text-lg" />
        </div>
        <div className="flex-1 flex flex-col justify-center mx-3">
          <Link to={`/recordings/${recording.id}`}>
            <p className="text-xl font-bold text-white truncate">
              {recording?.title}
            </p>
          </Link>
          <Link to={`/authors/${recording?.authorId}`}>
            <p className="text-base text-gray-300 mt-1">
              {recording?.authorName}
            </p>
          </Link>
        </div>
      </div>
      <PlayPause
        isPlaying={isPlaying}
        activeSong={activeSong}
        song={adaptedRecording}
        handlePause={handlePauseClick}
        handlePlay={handlePlayClick}
      />
    </div>
  );
};

const TopPlay = () => {
  const dispatch = useDispatch();
  const { activeSong, isPlaying } = useSelector((state) => state.player);
  const { data: recordings, isFetching, error } = useGetAllRecordingsQuery({
    from: 0,
    count: 5,
  });
  
  const divRef = useRef(null);

  useEffect(() => {
    if (divRef.current) {
      divRef.current.scrollIntoView({ behavior: 'smooth' });
    }
  });

  const handlePauseClick = () => {
    dispatch(playPause(false));
  };

  const handlePlayClick = (recording, i) => {
    const adaptedRecording = {
      ...recording,
      title: recording.title,
      author: recording.authorName,
      authorId: recording.authorId,
      audio: recording.path,
    };
    dispatch(setActiveSong({ song: adaptedRecording, data: recordings, i }));
    dispatch(playPause(true));
  };
  
  if (isFetching) return <Loader title="Загрузка..." />;
  if (error) return null; // Скрываем компонент при ошибке

  return (
    <div ref={divRef} className="xl:ml-6 ml-0 xl:mb-0 mb-6 flex-1 xl:max-w-[500px] max-w-full flex flex-col">
      <div className="w-full flex flex-col">
        <div className="flex flex-row justify-between items-center">
          <h2 className="text-white font-bold text-2xl">Популярные записи</h2>
          <Link to="/">
            <p className="text-gray-300 text-base cursor-pointer">Показать все</p>
          </Link>
        </div>

        <div className="mt-4 flex flex-col gap-1">
          {recordings?.map((recording, i) => (
            <RecordingCard
              key={recording.id}
              recording={recording}
              i={i}
              isPlaying={isPlaying}
              activeSong={activeSong}
              handlePauseClick={handlePauseClick}
              handlePlayClick={() => handlePlayClick(recording, i)}
            />
          ))}
          {(!recordings || recordings.length === 0) && (
            <p className="text-gray-400 mt-2">Нет доступных записей</p>
          )}
        </div>
      </div>

      <div className="w-full flex flex-col mt-8">
        <div className="flex flex-row justify-between items-center">
          <h2 className="text-white font-bold text-2xl">Тематические теги</h2>
          <Link to="/">
            <p className="text-gray-300 text-base cursor-pointer">Все категории</p>
          </Link>
        </div>

        <div className="flex flex-wrap gap-2 mt-4">
          <Link to="/" className="px-4 py-2 bg-blue-900/30 text-blue-200 rounded-full">
            Патриотические
          </Link>
          <Link to="/" className="px-4 py-2 bg-blue-900/30 text-blue-200 rounded-full">
            Победа
          </Link>
          <Link to="/" className="px-4 py-2 bg-blue-900/30 text-blue-200 rounded-full">
            Военные песни
          </Link>
          <Link to="/" className="px-4 py-2 bg-blue-900/30 text-blue-200 rounded-full">
            Фронтовые
          </Link>
        </div>
      </div>
    </div>
  );
};

export default TopPlay; 