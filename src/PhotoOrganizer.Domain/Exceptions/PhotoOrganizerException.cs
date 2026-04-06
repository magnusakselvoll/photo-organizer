namespace PhotoOrganizer.Domain.Exceptions;

public class PhotoOrganizerException : Exception
{
    public PhotoOrganizerException(string message) : base(message) { }
    public PhotoOrganizerException(string message, Exception inner) : base(message, inner) { }
}

public class FolderNotFoundException : PhotoOrganizerException
{
    public FolderNotFoundException(string path)
        : base($"Source folder not found: {path}") { }
}

public class SidecarParsingException : PhotoOrganizerException
{
    public SidecarParsingException(string sidecarPath, Exception inner)
        : base($"Failed to parse sidecar file: {sidecarPath}", inner) { }
}
