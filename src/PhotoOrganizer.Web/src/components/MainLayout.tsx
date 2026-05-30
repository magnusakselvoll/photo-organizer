import { Link, Outlet } from 'react-router-dom';

export function MainLayout() {
  return (
    <>
      <header className="app-header">
        <Link to="/" className="app-title">Photo Organizer</Link>
        <nav className="app-nav">
          <Link to="/slideshow" className="app-nav-link">Slideshow</Link>
        </nav>
      </header>
      <main className="app-main">
        <Outlet />
      </main>
    </>
  );
}
