import React, { useState } from 'react';
import { 
  useGetTagsQuery, 
  useUploadAudioMutation,
  useGetGenresQuery 
} from '../../redux/services/audioArchiveApi';
import { Loader, Error } from '../';

const UploadForm = () => {
  const [formData, setFormData] = useState({
    title: '',
    artistName: '',
    albumName: '',
    year: '',
    genres: [],
    thematicTags: []
  });
  const [audioFile, setAudioFile] = useState(null);
  const [uploadStatus, setUploadStatus] = useState({ status: '', message: '' });

  const { data: tags, isFetching: isTagsFetching, error: tagsError } = useGetTagsQuery();
  const { data: genres, isFetching: isGenresFetching, error: genresError } = useGetGenresQuery();
  const [uploadAudio, { isLoading: isUploading }] = useUploadAudioMutation();

  const handleInputChange = (e) => {
    const { name, value } = e.target;
    setFormData({ ...formData, [name]: value });
  };

  const handleTagChange = (e) => {
    const value = Array.from(e.target.selectedOptions, option => option.value);
    setFormData({ ...formData, thematicTags: value });
  };

  const handleGenreChange = (e) => {
    const value = Array.from(e.target.selectedOptions, option => option.value);
    setFormData({ ...formData, genres: value });
  };

  const handleFileChange = (e) => {
    setAudioFile(e.target.files[0]);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!audioFile) {
      setUploadStatus({ status: 'error', message: 'Пожалуйста, выберите аудиофайл' });
      return;
    }

    try {
      const submitData = new FormData();
      submitData.append('File', audioFile);
      submitData.append('FolderToUpload', 'uploads');
      submitData.append('Title', formData.title);
      submitData.append('ArtistName', formData.artistName);
      submitData.append('AlbumName', formData.albumName);
      
      if (formData.year) {
        submitData.append('Year', formData.year);
      }
      
      // Добавляем жанры
      if (formData.genres && formData.genres.length > 0) {
        formData.genres.forEach((genreId) => {
          submitData.append('GenreNames', genreId);
        });
      }

      const response = await uploadAudio(submitData).unwrap();
      
      setUploadStatus({ 
        status: 'success', 
        message: 'Запись успешно загружена. Обработка начата.' 
      });
      
      // Сброс формы
      setFormData({
        title: '',
        artistName: '',
        albumName: '',
        year: '',
        genres: [],
        thematicTags: []
      });
      setAudioFile(null);
      document.getElementById('audioFile').value = '';
      
    } catch (error) {
      console.error('Ошибка загрузки:', error);
      setUploadStatus({ 
        status: 'error', 
        message: `Ошибка при загрузке: ${error.data?.message || error.message || 'Неизвестная ошибка'}` 
      });
    }
  };

  if (isTagsFetching || isGenresFetching) return <Loader title="Загрузка..." />;
  if (tagsError || genresError) return <Error />;

  return (
    <div className="flex flex-col bg-black/20 backdrop-blur-lg rounded-lg p-6 max-w-3xl mx-auto">
      <h2 className="text-white text-2xl font-bold mb-6">Загрузка военной аудиозаписи</h2>
      
      {uploadStatus.message && (
        <div className={`mb-4 p-3 rounded ${uploadStatus.status === 'success' ? 'bg-green-500/20' : 'bg-red-500/20'}`}>
          <p className="text-white">{uploadStatus.message}</p>
        </div>
      )}
      
      <form onSubmit={handleSubmit} className="flex flex-col space-y-4">
        <div className="flex flex-col">
          <label className="text-gray-300 mb-1">Название записи</label>
          <input
            type="text"
            name="title"
            value={formData.title}
            onChange={handleInputChange}
            required
            className="bg-black/30 text-white rounded p-2"
          />
        </div>
        
        <div className="flex flex-col">
          <label className="text-gray-300 mb-1">Автор/Исполнитель</label>
          <input
            type="text"
            name="artistName"
            value={formData.artistName}
            onChange={handleInputChange}
            required
            className="bg-black/30 text-white rounded p-2"
          />
        </div>

        <div className="flex flex-col">
          <label className="text-gray-300 mb-1">Альбом (необязательно)</label>
          <input
            type="text"
            name="albumName"
            value={formData.albumName}
            onChange={handleInputChange}
            className="bg-black/30 text-white rounded p-2"
          />
        </div>
        
        <div className="flex flex-col">
          <label className="text-gray-300 mb-1">Год записи</label>
          <input
            type="number"
            name="year"
            min="1900"
            max="1950"
            value={formData.year}
            onChange={handleInputChange}
            className="bg-black/30 text-white rounded p-2"
          />
        </div>
        
        <div className="flex flex-col">
          <label className="text-gray-300 mb-1">Жанры</label>
          <select
            name="genres"
            multiple
            value={formData.genres}
            onChange={handleGenreChange}
            className="bg-black/30 text-white rounded p-2 h-32"
          >
            {genres?.map((genre) => (
              <option key={genre.id} value={genre.name}>{genre.name}</option>
            ))}
          </select>
          <p className="text-gray-400 text-sm mt-1">Удерживайте Ctrl для выбора нескольких жанров</p>
        </div>
        
        <div className="flex flex-col">
          <label className="text-gray-300 mb-1">Категории/Теги</label>
          <select
            name="thematicTags"
            multiple
            value={formData.thematicTags}
            onChange={handleTagChange}
            className="bg-black/30 text-white rounded p-2 h-32"
          >
            {tags?.map((tag) => (
              <option key={tag.id} value={tag.name}>{tag.name}</option>
            ))}
          </select>
          <p className="text-gray-400 text-sm mt-1">Удерживайте Ctrl для выбора нескольких тегов</p>
        </div>
        
        <div className="flex flex-col">
          <label className="text-gray-300 mb-1">Аудиофайл (WAV, FLAC, MP3)</label>
          <input
            type="file"
            id="audioFile"
            accept=".wav,.flac,.mp3"
            onChange={handleFileChange}
            required
            className="bg-black/30 text-white rounded p-2"
          />
        </div>
        
        <button
          type="submit"
          disabled={isUploading}
          className="bg-blue-600 hover:bg-blue-700 text-white font-bold py-2 px-4 rounded mt-4 disabled:opacity-50"
        >
          {isUploading ? 'Загрузка...' : 'Загрузить запись'}
        </button>
      </form>
    </div>
  );
};

export default UploadForm; 