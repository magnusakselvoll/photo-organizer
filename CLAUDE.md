# Intro
This repo will be used to create software for organizing and displaying my own photos.
Working method should be heavily inspired by this project (my own): https://github.com/magnusakselvoll/photo-booth-take-two

# Main idea and functionality
I have photos in several folders on my computer (Windows) and connected NAS (Synology). I want to be able to browse my photo
and show eternal slideshows.

Important points: 
- I have a few different photo folders. Some contain originals and some contain edits. Folders should be marked as such.
- The application should make attempts at identifying duplicates, typically on file names. If several versions exist,
the edited version is the preferred display version.

# Architecture
- The software should be organized into a dotnet 10 backend service / API, and a React frontend web app
- While a central database may exist, all the meta data about folders and/or files, should be kept in files
in each folder. Think sidecar files etc. They might be .md files or some other sidecar format.
- The architecture should be robust and extensible. Automatic taggig, location, face recognition etc. might
be future changes
