const mockSongs = [
    {
        id: "s1",
        title: "Священная война",
        author: "А. Александров, В. Лебедев-Кумач",
        year: 1941,
        tags: ["патриотика", "гимн", "начало войны"],
        moodGenre: ["торжественная", "марш", "воодушевляющая"],
        audioOriginalUrl: "audio/sacred_war_original.mp3", // TODO: Replace with actual or placeholder file
        audioEnhancedUrl: "audio/sacred_war_enhanced.mp3", // TODO: Replace with actual or placeholder file
        lyrics: [
            { time: 0, text: "Вставай, страна огромная," },
            { time: 3, text: "Вставай на смертный бой" },
            { time: 6, text: "С фашистской силой тёмною," },
            { time: 9, text: "С проклятою ордой!" },
            { time: 12, text: "Пусть ярость благородная" },
            { time: 15, text: "Вскипает, как волна," },
            { time: 18, text: "Идёт война народная," },
            { time: 21, text: "Священная война!" }
            // ... больше строк текста
        ]
    },
    {
        id: "s2",
        title: "Тёмная ночь",
        author: "Н. Богословский, В. Агатов",
        year: 1943,
        tags: ["тоска", "любовь", "ожидание", "личная"],
        moodGenre: ["лирическая", "медленная", "ностальгическая"],
        audioOriginalUrl: "audio/dark_night_original.mp3", // TODO: Replace with actual or placeholder file
        audioEnhancedUrl: "audio/dark_night_enhanced.mp3", // TODO: Replace with actual or placeholder file
        lyrics: [
            { time: 0, text: "Тёмная ночь, только пули свистят по степи," },
            { time: 4, text: "Только ветер гудит в проводах, тускло звезды мерцают." },
            { time: 8, text: "В тёмную ночь ты, любимая, знаю, не спишь," },
            { time: 12, text: "И у детской кроватки тайком ты слезу утираешь." }
            // ... больше строк текста
        ]
    },
    {
        id: "s3",
        title: "День Победы",
        author: "Д. Тухманов, В. Харитонов",
        year: 1975, // Написана позже, но посвящена ВОВ
        tags: ["победа", "радость", "память", "праздник"],
        moodGenre: ["торжественная", "марш", "праздничная"],
        audioOriginalUrl: "audio/victory_day_original.mp3", // TODO: Replace with actual or placeholder file
        audioEnhancedUrl: "audio/victory_day_enhanced.mp3", // TODO: Replace with actual or placeholder file
        lyrics: [
            { time: 0, text: "День Победы, как он был от нас далёк," },
            { time: 4, text: "Как в костре потухшем таял уголёк." },
            { time: 8, text: "Были вёрсты, обгорелые, в пыли," },
            { time: 12, text: "Этот день мы приближали как могли." }
            // ... больше строк текста
        ]
    },
    {
        id: "s4",
        title: "Катюша",
        author: "М. Блантер, М. Исаковский",
        year: 1938, // Довоенная, но популярна в годы войны
        tags: ["любовь", "ожидание", "девушка", "популярная"],
        moodGenre: ["народная", "лирическая", "мелодичная"],
        audioOriginalUrl: "audio/katyusha_original.mp3", // TODO: Replace with actual or placeholder file
        audioEnhancedUrl: "audio/katyusha_enhanced.mp3", // TODO: Replace with actual or placeholder file
        lyrics: [
            { time: 0, text: "Расцветали яблони и груши," },
            { time: 3, text: "Поплыли туманы над рекой." },
            { time: 6, text: "Выходила на берег Катюша," },
            { time: 9, text: "На высокий берег на крутой." }
            // ... больше строк текста
        ]
    },
    {
        id: "s5",
        title: "В землянке",
        author: "К. Листов, А. Сурков",
        year: 1942,
        tags: ["фронт", "тоска", "дом", "письмо"],
        moodGenre: ["лирическая", "медленная", "задушевная"],
        audioOriginalUrl: "audio/v_zemlyanke_original.mp3", // TODO: Replace with actual or placeholder file
        audioEnhancedUrl: "audio/v_zemlyanke_enhanced.mp3", // TODO: Replace with actual or placeholder file
        lyrics: [
            { time: 0, text: "Бьётся в тесной печурке огонь," },
            { time: 3, text: "На поленьях смола, как слеза." },
            { time: 6, text: "И поёт мне в землянке гармонь" },
            { time: 9, text: "Про улыбку твою и глаза." }
            // ... больше строк текста
        ]
    }
];

