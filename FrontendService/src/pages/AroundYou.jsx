import React, { useState, useEffect } from 'react';
import { useSelector } from 'react-redux';

import { Error, Loader, RecordingCard } from '../components';
import { useGetAllRecordingsQuery } from '../redux/services/audioArchiveApi';

const RecordingsByYear = () => {
  const [selectedYear, setSelectedYear] = useState('');
  const { activeSong, isPlaying } = useSelector((state) => state.player);
  const { data: allRecordings, isFetching, error } = useGetAllRecordingsQuery();
  const [filteredRecordings, setFilteredRecordings] = useState([]);
  
  // Список доступных годов для выбора (от 1941 до 1945)
  const availableYears = ['1941', '1942', '1943', '1944', '1945'];
  
  useEffect(() => {
    if (allRecordings) {
      if (selectedYear) {
        setFilteredRecordings(allRecordings.filter(recording => 
          recording.year && recording.year.toString() === selectedYear
        ));
      } else {
        // Если год не выбран, показываем записи военных лет
        setFilteredRecordings(allRecordings.filter(recording => 
          recording.year && recording.year >= 1941 && recording.year <= 1945
        ));
      }
    }
  }, [allRecordings, selectedYear]);

  const handleYearChange = (e) => {
    setSelectedYear(e.target.value);
  };

  if (isFetching) return <Loader title="Загрузка записей..." />;

  if (error) return <Error />;

  return (
    <div className="flex flex-col">
      <div className="flex flex-row justify-between items-center">
        <h2 className="font-bold text-3xl text-white text-left mt-4 mb-10">
          Записи {selectedYear ? `${selectedYear} года` : 'военных лет'}
        </h2>
        
        <select
          onChange={handleYearChange}
          value={selectedYear}
          className="bg-black text-gray-300 p-3 text-sm rounded-lg outline-none sm:mt-0 mt-5"
        >
          <option value="">Все военные годы</option>
          {availableYears.map((year) => (
            <option key={year} value={year}>{year}</option>
          ))}
        </select>
      </div>

      <div className="flex flex-wrap sm:justify-start justify-center gap-8">
        {filteredRecordings.length > 0 ? (
          filteredRecordings.map((recording, i) => (
            <RecordingCard
              key={recording.id}
              recording={recording}
              isPlaying={isPlaying}
              activeSong={activeSong}
              data={filteredRecordings}
              i={i}
            />
          ))
        ) : (
          <p className="text-white text-xl">Не найдено записей для выбранного года</p>
        )}
      </div>
    </div>
  );
};

export default RecordingsByYear; 