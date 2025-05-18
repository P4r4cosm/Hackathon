import React, { useState, useEffect } from 'react';
import { useGetStatisticsQuery } from '../redux/services/audioArchiveApi';
import { Error, Loader } from '../components';

const Analytics = () => {
  const { data, isFetching, error } = useGetStatisticsQuery();
  
  // Примеры диаграмм, в реальном приложении нужно использовать библиотеку визуализации (например Chart.js)
  // В этом примере мы просто стилизуем блоки для отображения статистики
  
  if (isFetching) return <Loader title="Загрузка статистики..." />;
  if (error) return <Error />;
  
  // Получение топ-5 тегов
  const topTags = data?.tagStats.slice(0, 5);
  // Получение топ-5 авторов
  const topAuthors = data?.authorStats.slice(0, 5);
  // Статистика по годам
  const yearStats = data?.yearStats;
  
  // Находим максимальные значения для масштабирования графиков
  const maxTagCount = Math.max(...topTags.map(tag => tag.count));
  const maxAuthorCount = Math.max(...topAuthors.map(author => author.count));
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
      
      {/* Популярные теги */}
      <div className="bg-black/20 backdrop-blur-lg rounded-lg p-6 mb-8">
        <h3 className="text-xl font-bold text-white mb-4">Популярные категории</h3>
        <div className="flex flex-col space-y-3">
          {topTags.map(tag => (
            <div key={tag.id} className="flex items-center">
              <div className="w-32 text-gray-300 truncate">{tag.name}</div>
              <div className="flex-1 h-6 bg-black/30 rounded-full overflow-hidden">
                <div 
                  className="h-full bg-green-600 rounded-full"
                  style={{ width: `${(tag.count / maxTagCount) * 100}%` }}
                ></div>
              </div>
              <div className="w-10 text-right text-gray-300 ml-2">{tag.count}</div>
            </div>
          ))}
        </div>
      </div>
      
      {/* Популярные авторы */}
      <div className="bg-black/20 backdrop-blur-lg rounded-lg p-6 mb-8">
        <h3 className="text-xl font-bold text-white mb-4">Популярные авторы</h3>
        <div className="flex flex-col space-y-3">
          {topAuthors.map(author => (
            <div key={author.id} className="flex items-center">
              <div className="w-32 text-gray-300 truncate">{author.name}</div>
              <div className="flex-1 h-6 bg-black/30 rounded-full overflow-hidden">
                <div 
                  className="h-full bg-purple-600 rounded-full"
                  style={{ width: `${(author.count / maxAuthorCount) * 100}%` }}
                ></div>
              </div>
              <div className="w-10 text-right text-gray-300 ml-2">{author.count}</div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
};

export default Analytics; 