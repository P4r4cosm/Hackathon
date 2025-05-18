import React from 'react';
import { useParams } from 'react-router-dom';
import { useSelector, useDispatch } from 'react-redux';
import { DetailsHeader, Error, Loader, RelatedSongs } from '../components';

import { useGetAuthorDetailsQuery, useGetRecordingsByAuthorQuery } from '../redux/services/audioArchiveApi';
import { setActiveSong, playPause } from '../redux/features/playerSlice';

const AuthorDetails = () => {
  const dispatch = useDispatch();
  const { authorId } = useParams();
  const { activeSong, isPlaying } = useSelector((state) => state.player);
  
  const { data: authorData, isFetching: isFetchingAuthorDetails, error: authorError } = useGetAuthorDetailsQuery(authorId);
  const { data: authorRecordings, isFetching: isFetchingRecordings, error: recordingsError } = useGetRecordingsByAuthorQuery(authorId);

  if (isFetchingAuthorDetails || isFetchingRecordings) 
    return <Loader title="Загрузка информации об авторе..." />;

  if (authorError || recordingsError) return <Error />;

  const handlePauseClick = () => {
    dispatch(playPause(false));
  };

  const handlePlayClick = (recording, i) => {
    const songWithAudioPath = {
      ...recording,
      audioPath: recording.originalAudioUrl,
      hub: { 
        actions: [
          { type: 'applemusicplay' }, 
          { uri: recording.originalAudioUrl } 
        ]
      }
    };
    
    dispatch(setActiveSong({ song: songWithAudioPath, data: authorRecordings, i }));
    dispatch(playPause(true));
  };

  return (
    <div className="flex flex-col">
      <DetailsHeader
        artistId={authorId}
        artistData={authorData}
      />

      <div className="mb-10">
        <h2 className="text-white text-3xl font-bold">Биография:</h2>
        <p className="text-gray-400 mt-5">
          {authorData?.biography || 'Биография отсутствует'}
        </p>
      </div>

      <div className="flex flex-col">
        <h2 className="text-white text-3xl font-bold">Записи автора:</h2>
        <div className="mt-6">
          {authorRecordings?.length > 0 ? (
            <RelatedSongs
              data={authorRecordings}
              isPlaying={isPlaying}
              activeSong={activeSong}
              handlePauseClick={handlePauseClick}
              handlePlayClick={handlePlayClick}
            />
          ) : (
            <p className="text-gray-400 text-base my-1">Записи не найдены</p>
          )}
        </div>
      </div>
    </div>
  );
};

export default AuthorDetails; 