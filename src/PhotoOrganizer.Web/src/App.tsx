import { BrowserRouter, Routes, Route, Link } from 'react-router-dom';
import BrowsePage from './pages/BrowsePage';
import PhotoDetailPage from './pages/PhotoDetailPage';
import './App.css';

function App() {
  return (
    <BrowserRouter>
      <header className="app-header">
        <Link to="/" className="app-title">Photo Organizer</Link>
      </header>
      <main className="app-main">
        <Routes>
          <Route path="/" element={<BrowsePage />} />
          <Route path="/photo/:id" element={<PhotoDetailPage />} />
        </Routes>
      </main>
    </BrowserRouter>
  );
}

export default App;
