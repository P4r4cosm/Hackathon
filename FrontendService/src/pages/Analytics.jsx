import React from 'react';
import { Loader } from '../components';

const Analytics = () => {
  // Используем заглушки для данных вместо API запроса
  const isLoading = false;
  
  // Заглушки данных статистики
  const mockData = {
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
  };
  
  const data = mockData;
  
  if (isLoading) return <Loader title="Загрузка статистики..." />;
  
  // Статистика по годам
  const yearStats = data.yearStats;
  
  // Находим максимальные значения для масштабирования графиков
  const maxYearCount = Math.max(...Object.values(yearStats));
  
  return (
    <div className="flex flex-col">
      <h2 className="font-bold text-3xl text-white text-left mt-4 mb-10">Аналитика архива военных лет</h2>
      
      <div className="grid grid-cols-1 md:grid-cols-2 gap-8 mb-10">
        {/* Общая статистика */}
        <div className="bg-black/20 backdrop-blur-lg rounded-lg p-6">
          <h3 className="text-xl font-bold text-white mb-4">Общая статистика</h3>
          <div className="grid grid-cols-2 gap-4">
            <div className="bg-blue-900/30 rounded-lg p-4 flex flex-col items-center justify-center">
              <p className="text-3xl font-bold text-white">{data.totalRecordings}</p>
              <p className="text-gray-300">Всего записей</p>
            </div>
            <div className="bg-green-900/30 rounded-lg p-4 flex flex-col items-center justify-center">
              <p className="text-3xl font-bold text-white">{data.restoredRecordings}</p>
              <p className="text-gray-300">Восстановлено</p>
            </div>
            <div className="bg-purple-900/30 rounded-lg p-4 flex flex-col items-center justify-center">
              <p className="text-3xl font-bold text-white">{data.totalAuthors}</p>
              <p className="text-gray-300">Авторов</p>
            </div>
            <div className="bg-orange-900/30 rounded-lg p-4 flex flex-col items-center justify-center">
              <p className="text-3xl font-bold text-white">{data.totalTags}</p>
              <p className="text-gray-300">Категорий</p>
            </div>
          </div>
        </div>
        
        {/* Распределение по годам */}
        <div className="bg-black/20 backdrop-blur-lg rounded-lg p-6">
          <h3 className="text-xl font-bold text-white mb-4">Распределение по годам</h3>
          <div className="flex flex-col space-y-3">
            {Object.entries(yearStats).map(([year, count]) => (
              <div key={year} className="flex items-center">
                <div className="w-16 text-gray-300">{year}</div>
                <div className="flex-1 h-6 bg-black/30 rounded-full overflow-hidden">
                  <div 
                    className="h-full bg-blue-600 rounded-full"
                    style={{ width: `${(count / maxYearCount) * 100}%` }}
                  ></div>
                </div>
                <div className="w-10 text-right text-gray-300 ml-2">{count}</div>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
};

export default Analytics; 