# Vývoj

## Ovládanie

- Potiahni dielik z ponuky na dosku. Pri myši je stred dielika pod kurzorom, pri dotyku mierne nad prstom (o 0,45 strany bunky). Počas ťahania sleduje pohyb bez vyhladzovania polohy; pri pustení použije poslednú polohu dotyku. Prichytáva sa na najbližšie voľné miesto.
- Bunky, ktoré sa po položení vyčistia, sa rozsvietia už počas ťahania.
- Ikona vľavo hore ukazuje režim: pohár je Arcade, oblak Flow, kalendár Daily. Ťuknutie otvorí menu s režimami, témami a cieľmi. Ťuknutie mimo položiek menu zavrie.
- Zahnutá šípka pod ikonou režimu vráti ťah, aj z obrazovky konca partie. Keď sa vrátiť nedá, je stlmená a po ťuknutí sa zatrasie. Arcade povoľuje jedno vrátenie na trojicu. Flow pamätá posledných 500 ťahov aj cez rozdania. Daily šípku nemá.
- Reproduktor vpravo hore zapína a vypína zvuk. Keď je zvuk vypnutý, namiesto vĺn má krížik.
- Otáznik pod reproduktorom otvorí kartu aktuálneho režimu: čo je to za režim, štyri jeho pravidlá s ikonami z hry a päť základov hry. Ťuknutie kamkoľvek kartu zavrie a partiu neovplyvní. Biela bodka na otázniku znamená, že kartu tohto režimu hráč ešte neotvoril (`help_seen` v `PlayerPrefs`).
- Pri úplne prvom spustení hra bez slov ukáže, ako potiahnuť dielik.
- Rozohraná partia sa ukladá a po návrate pokračuje. Tlačidlo Späť na Androide hru uloží a zavrie.

## Pravidlá

- Doska má 7 úrovní, 49 buniek a 15 línií: 5 vodorovných a 5 + 5 diagonálnych, dĺžky 5 až 13.
- Kratšie línie (1 a 3 bunky pri rohoch) sa nepočítajú, aby sa línia nedala vyčistiť jediným dielikom. Hranica je `ShortestLine` v `Assets/Game/Core/Lattice.cs`.
- Body: `2 × bunky dielika`, pri čistení `round((7 × bunky + 36 × línie²) × (1 + 0,38 × min(kombo − 1, 10)))`.
- V Arcade a Daily kombo zaniká po troch ťahoch bez čistenia. Tri bodky pod `×N` ukazujú, koľko ťahov zostáva. Vo Flow ďalšie čistenie vždy pokračuje v kombe.
- Perfect Trio: všetky tri dieliky ponuky niečo vyčistili, +200. Stav ukazujú tri značky pod ponukou. Hra počíta, koľko Perfect Trio partia mala (`Game.Perfects`).
- Tri smery naraz: keď jeden ťah vyčistí línie vo všetkých troch smeroch, pribudne +300 (`Game.TripleBonus`), tie línie sa rozsvietia zlato a v strede dosky sa ukáže „+300“. Bonus sa do obťažnosti nezapočíta, generátor dostane skóre bez neho. Arcade má preto rovnaké ťahy ako predtým, mediány 101, 59, 35 a 19 ostali. Je to vzácne: najlepší bot to v simulácii dal asi 3-krát na 100 partií, náhodný asi raz.
- V Daily je hlavná výzva počet Perfect Trio. Počas hry je pod skóre namiesto rekordu, na konci partie pod skóre s popisom „perfect trio“ a v zdieľanom texte hneď za dátumom.
- Koniec: žiadny zo zvyšných dielikov sa nedá položiť.

## Koniec partie

Skóre sa presunie do stredu (zlaté pri novom rekorde). Nad ním je ikona režimu, v Daily kalendár s dňom partie. Pod skóre je korunka s rekordom, počet vyčistených línií a najvyššie kombo. Tlačidlo s trojuholníkom ukazuje, že ťuknutie kamkoľvek spustí novú partiu. V Daily je nad ním tlačidlo zdieľania.

