## Einleitung

Diese Anleitung beschreibt die Funktion des OpenKNXproducers und dessen Einfluss auf das XML der ETS. Sie beschreibt nicht das XML der ETS, wie die ETS dieses XML interpretiert oder welche Möglichkeiten für ETS-Applikationen sich mit dem XML ergeben. Somit wird eine tiefgehende Kenntnis des ETS-XML vorausgesetzt.

> Anmerkung: Die Anleitung ist (noch) nicht vollständig. Sie beschreibt bisher nur die neusten Möglichkeiten des OpenKNXproducer, bereits länger vorhandene Funktionalitäten werden nach und nach beschrieben werden.

## Hintergrund

Beim Erstellen von ETS-Applikationen mittels einer ETS-XML treten potentiell mehrere Probleme auf: 

1. Man erzeugt syntaktisch inkorrektes XML. Hier kann eine entsprechende XSD und ein schemabasierter Editor helfen (z.B. VSCode + XML-Extension von RedHat).

2. Man erzeugt Inkonsistenzen bei den ETS-Schlüsseln (Id-Tag).

3. Man hält gewisse Typvorgaben der ETS nicht ein, die zu Laufzeitfehlern führen können.

4. Man hat Wiederholstrukturen (z.B. 8 gleiche Schaltkanäle), deren Definitionen man nicht 8 mal hinschreiben möchte. Hier können ab ETS 5.7.x ETS-Modules helfen.

Der OpenKNXproducer adressiert vor allem den Punkt 4 durch einen Template-Ansatz für die Vervielfachung von Wiederholstrukturen. Die Punkte 2 und 3 werden durch erweiterte Prüfungen des XML erreicht und Punkt  1 durch eine automatische Überprüfung gegen das XSD der ETS.

Ferner erzeugt der OpenKNXproducer eine C++ konforme Header-Datei, die alle Speicheradressen der ETS-Parameter enthält, zusätzlich auch Zugriffsfunktionen auf die Werte dieser Speicheradressen. Dies kann in der Firmware genutzt werden und erspart aufwendige Berechnungen für die Speicherzugriffe.

Der OpenKNXproducer ist ein Kommandozeilen-Werkzeug, dass eine eingebaute Kurzhilfe hat, die mit

    OpenKNXproducer help

aufgerufen werden kann. Der Beschreibung der Kommandozeilenparameter widmet sich ein eigenes Kapitel.

## Generelle Funktionsweise

Die grundsätzliche Funktionsweise vom OpenKNXproducer ist eine Include-Engine: Wenn im Quell-XML eine Include-Anweisung gefunden wird, wird dieses Include ersetzt durch die Datei, die inkludiert werden soll. Zusätzliche Informationen besagen, wie oft dieses Include eingefügt werden soll. So können Wiederholstrukturen einfach abgebildet werden.

## Tags, die vom OpenKNXproducer interpretiert werden

Im folgenden werden die XML-Tags beschrieben, die vom OpenKNXproducer genutzt werden. 

### &lt;op:ETS&gt; 

Dieser Tag muss direkt unter dem KNX-Tag stehen und beschreibt globale Informationen für die ETS. Folgende Attribute sind verfügbar

#### OpenKnxId

Dieses Attribut muss angegeben werden. Es enthält die Id, die ein Entwickler vom OpenKNX-Team zugewiesen bekommen hat oder 0xFF für privaten Bereich.

#### ApplicationNumber

Dieses Attribut muss angegeben werden. Es ist die Nummer der Applikation, die realisiert werden soll. Die Nummer ist frei vergebbar und darf während der Lebenszeit einer Applikation nicht geändert werden.

#### ApplicationVersion

Dieses Attribut muss angegeben werden. Es ist die Version der Applikation im Wertebereich 0-255. Bei jeder Änderung an der Applikation muss die Version erhöht werden. 

Als Werte sind dezimale Werte (0-255), hexadezimale Werte (0x00-0xFF) und Werte in ETS-Notation (0.0-15.15) zugelassen.

#### ReplaceVersions

Dieses Attribut muss angegeben werden. Es enthält eine Liste aller früheren Versionen, die zu dieser Version kompatibel sind. Die Einzelnen Versionen sind dabei durch ein Leerzeichen getrennt.

