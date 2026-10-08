# FamilyTheater

FamilyTheater 是一个面向 Windows 桌面的本地娱乐管理工具，用于把散落在磁盘上的电影、图片、漫画 PDF 和游戏整理成可浏览、可筛选、可播放的家庭媒体库。

FamilyTheater is a native entertainment management tool for Windows desktops. It organizes movies, images, comic PDFs, and games scattered across local disks into a browsable, filterable, and playable home media library.

- [中文说明](#中文说明)
- [English](#english)

## 中文说明

### 项目简介

FamilyTheater 使用 WPF + .NET 8 构建，数据保存在本机 SQLite 数据库中。媒体文件仍保留在原始磁盘目录，应用只维护索引、标签、封面、播放入口和用户配置。

它适合以下场景：

- 本地硬盘中有大量电影、图片、PDF 漫画或游戏目录。
- 希望按目录标签快速筛选内容。
- 希望用统一界面浏览、预览、播放和启动内容。
- 希望媒体文件保持原位置，不被导入到某个封闭库中。

### 主要功能

- 电影库：扫描本地视频文件，匹配或生成海报，支持播放、重命名、标签管理、删除记录和加载 SRT 字幕预览。
- 图片库：扫描本地图片，支持大图预览、缩放、拖拽、GIF 动图显示、标签管理和删除记录。
- 漫画库：扫描 PDF 文件，支持 PDF 阅读、自动生成封面、标签管理和删除记录。
- 游戏库：扫描游戏文件夹，支持封面匹配、启动项选择、截图目录、标签管理和启动游戏。
- 标签筛选：按标签快速过滤内容；电影、图片、漫画和游戏使用各自独立的标签分类。
- 用户与权限：支持注册、登录、`admin` / `user` 角色；管理员可以配置路径、扫描媒体库、清库和管理用户权限。
- 系统设置：支持播放器精确时间显示等全局选项。
- 本地化存储：数据库、日志和配置保存在当前 Windows 用户目录下。

### 支持的媒体格式

| 类型 | 支持格式 |
| --- | --- |
| 电影 | `.mp4`, `.mkv`, `.avi`, `.wmv`, `.flv`, `.mov`, `.rmvb`, `.ts`, `.m4v`, `.webm` |
| 字幕 | `.srt` |
| 图片 | `.jpg`, `.jpeg`, `.png`, `.webp`, `.bmp`, `.gif` |
| 漫画 | `.pdf` |
| 封面/海报 | `.jpg`, `.jpeg`, `.png`, `.webp`, `.bmp` |
| 游戏截图 | `.jpg`, `.jpeg`, `.png`, `.webp`, `.bmp`, `.gif` |

视频播放依赖 Windows 系统媒体能力。如果某些编码无法播放，例如 HEVC/H.265，通常需要安装对应的系统解码器。

### 安装

推荐从 GitHub Releases 下载安装包：

```text
FamilyTheater-Setup-x.y.z.exe
```

运行安装包后按提示安装即可。安装完成后启动 `FamilyTheater`。

如果你是从源码构建，请参考后面的“开发与构建”。

### 首次使用

1. 启动应用。
2. 注册一个账号。
3. 第一个用户会自动成为 `admin`。
4. 登录后进入主界面。
5. 打开设置窗口，配置媒体目录。
6. 保存配置，应用会开始扫描媒体库。

### 配置媒体目录

在设置窗口中可以配置以下路径：

| 配置项 | 说明 |
| --- | --- |
| 图片根目录 | 图片库扫描入口。目录下的图片会被导入图片库索引。 |
| 电影根目录 | 电影库扫描入口。目录下的视频文件会被导入电影库索引。 |
| 电影海报根目录 | 用于匹配电影海报；如果找不到海报，程序会自动抽帧生成 `.jpg` 海报。 |
| 漫画根目录 | 漫画库扫描入口。目录下的 PDF 会被导入漫画库索引。 |
| 漫画封面根目录 | 用于匹配或生成 PDF 封面。 |
| 游戏根目录 | 游戏库扫描入口。包含可执行文件的文件夹会被识别为游戏。 |
| 游戏海报根目录 | 用于匹配游戏封面。 |

建议不要把自动生成的海报目录放在 C 盘空间紧张的位置。电影海报会随电影数量增长，推荐放到媒体盘。

### 目录与标签规则

扫描时，FamilyTheater 会根据文件所在目录自动生成标签。

例如图片路径：

```text
D:\Pictures\旅行\日本\001.jpg
```

会生成标签：

```text
旅行
日本
```

电影、图片、漫画和游戏使用各自独立的标签分类。同名标签可以同时存在于不同媒体类型中，互不影响。

### 日常使用

主界面提供电影、图片、漫画、游戏四个分类。进入分类后可以：

- 查看当前分类内容。
- 按标签筛选。
- 打开详情页。
- 执行播放、预览、阅读或启动操作。

### 电影

电影详情页支持：

- 播放视频。
- 修改标题。修改标题会尝试同步重命名本地视频文件。
- 选择或替换海报。
- 添加、移除、删除标签。
- 删除记录，必要时也可以删除本地文件。

播放器支持：

- 单击视频画面播放或暂停。
- 空格键播放或暂停。
- `Esc` 关闭播放器。
- 可选的精确时间显示，例如 `00:01:22,500`。
- 手动加载 `.srt` 字幕文件，用于临时预览字幕效果。

当前字幕加载是临时功能：字幕文件会被读取到当前播放器窗口中并覆盖显示在画面底部。关闭播放器后不会记录字幕路径；如果修改了字幕文件，需要再次点击“字幕”按钮重新加载。

### 图片

图片支持详情页和大图预览。

大图预览支持：

- 自动适应窗口。
- 鼠标滚轮缩放。
- 鼠标左键拖拽平移。
- 双击恢复适应窗口。
- `+` / `-` 缩放，`0` 适应窗口，`Esc` 关闭。

GIF 会以动画方式显示。

### 漫画

漫画库目前以 PDF 为主。漫画详情页支持：

- 打开 PDF 阅读器。
- 修改标题。修改标题会尝试同步重命名本地 PDF 文件。
- 选择或替换封面。
- 添加、移除、删除标签。
- 删除记录，必要时也可以删除本地文件。

PDF 阅读器支持翻页、缩放、适应页面、自动翻页和隐藏控制栏。

### 游戏

游戏库会扫描游戏根目录下包含可执行文件的文件夹。游戏详情页支持：

- 选择启动项。
- 启动游戏。
- 设置游戏封面。
- 设置截图目录。
- 浏览截图。
- 添加、移除、删除标签。

### 重新扫描与清库

设置窗口提供增量扫描、完整重扫和清空媒体库能力。

- 增量扫描：跳过已存在记录，只导入新增内容。
- 完整重扫：重新读取已存在内容并更新元数据。
- 清空单个媒体库：删除该分类的数据库记录、标签关系和手动保存标签。
- 清空全部媒体库：删除所有媒体库数据库记录和标签记录。

清库不会删除磁盘上的媒体文件，也不会删除已生成的海报文件。需要释放海报占用空间时，请手动清理对应海报目录。

### 用户与权限

FamilyTheater 支持两类角色：

| 角色 | 能力 |
| --- | --- |
| `admin` | 配置路径、扫描媒体库、清空媒体库、管理用户权限。 |
| `user` | 登录并浏览媒体库。 |

当数据库中已经有用户但没有 `admin` 时，程序会自动把第一个用户提升为 `admin`。

### 数据位置

应用数据保存在当前 Windows 用户的 Roaming AppData 中：

```text
%APPDATA%\FamilyTheater
```

常见文件：

| 文件/目录 | 说明 |
| --- | --- |
| `FamilyTheater.db` | 管理员数据库。 |
| `FamilyTheater.User.db` | 普通用户数据库。 |
| `logs\yyyy-MM-dd.log` | 按日期生成的日志文件。 |

PDF 阅读器会在系统临时目录创建页面缓存，正常关闭窗口时会自动清理：

```text
%TEMP%\FamilyTheater\PdfViewer
```

### 开发与构建

需要：

- Windows 10 或更高版本。
- .NET SDK 8.0。
- Windows SDK `10.0.19041.0` 或兼容版本。
- Inno Setup 6，用于生成安装包。

仓库包含：

```text
FamilyTheater.sln
LoginWindow/                 WPF 桌面应用
FamilyTheater.Core/          数据模型、数据库、扫描和媒体服务
installer/FamilyTheater.iss  Inno Setup 安装脚本
scripts/build-installer.ps1  发布和打包脚本
```

还原和构建：

```powershell
dotnet restore
dotnet build
```

运行调试：

```powershell
dotnet run --project .\LoginWindow\FamilyTheater.App.csproj
```

### 构建安装包

使用脚本构建 Release 安装包：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1
```

也可以指定版本：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1 -Version 0.1.7
```

安装包输出目录：

```text
artifacts\installer
```

`artifacts` 目录不会提交到 Git，可以随时重新生成。

### 常见问题

#### 视频无法播放

FamilyTheater 使用 Windows 桌面媒体能力播放视频。若系统缺少某些编码的解码器，可能无法播放。可以尝试安装 HEVC 扩展或使用更通用的视频编码。

#### 清库后数据库文件没有明显变小

清库会删除数据库记录，但 SQLite 文件可能不会立刻收缩。这不影响使用。需要彻底释放数据库文件空间时，可以后续加入维护命令或手动执行 SQLite `VACUUM`。

#### 扫描后没有内容

请检查：

- 媒体根目录是否存在。
- 文件扩展名是否在支持列表中。
- 当前登录用户是否使用了正确的数据库。
- 是否误删或排除了相关标签。

#### 日志在哪里

日志位于：

```text
%APPDATA%\FamilyTheater\logs
```

如果扫描、播放或清库失败，优先查看当天日志文件。

## English

### Overview

FamilyTheater is built with WPF and .NET 8. It stores metadata in a local SQLite database while keeping your actual media files in their original folders. The app maintains indexes, tags, covers, playback entries, and user settings.

It is designed for users who:

- Keep movies, images, comic PDFs, or games across local drives.
- Want fast filtering based on folder-derived tags.
- Prefer one desktop interface for browsing, previewing, playing, and launching media.
- Want media files to remain in their original locations instead of being imported into a closed library.

### Features

- Movie library: scans local video files, matches or generates posters, supports playback, renaming, tag management, record deletion, and temporary SRT subtitle preview.
- Picture library: scans local images, supports large-image preview, zoom, pan, animated GIF display, tag management, and record deletion.
- Manga library: scans PDF files, supports PDF reading, automatic cover generation, tag management, and record deletion.
- Game library: scans game folders, supports cover matching, executable selection, screenshot folders, tag management, and game launching.
- Tag filtering: quickly filter content by tags. Movies, pictures, manga, and games use independent tag categories.
- Users and permissions: supports registration, login, `admin` / `user` roles, media scanning, library clearing, and user permission management.
- System settings: includes global options such as precise player time display.
- Local storage: databases, logs, and user settings are stored under the current Windows user profile.

### Supported Formats

| Type | Formats |
| --- | --- |
| Movies | `.mp4`, `.mkv`, `.avi`, `.wmv`, `.flv`, `.mov`, `.rmvb`, `.ts`, `.m4v`, `.webm` |
| Subtitles | `.srt` |
| Images | `.jpg`, `.jpeg`, `.png`, `.webp`, `.bmp`, `.gif` |
| Manga | `.pdf` |
| Covers/Posters | `.jpg`, `.jpeg`, `.png`, `.webp`, `.bmp` |
| Game screenshots | `.jpg`, `.jpeg`, `.png`, `.webp`, `.bmp`, `.gif` |

Video playback depends on Windows media capabilities. If a codec such as HEVC/H.265 cannot be played, install the corresponding system codec or use a more common video encoding.

### Installation

Download the installer from GitHub Releases:

```text
FamilyTheater-Setup-x.y.z.exe
```

Run the installer and follow the prompts. After installation, launch `FamilyTheater`.

To build from source, see “Development and Build” below.

### First Run

1. Launch the app.
2. Register an account.
3. The first user automatically becomes `admin`.
4. Log in to enter the home screen.
5. Open Settings and configure media directories.
6. Save the configuration. The app will start scanning your media libraries.

### Media Directory Settings

The Settings window supports these paths:

| Setting | Description |
| --- | --- |
| Picture root path | Entry point for the picture library scan. |
| Movie root path | Entry point for the movie library scan. |
| Movie poster root path | Used to match movie posters. If no poster is found, the app generates a `.jpg` poster from the video. |
| Manga root path | Entry point for the manga library scan. PDF files are imported into the manga index. |
| Manga cover root path | Used to match or generate PDF covers. |
| Game root path | Entry point for the game library scan. Folders containing executables are recognized as games. |
| Game poster root path | Used to match game covers. |

Avoid putting generated poster directories on a nearly full system drive. Movie posters grow with the number of movies, so a media drive is recommended.

### Folders and Tags

During scanning, FamilyTheater automatically creates tags based on folder paths.

For example:

```text
D:\Pictures\Travel\Japan\001.jpg
```

creates:

```text
Travel
Japan
```

Movies, pictures, manga, and games use independent tag categories. The same tag name can exist in different media types without affecting the others.

### Daily Use

The home screen provides four categories: Movies, Pictures, Manga, and Games. Inside each category you can:

- View indexed content.
- Filter by tags.
- Open detail pages.
- Play, preview, read, or launch items.

### Movies

The movie detail page supports:

- Playing videos.
- Editing titles. Title changes try to rename the local video file as well.
- Selecting or replacing posters.
- Adding, removing, and deleting tags.
- Deleting records, optionally with the local file.

The player supports:

- Click the video area to play or pause.
- Press Space to play or pause.
- Press `Esc` to close the player.
- Optional precise time display, such as `00:01:22,500`.
- Manual `.srt` subtitle loading for temporary subtitle preview.

Subtitle loading is currently temporary. The selected SRT file is read into the current player window and rendered as an overlay at the bottom of the video. The subtitle path is not saved after closing the player. If you edit the subtitle file, click the subtitle button again to reload it.

### Pictures

Pictures support detail pages and large-image preview.

Large-image preview supports:

- Automatic fit to window.
- Mouse-wheel zoom.
- Left-button drag to pan.
- Double-click to fit to window.
- `+` / `-` zoom, `0` fit to window, `Esc` close.

GIF files are displayed as animations.

### Manga

The manga library currently focuses on PDF files. The manga detail page supports:

- Opening the PDF reader.
- Editing titles. Title changes try to rename the local PDF file as well.
- Selecting or replacing covers.
- Adding, removing, and deleting tags.
- Deleting records, optionally with the local file.

The PDF reader supports page navigation, zoom, fit-to-page, auto page turning, and hiding the control bar.

### Games

The game library scans folders under the game root path and recognizes folders containing executable files as games. The game detail page supports:

- Selecting the launch executable.
- Launching the game.
- Setting the game cover.
- Setting the screenshot folder.
- Browsing screenshots.
- Adding, removing, and deleting tags.

### Rescan and Clear Libraries

The Settings window provides incremental scan, full rescan, and clear-library operations.

- Incremental scan: skips existing records and imports new content only.
- Full rescan: rereads existing content and updates metadata.
- Clear a single library: deletes database records, tag relationships, and manually saved tags for that category.
- Clear all libraries: deletes all media library records and tag records.

Clearing a library does not delete media files on disk and does not remove generated poster files. To free poster storage, manually clean the corresponding poster directory.

### Users and Permissions

FamilyTheater supports two roles:

| Role | Capabilities |
| --- | --- |
| `admin` | Configure paths, scan media libraries, clear libraries, and manage user permissions. |
| `user` | Log in and browse media libraries. |

If users already exist but no `admin` is present, the app automatically promotes the first user to `admin`.

### Data Location

Application data is stored under the current Windows user’s Roaming AppData:

```text
%APPDATA%\FamilyTheater
```

Common files:

| File/Directory | Description |
| --- | --- |
| `FamilyTheater.db` | Admin database. |
| `FamilyTheater.User.db` | Normal user database. |
| `logs\yyyy-MM-dd.log` | Daily log files. |

The PDF reader creates page caches under the system temporary directory and cleans them when the reader window closes normally:

```text
%TEMP%\FamilyTheater\PdfViewer
```

### Development and Build

Requirements:

- Windows 10 or later.
- .NET SDK 8.0.
- Windows SDK `10.0.19041.0` or a compatible version.
- Inno Setup 6 for installer packaging.

Repository layout:

```text
FamilyTheater.sln
LoginWindow/                 WPF desktop application
FamilyTheater.Core/          Data models, database, scanning, and media services
installer/FamilyTheater.iss  Inno Setup installer script
scripts/build-installer.ps1  Publish and packaging script
```

Restore and build:

```powershell
dotnet restore
dotnet build
```

Run for debugging:

```powershell
dotnet run --project .\LoginWindow\FamilyTheater.App.csproj
```

### Build Installer

Build a Release installer with:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1
```

Specify a version:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1 -Version 0.1.7
```

Installer output:

```text
artifacts\installer
```

The `artifacts` directory is not committed to Git and can be regenerated at any time.

### FAQ

#### Video does not play

FamilyTheater uses Windows desktop media capabilities for playback. If the system lacks a decoder for a specific codec, the video may not play. Try installing the HEVC extension or using a more common video encoding.

#### The database file does not shrink after clearing a library

Clearing a library deletes database records, but SQLite files may not shrink immediately. This does not affect normal use. To fully reclaim database file space, a future maintenance command or manual SQLite `VACUUM` can be used.

#### No content appears after scanning

Check that:

- The media root path exists.
- File extensions are included in the supported formats.
- The current user is using the expected database.
- Related tags were not accidentally deleted or excluded.

#### Where are logs stored?

Logs are stored in:

```text
%APPDATA%\FamilyTheater\logs
```

If scanning, playback, or library clearing fails, check the log file for the current date first.
