document.addEventListener('DOMContentLoaded', () => {
    const themeToggleButton = document.getElementById('theme-toggle');
    const currentTheme = localStorage.getItem('theme') || 'light';
    document.body.classList.toggle('dark-theme', currentTheme === 'dark');
    if (themeToggleButton) {
        themeToggleButton.textContent = currentTheme === 'dark' ? 'Светлая тема' : 'Темная тема';
    }

    if (themeToggleButton) {
        themeToggleButton.addEventListener('click', () => {
            document.body.classList.toggle('dark-theme');
            let theme = 'light';
            if (document.body.classList.contains('dark-theme')) {
                theme = 'dark';
            }
            localStorage.setItem('theme', theme);
            themeToggleButton.textContent = theme === 'dark' ? 'Светлая тема' : 'Темная тема';
            console.log(`Theme changed to ${theme}`);
        });
    }

    // Активная навигация
    const navLinks = document.querySelectorAll('header nav ul li a');
    const currentPage = window.location.pathname.split('/').pop() || 'index.html';

    navLinks.forEach(link => {
        if (link.getAttribute('href') === currentPage) {
            link.classList.add('active');
        } else {
            link.classList.remove('active');
        }
    });

    console.log('Основной скрипт main.js загружен и выполнен.');

    // Глобальная функция для отображения уведомлений (заглушка)
    window.showNotification = (message, type = 'info') => {
        // type может быть 'info', 'success', 'error'
        // TODO: Реализовать более красивое отображение уведомлений (например, toast)
        console.log(`NOTIFICATION (${type}): ${message}`);
        alert(`[${type.toUpperCase()}] ${message}`); // Временное решение
    };

    // Утилита для получения параметра из URL
    window.getUrlParameter = (name) => {
        name = name.replace(/[\[]/, '\\[').replace(/[\]]/, '\\]');
        const regex = new RegExp('[\\?&]' + name + '=([^&#]*)');
        const results = regex.exec(location.search);
        return results === null ? '' : decodeURIComponent(results[1].replace(/\+/g, ' '));
    };
}); 