Als Werte sind dezimale Werte (0-255), hexadezimale Werte (0x00-0xFF) und Werte in ETS-Notation (0.0-15.15) zugelassen.

#### ApplicationRevision

Dieses Attribut muss angegeben werden. Es enthält die Revision der Applikation.

Jede Erhöhung der ApplicationVersion erzwingt ein Update der dazugehörigen Firmware. Wenn man aber nur ein Bugfix-Update der ETS-Applikation machen will, das keine Auswirkungen auf die Firmware hat, muss man die ApplicationVersion UND die ApplicationRevision gleichzeitig erhöhen. Dann muss die zugehörigen Firmware nicht aktualisiert werden.

Will man dann wieder Firmware und ETS-Applikation wieder in Sync bringen, muss man erneut die ApplicationVersion erhöhen, die ApplicationRevision setzt man dann wieder auf 0.

#### ProductName

Dieses Attribut muss angegeben werden. Es ist der Name des Produkts, wie er in der ETS in der Geräteübersicht erscheint. Diesem Namen wird automatisch ein "OpenKNX: " vorangestellt.

#### ApplicationName

Dieses Attribut ist optional. Es ist der Name der Applikation.

Wird dieses Attribut nicht angegeben, wird der ProductName genommen.

#### CatalogName

Dieses Attribut ist optional. Es ist der Name, der im ETS-Katalog erscheint.

Wird dieses Attribut nicht angegeben, wird der ProductName genommen.

#### HardwareName

Dieses Attribut ist optional. Es ist der Name, der in der ETS als Hardwarereferenz erscheint.

Wird dieses Attribut nicht angegeben, wird der ProductName genommen.

#### BuildSuffix

Dieses Attribut ist optional. Man kann an alle oben genannten Namen (ProductName, ApplicationName, CatalogName, HardwareName) einen Suffix anhängen, der eine Variante der Applikation beschreibt. Wird häufig dazu verwendet, um eine Developer-Version von einer produktiven Version zu unterscheiden. In solchen fällen findet man häufig ein "-dev" als Wert.
Da dieser Wert intern auch für Werte verwendet wird, die keine Sonderzeichen erlauben, darf auch dieser Wert keine Sonderzeichen wie ä, ö, ü oder ß enthalten.

Wird dieses Attribut nicht angegeben, bleibt der BuildSuffix leer. Dies indiziert normalerweise die produktive Version.

#### BuildSuffixText

Dieses Attribut ist optional. Genau wie BuildSuffix kann hier ein Suffix an die entsprechenden Namen gehängt werden. Dieses Attribut erlaubt - im Gegensatz zu BuildSuffix - Sonderzeichen, weil es nur in Textfeldern verwendet wird.
Falls BuildSuffixText angegeben wird, muss auch BuildSuffix angegeben werden. Häufig wird der Wert " (dev)" als Wert angegeben. Das Leerzeichen vorweg ist wichtig, da diese Zeichenkette einfach nur konkateniert wird.

Wird dieses Attribut nicht angegeben, wird der BuildSuffix genommen.

#### BaggagesRootDir

Dieses Attribut ist optional. Wenn in der Applikation Baggages verwendet werden, müssen diese in einem passenden Unterverzeichnis gespeichert werden. 

Standardmäßig ist besteht dieses Unterverzeichnis aus der OpenKnxId und der ApplicationNumber. Es gibt normalerweise keinen Grund, dieses Verzeichnis zu setzen. Somit sollte das Attribut standardmäßig nicht angegeben werden.

#### SerialNumber

Dieses Attribut ist optional. Man kann die Seriennummer des Gerätes angeben, falls das gewünscht ist. Die Seriennummer darf sich während der Entwicklungszeit eines Gerätes nicht ändern. 

Wird dieses Attribut nicht angegeben, wird die Seriennummer aus der OpenKnxId und der ApplicationNumber generiert.

#### OrderNumber

Dieses Attribut ist optional. Man kann die Bestellnummer des Gerätes angeben, falls das gewünscht. 

Wird dieses Attribut nicht angegeben, ist die Bestellnummer gleich der Seriennummer.

#### BusCurrent

Dieses Attribut ist optional. Es erlaubt die Angabe vom Strom in mA, der vom Bus konsumiert wird.

