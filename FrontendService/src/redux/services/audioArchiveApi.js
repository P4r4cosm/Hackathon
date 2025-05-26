import { createApi, fetchBaseQuery } from '@reduxjs/toolkit/query/react';

export const audioArchiveApi = createApi({
  reducerPath: 'audioArchiveApi',
  baseQuery: fetchBaseQuery({
    baseUrl: 'http://localhost:8000/',
    credentials: 'include', // Включаем отправку и получение куки для JWT-аутентификации
    prepareHeaders: (headers) => {
      const token = localStorage.getItem('token');
      if (token) {
        headers.set('Authorization', `Bearer ${token}`);
      }
      headers.set('Content-Type', 'application/json');
      return headers;
    },
  }),
  tagTypes: ['Recording', 'Author', 'Tag'],
  endpoints: (builder) => ({
    // Получение списка записей
    getAllRecordings: builder.query({
      query: (params) => `/tracks?from=${params?.from || 0}&count=${params?.count || 10}`,
      providesTags: ['Recording'],
      transformResponse: (response) => {
        // Преобразование данных к формату, ожидаемому фронтендом
        return response.map(item => ({
          id: item.id,
          title: item.title,
          author: item.author?.name || 'Неизвестный автор',
          authorId: item.author?.id,
          year: item.year,
          filePath: item.filePath,
          restoredFilePath: item.restoredFilePath,
          tags: item.thematicTags?.map(tag => ({ id: tag.id, name: tag.name })) || [],
          keywords: item.keywords || []
        }));
      }
    }),
    
    // Получение записей по тегу
    getRecordingsByTag: builder.query({
      query: (params) => ({
        url: '/tag_tracks',
        method: 'POST',
        body: { 
          tags: [params.tagId], 
          from: params.from || 0, 
          count: params.count || 10 
        }
      }),
      transformResponse: (response) => {
        return response.map(item => ({
          id: item.id,
          title: item.title,
          author: item.author?.name || 'Неизвестный автор',
          authorId: item.author?.id,
          year: item.year,
          filePath: item.filePath,
          restoredFilePath: item.restoredFilePath,
          tags: item.thematicTags?.map(tag => ({ id: tag.id, name: tag.name })) || []
        }));
      }
    }),
    
    // Получение записей по году
    getRecordingsByYear: builder.query({
      query: (params) => `/year_tracks?year=${params.year}&from=${params.from || 0}&count=${params.count || 10}`,
      transformResponse: (response) => {
        return response.map(item => ({
          id: item.id,
          title: item.title,
          author: item.author?.name || 'Неизвестный автор',
          authorId: item.author?.id,
          year: item.year,
          filePath: item.filePath,
          restoredFilePath: item.restoredFilePath,
          tags: item.thematicTags?.map(tag => ({ id: tag.id, name: tag.name })) || []
        }));
      }
    }),
    
    // Получение записей по автору
    getRecordingsByAuthor: builder.query({
      query: (params) => `/author_tracks?id=${params.authorId}&from=${params.from || 0}&count=${params.count || 10}`,
      transformResponse: (response) => {
        return response.map(item => ({
          id: item.id,
          title: item.title,
          author: item.author?.name || 'Неизвестный автор',
          authorId: item.author?.id,
          year: item.year,
          filePath: item.filePath,
          restoredFilePath: item.restoredFilePath,
          tags: item.thematicTags?.map(tag => ({ id: tag.id, name: tag.name })) || []
        }));
      }
    }),
    
    // Поиск записей (пока используем полный список и фильтруем на фронтенде)
    searchRecordings: builder.query({
      query: (params) => `/tracks?from=0&count=100`, // Получаем больше записей для поиска
      transformResponse: (response, _, searchTerm) => {
        const filteredResults = response.filter(rec => 
          rec.title.toLowerCase().includes(searchTerm.toLowerCase()) || 
          rec.author?.name.toLowerCase().includes(searchTerm.toLowerCase())
        );
        
        return filteredResults.map(item => ({
          id: item.id,
          title: item.title,
          author: item.author?.name || 'Неизвестный автор',
          authorId: item.author?.id,
          year: item.year,
          filePath: item.filePath,
          restoredFilePath: item.restoredFilePath,
          tags: item.thematicTags?.map(tag => ({ id: tag.id, name: tag.name })) || []
        }));
      }
    }),
    
    // Детали записи
    getRecordingDetails: builder.query({
      query: (id) => `/track/${id}`,
      transformResponse: (response) => ({
        id: response.id,
        title: response.title,
        author: response.author?.name || 'Неизвестный автор',
        authorId: response.author?.id,
        year: response.year,
        filePath: response.filePath,
        restoredFilePath: response.restoredFilePath,
        tags: response.thematicTags?.map(tag => ({ id: tag.id, name: tag.name })) || [],
        keywords: response.keywords || [],
        description: response.description
      })
    }),
    
    // Получение текста записи
    getRecordingText: builder.query({
      query: (id) => `/track_text/${id}`,
      transformResponse: (response) => ({
        fullText: response.fullText || '',
        transcriptSegments: response.transcriptSegments || []
      })
    }),
    
    // Получение похожих записей - поиск по тегам
    getRelatedRecordings: builder.query({
      async queryFn(id, _queryApi, _extraOptions, fetchWithBQ) {
        // Сначала получаем детали записи для извлечения тегов
        const detailsResult = await fetchWithBQ(`/track/${id}`);
        if (detailsResult.error) return { error: detailsResult.error };
        
        const recording = detailsResult.data;
        if (!recording || !recording.thematicTags || recording.thematicTags.length === 0) {
          return { data: [] };
        }
        
        // Получаем записи с похожими тегами
        const tagNames = recording.thematicTags.map(tag => tag.name);
        const tagsResult = await fetchWithBQ({
          url: '/tag_tracks',
          method: 'POST',
          body: { tags: tagNames, from: 0, count: 4 }
        });
        
        if (tagsResult.error) return { error: tagsResult.error };
        
        // Фильтруем, чтобы исключить текущую запись
        const related = tagsResult.data
          .filter(rec => rec.id !== id)
          .slice(0, 3)
          .map(item => ({
            id: item.id,
            title: item.title,
            author: item.author?.name || 'Неизвестный автор',
            authorId: item.author?.id,
            year: item.year,
            filePath: item.filePath,
            restoredFilePath: item.restoredFilePath,
            tags: item.thematicTags?.map(tag => ({ id: tag.id, name: tag.name })) || []
          }));
        
        return { data: related };
      }
    }),
    
    // Список авторов
    getAuthors: builder.query({
      query: () => '/authors',
      transformResponse: (response) => {
        return response.map(author => ({
          id: author.id,
          name: author.name,
          biography: author.biography || 'Информация отсутствует'
        }));
      }
    }),
    
    // Детали автора (получаем из списка авторов)
    getAuthorDetails: builder.query({
      async queryFn(id, _queryApi, _extraOptions, fetchWithBQ) {
        const result = await fetchWithBQ('/authors');
        if (result.error) return { error: result.error };
        
        const author = result.data.find(a => a.id === parseInt(id));
        if (!author) {
          return { 
            data: { 
              id, 
              name: 'Неизвестный автор', 
              biography: 'Информация отсутствует' 
            } 
          };
        }
        
        return { 
          data: { 
            id: author.id, 
            name: author.name, 
            biography: author.biography || 'Информация отсутствует' 
          } 
        };
      }
    }),
    
    // Список тегов/категорий
    getTags: builder.query({
      query: () => '/tags',
      transformResponse: (response) => {
        return response.map(tag => ({
          id: tag.id,
          name: tag.name
        }));
      }
    }),
    
    // Список жанров
    getGenres: builder.query({
      query: () => '/genres',
      transformResponse: (response) => {
        return response.map(genre => ({
          id: genre.id,
          name: genre.name
        }));
      }
    }),
    
    // Список ключевых слов
    getKeywords: builder.query({
      query: () => '/keywords'
    }),
    
    // Статистика (компилируем из разных источников)
    getStatistics: builder.query({
      async queryFn(_arg, _queryApi, _extraOptions, fetchWithBQ) {
        try {
          // Получаем общее количество записей
          const recordingsResult = await fetchWithBQ('/tracks?from=0&count=1');
          if (recordingsResult.error) return { error: recordingsResult.error };
          
          // Получаем авторов
          const authorsResult = await fetchWithBQ('/authors');
          if (authorsResult.error) return { error: authorsResult.error };
          
          // Получаем теги
          const tagsResult = await fetchWithBQ('/tags');
          if (tagsResult.error) return { error: tagsResult.error };
          
          // Здесь можно реализовать логику подсчета статистики по годам и тегам
          // Для этого потребуется получить все записи
          const allRecordingsResult = await fetchWithBQ('/tracks?from=0&count=100');
          if (allRecordingsResult.error) return { error: allRecordingsResult.error };
          
          // Статистика по годам
          const yearMap = new Map();
          allRecordingsResult.data.forEach(rec => {
            if (rec.year) {
              const count = yearMap.get(rec.year) || 0;
              yearMap.set(rec.year, count + 1);
            }
          });
          
          const yearStats = Array.from(yearMap.entries()).map(([year, count]) => ({
            year,
            count
          })).sort((a, b) => a.year - b.year);
          
          // Статистика по тегам
          const tagMap = new Map();
          allRecordingsResult.data.forEach(rec => {
            if (rec.thematicTags) {
              rec.thematicTags.forEach(tag => {
                const count = tagMap.get(tag.name) || 0;
                tagMap.set(tag.name, count + 1);
              });
            }
          });
          
          const tagStats = Array.from(tagMap.entries()).map(([tag, count]) => ({
            tag,
            count
          })).sort((a, b) => b.count - a.count);
          
          return {
            data: {
              totalRecordings: allRecordingsResult.data.length,
              totalAuthors: authorsResult.data.length,
              yearStats,
              tagStats
            }
          };
        } catch (error) {
          return { error };
        }
      }
    }),
    
    // Загрузка новой записи
    uploadRecording: builder.mutation({
      query: (formData) => ({
        url: '/upload',
        method: 'POST',
        body: formData,
        formData: true,
      }),
      invalidatesTags: ['Recording']
    }),
    
    // Обновление транскрипции
    updateTranscription: builder.mutation({
      query: ({ recordingId, text }) => ({
        url: `/track_text/${recordingId}`,
        method: 'POST',
        body: { fullText: text },
      }),
      invalidatesTags: ['Recording']
    }),
    
    // Скачивание аудиофайла
    downloadAudio: builder.query({
      query: (path) => ({
        url: `/download?path=${encodeURIComponent(path)}`,
        responseHandler: response => response.blob(),
      })
    })
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
  useGetGenresQuery,
  useGetKeywordsQuery,
  useGetStatisticsQuery,
  useUploadRecordingMutation,
  useUpdateTranscriptionMutation,
  useDownloadAudioQuery,
} = audioArchiveApi; 