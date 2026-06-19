# MarkFlow

[한국어](README.md) | [English](README.en.md)

**A simple Markdown editor for Windows**

MarkFlow is a Markdown viewer and editor built with .NET 9 and WPF for Windows 10 and 11. It uses Markdig and WPF `FlowDocument` instead of a WebView-centered renderer, keeping the application lightweight while providing a polished, native Windows experience.

## Features

- Open Markdown files, revisit recent files, or drop files anywhere on the window
- Render headings, paragraphs, code blocks, inline code, tables, quotes, task lists, images, links, and horizontal rules
- Collapsible Library and Outline sidebar
- Full compact viewing mode that hides the sidebar and document header and resizes down to `360×240` (`Ctrl+Shift+F`)
- Markdown editor with an unsaved live preview
- `50%–200%` document zoom with browser-style shortcuts (`Ctrl++`, `Ctrl+-`, `Ctrl+0`, `Ctrl+wheel`)
- Raw Markdown and split editor/preview modes
- Clear indicators for unsaved changes
- Light, Dark, and System themes
- Korean, English, and Japanese application UI with a saved language preference
- Typography presets and cached Korean Google Fonts
- Windows legacy acrylic blur backdrop
- Custom title bar, rounded window corners, and caption controls
- Always-on-top pinning available in regular and compact modes
- Debounced file watching and document rendering

## Technology

- .NET 9 / WPF
- C# with nullable reference types enabled
- MVVM-oriented architecture
- Markdig `0.38.0`
- WPF `FlowDocument`
- Windows DWM / `SetWindowCompositionAttribute`
- `FileSystemWatcher` and `DispatcherTimer` debounce

## Project Structure

```text
MDViewer/
├─ Assets/                  # MarkFlow SVG source and generated PNG/ICO assets
├─ Models/                  # Models and enums shared by views and services
├─ Resources/               # Shared styles and Light/Dark theme resources
├─ Services/                # Rendering, theme, DWM, fonts, recent files, file watching
├─ Tools/MarkFlow.IconBuilder/ # Build-time SVG icon converter
├─ Utilities/               # MVVM commands and ObservableObject
├─ ViewModels/              # MainViewModel
├─ App.xaml                 # Application resource dictionaries
├─ MainWindow.xaml          # Main application UI
├─ SettingsWindow.xaml      # Settings window
├─ MDViewer.csproj          # WPF project
└─ MDViewer.sln             # Visual Studio solution
```

## Architecture

### MainWindow

The main application shell contains the custom title bar, Library and Outline sidebar, document viewer, editor tools, and file-drop overlay.

It is responsible for:

- Window-wide drag and drop
- Outline navigation
- Mouse-wheel scroll tuning
- Theme transition fades
- Markdown toolbar actions
- Minimize, maximize, and close controls

### MainViewModel

The central ViewModel manages the active document and UI state, including:

- Current file path and title
- Rendered `FlowDocument`
- Editable Markdown source
- Saved and unsaved state
- Recent files and outline entries
- Sidebar and editor modes
- Theme, typography, and document font settings

Primary commands include `OpenFileCommand`, `ReloadCommand`, `SaveCommand`, `ToggleEditCommand`, `ToggleSidebarCommand`, and `ToggleThemeCommand`.

### MarkdownRendererService

Parses Markdown with Markdig and converts it into a native WPF `FlowDocument`. File render results are cached using file metadata and the active rendering options.

Supported mappings include:

- Headings and outline entries
- Paragraphs, inline text, links, and images
- Quotes and code blocks
- Lists and task lists
- Pipe tables
- Horizontal rules

### ThemeService

Applies Light, Dark, or System themes and provides typography values. Themes are switched by replacing the merged `Resources/Theme.Light.xaml` and `Resources/Theme.Dark.xaml` resource dictionaries.

### LanguageService

Switches the Korean, English, and Japanese string resource dictionaries at runtime and applies the matching culture. The selected language is stored in `%APPDATA%\MarkFlow\settings.json` and restored on the next launch.

### AppSettingsService

Stores the theme, typography, document font, document zoom, always-on-top preference, editor mode, file watching preference, and language in `%APPDATA%\MarkFlow\settings.json`. Settings are written through a temporary file and restored on the next launch.

### DwmBackdropService

Manages the window backdrop and caption appearance. MarkFlow currently prioritizes the Windows 10 legacy acrylic blur implementation instead of Mica.

It handles:

- Transparent client-area preparation
- Acrylic blur through `SetWindowCompositionAttribute`
- Disabling the modern Windows 11 backdrop
- Dark caption attributes
- Rounded corners that are removed while maximized

### FontCacheService

Downloads a selected Korean Google Font when first used, stores it under `%APPDATA%\MDViewer\Fonts`, registers it as a private font resource, and applies it to WPF document rendering.

