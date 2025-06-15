// file: src/context/AuthContext.jsx
import React, { createContext, useContext, useState, useEffect } from 'react';
import api from '../api/axios'; // Импортируем наш новый клиент

const AuthContext = createContext();

export const useAuth = () => useContext(AuthContext);

// Функция для открытия окна авторизации
const openLoginWindow = () => {
  const width = 600, height = 700;
  const left = (window.innerWidth / 2) - (width / 2);
  const top = (window.innerHeight / 2) - (height / 2);
  window.open(
    'http://localhost:3010', // URL вашего приложения авторизации
    'authWindow',
    `width=${width},height=${height},top=${top},left=${left}`
  );
};

export const AuthProvider = ({ children }) => {
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);

  // Функция выхода
  const logout = async () => {
    // Опционально: сообщить бэкенду об аннулировании refresh-токена
    try {
      await api.post('/logout'); 
    } catch (e) {
      console.error("Ошибка при выходе на сервере", e);
    } finally {
      localStorage.removeItem('authToken');
      localStorage.removeItem('userData');
      setUser(null);
      setIsAuthenticated(false);
    }
  };

  useEffect(() => {
    // Слушаем событие ошибки аутентификации от axios interceptor
    const handleAuthError = () => {
      logout();
    };
    window.addEventListener('auth-error', handleAuthError);

    // Слушаем сообщения от окна авторизации
    const handleAuthMessage = (event) => {
      if (event.origin === 'http://localhost:3010' && event.data?.type === 'AUTH_TOKEN') {
        const { token, user: userData } = event.data;
        localStorage.setItem('authToken', token);
        localStorage.setItem('userData', JSON.stringify(userData));
        setUser(userData);
        setIsAuthenticated(true);
      }
    };
    window.addEventListener('message', handleAuthMessage);

    // Проверка при загрузке
    const token = localStorage.getItem('authToken');
    if (token) {
      setIsAuthenticated(true);
      const savedUser = localStorage.getItem('userData');
      if (savedUser) {
        setUser(JSON.parse(savedUser));
      }
    }
    setLoading(false);

    return () => {
      window.removeEventListener('message', handleAuthMessage);
      window.removeEventListener('auth-error', handleAuthError);
    };
  }, []);

  const value = {
    isAuthenticated,
    user,
    loading,
    logout,
    openLoginWindow // Предоставляем функцию для открытия окна
  };

  return (
    <AuthContext.Provider value={value}>
      {!loading && children}
    </AuthContext.Provider>
  );
};