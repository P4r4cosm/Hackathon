import React, { useState, useEffect } from 'react';
import { useUpdateTranscriptionMutation } from '../../redux/services/audioArchiveApi';

const TranscriptionEditor = ({ recordingId, initialText, timestamps }) => {
  const [transcriptionText, setTranscriptionText] = useState('');
  const [editMode, setEditMode] = useState(false);
  const [editStatus, setEditStatus] = useState({ status: '', message: '' });
  
  const [updateTranscription, { isLoading }] = useUpdateTranscriptionMutation();
  
  useEffect(() => {
    if (initialText) {
      setTranscriptionText(initialText);
    }
  }, [initialText]);
  
  const handleTextChange = (e) => {
    setTranscriptionText(e.target.value);
  };
  
  const handleEditClick = () => {
    setEditMode(true);
  };
  
  const handleCancelClick = () => {
    setTranscriptionText(initialText);
    setEditMode(false);
    setEditStatus({ status: '', message: '' });
  };
  
  const handleSaveClick = async () => {
    try {
      await updateTranscription({ 
        id: recordingId, 
        text: transcriptionText 
      }).unwrap();
      
      setEditMode(false);
      setEditStatus({ 
        status: 'success', 
        message: 'Транскрипция успешно обновлена.' 
      });
      
      // Скрываем сообщение через 3 секунды
      setTimeout(() => {
        setEditStatus({ status: '', message: '' });
      }, 3000);
      
    } catch (error) {
      setEditStatus({ 
        status: 'error', 
        message: `Ошибка при сохранении: ${error.message || 'Неизвестная ошибка'}` 
      });
    }
  };
  
  return (
    <div className="mt-6 bg-black/20 backdrop-blur-lg rounded-lg p-4">
      <div className="flex justify-between items-center mb-4">
        <h3 className="text-white text-xl font-bold">Текст записи</h3>
        
        {!editMode ? (
          <button 
            onClick={handleEditClick}
            className="bg-blue-600 hover:bg-blue-700 text-white px-3 py-1 rounded"
          >
            Редактировать
          </button>
        ) : (
          <div className="flex space-x-2">
            <button 
              onClick={handleCancelClick}
              disabled={isLoading}
              className="bg-gray-600 hover:bg-gray-700 text-white px-3 py-1 rounded disabled:opacity-50"
            >
              Отмена
            </button>
            <button 
              onClick={handleSaveClick}
              disabled={isLoading}
              className="bg-green-600 hover:bg-green-700 text-white px-3 py-1 rounded disabled:opacity-50"
            >
              {isLoading ? 'Сохранение...' : 'Сохранить'}
            </button>
          </div>
        )}
      </div>
      
      {editStatus.message && (
        <div className={`mb-4 p-3 rounded ${editStatus.status === 'success' ? 'bg-green-500/20' : 'bg-red-500/20'}`}>
          <p className="text-white">{editStatus.message}</p>
        </div>
      )}
      
      {editMode ? (
        <textarea
          value={transcriptionText}
          onChange={handleTextChange}
          rows="10"
          className="w-full bg-black/30 text-white rounded p-2"
        />
      ) : (
        <div className="text-gray-200 whitespace-pre-line">
          {timestamps ? (
            timestamps.map((item, index) => (
              <div key={`timestamp-${index}`} className="mb-2">
                <span className="text-gray-400">[{item.time}] </span>
                <span>{item.text}</span>
              </div>
            ))
          ) : (
            transcriptionText
          )}
        </div>
      )}
    </div>
  );
};

export default TranscriptionEditor; 