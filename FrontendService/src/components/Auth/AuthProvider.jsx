// file: src/components/Auth/AuthProvider.jsx
import React, { createContext, useContext, useState, useEffect, useCallback } from 'react';
import api from '../api/axios'; // Мы будем использовать наш централизованный Axios

export const AuthContext = createContext(null);
export const useAuth = () => useContext(AuthContext);

export const AuthProvider = ({ children }) => {
  const [user, setUser] = useState(null);
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  // loading теперь очень важен! Он показывает, что идет проверка сессии.
  const [loading, setLoading] = useState(true);

  // Функция проверки сессии. useCallback для мемоизации.
  const checkAuthStatus = useCallback(async () => {
    try {
      // Отправляем запрос на эндпоинт, который вернет данные пользователя, если сессия валидна.
      // Axios (настроенный ниже) автоматически отправит HttpOnly cookie.
      const response = await api.get('/profile'); // <-- Убедитесь, что такой эндпоинт есть на бэкенде!
      
      // Если запрос успешен (код 200), значит, сессия есть.
      setUser(response.data);
      setIsAuthenticated(true);
    } catch (error) {
      // Если бэкенд вернул 401, значит, сессии нет.
      console.log('Сессия не найдена или истекла.');
      setUser(null);
      setIsAuthenticated(false);
    } finally {
      // В любом случае, проверка завершена.
      setLoading(false);
    }
  }, []);

  // Запускаем проверку один раз при монтировании компонента
  useEffect(() => {
    checkAuthStatus();
  }, [checkAuthStatus]);

  const logout = async () => {
    try {
      // Отправляем запрос на выход, чтобы бэкенд очистил cookie
      await api.post('/logout');
    } catch (error) {
      console.error('Ошибка при выходе на сервере:', error);
    } finally {
      // Сбрасываем состояние на клиенте
      setUser(null);
      setIsAuthenticated(false);
      // Перенаправляем на страницу входа
      window.location.href = 'http://localhost:3010/login.html'; // Укажите правильный URL
    }
  };

  const value = { user, isAuthenticated, loading, logout };

  return (
    <AuthContext.Provider value={value}>
      {/* Не рендерим дочерние компоненты, пока идет проверка */}
      {!loading && children}
    </AuthContext.Provider>
  );
};