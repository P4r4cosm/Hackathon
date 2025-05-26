import React, { createContext, useContext, useState, useEffect } from 'react';

// Создаем контекст авторизации
const AuthContext = createContext();

export const useAuth = () => useContext(AuthContext);

export const AuthProvider = ({ children }) => {
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);

  // Функция проверки авторизации
  const checkAuth = async () => {
    try {
      // Проверяем наличие токена в localStorage
      const token = localStorage.getItem('authToken');
      
      if (!token) {
        // Если токена нет, пользователь не авторизован
        setIsAuthenticated(false);
        setUser(null);
      } else {
        // У нас есть токен, считаем пользователя авторизованным
        setIsAuthenticated(true);
        
        // Если в localStorage есть данные пользователя, используем их
        const savedUserData = localStorage.getItem('userData');
        if (savedUserData) {
          try {
            const userData = JSON.parse(savedUserData);
            setUser(userData);
          } catch (error) {
            console.error('Ошибка при разборе данных пользователя:', error);
          }
        }
      }
    } finally {
      setLoading(false);
    }
  };

  // Функция входа - сохраняет токен, полученный от сервиса авторизации
  const login = (token, userData) => {
    localStorage.setItem('authToken', token);
    if (userData) {
      localStorage.setItem('userData', JSON.stringify(userData));
    }
    setUser(userData);
    setIsAuthenticated(true);
  };

  // Функция выхода - удаляет токен и сбрасывает состояние
  const logout = async () => {
    try {
      // Отправляем запрос на выход к сервису авторизации
      await fetch('http://localhost:8000/logout', {
        method: 'POST',
        credentials: 'include'
      });
    } catch (error) {
      console.error('Ошибка при выходе:', error);
    } finally {
      localStorage.removeItem('authToken');
      localStorage.removeItem('userData');
      setUser(null);
      setIsAuthenticated(false);
    }
  };

  // При монтировании компонента проверяем авторизацию
  useEffect(() => {
    // Слушаем сообщения от окна авторизации
    const handleAuthMessage = (event) => {
      // Проверяем, что сообщение от нашего сервиса авторизации
      if (event.origin === 'http://localhost:3010' && event.data?.type === 'AUTH_TOKEN') {
        login(event.data.token, event.data.user);
      }
    };

    window.addEventListener('message', handleAuthMessage);
    checkAuth();

    return () => {
      window.removeEventListener('message', handleAuthMessage);
    };
  }, []);

  // Предоставляем контекст авторизации всем дочерним компонентам
  return (
    <AuthContext.Provider
      value={{
        isAuthenticated,
        user,
        loading,
        login,
        logout,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}; 