// Мок-данные для фильтров (можно генерировать из mockSongs при необходимости)
const mockAuthors = ["А. Александров, В. Лебедев-Кумач", "Н. Богословский, В. Агатов", "Д. Тухманов, В. Харитонов", "М. Блантер, М. Исаковский", "К. Листов, А. Сурков"];
const mockYears = [1938, 1941, 1942, 1943, 1975]; // Уникальные годы
const mockThemes = ["патриотика", "гимн", "начало войны", "тоска", "любовь", "ожидание", "личная", "победа", "радость", "память", "праздник", "девушка", "популярная", "фронт", "дом", "письмо"]; // Уникальные теги
const mockMoodGenres = ["торжественная", "марш", "воодушевляющая", "лирическая", "медленная", "ностальгическая", "праздничная", "народная", "мелодичная", "задушевная"];


// Мок-данные для административной панели
let mockAdminTags = [
    { id: "tag1", name: "патриотика", status: "approved" },
    { id: "tag2", name: "тоска по дому", status: "pending" },
    { id: "tag3", name: "фронтовая лирика", status: "approved" },
    { id: "tag4", name: "победа", status: "approved" },
    { id: "tag5", name: "гимн", status: "rejected" }
];

let mockAdminCategories = [
    { id: "cat1", name: "Песни о начале войны", description: "Песни, написанные в 1941-1942 годах", status: "approved" },
    { id: "cat2", name: "Лирические песни", description: "Песни о любви, разлуке, тоске", status: "pending" },
    { id: "cat3", name: "Песни победы", description: "Песни, посвященные победе в ВОВ", status: "approved" }
];


// Мок-данные для аналитической панели
const mockAnalyticsData = {
    songsByYear: [
        { year: 1938, count: 1 },
        { year: 1941, count: 1 },
        { year: 1942, count: 1 },
        { year: 1943, count: 1 },
        { year: 1975, count: 1 }
        // Заполняется на основе mockSongs
    ],
    songsByAuthor: [
        { author: "А. Александров, В. Лебедев-Кумач", count: 1 },
        { author: "Н. Богословский, В. Агатов", count: 1 },
        // Заполняется на основе mockSongs
    ],
    popularTags: [
        { tag: "любовь", count: 2 },
        { tag: "тоска", count: 2 },
        // Заполняется на основе mockSongs
    ]
};

// Функция для инициализации данных для аналитики (можно вызвать один раз)
function initializeAnalyticsData() {
    // Songs by Year
    const yearCounts = {};
    mockSongs.forEach(song => {
        yearCounts[song.year] = (yearCounts[song.year] || 0) + 1;
    });
    mockAnalyticsData.songsByYear = Object.entries(yearCounts).map(([year, count]) => ({ year: parseInt(year), count }));

    // Songs by Author
    const authorCounts = {};
    mockSongs.forEach(song => {
        authorCounts[song.author] = (authorCounts[song.author] || 0) + 1;
    });
    mockAnalyticsData.songsByAuthor = Object.entries(authorCounts).map(([author, count]) => ({ author, count }));

    // Popular Tags
    const tagCounts = {};
    mockSongs.forEach(song => {
        song.tags.forEach(tag => {
            tagCounts[tag] = (tagCounts[tag] || 0) + 1;
        });
    });
    mockAnalyticsData.popularTags = Object.entries(tagCounts)
        .map(([tag, count]) => ({ tag, count }))
        .sort((a, b) => b.count - a.count) // Сортировка по популярности
        .slice(0, 10); // Показать топ-10
}

// Инициализируем аналитические данные при загрузке скрипта
initializeAnalyticsData(); 