### FileWatcherService

Watches the active file for external changes. Repeated `FileSystemWatcher` events are consolidated with a dispatcher-based debounce before the document is refreshed.

### TweenService

Runs short UI transitions such as sidebar expansion and collapse without relying on a permanent `CompositionTarget.Rendering` loop.

### Icon Build Pipeline

`Assets/MarkFlowLogo.svg` is the single editable source for the MarkFlow icon. During builds, `Tools/MarkFlow.IconBuilder` renders 16, 20, 24, 32, 40, 48, 64, 128, 256, and 1024px PNG files and a multi-resolution `MarkFlow.ico`.

The PNG and ICO files are build outputs and should not be edited directly. MSBuild runs the `GenerateMarkFlowIcons` target only when the SVG source or converter code changes.

## How It Works

### Opening a File

1. Choose a Markdown file through the open command, recent list, or drag and drop.
2. `MainViewModel.LoadFileAsync` reads the file.
3. `MarkdownRendererService.RenderFileAsync` builds a `FlowDocument`.
4. The viewer, outline, document metadata, and recent-file list are updated.
5. `FileWatcherService` begins monitoring the file.

### Editing and Previewing

1. Switch between viewing and editing from the title bar controls.
2. Markdown mode provides a full-width source editor.
3. Split mode displays Markdown source and a rendered preview side by side.
4. Live preview updates after a 280 ms debounce.
5. Returning to the viewer previews the current text without requiring a save.
6. Unsaved content is identified by a status label and viewer overlay.

### Saving

1. `SaveCommand` writes the current Markdown text to the active file.
2. File watching is paused briefly during the write.
3. The file is reloaded to refresh the cache, metadata, and outline.

### Themes and Fonts

1. Select a theme or font from the application settings.
2. `ThemeService` replaces the active theme resources.
3. The open document is rendered again with the current state.
4. `FontCacheService` downloads and registers fonts that are not already cached.

## Development

Requirements:

- Windows 10 or later
- Visual Studio 2022 with the .NET Desktop Development workload, or .NET SDK 9+

Visual Studio:

1. Open `MDViewer.sln`.
2. Confirm that `MDViewer` is the startup project.
3. Build with the `Debug` or `Release` configuration.
4. The generated executable is named `MarkFlow.exe`.

Command-line build:

```powershell
dotnet restore
dotnet build -c Debug
```

Release build:

```powershell
dotnet build -c Release
```

## Publishing

Framework-dependent Windows x64 build:

```powershell
dotnet publish .\MDViewer.csproj -c Release -r win-x64 --self-contained false -o .\publish\win-x64
```

Single-file, self-contained Windows x64 build:

```powershell
dotnet publish .\MDViewer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\publish\win-x64-single
```

Before publishing a release, verify:

- `MarkFlow.exe` starts correctly
- Application and taskbar icons render correctly
- PNG and ICO assets regenerate after changing the SVG source
- Acrylic blur is active
- File open, drop, and recent-file workflows work
- Unsaved preview and save workflows work
- Document metadata and outline refresh after saving
- Light, Dark, and System themes switch correctly
- Korean fonts download, cache, and render correctly

## Release and Update Model

MarkFlow does not currently include an automatic updater. GitHub Releases is the recommended distribution path:

1. Merge completed work into `main`.
2. Verify the project with `dotnet build -c Release`.
3. Produce distributable files with `dotnet publish`.
4. Test the output and package it as a ZIP archive.
5. Publish the archive with a version tag and release notes on GitHub Releases.

A future update implementation can add:

- `UpdateService` for querying the GitHub Releases API
- A `VersionInfo` model for version, download URL, and release notes
- Current-version and update controls in `SettingsWindow`
- Optional update checks at startup
- A separate updater process for replacing installed files

Keeping release discovery separate from installation will allow updates to evolve without tightly coupling them to the main application.

## Performance Notes

- Render results are cached using file metadata and rendering options.
- External file changes and editor previews are debounced.
- Animations use short tweens or storyboards.
- The application avoids continuous polling and persistent rendering callbacks.
- Very large documents may eventually benefit from section-level virtualization or incremental rendering because rebuilding a complete `FlowDocument` remains relatively expensive.

## Current Limitations

- Mermaid rendering is planned but not implemented.
- The split editor provides immediate visual feedback, but it is not a full WYSIWYG editor.
- Automatic updates are not implemented yet.
- Very large documents may require further rendering-range optimization.

## License

MarkFlow is released under the [PolyForm Noncommercial 1.0.0](LICENSE). You may inspect, modify, study, and redistribute the source for personal and noncommercial purposes. Commercial use, including sale or paid redistribution, is not permitted.

This is a **source-available license**, not an OSI-approved open-source license. Contact the copyright holder separately for commercial licensing.
