import React from 'react';
import { Link } from 'react-router-dom';
import { useDispatch } from 'react-redux';

import PlayPause from './PlayPause';
import { playPause, setActiveSong } from '../redux/features/playerSlice';

const RecordingCard = ({ recording, isPlaying, activeSong, data, i }) => {
  const dispatch = useDispatch();

  const handlePauseClick = () => {
    dispatch(playPause(false));
  };

  const handlePlayClick = () => {
    dispatch(setActiveSong({ song: recording, data, i }));
    dispatch(playPause(true));
  };

  return (
    <div className="flex flex-col w-[250px] p-4 bg-white/5 bg-opacity-80 backdrop-blur-sm animate-slideup rounded-lg cursor-pointer">
      <div className="relative w-full h-56 group">
        <div className={`absolute inset-0 justify-center items-center bg-black bg-opacity-50 group-hover:flex ${activeSong?.title === recording.title ? 'flex bg-black bg-opacity-70' : 'hidden'}`}>
          <PlayPause
            isPlaying={isPlaying}
            activeSong={activeSong}
            song={recording}
            handlePause={handlePauseClick}
            handlePlay={handlePlayClick}
          />
        </div>
        <img alt="song_img" src={recording.coverImage || 'https://via.placeholder.com/400?text=Военная+запись'} className="w-full h-full rounded-lg" />
      </div>

      <div className="mt-4 flex flex-col">
        <p className="font-semibold text-lg text-white truncate">
          <Link to={`/recordings/${recording.id}`}>
            {recording.title}
          </Link>
        </p>
        <p className="text-sm truncate text-gray-300 mt-1">
          <Link to={`/authors/${recording.authorId}`}>
            {recording.author}
          </Link>
        </p>
        <div className="flex flex-wrap mt-2">
          {recording.tags?.map((tag) => (
            <Link
              key={`tag-${tag.id}`}
              to={`/tag/${tag.id}`}
              className="text-xs mr-2 mb-1 py-1 px-2 bg-black/30 text-gray-300 rounded-full"
            >
              {tag.name}
            </Link>
          ))}
        </div>
        <p className="text-xs text-gray-400 mt-1">
          {recording.year}
        </p>
      </div>
    </div>
  );
};

export default RecordingCard; 