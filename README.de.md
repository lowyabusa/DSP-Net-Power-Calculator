# DSP Net-Energy Calculator

[English](README.md) | [Deutsch](README.de.md) | [简体中文](README.zh-CN.md)

> Dieses Projekt wurde per Vibe Coding erstellt.

Ein browserbasierter Rechner für die elektrische Nettoleistung von Brennstoff-Produktionsketten in Dyson Sphere Program, umgesetzt mit .NET 10 Blazor WebAssembly. Die in C# geschriebene Berechnungslogik läuft lokal im Browser und unterstützt alle 24 Brennstoffe, die Wärmekraftwerke, Mini-Fusionskraftwerke und künstliche Sterne im Basisspiel verwenden. Gegenstände, Rezepte, Maschinen, Kraftwerke und Regeln für Proliferatoren sind in getrennten JSON-Datensätzen hinterlegt.

Im Unterschied zu allgemeinen Produktionsrechnern ermittelt DSP Net-Energy Calculator die **elektrische Nettoleistung**, die nach Abzug des Eigenverbrauchs der Produktionskette verbleibt. Berücksichtigt werden die Rohstoffgewinnung, die Herstellung von Proliferatoren und konfigurierte Logistikkosten. Kosten außerhalb der gewählten Bilanzgrenze werden ausdrücklich als nicht berücksichtigt gekennzeichnet.

## Website ohne .NET-Installation verwenden

