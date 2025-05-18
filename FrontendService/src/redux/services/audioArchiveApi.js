import { createApi, fetchBaseQuery } from '@reduxjs/toolkit/query/react';

export const audioArchiveApi = createApi({
  reducerPath: 'audioArchiveApi',
  baseQuery: fetchBaseQuery({
    baseUrl: 'http://localhost:8000/',
    credentials: 'include', // Включаем отправку и получение куки для JWT-аутентификации
    prepareHeaders: (headers) => {
      // Если нужны дополнительные заголовки
      headers.set('Content-Type', 'application/json');
      return headers;
    },
  }),
  tagTypes: ['Recording', 'Author', 'Tag'],
  endpoints: (builder) => ({
    // Получение списка записей
    getAllRecordings: builder.query({ 
      query: () => 'tracks' 
    }),
    
    // Получение записей по категории/тегу
    getRecordingsByTag: builder.query({ 
      query: (tag) => `tracks?tag=${tag}` 
    }),
    
    // Получение записей по году
    getRecordingsByYear: builder.query({ 
      query: (year) => `tracks?year=${year}` 
    }),
    
    // Получение записей по автору
    getRecordingsByAuthor: builder.query({ 
      query: (authorId) => `tracks?authorId=${authorId}` 
    }),
    
    // Поиск записей
    searchRecordings: builder.query({ 
      query: (searchTerm) => `tracks?query=${searchTerm}` 
    }),
    
    // Детали записи
    getRecordingDetails: builder.query({ 
      query: (id) => `track/${id}` 
    }),
    
    // Получение текста записи
    getRecordingText: builder.query({ 
      query: (id) => `track_text/${id}` 
    }),
    
    // Получение похожих записей (временно используем обычный список записей)
    getRelatedRecordings: builder.query({ 
      query: (id) => `tracks?related=${id}` 
    }),
    
    // Список авторов (временно возвращаем пустой массив, т.к. эндпоинта нет)
    getAuthors: builder.query({
      queryFn: () => ({ data: [] })
    }),
    
    // Детали автора (временно возвращаем заглушку, т.к. эндпоинта нет)
    getAuthorDetails: builder.query({
      queryFn: (id) => ({ 
        data: { 
          id, 
          name: 'Неизвестный автор', 
          biography: 'Информация отсутствует' 
        } 
      })
    }),
    
    // Список тегов/категорий (временно возвращаем пустой массив, т.к. эндпоинта нет)
    getTags: builder.query({
      queryFn: () => ({ data: [] })
    }),
    
    // Статистика (временно возвращаем заглушку, т.к. эндпоинта нет)
    getStatistics: builder.query({
      queryFn: () => ({ 
        data: { 
          totalRecordings: 0, 
          totalAuthors: 0,
          yearStats: [] 
        } 
      })
    }),
    
    // Загрузка новой записи
    uploadRecording: builder.mutation({
      query: (formData) => ({
        url: 'upload',
        method: 'POST',
        body: formData,
      }),
      invalidatesTags: ['Recording'],
    }),
    
    // Скачивание аудиофайла (позволяет получить blob)
    downloadAudio: builder.query({
      query: (path) => ({
        url: `download`,
        params: { path },
        responseHandler: (response) => response.blob(),
      }),
    }),
    
    // Обновление метаданных записи (временно деактивировано, т.к. эндпоинта нет)
    updateRecording: builder.mutation({
      queryFn: () => ({ data: { success: true } })
    }),
    
    // Обновление транскрипции текста (временно деактивировано, т.к. эндпоинта нет)
    updateTranscription: builder.mutation({
      queryFn: () => ({ data: { success: true } })
    }),
    
    // Управление тегами (временно деактивировано, т.к. эндпоинта нет)
    manageTag: builder.mutation({
      queryFn: () => ({ data: { success: true } })
    }),
  }),
});

export const {
  useGetAllRecordingsQuery,
  useGetRecordingsByTagQuery,
  useGetRecordingsByYearQuery,
  useGetRecordingsByAuthorQuery,
  useSearchRecordingsQuery,
  useGetRecordingDetailsQuery,
  useGetRecordingTextQuery,
  useGetRelatedRecordingsQuery,
  useGetAuthorsQuery,
  useGetAuthorDetailsQuery,
  useGetTagsQuery,
  useGetStatisticsQuery,
  useUploadRecordingMutation,
  useUpdateRecordingMutation,
  useUpdateTranscriptionMutation,
  useManageTagMutation,
  useDownloadAudioQuery,
} = audioArchiveApi; 