import React, { useState } from 'react';
import { useGetTagsQuery, useUploadRecordingMutation, useGetGenresQuery } from '../../redux/services/audioArchiveApi';
import { Loader, Error } from '../';

const UploadForm = () => {
  const [formData, setFormData] = useState({
    title: '',
    artistName: '',
    albumName: '',
    year: '',
    description: '',
    folderToUpload: 'uploads', // Папка по умолчанию
  });
  const [audioFile, setAudioFile] = useState(null);
  const [uploadStatus, setUploadStatus] = useState({ status: '', message: '' });
  const [uploadProgress, setUploadProgress] = useState(0);
  const [enhanceAudio, setEnhanceAudio] = useState(true);
  const [transcribeAudio, setTranscribeAudio] = useState(true);
  const [selectedTags, setSelectedTags] = useState([]);
  const [selectedGenres, setSelectedGenres] = useState([]);
  
  const { data: tags, isFetching: isTagsFetching, error: tagsError } = useGetTagsQuery();
  const { data: genres, isFetching: isGenresFetching, error: genresError } = useGetGenresQuery();
  
  // Используем мутацию из API
  const [uploadRecording, { isLoading: isUploading }] = useUploadRecordingMutation();

  const handleInputChange = (e) => {
    const { name, value } = e.target;
    setFormData({ ...formData, [name]: value });
  };

  const handleTagChange = (e) => {
    const value = Array.from(e.target.selectedOptions, option => option.value);
    setSelectedTags(value);
  };

  const handleGenreChange = (e) => {
    const value = Array.from(e.target.selectedOptions, option => option.value);
    setSelectedGenres(value);
  };

  const handleFileChange = (e) => {
    setAudioFile(e.target.files[0]);
  };

  const handleEnhanceChange = (e) => {
    setEnhanceAudio(e.target.checked);
  };

  const handleTranscribeChange = (e) => {
    setTranscribeAudio(e.target.checked);
  };

  // Обработка отправки формы загрузки
  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!audioFile) {
      setUploadStatus({ status: 'error', message: 'Пожалуйста, выберите аудиофайл' });
      return;
    }

    try {
      // Начало загрузки
      setUploadStatus({ 
        status: 'uploading', 
        message: 'Загрузка файла на сервер...' 
      });

      // Создаем FormData для отправки файла
      const submitData = new FormData();
      submitData.append('file', audioFile);
      submitData.append('title', formData.title);
      submitData.append('artistName', formData.artistName);
      submitData.append('albumName', formData.albumName);
      submitData.append('folderToUpload', formData.folderToUpload);
      
      if (formData.year) {
        submitData.append('year', parseInt(formData.year));
      }
      
      if (formData.description) {
        submitData.append('description', formData.description);
      }
      
      // Отправляем тэги если они выбраны
      if (selectedTags.length > 0) {
        submitData.append('tags', JSON.stringify(selectedTags));
      }
      
      // Отправляем жанры если они выбраны
      if (selectedGenres.length > 0) {
        submitData.append('genres', JSON.stringify(selectedGenres));
      }
      
      // Флаги для обработки
      submitData.append('enhanceAudio', enhanceAudio);
      submitData.append('transcribeAudio', transcribeAudio);
      
      // Отправка на сервер
      const response = await uploadRecording(submitData).unwrap();
      
      // Обработка успешного ответа
      if (response.success) {
        setUploadStatus({ 
          status: 'success', 
          message: 'Файл успешно загружен и отправлен на обработку.' 
        });
        
        // Сбрасываем форму
        setFormData({
          title: '',
          artistName: '',
          albumName: '',
          year: '',
          description: '',
          folderToUpload: 'uploads',
        });
        setAudioFile(null);
        setSelectedTags([]);
        setSelectedGenres([]);
        setUploadProgress(0);
      } else {
        setUploadStatus({ 
          status: 'error', 
          message: response.message || 'Произошла ошибка при загрузке файла.' 
        });
      }
    } catch (error) {
      console.error('Ошибка загрузки:', error);
      setUploadStatus({ 
        status: 'error', 
        message: `Ошибка: ${error.message || 'Неизвестная ошибка при загрузке файла'}` 
      });
    }
  };

  if (isTagsFetching || isGenresFetching) return <Loader title="Загрузка..." />;
  if (tagsError || genresError) return <Error />;

  return (
    <div className="bg-black/20 backdrop-blur-md rounded-lg p-6 w-full max-w-4xl mx-auto">
      <h2 className="text-white text-2xl font-bold mb-6">Загрузка новой записи</h2>
      
      <form onSubmit={handleSubmit} className="space-y-6">
        {/* Основная информация */}
        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
          <div>
            <label className="block text-gray-200 mb-2">Название записи</label>
            <input
              type="text"
              name="title"
              value={formData.title}
              onChange={handleInputChange}
              className="w-full p-3 bg-black/40 text-white rounded-md"
              placeholder="Введите название песни"
              required
            />
          </div>
          
          <div>
            <label className="block text-gray-200 mb-2">Автор/Исполнитель</label>
            <input
              type="text"
              name="artistName"
              value={formData.artistName}
              onChange={handleInputChange}
              className="w-full p-3 bg-black/40 text-white rounded-md"
              placeholder="Имя автора или исполнителя"
            />
          </div>
          
          <div>
            <label className="block text-gray-200 mb-2">Альбом</label>
            <input
              type="text"
              name="albumName"
              value={formData.albumName}
              onChange={handleInputChange}
              className="w-full p-3 bg-black/40 text-white rounded-md"
              placeholder="Название альбома (если есть)"
            />
          </div>
          
          <div>
            <label className="block text-gray-200 mb-2">Год</label>
            <input
              type="number"
              name="year"
              value={formData.year}
              onChange={handleInputChange}
              className="w-full p-3 bg-black/40 text-white rounded-md"
              placeholder="Год записи"
              min="1900"
              max={new Date().getFullYear()}
            />
          </div>
        </div>
        
        {/* Описание */}
        <div>
          <label className="block text-gray-200 mb-2">Описание</label>
          <textarea
            name="description"
            value={formData.description}
            onChange={handleInputChange}
            className="w-full p-3 bg-black/40 text-white rounded-md"
            placeholder="Дополнительная информация о записи"
            rows="3"
          />
        </div>
        
        {/* Теги и жанры */}
        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
          <div>
            <label className="block text-gray-200 mb-2">Тематические теги</label>
            <select
              multiple
              className="w-full p-3 bg-black/40 text-white rounded-md"
              onChange={handleTagChange}
              value={selectedTags}
            >
              {tags?.map(tag => (
                <option key={tag.id} value={tag.name}>
                  {tag.name}
                </option>
              ))}
            </select>
            <p className="text-gray-400 text-sm mt-1">Ctrl+клик для выбора нескольких</p>
          </div>
          
          <div>
            <label className="block text-gray-200 mb-2">Жанры</label>
            <select
              multiple
              className="w-full p-3 bg-black/40 text-white rounded-md"
              onChange={handleGenreChange}
              value={selectedGenres}
            >
              {genres?.map(genre => (
                <option key={genre.id} value={genre.name}>
                  {genre.name}
                </option>
              ))}
            </select>
            <p className="text-gray-400 text-sm mt-1">Ctrl+клик для выбора нескольких</p>
          </div>
        </div>
        
        {/* Загрузка файла */}
        <div>
          <label className="block text-gray-200 mb-2">Аудиофайл</label>
          <input
            type="file"
            accept="audio/*"
            onChange={handleFileChange}
            className="w-full p-3 bg-black/40 text-white rounded-md"
            required
          />
          <p className="text-gray-400 text-sm mt-1">Поддерживаемые форматы: MP3, WAV, FLAC</p>
        </div>
        
        {/* Папка для загрузки */}
        <div>
          <label className="block text-gray-200 mb-2">Папка для загрузки</label>
          <input
            type="text"
            name="folderToUpload"
            value={formData.folderToUpload}
            onChange={handleInputChange}
            className="w-full p-3 bg-black/40 text-white rounded-md"
            placeholder="Папка для загрузки (например, 'uploads' или 'original')"
          />
        </div>
        
        {/* Опции обработки */}
        <div className="flex flex-col space-y-3">
          <div className="flex items-center">
            <input
              type="checkbox"
              id="enhanceAudio"
              checked={enhanceAudio}
              onChange={handleEnhanceChange}
              className="mr-2 h-5 w-5"
            />
            <label htmlFor="enhanceAudio" className="text-gray-200">
              Улучшить качество звука с помощью ИИ
            </label>
          </div>
          
          <div className="flex items-center">
            <input
              type="checkbox"
              id="transcribeAudio"
              checked={transcribeAudio}
              onChange={handleTranscribeChange}
              className="mr-2 h-5 w-5"
            />
            <label htmlFor="transcribeAudio" className="text-gray-200">
              Распознать текст песни
            </label>
          </div>
        </div>
        
        {/* Статус загрузки */}
        {uploadStatus.status && (
          <div className={`p-3 rounded-md ${
            uploadStatus.status === 'error' ? 'bg-red-900/50 text-red-200' : 
            uploadStatus.status === 'success' ? 'bg-green-900/50 text-green-200' : 
            'bg-blue-900/50 text-blue-200'
          }`}>
            {uploadStatus.message}
            
            {uploadStatus.status === 'uploading' && (
              <div className="w-full bg-gray-700 rounded-full h-2.5 mt-2">
                <div 
                  className="bg-blue-600 h-2.5 rounded-full" 
                  style={{ width: `${uploadProgress}%` }}
                ></div>
              </div>
            )}
          </div>
        )}
        
        {/* Кнопка отправки */}
        <button
          type="submit"
          disabled={isUploading}
          className={`w-full py-3 rounded-md font-medium text-white 
            ${isUploading ? 'bg-blue-800/50 cursor-not-allowed' : 'bg-blue-700 hover:bg-blue-600'}`}
        >
          {isUploading ? 'Загрузка...' : 'Загрузить запись'}
        </button>
      </form>
    </div>
  );
};

export default UploadForm; 