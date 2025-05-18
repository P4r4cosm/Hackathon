import React from 'react';
import { useSelector } from 'react-redux';
import { useParams } from 'react-router-dom';

import { Error, Loader, RecordingCard } from '../components';
import { useSearchRecordingsQuery } from '../redux/services/audioArchiveApi';

const Search = () => {
  const { searchTerm } = useParams();
  const { activeSong, isPlaying } = useSelector((state) => state.player);
  const { data, isFetching, error } = useSearchRecordingsQuery(searchTerm);

  if (isFetching) return <Loader title={`Поиск: ${searchTerm}...`} />;

  if (error) return <Error />;

  return (
    <div className="flex flex-col">
      <h2 className="font-bold text-3xl text-white text-left mt-4 mb-10">Результаты поиска для: <span className="font-black">{searchTerm}</span></h2>

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

export default Search; 