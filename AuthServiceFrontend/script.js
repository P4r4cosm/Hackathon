const container = document.getElementById('container');
const registerBtn = document.getElementById('register');
const loginBtn = document.getElementById('login');
const registerForm = document.getElementById('registerForm');
const loginForm = document.getElementById('loginForm');
const registerError = document.getElementById('registerError');
const loginError = document.getElementById('loginError');

// URL к сервису аутентификации (через API Gateway)
const AUTH_SERVICE_URL = window.location.hostname === 'localhost' ? 'http://localhost:8000' : 'https://localhost:8001';

// Обработка переключения между формами регистрации и входа
registerBtn.addEventListener('click', () => {
    container.classList.add("active");
});

loginBtn.addEventListener('click', () => {
    container.classList.remove("active");
});

// Функция для валидации пароля
function isPasswordValid(password) {
    // Минимум 6 символов
    if (password.length < 6) return false;
    
    // Должен содержать хотя бы одну цифру
    if (!/\d/.test(password)) return false;
    
    // Должен содержать хотя бы одну заглавную букву
    if (!/[A-Z]/.test(password)) return false;
    
    return true;
}

// Обработка формы регистрации
registerForm.addEventListener('submit', async (e) => {
    e.preventDefault();
    
    const name = document.getElementById('registerName').value;
    const email = document.getElementById('registerEmail').value;
    const password = document.getElementById('registerPassword').value;
    
    // Проверка пароля перед отправкой
    if (!isPasswordValid(password)) {
        registerError.textContent = 'Пароль должен содержать минимум 6 символов, включая цифру и заглавную букву';
        return;
    }
    
    // Отладочное сообщение
    console.log('Отправляемые данные регистрации:', JSON.stringify({
        name,
        email,
        password
    }));
    
    try {
        const response = await fetch(`${AUTH_SERVICE_URL}/register`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                Name: name,
                Email: email,
                Password: password
            })
        });
        
        // Отладочное сообщение о статусе
        console.log('Статус ответа:', response.status);
        
        const data = await response.json();
        console.log('Ответ сервера:', data);
        
        if (response.ok) {
            registerError.textContent = '';
            // Автоматический вход после успешной регистрации
            try {
                const loginResponse = await fetch(`${AUTH_SERVICE_URL}/loginByEmail`, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify({
                        Email: email,
                        Password: password
                    }),
                    credentials: 'include' // Включаем передачу куки
                });
                
                if (loginResponse.ok) {
                    // Перенаправление на главную страницу приложения
                    window.location.href = 'http://localhost:3000';
                } else {
                    // Если автоматический вход не удался, просто показываем форму входа
                    alert('Регистрация успешна! Войдите в систему.');
                    container.classList.remove("active");
                }
            } catch (loginError) {
                console.error('Ошибка автоматического входа:', loginError);
                alert('Регистрация успешна! Войдите в систему.');
                container.classList.remove("active");
            }
        } else {
            registerError.textContent = 'Ошибка регистрации: ' + (data.message || JSON.stringify(data) || 'Попробуйте снова');
        }
    } catch (error) {
        registerError.textContent = 'Ошибка соединения с сервером';
        console.error('Ошибка:', error);
    }
});

// Обработка формы входа
loginForm.addEventListener('submit', async (e) => {
    e.preventDefault();
    
    const email = document.getElementById('loginEmail').value;
    const password = document.getElementById('loginPassword').value;
    
    console.log('Попытка входа с email:', email);
    
    try {
        const response = await fetch(`${AUTH_SERVICE_URL}/loginByEmail`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                Email: email,
                Password: password
            }),
            credentials: 'include' // Включаем передачу куки
        });
        
        console.log('Статус ответа входа:', response.status);
        const data = await response.json();
        console.log('Ответ сервера при входе:', data);
        
        if (response.ok) {
            loginError.textContent = '';
            console.log('Вход успешен, перенаправление на:', 'http://localhost:3000');
            // Перенаправление на главную страницу приложения после успешного входа
            window.location.href = 'http://localhost:3000'; // URL основного фронтенд-интерфейса
        } else {
            loginError.textContent = 'Ошибка входа: ' + (data.message || 'Неверные учетные данные');
        }
    } catch (error) {
        loginError.textContent = 'Ошибка соединения с сервером';
        console.error('Ошибка:', error);
    }
}); 