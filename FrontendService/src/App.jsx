import { useSelector } from 'react-redux';
import { Route, Routes, useLocation, Navigate } from 'react-router-dom';
import { useState, useEffect } from 'react';

import { Searchbar, Sidebar, MusicPlayer, TopPlay } from './components';
import { 
  AuthorDetails, 
  TopArtists, 
  RecordingsByYear, 
  Discover, 
  Search, 
  SongDetails, 
  TopCharts,
  ArchiveExplorer,
  RecordingDetails,
  Analytics
} from './pages';
import { UploadForm } from './components/Admin';

// Компонент для защиты маршрутов, требующих админских прав
const AdminRoute = ({ children }) => {
  // Для демонстрационных целей разрешаем доступ всем
  return children;
  
  // В реальном приложении проверяем роль из redux или localStorage
  // В этой имитации просто проверяем email пользователя
  /*
  const isAdmin = localStorage.getItem('email') === 'admin@admin.com';
  
  if (!isAdmin) {
    return <Navigate to="/" replace />;
  }
  
  return children;
  */
};

const App = () => {
  const { activeSong } = useSelector((state) => state.player);
  const location = useLocation();
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  
  useEffect(() => {
    // Проверяем статус аутентификации при загрузке
    const authStatus = localStorage.getItem('isAuthenticated') === 'true';
    setIsAuthenticated(authStatus);
    
    // Слушаем изменения в localStorage
    const handleStorageChange = () => {
      const newAuthStatus = localStorage.getItem('isAuthenticated') === 'true';
      setIsAuthenticated(newAuthStatus);
    };
    
    window.addEventListener('storage', handleStorageChange);
    return () => window.removeEventListener('storage', handleStorageChange);
  }, []);

  return (
    <div className="relative flex">
      <Sidebar />

      <div className="flex-1 flex flex-col bg-gradient-to-br from-black to-[#121286]">
        <div className="px-6 h-[calc(100vh-72px)] overflow-y-scroll hide-scrollbar flex xl:flex-row flex-col-reverse">
          <div className="flex-1 h-fit pb-40">
            <Routes>
              <Route path="/" element={<ArchiveExplorer />} />
              <Route path="/discover" element={<Discover />} />
              <Route path="/top-artists" element={<TopArtists />} />
              <Route path="/top-charts" element={<TopCharts />} />
              <Route path="/around-you" element={<RecordingsByYear />} />
              
              {/* Обновленные маршруты для использования с нашим API */}
              <Route path="/songs/:songid" element={<RecordingDetails />} />
              <Route path="/artists/:id" element={<AuthorDetails />} />
              <Route path="/search/:searchTerm" element={<Search />} />
              
              {/* Маршруты для архива военных записей */}
              <Route path="/archive" element={<ArchiveExplorer />} />
              <Route path="/recordings/:recordingId" element={<RecordingDetails />} />
              <Route path="/analytics" element={<Analytics />} />
              
              {/* Для демонстрационных целей убрали защиту маршрута */}
              <Route path="/upload" element={
                <AdminRoute>
                  <UploadForm />
                </AdminRoute>
              } />
              
              <Route path="/authors/:authorId" element={<AuthorDetails />} />
              <Route path="/tag/:tagId" element={<ArchiveExplorer />} />
            </Routes>
          </div>
          <div className="xl:sticky relative top-0 h-fit">
            <TopPlay />
          </div>
        </div>

        {activeSong?.title && (
          <div className="absolute h-28 bottom-0 left-0 right-0 flex animate-slideup bg-gradient-to-br from-white/10 to-[#2a2a80] backdrop-blur-lg rounded-t-3xl z-10">
            <MusicPlayer />
          </div>
        )}
      </div>
    </div>
  );
};

export default App; 