Die veröffentlichte Website lässt sich direkt im Browser öffnen. Wie die Website aus diesem Repository erstellt und veröffentlicht wird, ist unter [Entwicklung: Website erstellen und veröffentlichen](#entwicklung-website-erstellen-und-veröffentlichen) beschrieben. **Für die Nutzung müssen weder .NET noch das .NET SDK oder eine Desktop-Anwendung installiert werden.** Der Browser lädt die Anwendung und die benötigte WebAssembly-Laufzeit beim ersten Aufruf automatisch. Alle Berechnungen erfolgen lokal im Browser; ein Berechnungsserver ist nicht erforderlich.

Szenario, Sprache und importierte Icons werden im aktuellen Browserprofil für die jeweilige Website-Adresse gespeichert, sofern der Browserspeicher verfügbar ist. Beim Löschen der Website-Daten im Browser gehen auch diese Einstellungen und Icons verloren. Szenarien lassen sich zuvor als JSON-Datei exportieren und sichern.

## Offline nutzen

Die veröffentlichte Website speichert ihre Anwendungsdateien über einen Service Worker zwischen; dafür sind HTTPS oder localhost erforderlich. Sobald die Dateien vollständig zwischengespeichert wurden und der Service Worker aktiv ist, lässt sich der Rechner unter derselben Website-Adresse im selben Browserprofil einschließlich der gespeicherten Laufzeit offline neu laden und nutzen. Der Offline-Cache muss dafür erhalten bleiben; das Löschen der Website-Daten entfernt auch diesen Cache. Auch dafür ist keine separate .NET-Installation erforderlich. Der weiter unten beschriebene Entwicklungsserver stellt diesen Offline-Cache nicht bereit.

## Bedienung

- Bei der ersten Nutzung startet die Anwendung auf Englisch. Die Sprachschaltflächen oben rechts wechseln zwischen Englisch, Deutsch und vereinfachtem Chinesisch. Die Spracheinstellung wird getrennt vom Szenario gespeichert.
- Links einen Brennstoff und eine Proliferator-Stufe wählen. In der Mitte den gewünschten Brennstoffdurchsatz eingeben. Rechts werden Bruttoleistung, Verbrauch, Nettoleistung und Bilanzgrenze aktualisiert.
- **Alle Brennstoffe** öffnet die durchsuchbare Icon-Auswahl, gruppiert nach Kraftwerkstyp. Holz, Pflanzenbrennstoff und Energiesplitter werden als bereitgestellte Gegenstände behandelt. Die Bilanz weist deren Sammel- oder Beschaffungskosten als nicht berücksichtigt aus.
- Ein Klick auf Rezept-, Material- und Maschinen-Icons öffnet die verfügbaren Alternativen. Per Rechtsklick auf einen Produktionsschritt oder über dessen Aktionsschaltfläche lassen sich Maschinenanzahl, Auslastung und Leistung einstellen. Die Sprüh-Icons wählen **Zusatzprodukte** oder **Produktionsbeschleunigung**, sofern unterstützt. Tooltips erklären, warum eine Option nicht verfügbar ist.
- Der Referenzvergleich zwischen Kohle und energetischem Graphit vergleicht beide Wege bei 120 bereitgestellter Kohle/min; Rohstoffgewinnung, Sortierer und Logistik sind dabei ausgeschlossen. Der Proliferator-Vergleich verwendet das aktuelle Szenario. Aufgeklappte Materialflüsse zeigen Durchsatz, Betriebsleistung und Leerlaufleistung je Schritt.
- **JSON importieren** und **JSON exportieren** befinden sich im Kopfbereich des Szenarios. Exporte enthalten die aktuellen gültigen Rezept-, Maschinen-, Sprüh- und Logistikeinstellungen. Szenariodateien verwenden die Schemaversion 1. Bei einem fehlgeschlagenen Import bleibt das aktuelle Szenario erhalten.
- **Alternative Rezeptketten** sortiert umsetzbare Rezeptkombinationen nach ihrer Nettoleistung bei gleichem Brennstoffdurchsatz. Die vorgelagerte Proliferator-Produktion wird einbezogen. Kraftwerkslimit, bereitgestellte Materialien, Fabrikeinstellungen und Logistikparameter bleiben dabei konstant. Die Ansicht kennzeichnet extern bereitgestellte Materialien und zählt nicht umsetzbare Kombinationen. Mit **Übernehmen** wird eine der aufgeführten Produktionsketten angewendet.
- Gültige Szenarioänderungen werden automatisch im Browser gespeichert. Mit `?transient=1` an der Website-URL, beispielsweise `http://localhost:5080/?transient=1`, startet eine Sitzung auf Englisch mit Standardwerten, ohne gespeicherte Szenarien, Spracheinstellungen oder Icons zu laden oder zu speichern.

## Spiel-Icons

In Steam mit der rechten Maustaste auf **Dyson Sphere Program** klicken und **Verwalten > Lokale Dateien durchsuchen** wählen. Im Rechner über **Spiel-Icons > Spielordner wählen** diesen Installationsordner mit `DSPGAME.exe` und `DSPGAME_Data` auswählen. Eigene Steam-Bibliotheksverzeichnisse werden unterstützt. Im Browser muss der Ordner manuell ausgewählt werden; die Schaltfläche **Steam** öffnet die Bibliotheksseite des Spiels in Steam.

Nach der Ordnerauswahl liest der Browser ausschließlich `DSPGAME_Data/resources.assets` und `DSPGAME_Data/sharedassets0.assets` lokal, um unterstützte Icons zu extrahieren. Die dekodierten Icons werden für spätere Besuche und die Offline-Nutzung in der IndexedDB des Browsers gespeichert. Spieldateien und Icons werden nicht hochgeladen. Mit **Ohne Icons fortfahren** lässt sich der Rechner mit Textbeschriftungen verwenden. Über **Spiel-Icons** im Kopfbereich kann die Auswahl erneut geöffnet werden. Nicht verfügbare Icons werden durch Text ersetzt.

Der Icon-Reader unterstützt die Windows-Spieldateien, die mit DSP 0.10.35.29151 und Unity 2022.3.62 geprüft wurden. Die Spielgrafiken sind weder im Repository noch in der veröffentlichten Website enthalten und gehören den jeweiligen Rechteinhabern. DSP Net-Energy ist ein unabhängiges Fan-Werkzeug. Der Reader verwendet [AssetsTools.NET](https://github.com/nesrak1/AssetsTools.NET) unter dessen [MIT-Lizenz](src/DspNetEnergy.Desktop/Assets/AssetsTools.NET.LICENSE.txt).

## Entwicklung: Anwendung aus dem Quellcode starten

Das **.NET 10 SDK wird nur benötigt, um den Quellcode zu kompilieren, den Entwicklungsserver zu starten oder die .NET-Tests auszuführen**. Für die Nutzung der veröffentlichten Website ist es nicht erforderlich.

Voraussetzung ist ein installiertes .NET 10 SDK. Zum Starten folgenden Befehl in PowerShell im Projektordner ausführen:

```powershell
dotnet run --project src/DspNetEnergy.Web --configuration Release -- --urls http://localhost:5080
```

Sobald der Entwicklungsserver läuft, [http://localhost:5080/](http://localhost:5080/) im Browser öffnen. Das PowerShell-Fenster muss während der Nutzung geöffnet bleiben. Mit **Strg+C** lässt sich der Server beenden.

## Entwicklung: Website erstellen und veröffentlichen

Mit folgendem Befehl werden die Dateien für die Veröffentlichung der Website erstellt. Dafür muss das .NET 10 SDK installiert sein:

```powershell
dotnet publish src/DspNetEnergy.Web --configuration Release
```

Die fertigen Website-Dateien liegen anschließend unter `src/DspNetEnergy.Web/bin/Release/net10.0/publish/wwwroot`. Den gesamten Inhalt dieses Ordners auf einen Webserver kopieren, der statische Dateien ausliefert. Der Befehl erstellt die Dateien lokal; er lädt sie nicht auf den Server hoch. Auf dem Webserver selbst ist keine .NET-Installation erforderlich.

Die Website über HTTPS oder localhost aufrufen. Die Adresse muss mit einem Schrägstrich enden. Die lokale Datei `index.html` direkt im Browser zu öffnen, wird nicht unterstützt.

Für die Veröffentlichung über GitHub Pages enthält das Repository den manuell startbaren Workflow **Publish calculator to GitHub Pages**. Die Einrichtung und die Befehle zur Prüfung im Browser sind unter [Browser edition](docs/blazor-web.md) beschrieben.

## Tests

Die Tests der gesamten Solution unter Windows mit installiertem .NET 10 SDK im Projektordner ausführen. Die Solution enthält auch die WPF-Desktop-Anwendung.

```powershell
dotnet test DspNetEnergy.slnx --configuration Release
```

Die Prüfung auf gleiche Ergebnisse im Browser und im nativen .NET, Browserspeicherung, JSON-Import und -Export, Offline-Neuladen sowie den optionalen Import lokaler Spiel-Icons beschreibt [Browser edition](docs/blazor-web.md#verification).

## Logistikkosten

Unter **Erweiterte Szenarioeinstellungen** die Option **Logistikkosten bearbeiten** öffnen. Für Sortierer und Logistikstationen werden Anzahl, durchschnittliche Auslastung sowie Betriebs- und Leerlaufleistung pro Einheit eingetragen. Die Stationsleistung umfasst den Betrieb der Station ohne die Fahrzeugladung; diese wird für Drohnen und Schiffe getrennt eingetragen. Für Logistikdrohnen und interstellare Logistikschiffe ergibt sich die durchschnittliche Ladeleistung in MW aus den Rundflügen pro Minute, multipliziert mit den kJ pro Rundflug und geteilt durch 60.000. Der Energiebedarf pro Rundflug muss den Rückweg enthalten.

Leere Kategorien werden unter **Bilanzgrenze** als nicht berücksichtigt ausgewiesen. Mit einer Anzahl oder Flugrate von null lässt sich eine ungenutzte Kategorie ausdrücklich einbeziehen. Sobald aufgeschlüsselte Kosten konfiguriert sind, ist der pauschale Infrastrukturwert für zusätzliche Kosten vorgesehen. Der Rezeptvergleich verwendet für jede Produktionskette dieselben Logistikparameter.

## Eine Produktionskette berechnen

```csharp
using DspNetEnergy.Core;

var scenario = new ProductionScenario(FuelKind.EnergeticGraphite, FuelPerMinute: 60m)
{
    Proliferation = new(ProliferatorTier.MkIII, SprayFuel: true, SelfSpray: true),
    Steps = new Dictionary<string, ProductionStepSettings>
    {
        ["energetic-graphite"] = new("arc-smelter", Spray: SprayMode.ExtraProducts),
    },
};
ProductionChainBalance result = EnergyCalculator.Calculate(scenario);
```

- `FuelPerMinute` legt die Zielrate in Gegenständen pro Minute fest. Die Engine verfolgt die erforderlichen Eingangsmaterialien bis zu ihren Quellen zurück. `RecipeChoices` wählt alternative Rezepte je Material. Nutzbare Nebenprodukte und zurückgeführte Eingangsmaterialien werden gemeinsam in derselben Materialbilanz erfasst. Kreisläufe wie das Röntgen-Cracken setzen einen Anfangsbestand voraus.
- `Steps` legt je Rezept die Maschine, Anzahl, maximale Auslastung, den Durchsatzmultiplikator, optionale Leistungsüberschreibungen und den Sprühmodus fest. Reicht eine festgelegte Maschinenanzahl für die Zielrate nicht aus, meldet das Szenario einen Fehler. Eine begrenzte Kraftwerkskapazität kann dazu führen, dass Brennstoff unverbrannt bleibt. Im stationären Betrieb braucht die Fabrik einen Abnehmer für diesen Überschuss.
- `SuppliedItems` definiert die an der Bilanzgrenze bereitgestellten Materialien. Die Materialflüsse führen ihre Mengen auf; die Energiebilanz kennzeichnet die vorgelagerten Kosten als nicht berücksichtigt. Beute des Dunklen Nebels wird als bereitgestelltes Material behandelt. `Logistics` enthält die vier einzeln erfassten Kostenkategorien. Ohne diese Einstellung dient `OtherInfrastructureMw` als Pauschale für Sortierer und Logistik. Sind beide nicht gesetzt, bleiben diese Kosten unberücksichtigt.
- Jede der fünf Verbrauchskategorien erfasst einen eigenen Anteil des Energiebedarfs. Die Engine ordnet die für das Besprühen zusätzlich erforderliche Produktion und Rohstoffgewinnung der Proliferator-Produktion zu. Der Leerlaufverbrauch installierter Maschinen und Sprühbeschichter wird getrennt berücksichtigt.
- Die standardmäßig gemeinsam genutzte Kapazität beträgt 1.800 Gegenstände/min je Sprühbeschichter. Die Sprühkosten umfassen besprühte Produktionseingänge, tatsächlich verbrannten Brennstoff und das Besprühen der Proliferatoren selbst. Selbst besprühte Proliferatoren liefern 13, 28 beziehungsweise 75 Sprühvorgänge je Einheit für Mk.I, Mk.II beziehungsweise Mk.III. Nach Abzug der Ladung für das Besprühen des Proliferators selbst bleiben 12, 27 beziehungsweise 74 Ladungen für andere Gegenstände verfügbar. `ExternalDysonPowerMw` weist die durch kritische Photonen zugeführte Energie getrennt aus.

Die ursprünglichen Szenarien `DirectCombustionScenario` und `CoalToGraphiteScenario` verwenden bereitgestellten Brennstoff beziehungsweise bereitgestellte Kohle. Ihre Referenztests berücksichtigen weder Rohstoffgewinnung noch Sortierer oder Logistik.

## Spieldaten

Der eingebettete Rezeptkatalog basiert auf dem MIT-lizenzierten FactorioLab-Datensatz mit der Kennzeichnung `version.DSP = 0.10.29.21950`. Die veröffentlichte Quelle wies am 7. November 2026 weiterhin diese Version aus. Der Gegenprüfung liegt die am 15. Oktober 2026 veröffentlichte Spielversion **0.10.35.29151** zugrunde. [FactorioLab-Datensatz](https://github.com/factoriolab/factoriolab/blob/main/public/data/dsp/data.json) · [DSP-Patchnotes](https://steamcommunity.com/app/1366540/announcements/?l=english)

Die Brennstoffliste und die genauen Heizwerte wurden mit den Gegenstandsprototypen der installierten Spielversion für Steam-Build 25955035 abgeglichen. Die 24 Brennstoffe umfassen 21 chemische Brennstoffe, einen Fusionsbrennstoff und zwei Antimaterie-Brennstoffe. Geladene Akkumulatoren gehören zum Energieaustauscher und Linsen zum Strahlenempfänger. Gegenstands- und Rezeptnamen stammen aus den englischen, deutschen und vereinfachten chinesischen Lokalisierungstabellen dieses Spiel-Builds.

Der Katalog enthält die Variante des Strahlenempfängers mit Dunkle-Nebel-Linse. Der Patch, der die Pufferkapazität für Linsen verändert hat, betrifft das Anlaufverhalten. Der Rechner bildet den stationären Betrieb ab. [FactorioLab-MIT-Lizenz](src/DspNetEnergy.Core/GameData/FactorioLab.LICENSE.txt)

Die Standardwerte setzen sechs Erzadern je Bergbaumaschine, 1 Rohöl/s je Ölförderanlage und 1.800 ungestapelte Wasserstoffdurchläufe/min je Fraktionator voraus. Durch Anklicken der Rate im Schritt für die **Rohölquelle** lässt sich die aktuelle Fördergeschwindigkeit der Ölförderanlage bei voller Auslastung eingeben. Bei unterschiedlichen Quellen ist der Durchschnitt je Förderanlage zu verwenden. Der Materialfluss unter dem Schritt zeigt die gemeinsame Ausgabe in Gegenständen/min. Die Raten von Orbitalkollektoren verwenden die FactorioLab-Gaszusammensetzungen nach Abzug des internen Bedarfs von 30 MW pro Kollektor. Die Raten der Strahlenempfänger setzen einen kontinuierlichen Empfang und bei den Linsenvarianten einen Verbrauch von 0,1 Linsen/min voraus.