Daily sa dá dohrať raz za deň. Dohratá partia dňa sa uloží (`daily.final` v `PlayerPrefs`), jej skóre je rekord dňa a ide do zdieľania. Po konci je namiesto novej hry ikona hodín s časom do ďalšej výzvy (o polnoci podľa miestneho času) a ťuknutie otvorí menu. Po návrate do Daily alebo po reštarte hry sa ukáže dnešný výsledok. Partia začatá pred polnocou sa dá dohrať aj po nej a dnešnú výzvu nezamkne. Keď polnoc príde, kým je výsledok na obrazovke, objaví sa opäť „nová hra“. Daily neukazuje korunku s rekordom ani neoslavuje nový rekord, lebo pokus je jeden a rekord by sa vždy rovnal skóre. Dnešný výsledok je v menu pod Dennou.

## Dizajn

Počas hry sú len ikony a čísla a každý tvar má jeden význam. Krátke slovenské popisy sú len tam, kde sa hráč rozhoduje alebo číta výsledok:

- **Menu:** názov pod každým režimom (Arcade, Flow, Denná) a pod každým cieľom (Bez cieľa, Línie, Viac naraz, Kombo).
- **Koniec partie:** „rekord“, „línie“, „max kombo“, „zdieľať“ a „nová hra“ pod príslušným číslom alebo tlačidlom.
- **Prepnutie režimu:** názov pod ikonou nového režimu.
- **Karta režimu** pod otáznikom: názov režimu, jeho pravidlá a základy hry.

Popisy kreslí TextMeshPro (`Assets/Game/View/Labels.cs`) zaobleným písmom Varela Round, ktoré ladí s číslami (`Assets/Game/Resources/VarelaRound-Regular.ttf`, licencia OFL v `VarelaRound-OFL.txt`). Font asset vzniká za behu, takže netreba nič generovať v editore. Ležia nad sieťou hry a miznú spolu s menu a koncom partie. Výška veľkých písmen je násobok `side`: názvy režimov 0,27, názvy cieľov 0,23, popisy 0,21.
- **Čitateľnosť:** všetok text a čísla mimo odmien majú farbu `Icon` (kontrast aspoň 5 : 1 na pozadí a 4,3 : 1 na kruhu tlačidla). `Dim` ostal len pre stavové značky pod ponukou a stlmené Undo.

| Tvar | Význam |
|---|---|
| Pohár, oblak, kalendár | režim Arcade, Flow, Daily (v menu a na konci partie s dňom v kalendári) |
| Korunka s číslom | rekord režimu, v Daily rekord dňa |
| Zahnutá šípka | vrátiť ťah |
| Reproduktor | zvuk |
| Otáznik | karta režimu: čo je to za režim a ako sa hrá |
| Žiarovka, s lúčmi zapnutá | ukážka ťahu vo Flow |
| Pás troch trojuholníkov | línie |
| Dva pásy | ťahy, ktoré vyčistili 2 a viac línií naraz |
| ×N | kombo |
| Tri zlaté trojuholníky v rade | Perfect Trio |
| Hviezdica z troch čiar | línie vo všetkých troch smeroch naraz |
| Hviezda | splnený stupeň osobného cieľa |
| Prečiarknutý kruh | bez cieľa |
| Tri spojené bodky, fajka | zdieľať, skopírované |
| Trojuholník v kruhu | nová partia |