Wird dieses Attribut nicht angegeben, ist der Busstrom 10 mA.

#### IsIPEnabled

Dieses Attribut ist optional. Es muss auf "true" gesetzt werden, wenn das Gerät ein IP-Gerät ist.

Wird dieses Attribut nicht angegeben, ist der Wert "false".

#### IsRailMounted

Dieses Attribut ist optional. Es kann auf "true" gesetzt werden, wenn das Gerät für die Montage auf der Hutschiene geeignet ist.

Wird dieses Attribut nicht angegeben, ist der Wert "false".

### &lt;op:define&gt;

Im OpenKNXproducer ist es möglich, mehrere Includes als ein Modul zu betrachten, das eine ganze Sub-Applikation repräsentiert und eine Integration dieser Module zu einer Gesamtapplikation erlaubt.

Die Attribute vom define-Tag geben eine solche Moduldefinition bekannt.

#### prefix

Alle Includes eines Moduls haben einen bestimmten eindeutigen Prefix. Dieser ist nicht frei wählbar, sondern wird vom Entwickler des Moduls bestimmt. Dieses Attribut verbindet den Prefix des Moduls mit den weiteren Definitionen des Moduls.

#### header

Hier steht der Name des Header-Files, das vom OpenKNXproducer erzeugt wird, um der Firmware alle Adressen der Parameter und Kommunikationsobjekte mitzuteilen. 

>Achtung: Aus historischen Gründen muss der Name bei jedem Define angegeben werden, obwohl nur eine Heder-Datei generiert wird. Konsequenterweise muss der Name der Header-Datei bei allen define-Tags gleich lauten.

#### share

Hier wird der Name des Includes angegeben, das die Teile eines Moduls enthält, die keine Wiederholstrukturen enthalten.

#### template

Hier wird der Name des Includes angegeben, das die Teile eines Moduls enthält, die nur Wiederholstrukturen enthalten.

#### NumChannels

Hier wird die Anzahl der Wiederholungen für dieses Modul angegeben. 

Eine Zahl > 0 bedeutet, dass die unter "template" angegebene Datei so oft inkludiert wird.

Eine Zahl = 0 bedeutet, dass gar kein "template" inkludiert wird (Module ohne Kanäle).

#### KoOffset

Der OpenKNXproducer belässt alle KO-Nummern so, wie sie in der Quell-XML angegeben sind. Bei Wiederholstrukturen geht das natürlich nicht, da es dann doppelte KO-Nummern gäbe. Man kann bei Wiederholstrukturen statt KO-Nummern auch eine vordefinierte Ersetzung %K*n*% verwenden, wobei *n* bei 0 anfangen darf und relative KO-Nummern repräsentiert. 

Mit dem Attribut KoOffset gibt man die Nummer an, die für %K0% eingesetzt wird, %K1% ist dann KoOffset + 1 usw. Bei Wiederholstrukturen wird dann einfach weiter gezählt. 

Beispiel:

* KoOffset="7", Wiederholstruktur nutzt %K0% bis %K19%
* Erste Wiederholstruktur (Channel 1) hat dann die KO7 bis KO26
* Zweite Wiederholstruktur (Channel 2) hat dann die K27 bis KO46
* Dritte Wiederholstruktur (Channel 2) hat dann die K47 bis KO66
* usw.

#### KoSingleOffset

Dieses Attribut ist optional und vorbelegt mit dem Wert 0.

Wie beim KoOffset, nur adressiert das Attribut KoSingleOffset alle Kommunikationsobjekte, die in dem Include enthalten sind, dass nicht wiederholt wird (share-Include).

Da dieser Teil nicht wiederholt wird, können hier normal absolute Werte für KO-Nummern vergeben werden. 

Versucht man aber, mehrere Module zu verwenden, die ihrerseits alle Teile haben, die nicht wiederholt werden, kann es passieren, dass deren absolute Angaben für KO-Nummern kollidieren. 

In einem solchen Fall kann man einen KoSingleOffset angeben, um die Kollision zu vermeiden. Der Wert vom KoSingleOffset wird zu jeder absoluten KO-Nummer im share-Include hinzuaddiert.

#### ModuleType

Der ModuleType (Name ist historisch bedingt, es ist eher eine Modulnummer) dient dazu, die Namensräume der Objekt-Ids der einzelnen Module zu trennen, damit es keine Kollisionen zwischen verschiedenen Modulen gibt. 

