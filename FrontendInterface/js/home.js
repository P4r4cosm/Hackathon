document.addEventListener('DOMContentLoaded', () => {
    const songListContainer = document.getElementById('song-list-container');
    const authorFilter = document.getElementById('author-filter');
    const yearFilter = document.getElementById('year-filter');
    const themeFilter = document.getElementById('theme-filter');
    const moodGenreFilter = document.getElementById('mood-genre-filter');
    const sortBySelect = document.getElementById('sort-by');
    const applyFiltersButton = document.getElementById('apply-filters');
    const loadingMessage = document.querySelector('#song-list-section .loading-message');

    let allSongs = []; // Для хранения всех загруженных песен

    function displaySongs(songs) {
        songListContainer.innerHTML = ''; // Очищаем предыдущие песни
        if (loadingMessage) loadingMessage.style.display = 'none';

        if (songs.length === 0) {
            songListContainer.innerHTML = '<p class="no-results-message">По вашему запросу ничего не найдено.</p>';
            return;
        }

        songs.forEach(song => {
            const card = document.createElement('article');
            card.classList.add('song-card');
            card.innerHTML = `
                <h3><a href="player.html?songId=${song.id}">${song.title}</a></h3>
                <p><strong>Автор:</strong> ${song.author}</p>
                <p><strong>Год:</strong> ${song.year}</p>
                <p class="tags"><strong>Теги:</strong> ${song.tags.map(tag => `<span>${tag}</span>`).join(' ')}</p>
                <p class="tags"><strong>Настроение/Жанр:</strong> ${song.moodGenre.map(mg => `<span>${mg}</span>`).join(' ')}</p>
                <a href="player.html?songId=${song.id}" class="btn-primary" style="margin-top: 10px;">Слушать</a>
            `;
            // TODO: Добавить обработчик для кнопки "Слушать", если нужно не просто перейти по ссылке
            songListContainer.appendChild(card);
        });
    }

    async function populateFilters() {
        try {
            const options = await API.fetchFilterOptions();

            options.authors.forEach(author => {
                const option = document.createElement('option');
                option.value = author;
                option.textContent = author;
                authorFilter.appendChild(option);
            });

            options.years.forEach(year => {
                const option = document.createElement('option');
                option.value = year;
                option.textContent = year;
                yearFilter.appendChild(option);
            });

            options.themes.forEach(theme => {
                const option = document.createElement('option');
                option.value = theme;
                option.textContent = theme;
                themeFilter.appendChild(option);
            });

             options.moodGenres.forEach(mg => {
                const option = document.createElement('option');
                option.value = mg;
                option.textContent = mg;
                moodGenreFilter.appendChild(option);
            });

        } catch (error) {
            console.error("Ошибка при загрузке опций для фильтров:", error);
            if (loadingMessage) loadingMessage.textContent = 'Ошибка загрузки данных для фильтров.';
        }
    }

    async function loadAndDisplaySongs() {
        if (loadingMessage) loadingMessage.style.display = 'block';
        try {
            const filters = {
                author: authorFilter.value,
                year: yearFilter.value,
                theme: themeFilter.value,
                moodGenre: moodGenreFilter.value
            };
            const sortBy = sortBySelect.value;
            
            allSongs = await API.fetchSongs(filters, sortBy);
            displaySongs(allSongs);
        } catch (error) {
            console.error("Ошибка при загрузке песен:", error);
            if (loadingMessage) loadingMessage.textContent = 'Ошибка загрузки песен.';
            songListContainer.innerHTML = '<p class="no-results-message">Не удалось загрузить список песен. Попробуйте позже.</p>';
        }
    }

    if (applyFiltersButton) {
        applyFiltersButton.addEventListener('click', loadAndDisplaySongs);
    }
    // Также можно добавить обработчики на изменение select'ов для мгновенной фильтрации/сортировки
    // sortBySelect.addEventListener('change', loadAndDisplaySongs);
    // authorFilter.addEventListener('change', loadAndDisplaySongs); // и т.д.

    // Инициализация
    if (songListContainer) { // Убедимся, что мы на главной странице
        populateFilters();
        loadAndDisplaySongs(); 
    }
}); 