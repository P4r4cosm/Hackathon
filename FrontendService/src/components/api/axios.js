// file: src/api/axios.js (Правильная версия для HttpOnly Cookie)
import axios from 'axios';

// Создаем инстанс axios
const api = axios.create({
  // Указываем базовый URL вашего API Gateway
  baseURL: 'http://localhost:8000',

  // ЭТА СТРОКА - КЛЮЧЕВАЯ.
  // Она говорит браузеру автоматически прикреплять HttpOnly cookie
  // ко всем запросам, отправляемым на этот baseURL.
  withCredentials: true,
});


// Interceptor 1: Перехватчик запросов (request) - УДАЛЕН.
// Нам больше не нужно вручную добавлять заголовок 'Authorization',
// так как браузер сам будет прикреплять cookie.


// Interceptor 2: Перехватчик ответов (response) - ОСТАВЛЕН и АДАПТИРОВАН.
// Он нужен для автоматического обновления сессии ("token refresh").
api.interceptors.response.use(
  // 1. Если ответ успешный (статус 2xx), ничего не делаем, просто возвращаем его.
  (response) => response,

  // 2. Если в ответе ошибка...
  async (error) => {
    const originalRequest = error.config;

    // Проверяем, что ошибка - это "401 Unauthorized" и что мы еще не пытались обновить токен для этого запроса.
    if (error.response?.status === 401 && !originalRequest._isRetry) {
      originalRequest._isRetry = true; // Помечаем, что мы начали попытку обновления.

      try {
        // Отправляем запрос на эндпоинт /refresh.
        // `withCredentials: true` в настройках `api` заставит браузер прикрепить
        // HttpOnly `refresh_token` cookie к этому запросу.
        // Ваш C# эндпоинт `/refresh` должен называться именно так,
        // или нужно указать правильный путь, который слушает YARP.
        await api.post('/refresh');

        // Если запрос на /refresh прошел успешно, ваш бэкенд уже установил
        // в cookie НОВЫЕ `access_token` и `refresh_token`.
        // Теперь мы просто повторяем исходный запрос, который провалился.
        // Браузер автоматически прикрепит к нему уже новый `access_token`.
        return api(originalRequest);

      } catch (refreshError) {
        // Если и запрос на /refresh не удался (например, refresh_token тоже истек),
        // это означает, что сессия окончательно потеряна.
        console.error("Сессия истекла. Не удалось обновить токен.", refreshError);

        // Перенаправляем пользователя на страницу входа.
        // Можно сделать это и через кастомное событие, но прямой редирект надежнее.
        window.location.href = 'http://localhost:3010/login.html'; // Укажите точный URL

        return Promise.reject(refreshError);
      }
    }

    // Если ошибка не 401 или это повторный запрос, просто возвращаем ошибку дальше.
    return Promise.reject(error);
  }
);

export default api;