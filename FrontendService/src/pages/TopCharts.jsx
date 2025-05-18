import React from 'react';
import { useSelector } from 'react-redux';

import { Error, Loader, RecordingCard } from '../components';
import { useGetAllRecordingsQuery } from '../redux/services/audioArchiveApi';

const TopCharts = () => {
  const { data, isFetching, error } = useGetAllRecordingsQuery();
  const { activeSong, isPlaying } = useSelector((state) => state.player);

  if (isFetching) return <Loader title="Загрузка популярных записей" />;

  if (error) return <Error />;

  return (
    <div className="flex flex-col">
      <h2 className="font-bold text-3xl text-white text-left mt-4 mb-10">Популярные записи</h2>

      <div className="flex flex-wrap sm:justify-start justify-center gap-8">
        {data?.map((recording, i) => (
          <RecordingCard
            key={recording.id}
            recording={recording}
            isPlaying={isPlaying}
            activeSong={activeSong}
            data={data}
            i={i}
          />
        ))}
      </div>
    </div>
  );
};

export default TopCharts; 