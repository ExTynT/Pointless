# Vývoj

## Ovládanie

- Potiahni dielik z ponuky na dosku. Pri myši je stred dielika pod kurzorom, pri dotyku mierne nad prstom (o 0,45 strany bunky). Počas ťahania sleduje pohyb bez vyhladzovania polohy; pri pustení použije poslednú polohu dotyku. Prichytáva sa na najbližšie voľné miesto.
- Bunky, ktoré sa po položení vyčistia, sa rozsvietia už počas ťahania.
- Šípka vľavo hore: Undo, raz na trojicu. Funguje aj na obrazovke konca partie.
- Tri čiarky vpravo hore: zvuk.
- Pri úplne prvom spustení hra bez slov ukáže, ako potiahnuť dielik.
- Rozohraná partia sa ukladá a po návrate pokračuje. Tlačidlo Späť na Androide hru uloží a zavrie.

## Pravidlá

- Doska má 7 úrovní, 49 buniek a 15 línií: 5 vodorovných a 5 + 5 diagonálnych, dĺžky 5 až 13.
- Kratšie línie (1 a 3 bunky pri rohoch) sa nepočítajú, aby sa línia nedala vyčistiť jediným dielikom. Hranica je `ShortestLine` v `Assets/Game/Core/Lattice.cs`.
- Body: `2 × bunky dielika`, pri čistení `round((7 × bunky + 36 × línie²) × (1 + 0,38 × min(kombo − 1, 10)))`.
- Kombo zaniká po troch ťahoch bez čistenia. Tri bodky pod `×N` ukazujú, koľko ťahov zostáva.
- Perfect Trio: všetky tri dieliky ponuky niečo vyčistili, +200. Stav ukazujú tri značky pod ponukou.
- Koniec: žiadny zo zvyšných dielikov sa nedá položiť.

## Koniec partie

Skóre sa presunie do stredu (zlaté pri novom rekorde), pod ním je rekord, počet vyčistených línií a najvyššie kombo. Ťuknutie kamkoľvek spustí novú partiu.

## Štruktúra

| Priečinok | Obsah |
|---|---|
| `Assets/Game/Core` | Pravidlá bez Unity: mriežka, tvary, generátor, stav hry. Doska je jedno 64-bitové číslo. |
| `Assets/Game/View` | Vykresľovanie, vstup, zvuk, haptika. Celá hra je jedna sieť a jeden shader. |
| `Assets/Game/Editor` | Nastavenie projektu, build, robot na automatické hranie, simulácia partií. |

Po zmene nastavení projektu alebo scény spusti **Pointless > Set Up Project**.

Ťahanie over v režime Play volaním `Pointless.Editor.Robot.CheckDrag()`. Kontrola overuje mierny odstup nad prstom, nulový odstup od kurzora a ich zachovanie pri krokoch zodpovedajúcich 30, 60 a 120 FPS vrátane zmeny smeru; dielik potom vráti do ponuky.

## Ladenie obťažnosti

Veľkosti dielikov a miera pomoci sú v `Assets/Game/Core/Generator.cs`. Zmenu over simuláciou:

```csharp
Pointless.Editor.Trial.Run(100, 0.3f)
```

Prvé číslo je počet partií, druhé podiel náhodných ťahov bota (0 = vždy najlepší ťah, 1 = úplne náhodný).

Východiskový stav (100 partií na bota, medián ťahov do konca partie):

| Podiel náhodných ťahov | Ťahov | Línií na ťah |
|---|---|---|
| 0 | 101 | 0,45 |
| 0,3 | 59 | 0,40 |
| 0,6 | 35 | 0,34 |
| 1 | 19 | 0,23 |
