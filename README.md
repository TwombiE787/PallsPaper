# PallsPaper

PallsPaper ist eine Windows-App für animierte Desktop-Hintergründe. Das kleine Steuerfenster ist in C# WinForms geschrieben; Microsoft Edge WebView2 rendert Videos, Webseiten und HTML-Wallpaper.

## Features

- MP4-, WebM- und OGV-Videos abspielen.
- Animierte GIFs sowie Bilder einschließlich SVG anzeigen.
- Lokale HTML-Wallpaper samt Dateien im selben Ordner laden.
- HTTP- oder HTTPS-Webseiten als Wallpaper verwenden.
- Alle angeschlossenen Monitore abdecken und die Wiedergabe im Steuerfenster stoppen.
- Im Infobereich weiterlaufen, wenn das Steuerfenster geschlossen wird.
- Letztes Wallpaper merken und optional mit Windows starten.
- Wallpaper per Drag-and-drop auswählen.
- Pro Monitor ein eigenes Wiedergabefenster, damit Wallpaper bei unterschiedlichen Monitorgrößen korrekt skaliert werden.
- JPG/JPEG-Fotos und animierte GIFs werden über WebView2 geladen; ein fehlgeschlagenes Laden wird angezeigt.
- Bilder, GIFs und Videos werden links oben beginnend exakt auf Bildschirmbreite und -höhe skaliert.
- Dunkles, schlichtes Dashboard mit transparent gezeichneten Symbolen und nativer Windows-Titelleiste.

## Requirements

- Windows 10 oder neuer.
- Für `dotnet run`: .NET 8 Desktop Runtime und Microsoft Edge WebView2 Runtime.
- Der erstellte Installer enthält die .NET-Laufzeit und richtet WebView2 bei Bedarf ein.

## Build and run

```powershell
dotnet run
```

Wähle eine unterstützte Datei oder gib eine HTTP-/HTTPS-Adresse ein und klicke auf **Starten**. Zum Beenden des Wallpapers dient **Stoppen**. Lokale HTML-Wallpaper können auf Dateien im selben Ordner zugreifen. Das Schließen über die native Windows-Titelleiste blendet PallsPaper in den Infobereich aus, damit das Wallpaper aktiv bleibt. Zum vollständigen Beenden nutze **Beenden** im Infobereich-Menü. Mit **Mit Windows starten** wird PallsPaper beim Anmelden minimiert gestartet und das zuletzt genutzte Wallpaper wiederhergestellt.

## Installer erstellen

Installiere Inno Setup 6 und führe im Projektordner Folgendes aus:

```powershell
.\installer\build-installer.ps1
```

Der Installer wird unter `artifacts\installer\PallsPaper-Setup-1.0.1-x64.exe` erstellt. Er enthält die .NET-Laufzeit und richtet bei Bedarf die Microsoft Edge WebView2 Runtime ein. Das PallsPaper-Logo ist in der Programmdatei eingebettet und wird von Windows für Taskleiste, Task-Manager und Verknüpfungen verwendet.