- **Zlatá znamená len odmenu:** nový rekord, Perfect Trio, kombo (×N začína zlaté a s rastúcim kombom prechádza do farby dielika, doska sa pritom zohreje), hviezdy a postup k cieľu, potvrdenie zdieľania. Výber v menu je svetlý krúžok, nie zlatá.
- **Jas ukazuje stav:** svetlá (`Ink`) je vybraná alebo práve užitočná vec, ikona (`Icon`, `Dim` zosvetlená o štvrtinu k `Ink`) bežné ovládanie, stlmená nedostupné. Ikona má na kruhu tlačidla kontrast aspoň 4,3 : 1.
- **Tlačidlá** ležia na kruhu vo farbe prázdnej bunky (`CellTop`). Ťuknutie kruh krátko zväčší.
- **Lišta:** vľavo režim a pod ním Undo, vpravo zvuk, pod ním otáznik a vo Flow ešte nižšie žiarovka. Tlačidlá sú od seba tak, že sa ich dosahy ťuknutia neprekrývajú.
- **Karta režimu** úplne zakryje hru: ikona a názov režimu, štyri riadky pravidiel režimu, čiara a päť základov hry (ťahanie, línia, tri smery naraz, Perfect Trio, koniec). Každý riadok má ikonu, ktorú hráč pozná z hry, takže karta zároveň vysvetlí ikony. Texty sú v `helpRules` a `basicRules` v `GameView.cs`.
- **Menu** úplne zakryje hru: riadok režimov s názvom a rekordom pod každým, riadok tém (kruh s pozadím témy a štyrmi farbami dielikov) a riadok cieľov s názvom a počtom hviezd. Ťuknutie na názov alebo číslo pod položkou platí ako ťuknutie na položku. Pás cieľa sa pod koncom partie skryje.
- **Pás cieľa** nad ponukou: vľavo ikona cieľa, v strede postup, vpravo číslo ďalšieho stupňa a hviezda. Oslava (zlaté iskry z hviezdy, zvuk rekordu) príde len za hviezdu zvoleného cieľa. Hviezdy ostatných cieľov pribúdajú potichu a vidno ich v menu.
- **Prepnutie režimu** ukáže na doske na sekundu ikonu nového režimu.
- **Farby tém:** šesť farieb dielikov je od seba vzdialených aspoň 12 a od zlatej aspoň 13 v OKLab (× 100). Aurora 13,8 a 13,9, Sunset 12,8 a 13,1, Ocean 12,4 a 18,5. Pri zmene palety v `Theme.cs` to prepočítaj.
- **Rozmery** sú násobky `side`: tlačidlo v lište má kruh 0,44, ikonu 0,23 a dosah ťuknutia 0,55. V menu majú režimy kruh 0,75, témy 0,62 a ciele 0,6.

### Psychologické potreby

| Potreba | Čo ju v hre napĺňa |
|---|---|
| Kompetencia | náhľad čistenia, body pri čistení, kombo ×N s bodkami, Perfect Trio, korunka s rekordom, pás cieľa s číslom stupňa |
| Autonómia | menu s režimami, témami a cieľmi (aj bez cieľa), Undo, žiarovka vo Flow, zvuk |
| Vzťahovosť | Daily: rovnaké dieliky pre všetkých, kalendár s dňom, rekord dňa a zdieľanie výsledku |

Robot `Pointless.Editor.Headless.Run()` overuje kartu režimu (otáznik ju otvorí v každom režime, zapamätá si to a zatváracie ťuknutie nič nezmení) aj menu: dosahy tlačidiel sa neprekrývajú, stlmená šípka na konci partie nič nezmení ani nespustí novú partiu, Flow, Daily aj Arcade, každá téma aj cieľ reagujú na ťuknutie (aj na číslo pod nimi) a ťuknutie mimo menu ho zavrie.

## Štruktúra

| Priečinok | Obsah |
|---|---|
| `Assets/Game/Core` | Pravidlá bez Unity: mriežka, tvary, generátor, stav hry. Doska je jedno 64-bitové číslo. |
| `Assets/Game/View` | Vykresľovanie, vstup, zvuk, haptika. Celá hra je jedna sieť a jeden shader. |
| `Assets/Game/Editor` | Nastavenie projektu, build, robot na automatické hranie, simulácia partií. |

