import React, { useState } from 'react';

const AuthPage = () => {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const handleLogin = async (e) => {
    e.preventDefault();
    setLoading(true);
    setError('');

    try {
      const response = await fetch('http://localhost:8000/loginByEmail', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ email, password }),
        credentials: 'include',
      });

      if (response.ok) {
        const data = await response.json();
        
        // Отправляем токен обратно в основное окно
        if (window.opener) {
          window.opener.postMessage({
            type: 'AUTH_TOKEN',
            token: data.token,
            user: data.user
          }, 'http://localhost:3000');
          
          // Закрываем окно авторизации
          setTimeout(() => {
            window.close();
          }, 1000);
        }
      } else {
        const errorData = await response.json();
        setError(errorData.message || 'Ошибка авторизации');
      }
    } catch (err) {
      setError('Произошла ошибка при подключении к серверу');
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  const handleGoogleLogin = () => {
    window.location.href = 'http://localhost:8000/google-login';
  };

  return (
    <div className="min-h-screen flex flex-col justify-center items-center bg-gradient-to-br from-black to-[#121286] p-6">
      <div className="w-full max-w-md bg-black/30 backdrop-blur-lg rounded-lg shadow-lg p-8">
        <h2 className="text-2xl font-bold text-white text-center mb-6">Вход в архив</h2>
        
        {error && (
          <div className="mb-4 p-3 bg-red-500/20 text-red-200 rounded">
            {error}
          </div>
        )}
        
        <form onSubmit={handleLogin} className="space-y-4">
          <div>
            <label htmlFor="email" className="block text-gray-300 mb-1">
              Email
            </label>
            <input
              id="email"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
              className="w-full p-2 bg-black/30 text-white rounded focus:outline-none focus:ring-2 focus:ring-blue-600"
            />
          </div>
          
          <div>
            <label htmlFor="password" className="block text-gray-300 mb-1">
              Пароль
            </label>
            <input
              id="password"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
              className="w-full p-2 bg-black/30 text-white rounded focus:outline-none focus:ring-2 focus:ring-blue-600"
            />
          </div>
          
          <button
            type="submit"
            disabled={loading}
            className="w-full py-2 px-4 bg-blue-600 hover:bg-blue-700 text-white font-bold rounded disabled:opacity-50"
          >
            {loading ? 'Вход...' : 'Войти'}
          </button>
        </form>
        
        <div className="mt-6">
          <div className="relative">
            <div className="absolute inset-0 flex items-center">
              <div className="w-full border-t border-gray-600"></div>
            </div>
            <div className="relative flex justify-center text-sm">
              <span className="px-2 bg-black/30 text-gray-400">Или войти с помощью</span>
            </div>
          </div>
          
          <div className="mt-4">
            <button
              onClick={handleGoogleLogin}
              className="w-full flex justify-center items-center py-2 px-4 border border-gray-600 rounded text-gray-200 hover:bg-gray-800"
            >
              <svg className="w-5 h-5 mr-2" viewBox="0 0 24 24">
                <path
                  fill="currentColor"
                  d="M21.35,11.1H12.18V13.83H18.69C18.36,15.64 16.96,17.45 14.37,17.45C11.24,17.45 8.71,14.92 8.71,11.82C8.71,8.72 11.24,6.19 14.37,6.19C16.03,6.19 17.14,6.92 17.82,7.6L19.85,5.62C18.44,4.31 16.64,3.5 14.37,3.5C9.69,3.5 6,7.19 6,11.82C6,16.45 9.69,20.14 14.37,20.14C19.05,20.14 22,17.14 22,12.14C22,11.79 21.96,11.45 21.9,11.1H21.35Z"
                />
              </svg>
              Google
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};

export default AuthPage; 