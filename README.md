NovaSFTP2 WPF file change SFTP monitor for Windows
=================================

OVERVIEW
-----
NovaSFTP2 is Windows file change monitor that uploads to a remote SFTP server when it detects changes to local files.

![NovaSFTP2 Screenshot](https://raw.githubusercontent.com/mitchcapper/NovaSFTP2/master/screenshot.png "NovaSFTP2 Screenshot")

Features
-----
- Monitor a local folder (or sub folders) for changes
- Supports windows jump lists
- Will ignore .svn, .git, .tmp, mrgtmp, and ~ files by default (can change the regex, and note the regex ignores spaces)
- Supports windows taskbar progress support
- Can save defaults, or save as a profile
- Paegent support for publickey auth (in addition to password)
- TCP Keep alive support
- Uses Renci.SshNet


Notes
-----
- Will not create remote folders if they don't exist
- To create a profile, set the settings as you like then hit save as to give it a name.