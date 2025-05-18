import React, { useState, useEffect } from 'react';
import { useDispatch, useSelector } from 'react-redux';

import { Error, Loader, RecordingCard } from '../components';
import { useGetAllRecordingsQuery, useGetTagsQuery } from '../redux/services/audioArchiveApi';

const Discover = () => {
  const dispatch = useDispatch();
  const [selectedGenre, setSelectedGenre] = useState('');
  const { activeSong, isPlaying } = useSelector((state) => state.player);
  
  const { data: allRecordings, isFetching: isFetchingRecordings, error: recordingsError } = useGetAllRecordingsQuery();
  const { data: tags, isFetching: isFetchingTags } = useGetTagsQuery();
  
  const [filteredRecordings, setFilteredRecordings] = useState([]);
  
  useEffect(() => {
    if (allRecordings) {
      if (selectedGenre && selectedGenre !== 'all') {
        setFilteredRecordings(allRecordings.filter(recording => 
          recording.tags && recording.tags.some(tag => tag.id === selectedGenre)
        ));
      } else {
        setFilteredRecordings(allRecordings);
      }
    }
  }, [allRecordings, selectedGenre]);
  
  const handleGenreChange = (e) => {
    setSelectedGenre(e.target.value);
  };

  if (isFetchingRecordings || isFetchingTags) return <Loader title="Загрузка записей..." />;

  if (recordingsError) return <Error />;

  const genreTitle = selectedGenre ? 
    (tags?.find(tag => tag.id === selectedGenre)?.name || 'Все') : 
    'Все';

  return (
    <div className="flex flex-col">
      <div className="w-full flex justify-between items-center sm:flex-row flex-col mt-4 mb-10">
        <h2 className="font-bold text-3xl text-white text-left">Обзор: {genreTitle}</h2>

        <select
          onChange={handleGenreChange}
          value={selectedGenre}
          className="bg-black text-gray-300 p-3 text-sm rounded-lg outline-none sm:mt-0 mt-5"
        >
          <option value="">Все записи</option>
          {tags?.map((tag) => (
            <option key={tag.id} value={tag.id}>{tag.name}</option>
          ))}
        </select>
      </div>

      <div className="flex flex-wrap sm:justify-start justify-center gap-8">
        {filteredRecordings?.map((recording, i) => (
          <RecordingCard
            key={recording.id}
            recording={recording}
            isPlaying={isPlaying}
            activeSong={activeSong}
            data={filteredRecordings}
            i={i}
          />
        ))}
      </div>
    </div>
  );
};

export default Discover; 