import React from 'react';

const Error = ({ message }) => (
  <div className="w-full flex flex-col justify-center items-center p-6 mt-8">
    <h1 className="font-bold text-2xl text-white mb-4">
      {message || 'Произошла ошибка. Пожалуйста, попробуйте снова.'}
    </h1>
    <p className="text-gray-300">
      Если проблема повторяется, обратитесь к администратору системы.
    </p>
  </div>
);

export default Error; 