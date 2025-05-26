import { createApi, fetchBaseQuery } from '@reduxjs/toolkit/query/react';

// Заглушки для тестирования
const mockAuthors = [
  { id: 1, name: "Левитан Ю.Б." },
  { id: 2, name: "Александров А.В." },
  { id: 3, name: "Утесов Л.О." },
  { id: 4, name: "Шульженко К.И." },
  { id: 5, name: "Бернес М.Н." }
];

const mockGenres = [
  { id: 1, name: "Военные песни" },
  { id: 2, name: "Радиопередачи" },
  { id: 3, name: "Фронтовые записи" },
  { id: 4, name: "Речи" },
  { id: 5, name: "Музыка военных лет" }
];

const mockTags = [
  { id: 1, name: "Победа" },
  { id: 2, name: "Сводка с фронта" },
  { id: 3, name: "Исторический документ" },
  { id: 4, name: "Патриотизм" }
];

export const audioArchiveApi = createApi({
  reducerPath: 'audioArchiveApi',
  baseQuery: fetchBaseQuery({
    baseUrl: 'http://localhost:8000/',
    credentials: 'include',
    prepareHeaders: (headers) => {
      const token = localStorage.getItem('authToken');
      if (token) {
        headers.set('Authorization', `Bearer ${token}`);
      }
      return headers;
    },
  }),
  tagTypes: ['Recording', 'Author', 'Tag', 'Genre', 'Keyword'],
  endpoints: (builder) => ({
    // Получение списка записей
    getAllRecordings: builder.query({ 
      query: ({ from = 0, count = 20 }) => `tracks?from=${from}&count=${count}`,
      providesTags: ['Recording']
    }),
    
    // Получение списка авторов (с заглушкой)
    getAuthors: builder.query({
      queryFn: () => {
        // Возвращаем заглушку вместо запроса к API
        return { data: mockAuthors };
      },
      providesTags: ['Author']
    }),
    
    // Получение списка жанров (с заглушкой)
    getGenres: builder.query({
      queryFn: () => {
        // Возвращаем заглушку вместо запроса к API
        return { data: mockGenres };
      },
      providesTags: ['Genre']
    }),
    
    // Получение списка тегов (с заглушкой)
    getTags: builder.query({
      queryFn: () => {
        // Возвращаем заглушку вместо запроса к API
        return { data: mockTags };
      },
      providesTags: ['Tag']
    }),
    
    // Получение ключевых слов
    getKeywords: builder.query({
      query: () => 'keywords',
      providesTags: ['Keyword']
    }),
    
    // Получение записей по автору
    getRecordingsByAuthor: builder.query({ 
      query: ({ id, from = 0, count = 20 }) => `author_tracks?id=${id}&from=${from}&count=${count}`,
      providesTags: ['Recording']
    }),
    
    // Получение записей по году
    getRecordingsByYear: builder.query({ 
      query: ({ year, from = 0, count = 20 }) => `year_tracks?year=${year}&from=${from}&count=${count}`,
      providesTags: ['Recording']
    }),
    
    // Получение записей по жанру
    getRecordingsByGenre: builder.query({ 
      query: ({ id, from = 0, count = 20 }) => `genres_tracks?id=${id}&from=${from}&count=${count}`,
      providesTags: ['Recording']
    }),
    
    // Получение записей по тегам (POST запрос с массивом тегов)
    getRecordingsByTags: builder.mutation({ 
      query: ({ tags, from = 0, count = 20 }) => ({
        url: `tag_tracks?from=${from}&count=${count}`,
        method: 'POST',
        body: tags
      }),
      invalidatesTags: ['Recording']
    }),
    
    // Получение записей по ключевым словам (POST запрос с массивом ключевых слов)
    getRecordingsByKeywords: builder.mutation({ 
      query: ({ keywords, from = 0, count = 20 }) => ({
        url: `keyword_tracks?from=${from}&count=${count}`,
        method: 'POST',
        body: keywords
      }),
      invalidatesTags: ['Recording']
    }),
    
    // Скачивание аудио
    downloadAudio: builder.query({ 
      query: (path) => {
        if (!path) {
          console.error('Ошибка: пустой путь к аудио');
          return { url: '' };
        }
        
        // Отладка пути
        console.log('Запрос на скачивание аудио, путь:', path);
        
        return {
          url: `download?path=${encodeURIComponent(path)}`,
          responseHandler: async (response) => {
            if (!response.ok) {
              const errorText = await response.text();
              console.error(`Ошибка загрузки аудио: ${response.status} ${response.statusText}`, errorText);
              throw new Error(`${response.status} ${response.statusText}`);
            }
            return response.blob();
          },
        };
      },
    }),
    
    // Загрузка аудио
    uploadAudio: builder.mutation({
      query: (formData) => ({
        url: 'upload',
        method: 'POST',
        body: formData,
      }),
      invalidatesTags: ['Recording'],
    }),
    
    // Редактирование автора
    editAuthor: builder.mutation({
      query: ({ authorId, authorName }) => ({
        url: `edit/author?authorId=${authorId}&authorName=${encodeURIComponent(authorName)}`,
        method: 'PATCH',
      }),
      invalidatesTags: ['Author', 'Recording'],
    }),
    
    // Редактирование жанра
    editGenre: builder.mutation({
      query: ({ genreId, genreName }) => ({
        url: `edit/genre?genreId=${genreId}&genreName=${encodeURIComponent(genreName)}`,
        method: 'PATCH',
      }),
      invalidatesTags: ['Genre', 'Recording'],
    }),
    
    // Редактирование аудио записи (полное изменение метаданных)
    editAudio: builder.mutation({
      query: (data) => ({
        url: 'edit/audio',
        method: 'PATCH',
        body: data,
      }),
      invalidatesTags: ['Recording'],
    }),
    
    // Редактирование названия аудио записи
    editAudioTitle: builder.mutation({
      query: ({ id, title }) => ({
        url: `edit/audio_title?id=${id}&title=${encodeURIComponent(title)}`,
        method: 'PATCH',
      }),
      invalidatesTags: ['Recording'],
    }),
    
    // Статистика (для дашборда) - используем заглушку
    getStatistics: builder.query({
      queryFn: () => {
        // Возвращаем заглушку вместо запроса к API
        return {
          data: {
            totalRecordings: 254,
            restoredRecordings: 168,
            totalAuthors: 87,
            totalTags: 42,
            tagStats: [
              { id: 1, name: "Военные песни", count: 78 },
              { id: 2, name: "Радиопередачи", count: 56 },
              { id: 3, name: "Фронтовые записи", count: 44 },
              { id: 4, name: "Речи", count: 32 },
              { id: 5, name: "Музыка военных лет", count: 28 }
            ],
            authorStats: [
              { id: 1, name: "Левитан Ю.Б.", count: 24 },
              { id: 2, name: "Александров А.В.", count: 18 },
              { id: 3, name: "Утесов Л.О.", count: 15 },
              { id: 4, name: "Шульженко К.И.", count: 12 },
              { id: 5, name: "Бернес М.Н.", count: 10 }
            ],
            yearStats: {
              "1941": 42,
              "1942": 68,
              "1943": 56,
              "1944": 49,
              "1945": 39
            }
          }
        };
      }
    }),
  }),
});

export const {
  useGetAllRecordingsQuery,
  useGetAuthorsQuery,
  useGetGenresQuery,
  useGetTagsQuery,
  useGetKeywordsQuery,
  useGetRecordingsByAuthorQuery,
  useGetRecordingsByYearQuery,
  useGetRecordingsByGenreQuery,
  useGetRecordingsByTagsMutation,
  useGetRecordingsByKeywordsMutation,
  useDownloadAudioQuery,
  useUploadAudioMutation,
  useEditAuthorMutation,
  useEditGenreMutation,
  useEditAudioMutation,
  useEditAudioTitleMutation,
  useGetStatisticsQuery,
} = audioArchiveApi; 