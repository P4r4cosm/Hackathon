import React, { useState } from 'react';
import { useGetTagsQuery, useUploadRecordingMutation, useSearchRecordingsQuery } from '../../redux/services/audioArchiveApi';
import { Loader, Error } from '../';

const UploadForm = () => {
  const [formData, setFormData] = useState({
    title: '',
    author: '',
    year: '',
    description: '',
    tags: [],
  });
  const [audioFile, setAudioFile] = useState(null);
  const [uploadStatus, setUploadStatus] = useState({ status: '', message: '' });
  const [uploadProgress, setUploadProgress] = useState(0);
  const [isRestoring, setIsRestoring] = useState(false);
  const [enhanceAudio, setEnhanceAudio] = useState(true);
  const [transcribeAudio, setTranscribeAudio] = useState(true);
  
  // Состояния для этапа модерации
  const [isInModeration, setIsInModeration] = useState(false);
  const [recognizedText, setRecognizedText] = useState('');
  const [generatedTimestamps, setGeneratedTimestamps] = useState([]);

  const { data: tags, isFetching: isTagsFetching, error: tagsError } = useGetTagsQuery();
  
  // Используем запрос для поиска и получения текста
  const { data: searchResults } = useSearchRecordingsQuery(
    formData.title || 'пустой запрос', 
    { skip: !formData.title }
  );
  
  // Используем мутацию из API, но в имитационном режиме
  const [uploadRecording, { isLoading: isUploading }] = useUploadRecordingMutation();

  const handleInputChange = (e) => {
    const { name, value } = e.target;
    setFormData({ ...formData, [name]: value });
  };

  const handleTagChange = (e) => {
    const value = Array.from(e.target.selectedOptions, option => option.value);
    setFormData({ ...formData, tags: value });
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
  
  const handleTextChange = (e) => {
    setRecognizedText(e.target.value);
  };

  // Имитация процесса загрузки с отображением прогресса
  const simulateUpload = () => {
    return new Promise((resolve) => {
      let progress = 0;
      const interval = setInterval(() => {
        progress += 10;
        setUploadProgress(progress);
        
        if (progress >= 100) {
          clearInterval(interval);
          resolve();
        }
      }, 300);
    });
  };
  
  // Имитация процесса восстановления аудио
  const simulateRestoration = () => {
    return new Promise((resolve) => {
      setIsRestoring(true);
      setTimeout(() => {
        setIsRestoring(false);
        resolve();
      }, 3000);
    });
  };
  
  // Имитация распознавания текста - поиск в API
  const simulateTextRecognition = () => {
    return new Promise((resolve) => {
      setTimeout(() => {
        let foundText = '';
        let timestamps = [];
        
        if (searchResults && searchResults.length > 0) {
          // Находим наиболее подходящую запись
          const matchingRecord = searchResults.find(
            rec => rec.title.toLowerCase().includes(formData.title.toLowerCase())
          ) || searchResults[0];
          
          // Берем текст песни из найденной записи
          foundText = matchingRecord.text || '';
          
          // Если есть временные метки в записи, используем их
          if (matchingRecord.timestamps && matchingRecord.timestamps.length > 0) {
            timestamps = matchingRecord.timestamps;
          } else {
            // Иначе создаем временные метки на основе текста
            const lines = foundText.split('\n');
            let currentTime = 10;
            
            for (let i = 0; i < lines.length; i++) {
              if (lines[i].trim() !== '') {
                timestamps.push({
                  time: formatTimestamp(currentTime),
                  text: lines[i]
                });
                currentTime += 5 + Math.random() * 10;
              }
            }
          }
        } else {
          // Если ничего не найдено, используем шаблон
          foundText = `Текст песни "${formData.title}".\nАвтор: ${formData.author}.\nГод: ${formData.year}.\n\nЗдесь будет текст песни...`;
          
          // Создаем временные метки на основе шаблона
          const lines = foundText.split('\n');
          let currentTime = 10;
          
          for (let i = 0; i < lines.length; i++) {
            if (lines[i].trim() !== '') {
              timestamps.push({
                time: formatTimestamp(currentTime),
                text: lines[i]
              });
              currentTime += 5 + Math.random() * 10;
            }
          }
        }
        
        setRecognizedText(foundText);
        setGeneratedTimestamps(timestamps);
        resolve();
      }, 2000);
    });
  };
  
  // Форматирование времени для временных меток
  const formatTimestamp = (seconds) => {
    const minutes = Math.floor(seconds / 60);
    const secs = Math.floor(seconds % 60);
    return `${minutes}:${secs < 10 ? '0' : ''}${secs}`;
  };

  // Обработка первичной отправки формы загрузки
  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!audioFile) {
      setUploadStatus({ status: 'error', message: 'Пожалуйста, выберите аудиофайл' });
      return;
    }

    try {
      // Имитация начала загрузки
      setUploadStatus({ 
        status: 'uploading', 
        message: 'Загрузка файла на сервер...' 
      });
      
      // Имитация загрузки файла
      await simulateUpload();
      
      // Если выбрано улучшение записи
      if (enhanceAudio) {
        setUploadStatus({ 
          status: 'processing', 
          message: 'Восстановление звука с помощью ИИ...' 
        });
        await simulateRestoration();
      }
      
      // Если выбрано распознавание текста
      if (transcribeAudio) {
        setUploadStatus({ 
          status: 'processing', 
          message: 'Распознавание текста песни...' 
        });
        await simulateTextRecognition();
      }
      
      // Имитация анализа
      setUploadStatus({ 
        status: 'processing', 
        message: 'Анализ и классификация записи...' 
      });
      await new Promise(resolve => setTimeout(resolve, 1500));
      
      // Переходим к этапу модерации
      setUploadStatus({ 
        status: 'moderation', 
        message: 'Запись обработана. Проверьте данные перед отправкой в архив.' 
      });
      setIsInModeration(true);
      
    } catch (error) {
      setUploadStatus({ 
        status: 'error', 
        message: `Ошибка при загрузке: ${error.message || 'Неизвестная ошибка'}` 
      });
    }
  };
  
  // Обработка подтверждения модерации и отправки в архив
  const handleApprove = async () => {
    try {
      setUploadStatus({ 
        status: 'processing', 
        message: 'Сохранение записи в архив...' 
      });
      
      // Имитация финальной загрузки
      await new Promise(resolve => setTimeout(resolve, 1500));
      
      // Успешное завершение
      setUploadStatus({ 
        status: 'success', 
        message: 'Запись успешно добавлена в архив!' 
      });
      
      // Сброс формы
      setFormData({
        title: '',
        author: '',
        year: '',
        description: '',
        tags: [],
      });
      setAudioFile(null);
      setUploadProgress(0);
      setIsInModeration(false);
      setRecognizedText('');
      setGeneratedTimestamps([]);
      document.getElementById('audioFile').value = '';
      
    } catch (error) {
      setUploadStatus({ 
        status: 'error', 
        message: `Ошибка при сохранении: ${error.message || 'Неизвестная ошибка'}` 
      });
    }
  };
  
  // Отмена загрузки и возврат к форме
  const handleCancel = () => {
    setIsInModeration(false);
    setUploadStatus({ status: '', message: '' });
  };

  if (isTagsFetching) return <Loader title="Загрузка..." />;
  if (tagsError) return <Error />;

  return (
    <div className="flex flex-col bg-black/20 backdrop-blur-lg rounded-lg p-6 max-w-3xl mx-auto">
      <h2 className="text-white text-2xl font-bold mb-6">Загрузка военной аудиозаписи</h2>
      
      {uploadStatus.message && (
        <div className={`mb-4 p-4 rounded ${
          uploadStatus.status === 'success' ? 'bg-green-500/20' : 
          uploadStatus.status === 'error' ? 'bg-red-500/20' : 
          uploadStatus.status === 'moderation' ? 'bg-yellow-500/20' :
          'bg-blue-500/20'
        }`}>
          <p className="text-white">{uploadStatus.message}</p>
          
          {uploadStatus.status === 'uploading' && (
            <div className="mt-2">
              <div className="w-full h-2 bg-gray-700 rounded-full mt-1">
                <div 
                  className="h-full bg-blue-500 rounded-full" 
                  style={{ width: `${uploadProgress}%` }} 
                ></div>
              </div>
              <p className="text-white text-sm mt-1">{uploadProgress}%</p>
            </div>
          )}
          
          {uploadStatus.status === 'processing' && isRestoring && (
            <div className="flex items-center mt-2">
              <div className="animate-spin rounded-full h-4 w-4 border-b-2 border-white mr-2"></div>
              <p className="text-white text-sm">Обработка может занять некоторое время...</p>
            </div>
          )}
        </div>
      )}
      
      {!isInModeration ? (
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
              name="author"
              value={formData.author}
              onChange={handleInputChange}
              required
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
              required
              className="bg-black/30 text-white rounded p-2"
            />
          </div>
          
          <div className="flex flex-col">
            <label className="text-gray-300 mb-1">Описание</label>
            <textarea
              name="description"
              value={formData.description}
              onChange={handleInputChange}
              rows="3"
              className="bg-black/30 text-white rounded p-2"
            />
          </div>
          
          <div className="flex flex-col">
            <label className="text-gray-300 mb-1">Категории/Теги</label>
            <select
              name="tags"
              multiple
              value={formData.tags}
              onChange={handleTagChange}
              className="bg-black/30 text-white rounded p-2 h-32"
            >
              {tags?.map((tag) => (
                <option key={tag.id} value={tag.id}>{tag.name}</option>
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
          
          <div className="mt-4 p-4 bg-black/30 rounded-lg">
            <h3 className="text-white font-medium mb-3">Параметры обработки записи</h3>
            
            <div className="space-y-2">
              <div className="flex items-center">
                <input
                  type="checkbox"
                  id="enhanceAudio"
                  checked={enhanceAudio}
                  onChange={handleEnhanceChange}
                  className="w-4 h-4 mr-2"
                />
                <label htmlFor="enhanceAudio" className="text-gray-300">
                  Улучшить качество записи (шумоподавление, восстановление частот)
                </label>
              </div>
              
              <div className="flex items-center">
                <input
                  type="checkbox"
                  id="transcribeAudio"
                  checked={transcribeAudio}
                  onChange={handleTranscribeChange}
                  className="w-4 h-4 mr-2"
                />
                <label htmlFor="transcribeAudio" className="text-gray-300">
                  Распознать текст песни
                </label>
              </div>
            </div>
          </div>
          
          <button
            type="submit"
            disabled={isUploading || uploadStatus.status === 'uploading' || uploadStatus.status === 'processing'}
            className="bg-blue-600 hover:bg-blue-700 text-white font-bold py-2 px-4 rounded mt-4 disabled:opacity-50"
          >
            {isUploading || uploadStatus.status === 'uploading' || uploadStatus.status === 'processing' ? 'Обработка...' : 'Загрузить и обработать'}
          </button>
        </form>
      ) : (
        // Экран модерации
        <div className="flex flex-col space-y-6">
          <div className="bg-black/30 p-4 rounded-lg">
            <h3 className="text-white font-bold mb-2">Информация о записи:</h3>
            <p className="text-gray-300"><span className="text-gray-500">Название:</span> {formData.title}</p>
            <p className="text-gray-300"><span className="text-gray-500">Автор:</span> {formData.author}</p>
            <p className="text-gray-300"><span className="text-gray-500">Год:</span> {formData.year}</p>
            {formData.description && (
              <p className="text-gray-300"><span className="text-gray-500">Описание:</span> {formData.description}</p>
            )}
            <div className="mt-2">
              <span className="text-gray-500">Теги:</span>
              <div className="flex flex-wrap mt-1">
                {formData.tags.map((tagId) => {
                  const tag = tags?.find(t => t.id === tagId);
                  return tag ? (
                    <span 
                      key={tagId} 
                      className="text-sm mr-2 mb-1 py-1 px-2 bg-blue-800/30 text-blue-200 rounded-full"
                    >
                      {tag.name}
                    </span>
                  ) : null;
                })}
              </div>
            </div>
          </div>
          
          {/* Редактирование распознанного текста */}
          {transcribeAudio && (
            <div className="bg-black/30 p-4 rounded-lg">
              <h3 className="text-white font-bold mb-3">Распознанный текст:</h3>
              <p className="text-gray-400 text-sm mb-2">Проверьте и отредактируйте текст перед сохранением:</p>
              <textarea
                value={recognizedText}
                onChange={handleTextChange}
                rows="10"
                className="w-full bg-black/20 text-white rounded p-3 mb-3"
              />
              
              {generatedTimestamps.length > 0 && (
                <div>
                  <h4 className="text-white font-medium mb-2">Временные метки:</h4>
                  <div className="bg-black/20 p-3 rounded max-h-48 overflow-y-auto">
                    {generatedTimestamps.map((timestamp, index) => (
                      <div key={index} className="flex mb-1 text-sm">
                        <span className="text-gray-500 w-16">[{timestamp.time}]</span>
                        <span className="text-gray-300">{timestamp.text}</span>
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </div>
          )}
          
          <div className="flex space-x-4 justify-end">
            <button
              onClick={handleCancel}
              className="bg-gray-600 hover:bg-gray-700 text-white font-bold py-2 px-4 rounded"
            >
              Отмена
            </button>
            <button
              onClick={handleApprove}
              className="bg-green-600 hover:bg-green-700 text-white font-bold py-2 px-4 rounded"
            >
              Подтвердить и добавить в архив
            </button>
          </div>
        </div>
      )}
    </div>
  );
};

export default UploadForm; 