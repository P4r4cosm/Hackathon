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
      <img className="w-20 h-20 rounded-lg" src={recording?.coverArt || '/assets/default-cover.png'} alt={recording?.title} />
      <div className="flex-1 flex flex-col justify-center mx-3">
        <Link to={`/recordings/${recording.id}`}>
          <p className="text-xl font-bold text-white">
            {recording?.title}
          </p>
        </Link>
        <Link to={`/authors/${recording?.authorId}`}>
          <p className="text-base text-gray-300 mt-1">
            {recording?.author}
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
  const { data: recordings } = useGetAllRecordingsQuery();
  const { data: authors } = useGetAuthorsQuery();
  const divRef = useRef(null);

  useEffect(() => {
    divRef.current.scrollIntoView({ behavior: 'smooth' });
  });

  const topRecordings = recordings?.slice(0, 5);

  const handlePauseClick = () => {
    dispatch(playPause(false));
  };

  const handlePlayClick = (recording, i) => {
    dispatch(setActiveSong({ 
      song: {
        ...recording,
        audioPath: recording.originalAudioUrl,
        hub: { actions: [{ type: 'applemusicplay' }, { uri: recording.originalAudioUrl }] }
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
          {topRecordings?.map((recording, i) => (
            <TopChartCard
              key={recording.id}
              recording={recording}
              i={i}
              isPlaying={isPlaying}
              activeSong={activeSong}
              handlePauseClick={handlePauseClick}
              handlePlayClick={() => handlePlayClick(recording, i)}
            />
          ))}
        </div>
      </div>

      <div className="w-full flex flex-col mt-8">
        <div className="flex flex-row justify-between items-center">
          <h2 className="text-white font-bold text-2xl">Популярные авторы</h2>
          <Link to="/top-artists">
            <p className="text-gray-300 text-base cursor-pointer">Больше</p>
          </Link>
        </div>

        <Swiper
          slidesPerView="auto"
          spaceBetween={15}
          freeMode
          centeredSlides
          centeredSlidesBounds
          modules={[FreeMode]}
          className="mt-4"
        >
          {authors?.slice(0, 5).map((author) => (
            <SwiperSlide
              key={author?.id}
              style={{ width: '25%', height: 'auto' }}
              className="shadow-lg rounded-full animate-slideright"
            >
              <Link to={`/authors/${author?.id}`}>
                <img src={author?.imageUrl || '/assets/default-artist.png'} alt={author?.name} className="rounded-full w-full object-cover" />
              </Link>
            </SwiperSlide>
          ))}
        </Swiper>
      </div>
    </div>
  );
};

export default TopPlay; 