Der ModuleType muss genau eine Ziffer (1-9) sein. Es können maximal 9 Module zu einer Applikation zusammengebaut werden.

### &lt;op:verify&gt;

Dieses Tag muss innerhalb vom define-Tag stehen und dient zur Versionsüberprüfung von Modulen.

Alle Module, die von anderen verwendet werden können, müssen über die Datei library.json im Projekt versioniert werden. 

Die dort enthaltene Version wird dann durch das verify-Tag geprüft. Die genaue Funktionsweise ist im Wiki unter [Versionierung von Modulen](https://github.com/OpenKNX/OpenKNX/wiki/Versionierung-von-Modulen-(OFM)) beschrieben.

#### File

Die Datei, die die Modulversion des zu nutzenden Moduls enthält. Üblicherweise heißt die Datei "library.json", aber es muss auch der vollständige Pfad angegeben werden, damit die Datei gefunden werden kann.

Technisch ist es auch möglich, gegen andere Dateien zu prüfen, siehe das optionale Regex-Attribut.

#### ModuleVersion

Hier wird die erwartete Version des Moduls angegeben. Wenn der Wert in der library.json von diesem Wert abweicht, meldet der OpenKNXproducer einen Versionsfehler.

#### Regex

Dieses Attribut ist optional.

Es ist auch möglich, gegen andere Datein als die library.json zu prüfen. Dann muss hier eine Regex angegeben werden, die die Version des Moduls in der Datei findet. Dabei wird die Datei zeilenweise eingelesen und jede Zeile wird gegen diese Regex geprüft.  

### &lt;op:include&gt;

Mit dem op:include-Tag wird eine weitere Datei exakt an der Stelle eingefügt, an der das op:include-Tag steht und dieses Tag damit ersetzt. Falls das Include eine Wiederholstruktur repräsentiert, wird es so oft wiederholt, wie im entsprechenden op:define-Tag angegeben.

#### prefix

Alle Includes, die logisch zu einem Modul gehören, haben den gleichen Prefix. Der Prefix wird vom Modulentwickler festgelegt.

#### href

Die Datei, die inkludiert werden soll.

#### xpath

Im Allgemeinen will man nicht eine Datei komplett inkludieren, sondern nur den Teil, der an der include-Stelle relevant ist. Mit einem passenden xpath-Ausdruck kann man bestimmen, welcher Ausschnitt der Gesamtdatei inkludiert wird. 

#### type

Dieses Attribut ist optional. Es hat nur 2 Werte und muss nur in 2 Fällen angegeben werden:

##### type="parameter"

Immer wenn der Include Definitionen von ETS-Parametern enthält, die nicht wiederholt werden, muss type="parameter" angegeben werden. 

Praktisch findet das Verwendung, wenn op:include innerhalb eines &lt;parameter&gt;&lt;/parameter&gt; Blocks steht.

##### type="template"

Immer wenn der Include wiederholblöcke enthält, muss type="template" angegeben werden, damit der OpenKNXproducer diese vervielfachen kann.

### &lt;generate&gt;

Die Struktur des ETS-XML ist ziemlich aufwändig, es müssen verschiedene Teile eines Moduls an verschiedenen Teilen im Zieldokument eingefügt werden. Das neue Tag &lt;generate&gt; erlaubt es, das Mehrfache einfügen des selben Include-Files in verschiedene Teile der Zieldatei zu vereinfachen. 

Vereinfacht gesagt, muss das generate-Tag nur den Dynamic-Tag der ETS enthalten und erzeugt den Rest der Applikation (Catalog, Hardware, Product und ApplicationProgram/Static) selbst.

Voraussetzung ist, dass alle Includes sich an die Struktur einer ETS-Applikation halten und somit die Pfade zu den einzelnen Teilen der Applikation klar sind.

> Achtung: &lt;generate&gt; liegt nicht im Namensraum von OpenKNXproducer, sondern im Namensraum der ETS. Es heißt somit wirklich &lt;generate&gt; und nicht &lt;op:generate&gt;!

#### base

Dieses Attribut ist optional. Wenn es angegeben wird, muss es eine Datei mit diesem Namen geben, die die Generierungsvorschrift für den generate-Tag beinhaltet. Diese Option ist derzeit experimentell und nur für fortgeschrittene Benutzer gedacht und wird erst zukünftig in die Anleitung aufgenommen. 

### &lt;op:config&gt;

OpenKNXproducer erlaubt eine einfache Form der Parametrisierung der XML-Dateien. Dies geschieht über Ersetzungen von Tokens. Einige Tokens sind vordefiniert und werden automatisch ersetzt (siehe Kapitel Automatische Ersetzungen). Über das config-Tag können auch noch benutzerdefinierte Ersetzungen definiert werden.

Ein Token beginnt und endet immer mit einem %-Zeichen, dazwischen ist ein normaler String ohne Sonderzeichen.

Beispiel:

    <op:config name="%trigger%" value="1" />

In der XML-Quelldatei kann jetzt irgendwo stehen:

    <choose Id="....">
      <when text="%trigger%">
        ...
      </when>
    </choose>

In der *.knxprod-Datei würde dann entsprechend folgendes stehen:

    <choose Id="....">
      <when text="1">
        ...
      </when>
    </choose>


#### name

Dieses Attribut gibt den Namen des Tokens an, das ersetzt werden soll. Der Name muss auch die %-Zeichen am Anfang und am Ende enthalten. 

#### value

Dieses Attribut gibt den Wert an, mit dem das Token im XML-Dokument ersetzt werden soll.  

### &lt;nowarn&gt;

Bei den vom OpenKNXproducer durchgeführten Prüfungen werden neben Fehlermeldungen, die eine Generierung einer Applikation für die ETS (*.knxprod-Datei) verhindern, auch Warnungen, die den Benutzer darauf hinweisen, dass er womöglich eine unbeabsichtigte Konstellation gewählt hat. 

Ist die Konstellation doch beabsichtigt, kann man dies dem Programm mitteilen und somit die Warnung unterdrücken.

#### id

Jede Warnung hat eine Nummer. Diese Nummer wird hier angegeben.

Es ist nicht zu empfehlen, alle Warnungen mit einer bestimmten Nummer zu unterdrücken, da es durchaus die selbe Warnung an verschiedenen Stellen geben kann. Damit hat die Warnung die gleiche Nummer, aber unterschiedliche Warntexte. Würde man alle Meldungen einer bestimmten Id unterdrücken, würde man nicht nur Meldung für die spezifische (beabsichtigte) Konstellation unterdrücken, sondern auch für weitere möglicherweise unbeabsichtigte Konstellationen.

#### regex

Jede Warnung hat auch einen Text. Mit dem angegebenen Regex-Ausdruck sollte man möglichst spezifisch den Meldungstext auf genau eine Meldung filtern, damit man nicht mehr Meldungen als nötig unterdrückt.

In Modulen sollte niemals eine Warnung nur anhand ihrer Id, sonder immer anhand der Kombination einer Id mit einem möglichst spezifischen Regex unterdrückt werden.

## Vordefinierte Ersetzungen

Der OpenKNXproducer erlaubt die Ersetzung von Token im Quell-XML. Alle Token haben immer das Format %*text*%, wobei Text keine Sonderzeichen enthalten sollte.

Es werden einige vordefinierte Ersetzungen vorgenommen, um die Kernfunktionen des OpenKNXproducers zu unterstützen. Die meisten vordefinierten Ersetzungen betreffen kanalspezifische Informationen, die sich während der Vervielfachung von Wiederholblöcken ändern.

### Aktueller Kanal als Zahl - %C%, %CC%, %CCC%

%C% enthält die aktuelle Kanalnummer (Channel). Der Unterschied zwischen %C%, %CC% und %CCC% liegt nur in der Anzahl der Stellen, die diese Kanalnummer repräsentiert. Somit ist:

%C% - einstellig
%CC% - zweistellig, mit führender Null bei einstelliger Kanalnummer
%CCC% - dreistellig, mit führenden Nullen bei ein- oder zweistelliger Kanalnummer

Ist die Kanalnummer länger als die verfügbare Stellenzahl, so wird immer die komplette Kanalnummer genommen.

Beispiel (Kanalnummer ist 76):

    %C%   ergibt 76
    %CC%  ergibt 76
    %CCC% ergibt 076

In der Praxis kommt %C% innerhalb von Texten zum tragen, %CCC% bei der Berechnung von ETS-Ids.

### Aktueller Kanal als Buchstabe - %Z%, %ZZ%, %ZZZ%

Gleiche Regeln wie bei "Aktueller Kanal als Zahl", nur werden die Kanalnummern als Buchstaben dargestellt und statt führender Null (0) führende A (entspricht der 0) genommen. Die Berechnungsvorschrift für die Buchstaben entsprechend einer Zahl zur Basis 26.

Beispiel (Kanalnummer ist 76):

    %Z%   ergibt CY
    %ZZ%  ergibt CY
    %ZZZ% ergibt ACY

In der Praxis kommt %Z% in Texten zur Kanalbenennung vor. Normalerweise hat man hier weniger als 26 Kanäle und somit nur einstellige Buchstaben.

### Maximale Anzahl der Kanäle für dieses Modul - %N%

Dieser Wert wird durch die Zahl ersetzt, die bei op:define für NumChannels angegeben wurde.

### Aktuelle Modultyp - %T%

Dieser Wert wird durch die Zahl ersetzt, die bei op:define für ModuleType angegeben wurde.

### Aktuelle KO-Nummer - %K0%, %K1%, %K2%, ... %K*n*%

Dieser Wert wird durch die berechnete KO-Nummer für diesen Wiederholblock ersetzt. Dabei wird die Zahl *n* hinter dem K hinzugerechnet. So kann man in Wiederholblöcken relative KO-Nummern adressieren.

## Kommandozeilenparameter

Dieses Kapitel ist nur partiell verfügbar. Bis dahin bitte die Kurzhilfe über

    OpenKNXproducer help

benutzen.

### version

Der Aufruf des Kommandos *version* gibt die aktuell installierte Version vom OpenKNXproducer aus. Das Kommando hat keine weiteren Parameter.

    OpenKNXproducer version

### create

Das Kommando *create* nimmt eine xml-Datei entgegen und baut die sich daraus ergebende knxprod-Datei. Hierbei werden alle Funktionalitäten berücksichtigt, die der OpenKXNproducer anbietet:

* Prozessierung von Includes, 
* Vervielfältigung von Kanälen, 
* Ersetzung von Objekt-ID's, 
* Ersetzung von Parametern,
* Generierung einer Header-Datei
* Konsistenzprüfungen

Durch weitere Kommandozeilenparameter kann man die Funktionsweise beeinflussen.

#### xml-Hauptdatei

Das Kommando muss mit einer xml-Datei aufgerufen werden. Alle darin enthaltenen Anweisungen werden befolgt und das Ergebnis ist eine knxprod-Datei.

    OpenKNXproducer create MyApplication.xml

#### --help

Wird der Parameter --help verwendet, wird eine Kurzhilfe zum Kommando *create* mit allen Kommandozeilenoptionen ausgegeben.

    OpenKNXproducer create --help

#### --version

Wird der Parameter --version verwendet, wird die aktuell installierte Version vom OpenKNXproducer ausgegeben. Das entspricht dem Kommando *version*.

    OpenKNXproducer create --version

#### --HeaderFileName=FILE, -h FILE

Die Option --HeaderFileName (Kurzschreibweise -h) gibt den Namen der zu generierenden Header-Datei an. Wird kein Name angegeben, wird der Name der xml-Datei genommen. Die Datei endet auf .h statt .xml.

#### --Debug, -d

Die Option --Debug (Kurzschreibweise -d) gibt das Ergebnis aller Transformationen, die durch den OpenKNXproducer vorgenommen wurden, als xml-Datei aus. Dies ist die Datei, die als Eingabe für den knxprod-Konverter fungiert. 

Der Name dieser xml-Datei ist zusammengesetzt aus der Original-xml-Datei, gefolgt von .debug.xml.

#### --NoRenumber, -R

Die Option --NoRenumber (Kurzschreibweise -R) ist ein Kompatibilitätsmodus zu früheren OpenKNXproducer-Versionen (vor 1.5). Dort musste man selbst für die Eindeutigkeit der IDs von ParameterBlock- und ParameterSeparator-Tags sorgen. Da dies bei langen xml-Dateien schwierig ist und die IDs keine weitere Bedeutung haben, nummeriert der OpenKNXproducer alle ParameterBlock- und ParameterSeparator-IDs so, dass sie eindeutig im ganzen Dokument sind. 

Dieser Parameter sollte nur gewählt werden, wenn man aus irgendwelchem Grund die selbst vergebenen IDs behalten will. Wird dieser Parameter gesetzt, führt das zwangsläufig zu Problemen mit Modulen anderer Entwickler, die nicht mit diesem Parameter arbeiten.

#### --AbsoluteSingeParameters, -A

Die Option --NoRenumber (Kurzschreibweise -R) ist ein Kompatibilitätsmodus zu früheren OpenKNXproducer-Versionen (vor 1.5). Bis 1.5 wurden nur die Speicherpositionen von Parametern berechnet, die vervielfacht wurden. Parameterpositionen von Parametern, die nur einmal vorkamen (die Parameter in der .share.xml-Datei) wurden an die absolute Speicherposition gesetzt, die in der Parameterdefinition stand. 

Da das Verfahren zu Speicherkollisionen bei der Verwendung von mehreren Modulen führen konnte, wurden ab 1.6 auch die Adressen von nur einmal vorkommenden Parametern als relative Adressen betrachtet und die absolute Speicherposition vom OpenKNXproducer berechnet.

Dieser Parameter sollte nur gewählt werden, wenn man noch mit absoluten Adressen für einmal vorkommende Parameter arbeitet. Wird dieser Parameter gesetzt, führt das zwangsläufig zu Problemen mit Modulen anderer Entwickler, die nicht mit diesem Parameter arbeiten.

#### --ConfigFileName=FILE, -c FILE

Hier kann eine xml-Datei angegeben werden, die &lt;op:config name="..." value="..."&gt; Listen enthält, die vom OpenKNXproducer in den jeweiligen xml-Dateien ersetzt werden. Das Verfahren ist das gleiche wie bei einem &lt;op:config&gt;-Tag in allen anderen xml-Dateien (siehe [op:config](#opconfig)).

Die Ersetzungspriorität bei verschachtelten &lt;op:config&gt;-Einträgen ist so gewählt, dass ein "äußerer" Eintrag immer vor einem "inneren" gewinnt. Somit gewinnen bei Kollisionen immer alle Einträge einer Config-Datei.

#### --Output=FILE, -o FILE

Hier kann der Dateiname der Ausgabedatei angegeben werden. Falls noch nicht mit angegeben, wird die Endung .knxprod angehängt.

#### --Xsd=FILE, -V FILE

Hier kann der Name einer xsd-Datei angegeben werden, gegen die das generierte xml geprüft wird, bevor es dem knxprod-Generator übergeben wird. 

Wird keine xsd-Datei angegeben, wird versucht, eine Standard-xsd zu finden. Wird keine Standard-xsd gefunden, wird auch nicht geprüft.

Eine xsd-Datei ist nicht Voraussetzung für die Funktion vom OpenKNXproducer.

#### --NoXsd, -N

Mit dieser Option wird eine Prüfung gegen eine xsd-Datei verhindert.

### baggages

OpenKNXproducer unterstützt die Erstellung von kontextsensitiver Hilfe innerhalb der ETS. Dazu ist ein kleiner Text-Parser implementiert, der das Parsing von .md-Dateien erlaubt. Weitere Details hierzu stehen im Kapitel [Kontextsensitive Hilfe](#kontextsensitive-hilfe-baggages).   

#### --BaggagesDir=DIR, -b DIR

Der Parameter --BaggageDir (Kurzschreibweise -b) gibt das Verzeichnis an, in das die erzeugten Texte für die kontextsensitive Hilfe gespeichert werden. Dieser Parameter muss angegeben werden.

#### --DocFileName=FILE, -d FILE

Der Parameter --DocFileName (Kurzschreibweise -d) gibt die Quelldatei an (z.B. eine Dokumentation als .md-Datei). Diese Datei wird analysiert und entsprechende Dateien für die kontextsensitive Hilfe generiert.

#### --Prefix=STRING, -p STRING

Hier muss der selbe Prefix angegeben werden, den der Modulentwickler für die knxprod-Generierung für das Modul verwendet. 

### help

### knxprod

### check

### new

## Kontextsensitive Hilfe (Baggages)


