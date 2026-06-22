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
- kann im Update-Tab zwischen Stable- und Beta-Kanal wählen
- wiederholt die Updateprüfung im Hintergrund und markiert verfügbare Updates in der Übersicht mit Priorität `message`
- zeigt Datei-, Verbindungs-, Heartbeat- und Updatefehler in einem eigenen Protokoll-Tab
- zeigt unter der TCP-LED den Modemstatus: per Socket anhand der direkten Heartbeat-Verbindung, per MQTT nach einer Rückmeldung mit passender IMEI
- meldet nach 60 Sekunden ohne Modemstatus-Heartbeat einen Verbindungsverlust
- zeigt Modem-Firmware, Recovery, WWW-Version, Stable/Beta-Kanal und OTA-Status
- bietet in der Übersicht einen dringenden Testalarm mit Priorität `urgent`, der gespeichert und über MQTT sowie TCP versendet wird
- zeigt Kuhnummer und installierte Programmversion direkt in der Oberfläche
- kann einen gekennzeichneten Testfehler über die aktivierten Versandwege senden
- enthält einen Info-Dialog mit Firmenkontakt, Tätigkeitsbereich und Programmversion
- speichert eine konfigurierbare Anzahl Alarme und Fehler mit Bestätigungsstatus, Filtern und sortierbaren Spalten
- ergänzt fehlende Alarmtexte aus `Mione_AlarmCodes_UK_DE.xlsx` und zeigt Ursache/Lösung als Hinweis
- speichert das MQTT-Passwort mit Windows DPAPI verschlüsselt
- kann DPProcessControl/Melkroboter-Daten aus `D:\DairyPln` prüfen und nach `<Benutzername>/Melkroboter` per MQTT veröffentlichen
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

TCP sendet je JSON-Objekt eine UTF-8-Zeile. Bei Alarmen bleibt die Verbindung bis zur Antwort des Modems offen. Bei MQTT bildet der Benutzername das Top-Topic. Alarme werden nach `<Benutzername>/Alarmfunktionen/Alarm`, Mobilnummern samt Aktivzustand, `AlarmsTo` und dem Zeitfenster für technische Alarme nach `<Benutzername>/Alarmfunktionen/Config/Mobile` und Heartbeats nach `<Benutzername>/Alarmfunktionen/Heartbeat` gesendet. Die Mobilkonfiguration wird auch per TCP übertragen. Die Sekundenwerte `technicalAlarmMessagingFrom` und `technicalAlarmMessagingUntil` werden zusätzlich als lesbare `...Text`-Uhrzeiten ausgegeben. Alarm, Mobilkonfiguration und Heartbeat enthalten außerdem das Feld `modemImei`, damit nur das konfigurierte Modem reagiert. Der eingebaute MQTT-Client unterstützt bewusst unverschlüsseltes MQTT auf typischerweise Port 1883; TLS kann wegen der sehr alten TLS-Unterstützung von Windows XP nicht zuverlässig für XP bis Windows 11 gemeinsam angeboten werden.

Die Option **Alarmfortschritt 30 Sekunden anzeigen** im Tab **Versand** aktiviert
das kleine Statusfenster. Über MQTT abonniert MiOne dafür
`<Benutzername>/Alarmfunktionen/AlarmStatus`; über TCP liest es die Antwortzeilen derselben
Alarmverbindung. Meldungen mit einer anderen `modemImei` werden verworfen. Das
Fenster zeigt Alarmcode und -text sowie Rufnummer, SMS/Anruf, Status und
Transportweg und schließt 30 Sekunden nach der letzten Meldung automatisch.

Das Modem sendet alle fünf Sekunden einen eigenen Status-Heartbeat nach
`<Benutzername>/Alarmfunktionen/ModemStatus`. MiOne prüft dessen IMEI und Zeitabstand.
Über TCP fordert MiOne denselben Datensatz mit `{"type":"statusRequest"}` an.
Der Status enthält außerdem die installierten Versionen von Hauptprogramm,
Recovery und WWW sowie Kanal, Updateverfügbarkeit und Fortschritt.

