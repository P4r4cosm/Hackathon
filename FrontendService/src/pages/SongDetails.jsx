import React from 'react';
import { useParams } from 'react-router-dom';
import { useSelector, useDispatch } from 'react-redux';
import { DetailsHeader, Error, Loader, RelatedSongs } from '../components';

import { setActiveSong, playPause } from '../redux/features/playerSlice';
import { useGetRecordingDetailsQuery, useGetRelatedRecordingsQuery } from '../redux/services/audioArchiveApi';

const SongDetails = () => {
  const dispatch = useDispatch();
  const { songid } = useParams();
  const { activeSong, isPlaying } = useSelector((state) => state.player);

  const { data: relatedRecordings, isFetching: isFetchingRelated, error: relatedError } = useGetRelatedRecordingsQuery(songid);
  const { data: recordingData, isFetching: isFetchingDetails, error: detailsError } = useGetRecordingDetailsQuery(songid);

  if (isFetchingDetails || isFetchingRelated) return <Loader title="Загрузка данных о записи" />;

  if (relatedError || detailsError) return <Error />;

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
    
    dispatch(setActiveSong({ song: songWithAudioPath, data: relatedRecordings, i }));
    dispatch(playPause(true));
  };

  return (
    <div className="flex flex-col">
      <DetailsHeader
        artistId={recordingData?.authorId}
        songData={recordingData}
      />

      <div className="mb-10">
        <h2 className="text-white text-3xl font-bold">Текст:</h2>

        <div className="mt-5">
          {recordingData?.transcription ? (
            <div className="text-gray-300 whitespace-pre-line">
              {recordingData.transcription}
            </div>
          ) : (
            <p className="text-gray-400 text-base my-1">Текст не найден!</p>
          )}
        </div>
      </div>

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

export default SongDetails; 