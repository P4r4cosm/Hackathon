import { createApi, fetchBaseQuery } from '@reduxjs/toolkit/query/react';

export const audioArchiveApi = createApi({
  reducerPath: 'audioArchiveApi',
  baseQuery: fetchBaseQuery({
    baseUrl: 'http://localhost:8000/api/',
  }),
  tagTypes: ['Recording', 'Author', 'Tag'],
  endpoints: (builder) => ({
    // Получение списка записей
    getAllRecordings: builder.query({ 
      query: () => 'recordings' 
    }),
    
    // Получение записей по категории/тегу
    getRecordingsByTag: builder.query({ 
      query: (tag) => `recordings/tag/${tag}` 
    }),
    
    // Получение записей по году
    getRecordingsByYear: builder.query({ 
      query: (year) => `recordings/year/${year}` 
    }),
    
    // Получение записей по автору
    getRecordingsByAuthor: builder.query({ 
      query: (authorId) => `recordings/author/${authorId}` 
    }),
    
    // Поиск записей
    searchRecordings: builder.query({ 
      query: (searchTerm) => `recordings/search?query=${searchTerm}` 
    }),
    
    // Детали записи
    getRecordingDetails: builder.query({ 
      query: (id) => `recordings/${id}` 
    }),
    
    // Получение похожих записей
    getRelatedRecordings: builder.query({ 
      query: (id) => `recordings/${id}/related` 
    }),
    
    // Список авторов
    getAuthors: builder.query({
      query: () => 'authors'
    }),
    
    // Детали автора
    getAuthorDetails: builder.query({
      query: (id) => `authors/${id}`
    }),
    
    // Список тегов/категорий
    getTags: builder.query({
      query: () => 'tags'
    }),
    
    // Статистика (для дашборда)
    getStatistics: builder.query({
      query: () => 'statistics'
    }),
    
    // Загрузка новой записи
    uploadRecording: builder.mutation({
      query: (formData) => ({
        url: 'audio/upload',
        method: 'POST',
        body: formData,
      }),
      invalidatesTags: ['Recording'],
    }),
    
    // Скачивание аудиофайла (позволяет получить blob)
    downloadAudio: builder.query({
      query: (path) => ({
        url: `audio/download`,
        params: { path },
        responseHandler: (response) => response.blob(),
      }),
    }),
    
    // Обновление метаданных записи
    updateRecording: builder.mutation({
      query: ({ id, data }) => ({
        url: `recordings/${id}`,
        method: 'PUT',
        body: data,
      }),
      invalidatesTags: ['Recording'],
    }),
    
    // Обновление транскрипции текста
    updateTranscription: builder.mutation({
      query: ({ id, text }) => ({
        url: `recordings/${id}/transcription`,
        method: 'PUT',
        body: { text },
      }),
      invalidatesTags: ['Recording'],
    }),
    
    // Управление тегами
    manageTag: builder.mutation({
      query: ({ action, tagData }) => ({
        url: action === 'create' ? 'tags' : `tags/${tagData.id}`,
        method: action === 'create' ? 'POST' : action === 'update' ? 'PUT' : 'DELETE',
        body: action !== 'delete' ? tagData : undefined,
      }),
      invalidatesTags: ['Tag'],
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