Im Tab **Melkroboter** kann die DPProcessControl-Brücke aktiviert werden. Der
Standardpfad ist `D:\DairyPln`; die Pfadprüfung kontrolliert unter anderem
`DPProcessControl.exe`, `RDM_DP_Com.dll`, `RDM_DP_Com_CORBA.dll`,
`RDM_DP_Com_Server.dll`, `DP_RDM_Link.dll`, `RDM_JNI_DB.dll` und
`RDM\CORBA\DP_RDM_COM.ior`. Bei aktivierter MQTT-Verbindung sendet MiOne einen
retained JSON-Snapshot nach `<Benutzername>/Melkroboter` und den aus
`rdm-manager.jar`/`RDM_DP_Com` abgeleiteten Funktionskatalog nach
`<Benutzername>/Melkroboter/Funktionen`. Enthalten sind unter anderem
Roboterinitialisierung, Systeminitialisierung, Automatikbetrieb,
System-/Kurzreinigung, Melkvorgang abbrechen, Roboterposition, Dosierer-
Kalibrierung und Probenahme.
Der Funktionskatalog enthält je Funktion die benötigten Parameter und ein
`payloadExample`. Befehle können an `<Benutzername>/Melkroboter/Command` oder
`<Benutzername>/Melkroboter/Befehl` gesendet werden, zum Beispiel
`{"requestId":"1","command":"stopMilking","boxNumber":1}`. Die Antwort erscheint
auf `<Benutzername>/Melkroboter/Result`; fehlende Parameter werden dort als
`invalidParameters` gemeldet.
Im Tab **Funktionslog** zeigt MiOne jeden empfangenen Melkroboter-Befehl mit
Topic, Funktion, Parametern, Validierungsstatus und ob das Result wieder per
MQTT veröffentlicht wurde.
Gültige Befehle werden über `Assets\MioneDairyPlanBridge.jar` und
`rdm-manager.jar` an die CORBA-Schnittstelle von DPProcessControl übergeben.
Auf dem DairyPlan-Rechner muss dafür Java 6 bis Java 8 verfügbar sein, da neuere
Java-Versionen die benötigten CORBA-Klassen nicht mehr enthalten. Der
Alarmmelder startet `rdm-manager.jar` und `DPProcessControl.exe` nicht selbst,
sondern wartet bis zu 60 Sekunden darauf, dass `DPProcessControl` bereits vom
System gestartet wurde. Die Bridge erwartet `RDM\CORBA\DP_RDM_COM.ior` im
konfigurierten DairyPln-Pfad.

Der versendete `alarmText` wird für Modemkompatibilität ohne deutsche Umlaute ausgegeben (`ä` = `ae`, `ö` = `oe`, `ü` = `ue`, `ß` = `ss`). Die Anzeige in der Anwendung bleibt unverändert.

### MQTT-Testprogramm auf macOS

Das Skript `tools/mqtt_test.py` kann ohne externe Python-Pakete MQTT 3.1.1
publishen und abonnieren. Beispiele:

```bash
python3 tools/mqtt_test.py --host 192.168.1.10 --user mqtt_benutzer functions
python3 tools/mqtt_test.py --host 192.168.1.10 --user mqtt_benutzer command stopMilking --box 1
python3 tools/mqtt_test.py --host 192.168.1.10 --user mqtt_benutzer listen Melkroboter/Result
python3 tools/mqtt_test.py --host 192.168.1.10 --user mqtt_benutzer alarm --modem-imei 123456789012345
```

Für die grafische Überwachung auf macOS gibt es zusätzlich eine Tkinter-GUI:

```bash
python3 tools/mqtt_gui.py
```

Falls `import tkinter` auf macOS fehlschlägt, kann Tkinter mit
`brew install python-tk@3.14` nachinstalliert werden. Die GUI abonniert nach dem
Verbinden automatisch `<Benutzername>/Melkroboter/#` und
`<Benutzername>/Alarmfunktionen/#`, zeigt die Boxen parallel als Tabelle, listet
empfangene Topics und kann die im Funktionskatalog gefundenen
Melkroboter-Befehle nach `<Benutzername>/Melkroboter/Command` publishen. Server,
Port und Benutzer werden lokal gespeichert; das Passwort wird nur für die
aktuelle Sitzung verwendet.

## GitHub-Updates

Im Tab **Updates** das öffentliche Repository als `Besitzer/Repository` eintragen und den Kanal wählen. **Stable** prüft das neueste öffentliche GitHub-Release. **Beta** prüft den Release-Tag `beta` und liest die Version aus dem ZIP-Asset, zum Beispiel `MioneAlarmmelder-1.1.9.29_Beta.zip`. Beta-Builds werden in der Oberfläche mit `_Beta` angezeigt. Nach Bestätigung lädt die Anwendung dieses Asset, prüft einen vorhandenen GitHub-SHA-256-Digest, ersetzt die EXE und startet neu. GitHub erfordert TLS 1.2; deshalb funktioniert die direkte Updateprüfung auf Windows XP abhängig von dessen TLS-Konfiguration möglicherweise nicht.
