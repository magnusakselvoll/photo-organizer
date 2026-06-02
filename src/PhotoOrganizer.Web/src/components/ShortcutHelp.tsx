export function ShortcutHelp() {
  return (
    <div className="slideshow-shortcut-help">
      <h3 className="slideshow-shortcut-title">Keyboard shortcuts</h3>
      <table className="slideshow-shortcut-table">
        <tbody>
          <tr><td><kbd>←</kbd> / <kbd>→</kbd></td><td>Previous / Next photo</td></tr>
          <tr><td><kbd>Space</kbd></td><td>Play / Pause</td></tr>
          <tr><td><kbd>+</kbd> / <kbd>−</kbd></td><td>Increase / Decrease display time</td></tr>
          <tr><td><kbd>I</kbd></td><td>Show photo info</td></tr>
          <tr><td><kbd>?</kbd></td><td>Show this help</td></tr>
          <tr><td><kbd>Esc</kbd></td><td>Exit slideshow</td></tr>
        </tbody>
      </table>
    </div>
  );
}
