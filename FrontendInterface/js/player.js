document.addEventListener('DOMContentLoaded', () => {
    const songTitleElement = document.getElementById('song-title-player');
    const songAuthorElement = document.getElementById('song-author-player');
    const audioPlayer = document.getElementById('audio-player');
    const originalVersionRadio = document.getElementById('original-version');
    const enhancedVersionRadio = document.getElementById('enhanced-version');
    const lyricsContainer = document.getElementById('lyrics-container');
    const lyricsSearchInput = document.getElementById('lyrics-search');

    let currentSong = null;
    let currentLyrics = [];
    let lyricLines = []; // Для хранения DOM-элементов строк текста

    async function loadSongData() {
        const songId = getUrlParameter('songId');
        if (!songId) {
            showNotification('ID песни не указан.', 'error');
            songTitleElement.textContent = 'Ошибка: ID песни не найден';
            lyricsContainer.innerHTML = '<p>Невозможно загрузить песню.</p>';
            return;
        }

        try {
            currentSong = await API.fetchSongById(songId);
            currentLyrics = await API.getLyricsById(songId);

            if (!currentSong) {
                showNotification('Песня не найдена.', 'error');
                songTitleElement.textContent = 'Песня не найдена';
                lyricsContainer.innerHTML = '<p>Не удалось загрузить данные о песне.</p>';
                return;
            }

            songTitleElement.textContent = currentSong.title;
            songAuthorElement.textContent = `Автор: ${currentSong.author}`;
            
            // Установка начальной версии аудио
            updateAudioSource(); 
            displayLyrics(currentLyrics);

        } catch (error) {
            console.error("Ошибка при загрузке данных песни:", error);
            showNotification('Ошибка загрузки данных песни.', 'error');
            songTitleElement.textContent = 'Ошибка загрузки';
            lyricsContainer.innerHTML = '<p>Не удалось загрузить данные. Попробуйте позже.</p>';
        }
    }

    function updateAudioSource() {
        if (!currentSong) return;
        const selectedVersion = enhancedVersionRadio.checked ? 'enhanced' : 'original';
        const audioUrl = selectedVersion === 'enhanced' ? currentSong.audioEnhancedUrl : currentSong.audioOriginalUrl;
        
        // Сохраняем текущее время и состояние воспроизведения
        const currentTime = audioPlayer.currentTime;
        const isPlaying = !audioPlayer.paused;

        audioPlayer.src = audioUrl;
        // TODO: Убедиться, что аудио файлы существуют по указанным мок-путям или заменить на реальные заглушки.
        // Если нет, плеер не будет работать. Можно создать пустые .mp3 файлы в папке audio/.
        
        audioPlayer.onloadedmetadata = () => {
            audioPlayer.currentTime = currentTime; // Восстанавливаем время
            if (isPlaying) {
                audioPlayer.play().catch(e => console.warn("Ошибка автовоспроизведения после смены источника:", e));
            }
        };
        console.log(`Аудио установлено на: ${selectedVersion} (${audioUrl})`);
    }

    function displayLyrics(lyrics) {
        lyricsContainer.innerHTML = '';
        lyricLines = []; // Очищаем массив ссылок на DOM-элементы
        if (!lyrics || lyrics.length === 0) {
            lyricsContainer.innerHTML = '<p>Текст песни отсутствует.</p>';
            return;
        }
        lyrics.forEach(line => {
            const p = document.createElement('p');
            p.textContent = line.text;
            p.dataset.time = line.time;
            lyricsContainer.appendChild(p);
            lyricLines.push(p); // Сохраняем ссылку на элемент
        });
    }

    function highlightCurrentLyric() {
        if (!audioPlayer || lyricLines.length === 0) return;
        const currentTime = audioPlayer.currentTime;
        let activeLine = null;

        for (let i = lyricLines.length - 1; i >= 0; i--) {
            const line = lyricLines[i];
            if (parseFloat(line.dataset.time) <= currentTime) {
                activeLine = line;
                break;
            }
        }

        lyricLines.forEach(line => line.classList.remove('active'));
        if (activeLine) {
            activeLine.classList.add('active');
            // Автоматическая прокрутка к активной строке
            // activeLine.scrollIntoView({ behavior: 'smooth', block: 'center' });
            // Закомментировано, т.к. может быть навязчивым. Можно настроить.
        }
    }

    function searchLyrics() {
        const searchTerm = lyricsSearchInput.value.toLowerCase();
        lyricLines.forEach(line => {
            const text = line.textContent.toLowerCase();
            if (searchTerm && text.includes(searchTerm)) {
                line.style.backgroundColor = 'yellow'; // Пример подсветки
                line.style.color = 'black';
            } else {
                line.style.backgroundColor = '';
                line.style.color = '';
            }
        });
    }

    // Инициализация и обработчики событий
    if (songTitleElement) { // Убедимся, что мы на странице плеера
        loadSongData();

        originalVersionRadio.addEventListener('change', updateAudioSource);
        enhancedVersionRadio.addEventListener('change', updateAudioSource);

        audioPlayer.addEventListener('timeupdate', highlightCurrentLyric);
        lyricsSearchInput.addEventListener('input', searchLyrics);

        // TODO: Для реальных аудиофайлов создать папку audio/ и поместить туда заглушки,
        // например, sacred_war_original.mp3, sacred_war_enhanced.mp3 и т.д.
        // Это можно сделать вручную или через команду, если есть такая возможность.
        console.log("Скрипт плеера player.js загружен и инициализирован.");
    }
}); 