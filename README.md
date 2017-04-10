NovaSFTP2 WPF file change SFTP/Docker monitor for Windows
=================================

OVERVIEW
-----
NovaSFTP2 is Windows file change monitor that uploads to a remote SFTP server or docker container when it detects changes to local files.

![NovaSFTP2 Screenshot](https://raw.githubusercontent.com/mitchcapper/NovaSFTP2/master/screenshot.png "NovaSFTP2 Screenshot")

Features
-----
- Monitor a local folder (or sub folders) for changes
- Supports windows jump lists
- Will ignore .svn, .git, .tmp, mrgtmp, and ~ files by default (can change the regex, and note the regex ignores spaces)
- Supports windows taskbar progress support (only SFTP servers currently get full progress, docker does not report upload progress)
- Can save defaults, or save as a profile
- SFTP Features:
  - Paegent support for publickey auth (in addition to password)
  - TCP Keep alive support
  - Uses Renci.SshNet
- Docker Features:
  - Certificate based authentication
  - Basic HTTP authentication
  - Anonymous Authentication  
  - Optional manual CA cert specification (otherwise uses computer's store)
  - Can ignore TLS hostname mis-mismatches
  - Option to use bzip2 compression (for files up to 5MB)
  - Uses Docker.DotNet
Notes
-----
- Will not create remote folders if they don't exist
- To create a profile, set the settings as you like then hit save as to give it a name.