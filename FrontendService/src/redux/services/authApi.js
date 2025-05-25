import { createApi, fetchBaseQuery } from '@reduxjs/toolkit/query/react';

export const authApi = createApi({
  reducerPath: 'authApi',
  baseQuery: fetchBaseQuery({
    baseUrl: 'http://localhost:8000/',
    credentials: 'include',
    prepareHeaders: (headers) => {
      headers.set('Content-Type', 'application/json');
      return headers;
    },
  }),
  endpoints: (builder) => ({
    loginByEmail: builder.mutation({
      query: (credentials) => ({
        url: '/loginByEmail',
        method: 'POST',
        body: credentials,
      }),
      transformResponse: (response) => {
        if (response.token) {
          localStorage.setItem('token', response.token);
          localStorage.setItem('isAuthenticated', 'true');
          localStorage.setItem('user', JSON.stringify({
            id: response.id,
            email: response.email,
            name: response.name,
            roles: response.roles
          }));
        }
        return response;
      },
    }),
    
    loginByName: builder.mutation({
      query: (credentials) => ({
        url: '/login_name',
        method: 'POST',
        body: credentials,
      }),
      transformResponse: (response) => {
        if (response.token) {
          localStorage.setItem('token', response.token);
          localStorage.setItem('isAuthenticated', 'true');
          localStorage.setItem('user', JSON.stringify({
            id: response.id,
            email: response.email,
            name: response.name,
            roles: response.roles
          }));
        }
        return response;
      },
    }),
    
    register: builder.mutation({
      query: (userData) => ({
        url: '/register',
        method: 'POST',
        body: userData,
      }),
    }),
    
    refresh: builder.mutation({
      query: () => ({
        url: '/refresh',
        method: 'POST',
      }),
      transformResponse: (response) => {
        if (response.token) {
          localStorage.setItem('token', response.token);
        }
        return response;
      },
    }),
    
    googleLogin: builder.query({
      query: () => ({
        url: '/google-login',
        method: 'GET',
        responseHandler: 'text', // Получаем ответ как текст для перенаправления
      }),
    }),

    logout: builder.mutation({
      query: () => ({
        url: '/logout',
        method: 'POST',
      }),
      onQueryStarted: async (_, { queryFulfilled }) => {
        try {
          await queryFulfilled;
          // Очищаем данные аутентификации при выходе
          localStorage.removeItem('token');
          localStorage.removeItem('isAuthenticated');
          localStorage.removeItem('user');
        } catch (err) {
          console.error('Ошибка при выходе:', err);
        }
      },
    }),
  }),
});

export const {
  useLoginByEmailMutation,
  useLoginByNameMutation,
  useRegisterMutation,
  useRefreshMutation,
  useGoogleLoginQuery,
  useLogoutMutation,
} = authApi; 