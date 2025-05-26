import React, { useState, useEffect } from 'react';
import { useSelector } from 'react-redux';

import { Error, Loader, RecordingCard } from '../components';
import { 
  useGetAllRecordingsQuery, 
  useGetTagsQuery, 
  useGetAuthorsQuery,
  useGetRecordingsByTagsMutation,
  useGetRecordingsByAuthorQuery,
  useGetRecordingsByYearQuery,
  useGetGenresQuery,
  useGetRecordingsByGenreQuery
} from '../redux/services/audioArchiveApi';

const ArchiveExplorer = () => {
  const { activeSong, isPlaying } = useSelector((state) => state.player);
  const [activeFilters, setActiveFilters] = useState({
    tags: [],
    years: [],
    authors: [],
    genres: []
  });
  const [searchTerm, setSearchTerm] = useState('');
  const [paginationParams, setPaginationParams] = useState({
    from: 0,
    count: 20
  });
  const [filteredRecordings, setFilteredRecordings] = useState([]);
  const [isLoadingMore, setIsLoadingMore] = useState(false);

  // Загрузка основных данных
  const { data: allRecordings, isFetching: isRecordingsFetching, error: recordingsError } = 
    useGetAllRecordingsQuery(paginationParams);
  const { data: tags, isFetching: isTagsFetching } = useGetTagsQuery();
  const { data: authors, isFetching: isAuthorsFetching } = useGetAuthorsQuery();
  const { data: genres, isFetching: isGenresFetching } = useGetGenresQuery();

  // Запросы для фильтрации
  const [getRecordingsByTags] = useGetRecordingsByTagsMutation();
  
  // Для активного фильтра по автору
  const { data: authorRecordings, isFetching: isAuthorRecordingsFetching } = 
    useGetRecordingsByAuthorQuery(
      { id: activeFilters.authors[0], ...paginationParams },
      { skip: activeFilters.authors.length === 0 }
    );
  
  // Для активного фильтра по году
  const { data: yearRecordings, isFetching: isYearRecordingsFetching } = 
    useGetRecordingsByYearQuery(
      { year: activeFilters.years[0], ...paginationParams },
      { skip: activeFilters.years.length === 0 }
    );
  
  // Для активного фильтра по жанру
  const { data: genreRecordings, isFetching: isGenreRecordingsFetching } = 
    useGetRecordingsByGenreQuery(
      { id: activeFilters.genres[0], ...paginationParams },
      { skip: activeFilters.genres.length === 0 }
    );

  // Обработка изменений фильтров и загрузка отфильтрованных данных
  useEffect(() => {
    const loadFilteredData = async () => {
      try {
        // Сбрасываем пагинацию при смене фильтров
        if (paginationParams.from !== 0) {
          setPaginationParams({
            from: 0,
            count: 20
          });
          return; // useEffect сработает повторно с обновлёнными параметрами пагинации
        }

        setIsLoadingMore(true);
        let recordingsToShow = allRecordings || [];

        // Приоритет фильтров: теги > авторы > годы > жанры
        if (activeFilters.tags.length > 0) {
          const response = await getRecordingsByTags({
            tags: activeFilters.tags,
            ...paginationParams
          });
          if (response.data) {
            recordingsToShow = response.data;
          }
        } else if (activeFilters.authors.length > 0) {
          recordingsToShow = authorRecordings || [];
        } else if (activeFilters.years.length > 0) {
          recordingsToShow = yearRecordings || [];
        } else if (activeFilters.genres.length > 0) {
          recordingsToShow = genreRecordings || [];
        }

        // Применяем текстовый поиск на клиентской стороне
        if (searchTerm) {
          recordingsToShow = recordingsToShow.filter(recording => 
            recording.title.toLowerCase().includes(searchTerm.toLowerCase()) || 
            recording.authorName.toLowerCase().includes(searchTerm.toLowerCase())
          );
        }

        setFilteredRecordings(recordingsToShow);
        setIsLoadingMore(false);
      } catch (error) {
        console.error('Ошибка при загрузке данных:', error);
        setIsLoadingMore(false);
      }
    };

    loadFilteredData();
  }, [
    allRecordings, 
    activeFilters, 
    searchTerm, 
    paginationParams,
    authorRecordings,
    yearRecordings,
    genreRecordings,
    getRecordingsByTags
  ]);

  // Функция для обработки изменений в фильтрах
  const handleFilterChange = (filterType, value) => {
    setActiveFilters(prev => {
      const newFilters = { ...prev };
      
      // Сбрасываем все другие фильтры при выборе нового
      if (filterType === 'tags') {
        newFilters.authors = [];
        newFilters.years = [];
        newFilters.genres = [];
      } else if (filterType === 'authors') {
        newFilters.tags = [];
        newFilters.years = [];
        newFilters.genres = [];
      } else if (filterType === 'years') {
        newFilters.tags = [];
        newFilters.authors = [];
        newFilters.genres = [];
      } else if (filterType === 'genres') {
        newFilters.tags = [];
        newFilters.authors = [];
        newFilters.years = [];
      }
      
      // Обновляем выбранный фильтр
      if (newFilters[filterType].includes(value)) {
        // Удаляем фильтр если он уже выбран
        newFilters[filterType] = newFilters[filterType].filter(item => item !== value);
      } else {
        // Добавляем фильтр, заменяя предыдущий
        newFilters[filterType] = [value];
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
      genres: []
    });
    setSearchTerm('');
  };

  // Функция для загрузки дополнительных записей
  const loadMoreRecordings = () => {
    setPaginationParams(prev => ({
      from: prev.from + prev.count,
      count: prev.count
    }));
  };

  // Определяем, загружаются ли данные
  const isLoading = 
    isRecordingsFetching || 
    isTagsFetching || 
    isAuthorsFetching || 
    isGenresFetching ||
    isAuthorRecordingsFetching ||
    isYearRecordingsFetching ||
    isGenreRecordingsFetching ||
    isLoadingMore;

  if (isLoading && filteredRecordings.length === 0) {
    return <Loader title="Загрузка архива..." />;
  }

  if (recordingsError) {
    console.error('Error loading recordings:', recordingsError);
    return <Error message={`Ошибка при загрузке данных. ${recordingsError.status === 'FETCH_ERROR' ? 'Сервер недоступен. Проверьте подключение или убедитесь, что API сервер запущен.' : recordingsError.error}`} />;
  }

  // Получение уникальных годов из данных (если доступны)
  const years = allRecordings 
    ? [...new Set(allRecordings.map(recording => recording.year?.toString()))]
        .filter(year => year) // Убираем null/undefined
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
          
          {/* Фильтр по жанрам */}
          <div className="mb-4">
            <h4 className="text-gray-300 mb-2">Жанры</h4>
            <div className="flex flex-wrap gap-2">
              {genres?.map(genre => (
                <button
                  key={genre.id}
                  onClick={() => handleFilterChange('genres', genre.id)}
                  className={`py-1 px-3 rounded-full text-sm ${
                    activeFilters.genres.includes(genre.id) 
                      ? 'bg-blue-600 text-white' 
                      : 'bg-black/30 text-gray-300'
                  }`}
                >
                  {genre.name}
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
          <>
            {filteredRecordings.map((recording, i) => (
              <RecordingCard
                key={`${recording.id}-${i}`}
                recording={recording}
                isPlaying={isPlaying}
                activeSong={activeSong}
                data={filteredRecordings}
                i={i}
              />
            ))}
            <div className="w-full flex justify-center mt-8">
              <button 
                onClick={loadMoreRecordings}
                className="py-2 px-4 bg-blue-600 hover:bg-blue-700 text-white rounded disabled:opacity-50"
                disabled={isLoading}
              >
                {isLoading ? 'Загрузка...' : 'Загрузить еще'}
              </button>
            </div>
          </>
        ) : (
          <p className="text-gray-400 text-lg">Нет записей, соответствующих выбранным фильтрам.</p>
        )}
      </div>
    </div>
  );
};

export default ArchiveExplorer; 