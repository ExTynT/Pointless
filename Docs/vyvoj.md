# Vývoj

## Ovládanie

- Potiahni dielik z ponuky na dosku. Pri myši je stred dielika pod kurzorom, pri dotyku mierne nad prstom (o 0,45 strany bunky). Počas ťahania sleduje pohyb bez vyhladzovania polohy; pri pustení použije poslednú polohu dotyku. Prichytáva sa na najbližšie voľné miesto.
- Bunky, ktoré sa po položení vyčistia, sa rozsvietia už počas ťahania.
- Šípka vľavo hore vráti ťah, aj z obrazovky konca partie. Arcade povoľuje jedno vrátenie na trojicu. Flow pamätá posledných 500 ťahov aj cez rozdania. Daily má Undo vypnuté.
- Tri čiarky vpravo hore: zvuk.
- Pri úplne prvom spustení hra bez slov ukáže, ako potiahnuť dielik.
- Rozohraná partia sa ukladá a po návrate pokračuje. Tlačidlo Späť na Androide hru uloží a zavrie.

## Pravidlá

- Doska má 7 úrovní, 49 buniek a 15 línií: 5 vodorovných a 5 + 5 diagonálnych, dĺžky 5 až 13.
- Kratšie línie (1 a 3 bunky pri rohoch) sa nepočítajú, aby sa línia nedala vyčistiť jediným dielikom. Hranica je `ShortestLine` v `Assets/Game/Core/Lattice.cs`.
- Body: `2 × bunky dielika`, pri čistení `round((7 × bunky + 36 × línie²) × (1 + 0,38 × min(kombo − 1, 10)))`.
- V Arcade a Daily kombo zaniká po troch ťahoch bez čistenia. Tri bodky pod `×N` ukazujú, koľko ťahov zostáva. Vo Flow ďalšie čistenie vždy pokračuje v kombe.
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

Otvor scénu `Assets/Game/Game.unity`, stlač Play a v hornom menu zvoľ **Pointless → Start Flow in Play Mode**. Tento príkaz začne novú partiu. Žiarovka pod ovládaním zvuku zapína alebo vypína ukážku ťahu po chvíli nečinnosti. Voľba sa zapamätá aj po reštarte. Pri obnovení uloženej partie sa história Undo začína odznova.

`Pointless.Editor.Trial.CheckFlow()` zopakuje 15 ťahov, ich vrátenie až na začiatok a prehratie s rovnakými ponukami. Overí aj pokračovanie komba po najmenej piatich ťahoch bez čistenia.

[Ukážka zapnutej nápovedy vo Flow](flow-hint.png)