Po zmene nastavení projektu alebo scény spusti **Pointless > Set Up Project**.

Ťahanie over v režime Play volaním `Pointless.Editor.Robot.CheckDrag()`. Kontrola overuje mierny odstup nad prstom, nulový odstup od kurzora a ich zachovanie pri krokoch zodpovedajúcich 30, 60 a 120 FPS vrátane zmeny smeru; dielik potom vráti do ponuky.

## Logovanie

- Hra píše do konzoly Unity správy so značkou `Pointless:` cez `Log.Info` a `Log.Warn` v `Assets/Game/View/Log.cs`.
- `Log.Info` beží len v editore a v development builde (od Unity 6.6 vo variantoch Debug, Checked a Instrumented). V release builde sa volanie aj so skladaním textu vynechá.
- `Log.Warn` sa píše vždy, aj v release builde: poškodená uložená partia alebo ciele a zlyhané zdieľanie na Androide. V editore aj chýbajúce pole pri **Set Up Project**.
- Zapisuje sa štart, rozmer obrazovky, nová a obnovená partia, prepnutie režimu, Undo, koniec partie, hviezdy z cieľov, uloženie a zmena nastavení. Nič sa nepíše v každom snímku.
- Zapisuje sa aj všetko, čo hráč robí: každý ťah (`Move <n> <režim>: slot, shape at <miesto>, lines, +body, combo, score`, pri Perfect Trio aj bonus), zdvihnutie dielika (`Pick`), vrátenie do ponuky (`Put back`), každá nová trojica (`Trio shapes`), ukážka ťahu (`Hint`), nedostupné Undo, otvorenie a zavretie menu a karty režimu a pauza aplikácie. Z logu sa tak dá prečítať celý priebeh partie.
- Na telefóne: `adb logcat -s Unity` a hľadaj `Pointless:`.

## Hráči a štatistiky

- **Hráči:** v menu hore je meno hráča a „zmeniť hráča“. Otvorí zoznam (najviac 8 hráčov) a „nový hráč“, kde sa napíše meno. Na telefóne sa otvorí systémová klávesnica, v editore sa píše priamo, Enter potvrdí a Esc zruší. Ťuknutie na meno prepne hráča a na doske sa na chvíľu ukáže jeho meno.
- Každý hráč má vlastné uložené partie, rekordy, Daily, ciele, tému, zvuk, nápovedu aj videné karty režimov. Prvý hráč používa pôvodné kľúče v `PlayerPrefs`, ďalší majú predponu `p<n>.`. Zoznam mien je v `players` a aktuálny hráč v `player`.
- **Štatistiky:** každá udalosť je jeden riadok JSON v `<persistentDataPath>/stats/<dátum>.jsonl` (`Assets/Game/View/Stats.cs`). Zapisuje sa vždy, aj v release builde, a dáta neopustia zariadenie.
  - Telefón: `adb pull /sdcard/Android/data/com.extynt.pointless/files/stats`
  - Editor: `%USERPROFILE%\AppData\LocalLow\ExTynT\Pointless\stats`
  - Robot (`Headless.Run`) počas behu nič nezapisuje a hrá za prvého hráča.
