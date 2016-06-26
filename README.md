NovaSFTP2 WPF file change SFTP monitor for Windows
=================================


OVERVIEW
-----
NovaSFTP2 is Windows file change monitor that uploads to a remote SFTP server when it detects changes to local files.

Features
-----
- Monitor a local folder (or sub folders) for changes
- Supports windows jump lists
- Will ignore .svn, .git, .tp, mrgtmp, and ~ files 
- Supports windows taskbar progress support
- Can save defaults, or save as a profile
- Paegent support for publickey auth (in addition to password)
- TCP Keep alive support
- Uses Renci.SshNet


Notes
-----
- Will not create remote folders if they don't exist
- Does not have a way to configure what files are ignored, hard coded into the code currently
- To create a profile, set the settings as you like then hit save as to give it a name.