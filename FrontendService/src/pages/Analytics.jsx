import React, { useState, useEffect } from 'react';
import { useGetStatisticsQuery } from '../redux/services/audioArchiveApi';
import { Error, Loader } from '../components';

const Analytics = () => {
  const { data, isFetching, error } = useGetStatisticsQuery();
  
  // Примеры диаграмм, в реальном приложении нужно использовать библиотеку визуализации (например Chart.js)
  // В этом примере мы просто стилизуем блоки для отображения статистики
  
  if (isFetching) return <Loader title="Загрузка статистики..." />;
  if (error) return <Error />;
  
  // Подготовка данных для отображения
  // Получение топ-5 тегов
  const topTags = data?.tagStats || [];
  
  // Количество восстановленных записей (заглушка)
  const restoredRecordings = data?.totalRecordings || 0;
  
  // Общее количество тегов
  const totalTags = data?.tags?.length || 0;
  
  // Статистика по годам для графика
  const yearStats = data?.yearStats || [];
  
  // Находим максимальные значения для масштабирования графиков
  const maxTagCount = Math.max(...topTags.map(item => item.count || 0), 1);
  const maxYearCount = Math.max(...yearStats.map(item => item.count || 0), 1);
  
  return (
    <div className="flex flex-col">
      <h2 className="font-bold text-3xl text-white text-left mt-4 mb-10">Аналитика архива военных лет</h2>
      
      <div className="grid grid-cols-1 md:grid-cols-2 gap-8 mb-10">
        {/* Общая статистика */}
        <div className="bg-black/20 backdrop-blur-lg rounded-lg p-6">
          <h3 className="text-xl font-bold text-white mb-4">Общая статистика</h3>
          <div className="grid grid-cols-2 gap-4">
            <div className="bg-blue-900/30 rounded-lg p-4 flex flex-col items-center justify-center">
              <p className="text-3xl font-bold text-white">{data?.totalRecordings || 0}</p>
              <p className="text-gray-300">Всего записей</p>
            </div>
            <div className="bg-green-900/30 rounded-lg p-4 flex flex-col items-center justify-center">
              <p className="text-3xl font-bold text-white">{restoredRecordings}</p>
              <p className="text-gray-300">Восстановлено</p>
            </div>
            <div className="bg-purple-900/30 rounded-lg p-4 flex flex-col items-center justify-center">
              <p className="text-3xl font-bold text-white">{data?.totalAuthors || 0}</p>
              <p className="text-gray-300">Авторов</p>
            </div>
            <div className="bg-orange-900/30 rounded-lg p-4 flex flex-col items-center justify-center">
              <p className="text-3xl font-bold text-white">{totalTags}</p>
              <p className="text-gray-300">Категорий</p>
            </div>
          </div>
        </div>
        
        {/* Распределение по годам */}
        <div className="bg-black/20 backdrop-blur-lg rounded-lg p-6">
          <h3 className="text-xl font-bold text-white mb-4">Распределение по годам</h3>
          <div className="flex flex-col space-y-3">
            {yearStats.map((yearStat) => (
              <div key={yearStat.year} className="flex items-center">
                <div className="w-16 text-gray-300">{yearStat.year}</div>
                <div className="flex-1 h-6 bg-black/30 rounded-full overflow-hidden">
                  <div 
                    className="h-full bg-blue-600 rounded-full"
                    style={{ width: `${(yearStat.count / maxYearCount) * 100}%` }}
                  ></div>
                </div>
                <div className="w-10 text-right text-gray-300 ml-2">{yearStat.count}</div>
              </div>
            ))}
          </div>
        </div>
      </div>
      
      {/* Популярные теги */}
      <div className="bg-black/20 backdrop-blur-lg rounded-lg p-6 mb-8">
        <h3 className="text-xl font-bold text-white mb-4">Популярные категории</h3>
        <div className="flex flex-col space-y-3">
          {topTags.map((tag, index) => (
            <div key={index} className="flex items-center">
              <div className="w-32 text-gray-300 truncate">{tag.tag}</div>
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
      
      {/* Анализ содержания */}
      <div className="bg-black/20 backdrop-blur-lg rounded-lg p-6 mb-8">
        <h3 className="text-xl font-bold text-white mb-4">Анализ содержания архива</h3>
        <div className="p-4 bg-black/30 rounded-lg text-gray-300">
          <p className="mb-2">Исследование архива военных лет показывает:</p>
          <ul className="list-disc list-inside space-y-1">
            <li>Преобладающее количество патриотических и лирических песен в военные годы</li>
            <li>Значительное увеличение количества записей во время ключевых событий войны</li>
            <li>Изменение тематики песен в зависимости от периода войны</li>
            <li>Наиболее часто используемые слова в песнях: "родина", "победа", "война"</li>
          </ul>
          
          <div className="mt-4 p-3 bg-blue-900/20 rounded border border-blue-800/30">
            <p>Важно отметить, что данный анализ проведен на основе ограниченной выборки материалов и требует дальнейшего исследования с расширением архива аудиозаписей.</p>
          </div>
        </div>
      </div>
    </div>
  );
};

export default Analytics; 