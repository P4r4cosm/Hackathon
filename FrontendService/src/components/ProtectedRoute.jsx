// file: src/components/ProtectedRoute.jsx
import React from 'react';
import { useAuth } from './Auth/AuthProvider';
import { Outlet } from 'react-router-dom';

const ProtectedRoute = () => {
  const { isAuthenticated, loading } = useAuth();

  // Пока идет проверка сессии, показываем заглушку
  if (loading) {
    return <div className="flex items-center justify-center h-screen bg-black text-white">Проверка сессии...</div>;
  }

  // Если проверка завершена и пользователь не авторизован
  if (!isAuthenticated) {
    // Выполняем полный редирект на сервис авторизации.
    // Мы не используем <Navigate> из роутера, так как это другой сервис.
    window.location.href = 'http://localhost:3010/login.html'; // <-- Укажите точный URL вашей страницы входа
    
    // Пока браузер делает редирект, ничего не рендерим
    return null;
  }

  // Если все в порядке, рендерим вложенный контент (AppLayout)
  return <Outlet />;
};

export default ProtectedRoute;