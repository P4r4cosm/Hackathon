import { useSelector } from 'react-redux';
import { Route, Routes } from 'react-router-dom';

import { Searchbar, Sidebar, MusicPlayer, TopPlay } from './components';
import { 
  ArtistDetails, 
  Search, 
  SongDetails, 
  ArchiveExplorer,
  RecordingDetails,
  Analytics
} from './pages';
import { UploadForm } from './components/Admin';
import { AuthProvider } from './components/Auth/AuthProvider';
import AuthPage from './components/Auth/AuthPage';

const App = () => {
  const { activeSong } = useSelector((state) => state.player);

  // Проверяем, если текущая страница - страница авторизации
  const isAuthPage = window.location.pathname === '/auth';

  // Если это страница авторизации, показываем только её
  if (isAuthPage) {
    return <AuthPage />;
  }

  return (
    <AuthProvider>
      <div className="relative flex">
        <Sidebar />
        <div className="flex-1 flex flex-col bg-gradient-to-br from-black to-[#121286]">
          <Searchbar />

          <div className="px-6 h-[calc(100vh-72px)] overflow-y-scroll hide-scrollbar flex xl:flex-row flex-col-reverse">
            <div className="flex-1 h-fit pb-40">
              <Routes>
                <Route path="/" element={<ArchiveExplorer />} />
                <Route path="/artists/:id" element={<ArtistDetails />} />
                <Route path="/songs/:songid" element={<SongDetails />} />
                <Route path="/search/:searchTerm" element={<Search />} />
                
                {/* Новые маршруты для архива военных записей */}
                <Route path="/archive" element={<ArchiveExplorer />} />
                <Route path="/recordings/:recordingId" element={<RecordingDetails />} />
                <Route path="/analytics" element={<Analytics />} />
                <Route path="/upload" element={<UploadForm />} />
                <Route path="/authors/:authorId" element={<ArtistDetails />} />
                <Route path="/tag/:tagId" element={<ArchiveExplorer />} />
                <Route path="/auth" element={<AuthPage />} />
              </Routes>
            </div>
            <div className="xl:sticky relative top-0 h-fit">
              <TopPlay />
            </div>
          </div>
        </div>

        {activeSong?.title && (
          <div className="absolute h-28 bottom-0 left-0 right-0 flex animate-slideup bg-gradient-to-br from-white/10 to-[#2a2a80] backdrop-blur-lg rounded-t-3xl z-10">
            <MusicPlayer />
          </div>
        )}
      </div>
    </AuthProvider>
  );
};

export default App; 