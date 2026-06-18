# Mione Alarmmelder

Klassische Windows-Forms-Anwendung für **.NET Framework 3.5**. Die Lösung kann in Visual Studio geöffnet und angepasst werden.

## Funktionen

- überwacht die letzte Zeile der DairyPlan-Alarmdatei
- lädt Rufnummern/Aktivschalter, Alarmprioritäten und deutsche Alarmtexte dynamisch neu
- erzeugt pro aktiver Rufnummer eine JSON-Nachricht
- versendet per MQTT 3.1.1 (QoS 0) und/oder TCP-Socket
- sendet für MQTT und TCP getrennte JSON-Heartbeats (Standard: alle 60 Sekunden)
- prüft ein konfigurierbares öffentliches GitHub-Repository automatisch auf neue Releases
- speichert das MQTT-Passwort mit Windows DPAPI verschlüsselt
- läuft nach dem Schließen im Infobereich weiter; LED-Farbe zeigt den Zustand
- zeigt beim Start das Banner und auf jedem Dialog das Logo

Die LED im Infobereich zeigt **gelb** beim Start/Warten, **grün** bei aktiver Überwachung bzw. nach erfolgreichem Versand, **blau** während des Versands und **rot** beim letzten Datei- oder Verbindungsfehler.

Standardpfade:

- `D:\DairyPln\MessageLog_1.adf`
- `D:\DairyPln\RDM\configuration\preferences\user\alarmssettings.properties`
- `D:\DairyPln\RDM\configuration\data\rdm\useralarmpriorities.properties`
- `D:\Release\Assets\translations_de.properties`

## Start

1. `MioneAlarmmelder.sln` in Visual Studio auf Windows öffnen.
2. Sicherstellen, dass das Zielpaket für .NET Framework 3.5 installiert/aktiviert ist.
3. Projekt in `Release | Any CPU` erstellen.
4. Den gesamten Inhalt von `Alarmmelder\bin\Release` auf den Zielrechner kopieren.

Alternativ kann `build-release.cmd` in einer Visual-Studio-Entwicklerkonsole ausgeführt werden.

### Kompilieren unter macOS mit VS Code

Der native Microsoft-.NET-SDK-Befehl `dotnet build` kann klassische .NET-Framework-3.5-WinForms-Projekte unter macOS nicht bauen. Als Kompatibilitätsweg kann Mono verwendet werden:

```bash
brew install mono
cd "/Users/toto/Documents/Mione Alarmmelder"
chmod +x build-release-mac.sh
./build-release-mac.sh
```

In VS Code kann anschließend mit **Cmd+Shift+B** die Aufgabe `Mione Alarmmelder: Release kompilieren` gestartet werden. Das Ergebnis liegt unter `Alarmmelder/bin/Release`. Der abschließende Test der EXE, des Tray-Icons, der DPAPI-Verschlüsselung und des Windows-Autostarts muss auf Windows erfolgen.

Für korrekte IntelliSense- und Fehleranzeigen verwendet der Arbeitsbereich OmniSharp mit Mono. Dazu die Erweiterung **C# Dev Kit** für diesen Arbeitsbereich deaktivieren, die Microsoft-Erweiterung **C#** aktiviert lassen und anschließend `Developer: Reload Window` ausführen.

Windows XP benötigt .NET Framework 3.5 SP1. Unter Windows 10/11 muss das optionale Windows-Feature **.NET Framework 3.5 (enthält .NET 2.0 und 3.0)** aktiviert sein.

## JSON-Format

```json
{"kunde":"K-123","rufnummer":"+4912345","datum":"20.06.26","uhrzeit":"13:10:59","alarmCode":"131","ort":"Melkbox 1","kuhnummer":"0","prioritaet":"urgent","alarmText":"..."}
```

TCP sendet je JSON-Objekt eine UTF-8-Zeile. MQTT ersetzt im Topic den Platzhalter `{kunde}`. Der eingebaute MQTT-Client unterstützt bewusst unverschlüsseltes MQTT auf typischerweise Port 1883; TLS kann wegen der sehr alten TLS-Unterstützung von Windows XP nicht zuverlässig für XP bis Windows 11 gemeinsam angeboten werden.

## GitHub-Updates

Im Tab **Updates** das öffentliche Repository als `Besitzer/Repository` eintragen. Ein Release-Tag muss eine höhere Version als die installierte Anwendung enthalten, zum Beispiel `v1.2.0`. Das Release benötigt standardmäßig ein Asset namens `MioneAlarmmelder.exe`. Nach Bestätigung lädt die Anwendung dieses Asset, prüft einen vorhandenen GitHub-SHA-256-Digest, ersetzt die EXE und startet neu. GitHub erfordert TLS 1.2; deshalb funktioniert die direkte Updateprüfung auf Windows XP abhängig von dessen TLS-Konfiguration möglicherweise nicht.