- **Polia** (každý riadok má všetky, nepoužité sú 0, -1 alebo prázdne): `time` a `ms` (čas), `session`, `install` (náhodný identifikátor inštalácie, vznikne raz), `player` a `pid` (meno a poradie hráča v tomto zariadení), `ev` (udalosť), `mode`, `game` (poradie partie v relácii), `move` (poradie ťahu v partii), `slot`, `shape`, `size`, `place`, `lines`, `points`, `combo`, `score`, `best`, `perfects`, `triples`, `fill` (obsadené bunky po ťahu), `fits` (koľko dielikov sa ešte zmestí), `misses` (ťahy bez čistenia, kombo v Arcade a Daily zanikne pri 3), `hovers` (nad koľkými miestami hráč dielik podržal), `board` (obsadené bunky ako 64-bitová maska), `tray` (dieliky v ponuke, -1 = použitý), `perfect`, `triple`, `broke`, `dealt`, `over`, `record`, `think` (ms od poslednej akcie po zdvihnutie), `drag` (ms ťahania), `duration` (ms partie alebo otvoreného menu), `value`, `info`.
- **Udalosti:** `session`, `player`, `player_new`, `player_switch`, `players_open`, `players_close`, `game_start`, `game_resume`, `daily_review`, `deal` (nová trojica), `pick`, `put_back`, `move`, `hint`, `undo`, `undo_denied`, `game_over`, `switch` (opustená partia), `menu_open`, `menu_close`, `help_open` (`value` 1 = prvýkrát), `help_close`, `theme`, `goal`, `sound`, `hints`, `share`, `goal_star`, `pause`, `resume`, `back` (tlačidlo Späť), `quit` (zatvorenie aplikácie).
- **Koniec relácie:** keď systém aplikáciu v pozadí zabije, nič sa nezapíše. Za koniec relácie ber posledný `pause` alebo poslednú udalosť. Mená sú v jednom zariadení jedinečné, napísanie existujúceho mena na neho prepne. Hráčov z rôznych zariadení rozlíši `install`.
- **Čo sa z toho dá vyčítať:** dĺžka relácií a partií, počet ťahov a skóre podľa režimu a hráča, čas premýšľania a ťahania, ako plná je doska pred koncom, používanie Undo a nápovedy, obľúbenosť režimov, tém a cieľov, Perfect Trio a tri smery naraz, opustené partie a zdieľanie Daily.

## Ladenie obťažnosti

Veľkosti dielikov a miera pomoci sú v `Assets/Game/Core/Generator.cs`. Zmenu over simuláciou:

```csharp
Pointless.Editor.Trial.Run(100, 0.3f, Pointless.Core.Mode.Flow)
```

Prvé číslo je počet partií, druhé podiel náhodných ťahov bota (0 = vždy najlepší ťah, 1 = úplne náhodný).

Každý riadok porovnáva 100 partií na režim s rovnakými seedmi a rovnakým botom. Simulácia končí najneskôr pri 4 000 ťahoch.

| Podiel náhodných ťahov | Medián Arcade | Medián Flow | Línie na ťah Arcade | Línie na ťah Flow | Flow pri strope 4 000 |
|---|---:|---:|---:|---:|---:|
| 0 % | 101 | 4 000 | 0,45 | 0,32 | 96 zo 100 |
| 30 % | 59 | 1 298 | 0,40 | 0,30 | 14 zo 100 |
| 60 % | 35 | 221 | 0,34 | 0,26 | 0 zo 100 |
| 100 % | 19 | 50 | 0,23 | 0,17 | 0 zo 100 |

Limit nedosiahla žiadna partia Arcade. Pri 0 % je medián Flow ohraničený stropom simulácie. Pri 60 % a 100 % je medián Flow viac ako dvojnásobný oproti Arcade.

## Vyskúšanie Flow v Unity

Otvor scénu `Assets/Game/Game.unity`, stlač Play a v hornom menu zvoľ **Pointless → Start Flow in Play Mode**. Tento príkaz začne novú partiu. Žiarovka pod otáznikom zapína alebo vypína ukážku ťahu po chvíli nečinnosti. Voľba sa zapamätá aj po reštarte. Pri obnovení uloženej partie sa história Undo začína odznova.

`Pointless.Editor.Trial.CheckFlow()` zopakuje 15 ťahov, ich vrátenie až na začiatok a prehratie s rovnakými ponukami. Overí aj pokračovanie komba po najmenej piatich ťahoch bez čistenia.

[Ukážka zapnutej nápovedy vo Flow](flow-hint.png)
