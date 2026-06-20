# Mione Alarmmelder

Klassische Windows-Forms-Anwendung für **.NET Framework 3.5**. Die Lösung kann in Visual Studio geöffnet und angepasst werden.

## Funktionen

- überwacht die letzte Zeile der DairyPlan-Alarmdatei
- lädt Rufnummern/Aktivschalter, Alarmprioritäten und deutsche Alarmtexte dynamisch neu
- erzeugt pro aktiver Rufnummer eine JSON-Nachricht
- versendet per MQTT 3.1.1 (QoS 0) und/oder TCP-Socket
- zeigt auf Wunsch etwa 30 Sekunden den Fortschritt je Alarm, Rufnummer, SMS und Anruf
- unterstützt für MQTT und TCP sowohl IPv4 als auch IPv6
- sendet für MQTT und TCP alle fünf Sekunden einen JSON-Heartbeat mit wechselndem `true`/`false`-Wert
- prüft ein konfigurierbares öffentliches GitHub-Repository automatisch auf neue Releases
- wiederholt die Updateprüfung im Hintergrund und markiert verfügbare Updates in der Übersicht mit Priorität `message`
- zeigt Datei-, Verbindungs-, Heartbeat- und Updatefehler in einem eigenen Protokoll-Tab
- zeigt unter der TCP-LED den Modemstatus: per Socket anhand der direkten Heartbeat-Verbindung, per MQTT nach einer Rückmeldung mit passender IMEI
- meldet nach 15 Sekunden ohne Modemstatus-Heartbeat einen Verbindungsverlust
- zeigt Modem-Firmware, Recovery, WWW-Version, Stable/Beta-Kanal und OTA-Status
- bietet in der Übersicht einen dringenden Testalarm mit Priorität `urgent`, der gespeichert und über MQTT sowie TCP versendet wird
- zeigt Kuhnummer und installierte Programmversion direkt in der Oberfläche
- kann einen gekennzeichneten Testfehler über die aktivierten Versandwege senden
- enthält einen Info-Dialog mit Firmenkontakt, Tätigkeitsbereich und Programmversion
- speichert eine konfigurierbare Anzahl Alarme und Fehler mit Bestätigungsstatus, Filtern und sortierbaren Spalten
- ergänzt fehlende Alarmtexte aus `Mione_AlarmCodes_UK_DE.xlsx` und zeigt Ursache/Lösung als Hinweis
- speichert das MQTT-Passwort mit Windows DPAPI verschlüsselt
- läuft nach dem Schließen im Infobereich weiter; LED-Farbe zeigt den Zustand
- zeigt beim Start das Banner und auf jedem Dialog das Logo

Die LED im Infobereich zeigt **gelb** beim Start/Warten, **grün** bei aktiver Überwachung bzw. nach erfolgreichem Versand, **blau** während des Versands und **rot** beim letzten Datei- oder Verbindungsfehler.

Standardpfade:

- `D:\DairyPln\MessageLog_1.adf`
- `D:\DairyPln\RDM\configuration\preferences\user\alarmssettings.properties`
- `D:\DairyPln\RDM\configuration\data\rdm\useralarmpriorities.properties`
- `D:\Release\Assets\translations_de.properties`
- `D:\Release\Assets\Mione_AlarmCodes_UK_DE.xlsx`

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
{"datum":"20.06.26","uhrzeit":"13:10:59","alarmCode":"131","ort":"Melkbox 1","kuh":"0","prioritaet":"urgent","alarmText":"..."}
```

TCP sendet je JSON-Objekt eine UTF-8-Zeile. Bei Alarmen bleibt die Verbindung bis zur Antwort des Modems offen. Bei MQTT bildet der Benutzername das Top-Topic. Alarme werden nach `<Benutzername>/MiOne/Alarm`, Mobilnummern samt Aktivzustand, `AlarmsTo` und dem Zeitfenster für technische Alarme nach `<Benutzername>/MiOne/Config/Mobile` und Heartbeats nach `<Benutzername>/MiOne/Heartbeat` gesendet. Die Mobilkonfiguration wird auch per TCP übertragen. Die Sekundenwerte `technicalAlarmMessagingFrom` und `technicalAlarmMessagingUntil` werden zusätzlich als lesbare `...Text`-Uhrzeiten ausgegeben. Alarm, Mobilkonfiguration und Heartbeat enthalten außerdem das Feld `modemImei`, damit nur das konfigurierte Modem reagiert. Der eingebaute MQTT-Client unterstützt bewusst unverschlüsseltes MQTT auf typischerweise Port 1883; TLS kann wegen der sehr alten TLS-Unterstützung von Windows XP nicht zuverlässig für XP bis Windows 11 gemeinsam angeboten werden.

Die Option **Alarmfortschritt 30 Sekunden anzeigen** im Tab **Versand** aktiviert
das kleine Statusfenster. Über MQTT abonniert MiOne dafür
`<Benutzername>/MiOne/AlarmStatus`; über TCP liest es die Antwortzeilen derselben
Alarmverbindung. Meldungen mit einer anderen `modemImei` werden verworfen. Das
Fenster zeigt Alarmcode und -text sowie Rufnummer, SMS/Anruf, Status und
Transportweg und schließt 30 Sekunden nach der letzten Meldung automatisch.

Das Modem sendet alle fünf Sekunden einen eigenen Status-Heartbeat nach
`<Benutzername>/MiOne/ModemStatus`. MiOne prüft dessen IMEI und Zeitabstand.
Über TCP fordert MiOne denselben Datensatz mit `{"type":"statusRequest"}` an.
Der Status enthält außerdem die installierten Versionen von Hauptprogramm,
Recovery und WWW sowie Kanal, Updateverfügbarkeit und Fortschritt.

Der versendete `alarmText` wird für Modemkompatibilität ohne deutsche Umlaute ausgegeben (`ä` = `ae`, `ö` = `oe`, `ü` = `ue`, `ß` = `ss`). Die Anzeige in der Anwendung bleibt unverändert.

## GitHub-Updates

Im Tab **Updates** das öffentliche Repository als `Besitzer/Repository` eintragen. Ein Release-Tag muss eine höhere Version als die installierte Anwendung enthalten, zum Beispiel `v1.2.0`. Das Release benötigt standardmäßig ein Asset namens `MioneAlarmmelder.exe`. Nach Bestätigung lädt die Anwendung dieses Asset, prüft einen vorhandenen GitHub-SHA-256-Digest, ersetzt die EXE und startet neu. GitHub erfordert TLS 1.2; deshalb funktioniert die direkte Updateprüfung auf Windows XP abhängig von dessen TLS-Konfiguration möglicherweise nicht.
