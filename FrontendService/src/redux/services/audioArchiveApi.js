import { createApi, fetchBaseQuery } from '@reduxjs/toolkit/query/react';

// Захардкоженные данные для имитации API
const mockData = {
  recordings: [
    {
      id: '1',
      title: 'Священная война',
      author: 'А. Александров',
      authorId: '1',
      year: 1941,
      coverImage: 'https://lh3.googleusercontent.com/proxy/FBZz0pVv5DQyUy-ntOkoY7-yfOzcgaJtf2ArZTPNpbJtvJNmEHSWMpsiSrgjlYxJsBdG8jmGTzsncGHp7mTnhl0lUM6rx4pYxwDWcfGs',
      originalAudioUrl: 'https://storage.googleapis.com/public-sounds-295903.appspot.com/svyaschennaya.mp3',
      restoredAudioUrl: 'https://storage.googleapis.com/public-sounds-295903.appspot.com/svyaschennaya_restored.mp3',
      tags: [
        { id: '1', name: 'Патриотические' },
        { id: '2', name: 'Хоровые' }
      ],
      text: `Вставай, страна огромная,\nВставай на смертный бой\nС фашистской силой тёмною,\nС проклятою ордой.\n\nПусть ярость благородная\nВскипает, как волна, —\nИдёт война народная,\nСвященная война!`
    },
    {
      id: '2',
      title: 'Катюша',
      author: 'М. Блантер',
      authorId: '2',
      year: 1938,
      coverImage: 'https://kuban24.tv/wp-content/uploads/2020/04/Katyuscha.jpg',
      originalAudioUrl: 'https://storage.googleapis.com/public-sounds-295903.appspot.com/katyusha.mp3',
      restoredAudioUrl: 'https://storage.googleapis.com/public-sounds-295903.appspot.com/katyusha_restored.mp3',
      tags: [
        { id: '3', name: 'Лирические' },
        { id: '4', name: 'Довоенные' }
      ],
      text: `Расцветали яблони и груши,\nПоплыли туманы над рекой.\nВыходила на берег Катюша,\nНа высокий берег на крутой.`
    },
    {
      id: '3',
      title: 'Синий платочек',
      author: 'Е. Петербургский',
      authorId: '3',
      year: 1940,
      coverImage: 'https://txt-music.ru/wp-content/uploads/2018/03/Strochit-pulemetchik-Za-sinii-platochek-CHtob-byl-na-plechah-dorogih.jpg',
      originalAudioUrl: 'https://storage.googleapis.com/public-sounds-295903.appspot.com/siniy_platochek.mp3', 
      restoredAudioUrl: 'https://storage.googleapis.com/public-sounds-295903.appspot.com/siniy_platochek_restored.mp3',
      tags: [
        { id: '3', name: 'Лирические' },
        { id: '5', name: 'Вальс' }
      ],
      text: `Синенький скромный платочек\nПадал с опущенных плеч.\nТы говорила, что не забудешь\nЛасковых, радостных встреч.`
    },
    {
      id: '4',
      title: 'Тёмная ночь',
      author: 'Н. Богословский',
      authorId: '4',
      year: 1943,
      coverImage: 'https://cdn-image.zvuk.com/pic?type=release&id=8657618&size=medium&hash=569941bf-ea57-478d-a058-2a1f331b230e',
      originalAudioUrl: 'https://storage.googleapis.com/public-sounds-295903.appspot.com/temnaya_noch.mp3',
      restoredAudioUrl: 'https://storage.googleapis.com/public-sounds-295903.appspot.com/temnaya_noch_restored.mp3',
      tags: [
        { id: '3', name: 'Лирические' },
        { id: '6', name: 'Из кинофильмов' }
      ],
      text: `Темная ночь, только пули свистят по степи,\nТолько ветер гудит в проводах, тускло звезды мерцают.\nВ темную ночь ты, любимая, знаю, не спишь,\nИ у детской кроватки тайком ты слезу утираешь.`
    },
    {
      id: '5',
      title: 'День Победы',
      author: 'Д. Тухманов',
      authorId: '5',
      year: 1975,
      coverImage: 'https://cdn.er.ru/media/news/May2021/UDAGBUc2KpmthBYwLAHn.jpg',
      originalAudioUrl: 'https://storage.googleapis.com/public-sounds-295903.appspot.com/den_pobedy.mp3',
      restoredAudioUrl: 'https://storage.googleapis.com/public-sounds-295903.appspot.com/den_pobedy_restored.mp3',
      tags: [
        { id: '1', name: 'Патриотические' },
        { id: '7', name: 'Послевоенные' }
      ],
      text: `День Победы, как он был от нас далек,\nКак в костре потухшем таял уголек.\nБыли версты, обгорелые, в пыли, —\nЭтот день мы приближали как могли.`
    }
  ],
  authors: [
    { id: '1', name: 'Александр Васильевич Александров', biography: 'Советский композитор, дирижёр, хормейстер, педагог. Автор музыки гимна СССР и России. Основатель и первый художественный руководитель Ансамбля песни и пляски Советской Армии.' },
    { id: '2', name: 'Матвей Исаакович Блантер', biography: 'Советский композитор, народный артист СССР, Герой Социалистического Труда. Автор более 200 песен, включая знаменитую "Катюшу".' },
    { id: '3', name: 'Ежи Петербургский', biography: 'Польский и советский композитор, пианист, скрипач и дирижёр. Автор таких песен, как "Синий платочек" и "Утомленное солнце".' },
    { id: '4', name: 'Никита Владимирович Богословский', biography: 'Советский композитор, дирижёр, пианист. Создатель популярных песен к кинофильмам, среди которых "Тёмная ночь" из фильма "Два бойца".' },
    { id: '5', name: 'Давид Фёдорович Тухманов', biography: 'Советский и российский композитор, народный артист России. Автор песни "День Победы", написанной к 30-летию Победы в Великой Отечественной войне.' }
  ],
  tags: [
    { id: '1', name: 'Патриотические' },
    { id: '2', name: 'Хоровые' },
    { id: '3', name: 'Лирические' },
    { id: '4', name: 'Довоенные' },
    { id: '5', name: 'Вальс' },
    { id: '6', name: 'Из кинофильмов' },
    { id: '7', name: 'Послевоенные' },
    { id: '8', name: 'Марш' }
  ],
  statistics: {
    totalRecordings: 5,
    totalAuthors: 5,
    yearStats: [
      { year: 1938, count: 1 },
      { year: 1940, count: 1 },
      { year: 1941, count: 1 },
      { year: 1943, count: 1 },
      { year: 1975, count: 1 }
    ],
    tagStats: [
      { tag: 'Патриотические', count: 2 },
      { tag: 'Лирические', count: 3 },
      { tag: 'Хоровые', count: 1 },
      { tag: 'Довоенные', count: 1 },
      { tag: 'Вальс', count: 1 },
      { tag: 'Из кинофильмов', count: 1 },
      { tag: 'Послевоенные', count: 1 }
    ]
  }
};

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
      queryFn: () => ({ data: mockData.recordings })
    }),
    
    // Получение записей по категории/тегу
    getRecordingsByTag: builder.query({ 
      queryFn: (tagId) => ({ 
        data: mockData.recordings.filter(rec => 
          rec.tags.some(tag => tag.id === tagId)
        )
      })
    }),
    
    // Получение записей по году
    getRecordingsByYear: builder.query({ 
      queryFn: (year) => ({ 
        data: mockData.recordings.filter(rec => 
          rec.year.toString() === year.toString()
        )
      })
    }),
    
    // Получение записей по автору
    getRecordingsByAuthor: builder.query({ 
      queryFn: (authorId) => ({ 
        data: mockData.recordings.filter(rec => 
          rec.authorId === authorId
        )
      })
    }),
    
    // Поиск записей
    searchRecordings: builder.query({ 
      queryFn: (searchTerm) => ({ 
        data: mockData.recordings.filter(rec => 
          rec.title.toLowerCase().includes(searchTerm.toLowerCase()) || 
          rec.author.toLowerCase().includes(searchTerm.toLowerCase())
        )
      })
    }),
    
    // Детали записи
    getRecordingDetails: builder.query({ 
      queryFn: (id) => ({ 
        data: mockData.recordings.find(rec => rec.id === id) || null
      })
    }),
    
    // Получение текста записи
    getRecordingText: builder.query({ 
      queryFn: (id) => {
        const recording = mockData.recordings.find(rec => rec.id === id);
        return { data: recording ? recording.text : '' };
      }
    }),
    
    // Получение похожих записей
    getRelatedRecordings: builder.query({ 
      queryFn: (id) => {
        const recording = mockData.recordings.find(rec => rec.id === id);
        if (!recording) return { data: [] };
        
        // Находим записи с похожими тегами
        const relatedIds = new Set([id]); // Исключаем текущую запись
        const related = mockData.recordings
          .filter(rec => {
            if (relatedIds.has(rec.id)) return false;
            
            // Проверяем, есть ли общие теги
            const hasCommonTag = rec.tags.some(tag => 
              recording.tags.some(t => t.id === tag.id)
            );
            
            if (hasCommonTag) {
              relatedIds.add(rec.id);
              return true;
            }
            return false;
          })
          .slice(0, 3); // Ограничиваем до 3 похожих записей
          
        return { data: related };
      }
    }),
    
    // Список авторов
    getAuthors: builder.query({
      queryFn: () => ({ data: mockData.authors })
    }),
    
    // Детали автора
    getAuthorDetails: builder.query({
      queryFn: (id) => ({ 
        data: mockData.authors.find(a => a.id === id) || { 
          id, 
          name: 'Неизвестный автор', 
          biography: 'Информация отсутствует' 
        } 
      })
    }),
    
    // Список тегов/категорий
    getTags: builder.query({
      queryFn: () => ({ data: mockData.tags })
    }),
    
    // Статистика
    getStatistics: builder.query({
      queryFn: () => ({ data: mockData.statistics })
    }),
    
    // Загрузка новой записи (имитация)
    uploadRecording: builder.mutation({
      queryFn: (formData) => {
        // В реальной реализации здесь был бы запрос к API
        console.log('Имитация загрузки файла', formData);
        return { data: { success: true, id: 'new-recording-id' } };
      }
    }),
    
    // Скачивание аудиофайла (имитация)
    downloadAudio: builder.query({
      queryFn: (path) => {
        console.log('Имитация загрузки аудио по пути', path);
        return { data: new Blob(['fake audio data'], { type: 'audio/mpeg' }) };
      }
    }),
    
    // Обновление метаданных записи (имитация)
    updateRecording: builder.mutation({
      queryFn: (data) => {
        console.log('Имитация обновления записи', data);
        return { data: { success: true } };
      }
    }),
    
    // Обновление транскрипции текста (имитация)
    updateTranscription: builder.mutation({
      queryFn: (data) => {
        console.log('Имитация обновления транскрипции', data);
        return { data: { success: true } };
      }
    }),
    
    // Управление тегами (имитация)
    manageTag: builder.mutation({
      queryFn: (data) => {
        console.log('Имитация управления тегами', data);
        return { data: { success: true } };
      }
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