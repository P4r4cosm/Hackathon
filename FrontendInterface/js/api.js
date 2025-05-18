// API-функции для взаимодействия с бэкендом (заглушки)

const API = {
    /**
     * Запрашивает список песен с сервера.
     * @param {object} filters - Объект с фильтрами (author, year, theme, moodGenre).
     * @param {string} sortBy - Критерий сортировки (например, 'title-asc').
     * @returns {Promise<Array<object>>} Промис с массивом песен.
     */
    fetchSongs: async (filters = {}, sortBy = 'title-asc') => {
        console.log('API: Вызов fetchSongs с фильтрами:', filters, 'и сортировкой:', sortBy);
        // TODO: Реализовать реальный запрос к бэкенду
        return new Promise(resolve => {
            setTimeout(() => {
                let songs = [...mockSongs]; // Копируем, чтобы не изменять исходные мок-данные

                // Применяем фильтры
                if (filters.author) {
                    songs = songs.filter(song => song.author === filters.author);
                }
                if (filters.year) {
                    songs = songs.filter(song => song.year === parseInt(filters.year));
                }
                if (filters.theme) {
                    songs = songs.filter(song => song.tags.includes(filters.theme));
                }
                if (filters.moodGenre) {
                    songs = songs.filter(song => song.moodGenre.includes(filters.moodGenre));
                }

                // Применяем сортировку
                const [sortField, sortOrder] = sortBy.split('-');
                songs.sort((a, b) => {
                    let valA = a[sortField];
                    let valB = b[sortField];

                    if (sortField === 'year') {
                        valA = parseInt(valA);
                        valB = parseInt(valB);
                    }

                    if (typeof valA === 'string') valA = valA.toLowerCase();
                    if (typeof valB === 'string') valB = valB.toLowerCase();

                    if (valA < valB) return sortOrder === 'asc' ? -1 : 1;
                    if (valA > valB) return sortOrder === 'asc' ? 1 : -1;
                    return 0;
                });

                resolve(songs);
            }, 500); // Имитация задержки сети
        });
    },

    /**
     * Запрашивает детали одной песни по ID.
     * @param {string} songId - ID песни.
     * @returns {Promise<object|null>} Промис с объектом песни или null, если не найдена.
     */
    fetchSongById: async (songId) => {
        console.log('API: Вызов fetchSongById с ID:', songId);
        // TODO: Реализовать реальный запрос к бэкенду
        return new Promise(resolve => {
            setTimeout(() => {
                const song = mockSongs.find(s => s.id === songId) || null;
                resolve(song);
            }, 300);
        });
    },

    /**
     * Загружает аудиофайл и его метаданные.
     * @param {FormData} formData - Данные формы, включая файл и метаданные.
     * @returns {Promise<object>} Промис с результатом загрузки.
     */
    uploadAudio: async (formData) => {
        const title = formData.get('title');
        const author = formData.get('author');
        const year = formData.get('year');
        const keywords = formData.get('keywords');
        const audioFile = formData.get('audioFile');

        console.log('API: Вызов uploadAudio');
        console.log('API Data:', { title, author, year, keywords, fileName: audioFile ? audioFile.name : 'No file' });
        // TODO: Реализовать реальную загрузку файла и отправку метаданных на бэкенд.
        // TODO: Запустить конвейер обработки после успешной загрузки.
        return new Promise(resolve => {
            setTimeout(() => {
                if (audioFile) {
                    resolve({ success: true, message: `Файл "${audioFile.name}" и метаданные якобы загружены.` });
                } else {
                    resolve({ success: false, message: "Файл не выбран." });
                }
            }, 1000);
        });
    },

    /**
     * Получает текст песни (лирику) по ID песни.
     * @param {string} songId - ID песни.
     * @returns {Promise<Array<object>|null>} Промис с массивом строк текста (объекты {time, text}) или null.
     */
    getLyricsById: async (songId) => {
        console.log('API: Вызов getLyricsById с ID:', songId);
        // TODO: Реализовать реальный запрос к бэкенду
        return new Promise(resolve => {
            setTimeout(() => {
                const song = mockSongs.find(s => s.id === songId);
                resolve(song ? song.lyrics : null);
            }, 200);
        });
    },

    // --- Функции для административной панели --- 

    /**
     * Получает список тегов для модерации.
     * @returns {Promise<Array<object>>} Промис с массивом тегов.
     */
    fetchAdminTags: async () => {
        console.log('API: Вызов fetchAdminTags');
        // TODO: Реализовать реальный запрос к бэкенду
        return new Promise(resolve => {
            setTimeout(() => {
                resolve([...mockAdminTags]);
            }, 400);
        });
    },

    /**
     * Обновляет статус тега (approve, reject, edit).
     * @param {string} tagId - ID тега.
     * @param {string} action - 'approve' | 'reject'.
     * @param {string} [newName] - Новое имя тега (если редактирование).
     * @returns {Promise<object>} Промис с результатом операции.
     */
    updateAdminTag: async (tagId, action, newName) => {
        console.log('API: Вызов updateAdminTag:', { tagId, action, newName });
        // TODO: Реализовать реальный запрос к бэкенду
        return new Promise(resolve => {
            setTimeout(() => {
                const tag = mockAdminTags.find(t => t.id === tagId);
                if (tag) {
                    if (action === 'approve') tag.status = 'approved';
                    else if (action === 'reject') tag.status = 'rejected';
                    else if (action === 'edit' && newName) tag.name = newName;
                    // В реальном API здесь был бы PUT/POST запрос
                    resolve({ success: true, message: `Тег '${tag.name}' обновлен.` });
                } else {
                    resolve({ success: false, message: "Тег не найден." });
                }
            }, 300);
        });
    },

    /**
     * Получает список категорий/тем для модерации.
     * @returns {Promise<Array<object>>} Промис с массивом категорий.
     */
    fetchAdminCategories: async () => {
        console.log('API: Вызов fetchAdminCategories');
        // TODO: Реализовать реальный запрос к бэкенду
        return new Promise(resolve => {
            setTimeout(() => {
                resolve([...mockAdminCategories]);
            }, 400);
        });
    },

    /**
     * Обновляет категорию.
     * @param {string} categoryId - ID категории.
     * @param {object} data - Данные для обновления (name, description, status).
     * @returns {Promise<object>} Промис с результатом операции.
     */
    updateAdminCategory: async (categoryId, data) => {
        console.log('API: Вызов updateAdminCategory:', { categoryId, data });
        // TODO: Реализовать реальный запрос к бэкенду
        return new Promise(resolve => {
            setTimeout(() => {
                const category = mockAdminCategories.find(c => c.id === categoryId);
                if (category) {
                    if (data.name) category.name = data.name;
                    if (data.description) category.description = data.description;
                    if (data.status) category.status = data.status;
                    resolve({ success: true, message: `Категория '${category.name}' обновлена.` });
                } else {
                    resolve({ success: false, message: "Категория не найдена." });
                }
            }, 300);
        });
    },

    /**
     * Обновляет метаданные песни и текст.
     * @param {string} songId - ID песни.
     * @param {object} metadata - Объект с метаданными (title, author, year, tags, moodGenre).
     * @param {Array<object>} lyrics - Массив объектов текста песни.
     * @returns {Promise<object>} Промис с результатом операции.
     */
    updateSongDetails: async (songId, metadata, lyrics) => {
        console.log('API: Вызов updateSongDetails:', { songId, metadata, lyrics });
        // TODO: Реализовать реальный запрос к бэкенду
        return new Promise(resolve => {
            setTimeout(() => {
                const songIndex = mockSongs.findIndex(s => s.id === songId);
                if (songIndex !== -1) {
                    if(metadata) {
                        mockSongs[songIndex] = { ...mockSongs[songIndex], ...metadata };
                    }
                    if(lyrics) {
                         mockSongs[songIndex].lyrics = lyrics;
                    }
                    resolve({ success: true, message: `Данные песни '${mockSongs[songIndex].title}' обновлены.` });
                } else {
                    resolve({ success: false, message: "Песня не найдена." });
                }
            }, 500);
        });
    },

    // --- Функции для аналитической панели --- 

    /**
     * Получает данные для аналитической панели.
     * @returns {Promise<object>} Промис с объектом аналитических данных.
     */
    fetchAnalyticsData: async () => {
        console.log('API: Вызов fetchAnalyticsData');
        // TODO: Реализовать реальный запрос к бэкенду
        return new Promise(resolve => {
            setTimeout(() => {
                // Данные уже подготовлены в data.js через initializeAnalyticsData()
                resolve(JSON.parse(JSON.stringify(mockAnalyticsData))); // Возвращаем копию
            }, 600);
        });
    },

    // --- Функции для получения данных для фильтров --- 
    fetchFilterOptions: async () => {
        console.log('API: Вызов fetchFilterOptions');
        return new Promise(resolve => {
            setTimeout(() => {
                const authors = [...new Set(mockSongs.map(song => song.author))].sort();
                const years = [...new Set(mockSongs.map(song => song.year))].sort((a,b) => a - b);
                const themes = [...new Set(mockSongs.flatMap(song => song.tags))].sort();
                const moodGenres = [...new Set(mockSongs.flatMap(song => song.moodGenre))].sort();
                resolve({
                    authors,
                    years,
                    themes,
                    moodGenres
                });
            }, 100);
        });
    }
}; 