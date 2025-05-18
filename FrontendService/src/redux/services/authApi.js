import { createApi, fetchBaseQuery } from '@reduxjs/toolkit/query/react';

export const authApi = createApi({
  reducerPath: 'authApi',
  baseQuery: fetchBaseQuery({
    baseUrl: 'http://localhost:8000/api/',
    credentials: 'include', // Обеспечивает отправку и получение cookies
  }),
  endpoints: (builder) => ({
    // Логин пользователя по имени
    login: builder.mutation({
      query: (credentials) => ({
        url: 'auth/loginByName',
        method: 'POST',
        body: {
          name: credentials.username,
          password: credentials.password
        },
      }),
      // При успешном входе сохраним информацию о пользователе
      async onQueryStarted(_, { queryFulfilled }) {
        try {
          const result = await queryFulfilled;
          console.log('Успешный вход в систему:', result);
          // Сохраняем статус авторизации в localStorage
          localStorage.setItem('isAuthenticated', 'true');
          localStorage.setItem('username', _.username);
        } catch (error) {
          console.error('Ошибка авторизации:', error);
        }
      }
    }),
    
    // Вход по email
    loginByEmail: builder.mutation({
      query: (credentials) => ({
        url: 'auth/loginByEmail',
        method: 'POST',
        body: {
          email: credentials.email,
          password: credentials.password
        },
      }),
      async onQueryStarted(_, { queryFulfilled }) {
        try {
          const result = await queryFulfilled;
          console.log('Успешный вход в систему:', result);
          localStorage.setItem('isAuthenticated', 'true');
          localStorage.setItem('email', _.email);
        } catch (error) {
          console.error('Ошибка авторизации:', error);
        }
      }
    }),
    
    // Регистрация пользователя
    register: builder.mutation({
      query: (userData) => ({
        url: 'auth/register',
        method: 'POST',
        body: {
          name: userData.username,
          email: userData.email,
          password: userData.password
        },
      }),
    }),
    
    // Выход пользователя
    logout: builder.mutation({
      query: () => ({
        url: 'auth/logout',
        method: 'POST',
      }),
      // Обработчик успешного выхода
      async onQueryStarted(_, { queryFulfilled }) {
        try {
          await queryFulfilled;
          // Удаляем данные авторизации
          localStorage.removeItem('isAuthenticated');
          localStorage.removeItem('username');
          localStorage.removeItem('email');
        } catch (error) {
          console.error('Ошибка при выходе:', error);
        }
      }
    }),
  }),
});

export const {
  useLoginMutation,
  useLoginByEmailMutation,
  useRegisterMutation,
  useLogoutMutation,
} = authApi; 