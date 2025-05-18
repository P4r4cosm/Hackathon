import React, { useState } from 'react';
import { useSelector } from 'react-redux';

import { Error, Loader, RecordingCard } from '../components';
import { useGetAllRecordingsQuery, useGetTagsQuery, useGetAuthorsQuery } from '../redux/services/audioArchiveApi';

const ArchiveExplorer = () => {
  const { activeSong, isPlaying } = useSelector((state) => state.player);
  const [activeFilters, setActiveFilters] = useState({
    tags: [],
    years: [],
    authors: [],
  });
  const [searchTerm, setSearchTerm] = useState('');
  const [yearRange, setYearRange] = useState({ min: 1941, max: 1945 });

  const { data: recordings, isFetching: isRecordingsFetching, error: recordingsError } = useGetAllRecordingsQuery();
  const { data: tags, isFetching: isTagsFetching } = useGetTagsQuery();
  const { data: authors, isFetching: isAuthorsFetching } = useGetAuthorsQuery();

  if (isRecordingsFetching || isTagsFetching || isAuthorsFetching) {
    return <Loader title="Загрузка архива..." />;
  }

  if (recordingsError) {
    return <Error />;
  }

  // Функция для обработки изменений в фильтрах
  const handleFilterChange = (filterType, value) => {
    setActiveFilters(prev => {
      const newFilters = { ...prev };
      
      if (newFilters[filterType].includes(value)) {
        // Удаляем фильтр если он уже выбран
        newFilters[filterType] = newFilters[filterType].filter(item => item !== value);
      } else {
        // Добавляем фильтр
        newFilters[filterType] = [...newFilters[filterType], value];
      }
      
      return newFilters;
    });
  };
  
  // Функция для сброса фильтров
  const resetFilters = () => {
    setActiveFilters({
      tags: [],
      years: [],
      authors: [],
    });
    setSearchTerm('');
  };

  // Получение отфильтрованных записей
  const filteredRecordings = recordings?.filter(recording => {
    // Фильтр по поиску
    if (searchTerm && !recording.title.toLowerCase().includes(searchTerm.toLowerCase()) && 
        !recording.author.toLowerCase().includes(searchTerm.toLowerCase())) {
      return false;
    }
    
    // Фильтр по тегам
    if (activeFilters.tags.length > 0 && 
        !recording.tags.some(tag => activeFilters.tags.includes(tag.id))) {
      return false;
    }
    
    // Фильтр по годам
    if (activeFilters.years.length > 0 && 
        !activeFilters.years.includes(recording.year.toString())) {
      return false;
    }
    
    // Фильтр по авторам
    if (activeFilters.authors.length > 0 && 
        !activeFilters.authors.includes(recording.authorId)) {
      return false;
    }
    
    return true;
  });

  // Получение уникальных годов из данных
  const years = recordings 
    ? [...new Set(recordings.map(recording => recording.year.toString()))]
        .sort((a, b) => parseInt(a) - parseInt(b)) 
    : [];

  return (
    <div className="flex flex-col">
      <div className="w-full flex flex-col mt-4 mb-10">
        <h2 className="font-bold text-3xl text-white text-left mb-6">Архив военных лет</h2>
        
        {/* Строка поиска */}
        <div className="mb-6">
          <input
            type="text"
            placeholder="Поиск по названию или автору..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            className="w-full p-3 bg-black/30 text-white rounded-lg outline-none"
          />
        </div>
        
        {/* Фильтры */}
        <div className="flex flex-col mb-6 p-4 bg-black/20 backdrop-blur-sm rounded-lg">
          <h3 className="text-white text-lg font-bold mb-4">Фильтры</h3>
          
          {/* Фильтр по тегам */}
          <div className="mb-4">
            <h4 className="text-gray-300 mb-2">Категории</h4>
            <div className="flex flex-wrap gap-2">
              {tags?.map(tag => (
                <button
                  key={tag.id}
                  onClick={() => handleFilterChange('tags', tag.id)}
                  className={`py-1 px-3 rounded-full text-sm ${
                    activeFilters.tags.includes(tag.id) 
                      ? 'bg-blue-600 text-white' 
                      : 'bg-black/30 text-gray-300'
                  }`}
                >
                  {tag.name}
                </button>
              ))}
            </div>
          </div>
          
          {/* Фильтр по годам */}
          <div className="mb-4">
            <h4 className="text-gray-300 mb-2">Годы</h4>
            <div className="flex flex-wrap gap-2">
              {years.map(year => (
                <button
                  key={year}
                  onClick={() => handleFilterChange('years', year)}
                  className={`py-1 px-3 rounded-full text-sm ${
                    activeFilters.years.includes(year) 
                      ? 'bg-blue-600 text-white' 
                      : 'bg-black/30 text-gray-300'
                  }`}
                >
                  {year}
                </button>
              ))}
            </div>
          </div>
          
          {/* Фильтр по авторам */}
          <div className="mb-4">
            <h4 className="text-gray-300 mb-2">Авторы</h4>
            <div className="flex flex-wrap gap-2">
              {authors?.map(author => (
                <button
                  key={author.id}
                  onClick={() => handleFilterChange('authors', author.id)}
                  className={`py-1 px-3 rounded-full text-sm ${
                    activeFilters.authors.includes(author.id) 
                      ? 'bg-blue-600 text-white' 
                      : 'bg-black/30 text-gray-300'
                  }`}
                >
                  {author.name}
                </button>
              ))}
            </div>
          </div>
          
          {/* Кнопка сброса фильтров */}
          <button
            onClick={resetFilters}
            className="self-start py-2 px-4 bg-gray-600 hover:bg-gray-700 text-white rounded"
          >
            Сбросить фильтры
          </button>
        </div>
      </div>

      {/* Результаты */}
      <div className="flex flex-wrap sm:justify-start justify-center gap-8">
        {filteredRecordings?.length > 0 ? (
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
          <p className="text-gray-400 text-lg">Нет записей, соответствующих выбранным фильтрам.</p>
        )}
      </div>
    </div>
  );
};

export default ArchiveExplorer; 