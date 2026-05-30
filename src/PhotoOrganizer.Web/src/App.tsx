import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { MainLayout } from './components/MainLayout';
import BrowsePage from './pages/BrowsePage';
import PhotoDetailPage from './pages/PhotoDetailPage';
import SlideshowPage from './pages/SlideshowPage';
import './App.css';

function App() {
  return (
    <BrowserRouter>
      <Routes>
        {/* Full-screen slideshow — no header/chrome */}
        <Route path="/slideshow" element={<SlideshowPage />} />
        {/* All other routes get the standard header + main wrapper */}
        <Route element={<MainLayout />}>
          <Route path="/" element={<BrowsePage />} />
          <Route path="/photo/:id" element={<PhotoDetailPage />} />
        </Route>
      </Routes>
    </BrowserRouter>
  );
}

export default App;
