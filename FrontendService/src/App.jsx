// file: App.jsx
import { Route, Routes } from 'react-router-dom';

// Наш новый лейаут и защитник роутов
import AppLayout from './AppLayout.jsx';
import ProtectedRoute from './components/ProtectedRoute';

// Страницы
import { 
  ArtistDetails, 
  Search, 
  SongDetails, 
  ArchiveExplorer,
  RecordingDetails,
  Analytics
} from './pages';
import { UploadForm } from './components/Admin';
// AuthPage здесь больше не нужен!

const App = () => {
  return (
    <Routes>
      {/* 
        Создаем родительский роут, который использует ProtectedRoute.
        Все вложенные в него роуты станут защищенными.
      */}
      <Route element={<ProtectedRoute />}>
        {/* 
          Внутри защищенной зоны мы рендерим наш основной лейаут AppLayout.
          Все страницы теперь будут отображаться внутри него (в <Outlet />).
        */}
        <Route element={<AppLayout />}>
          <Route path="/" element={<ArchiveExplorer />} />
          <Route path="/artists/:id" element={<ArtistDetails />} />
          <Route path="/songs/:songid" element={<SongDetails />} />
          <Route path="/search/:searchTerm" element={<Search />} />
          
          <Route path="/archive" element={<ArchiveExplorer />} />
          <Route path="/recordings/:recordingId" element={<RecordingDetails />} />
          <Route path="/analytics" element={<Analytics />} />
          <Route path="/upload" element={<UploadForm />} />
          <Route path="/authors/:authorId" element={<ArtistDetails />} />
          <Route path="/tag/:tagId" element={<ArchiveExplorer />} />
        </Route>
      </Route>

      {/* 
        Здесь можно добавить публичные роуты, если они нужны.
        Например, страница "О проекте", которая не требует входа.
        <Route path="/about" element={<AboutPage />} /> 
      */}

      {/* Роут для страницы "Не найдено" */}
      <Route path="*" element={<div>404 - Страница не найдена</div>} />
    </Routes>
  );
};

export default App;

