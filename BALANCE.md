# Suspensiebalans v0.3.0

Onderzocht op de geïnstalleerde Sprocket 0.2.55.5-binary. Vergelijking van suspension-units per loopwiel, zonder gemeenschappelijke wielen, assen en rupsen. Kosten zijn interne spelwaarden; totale voertuigkosten kunnen extra posten bevatten.

| Part | Massa vóór afmetingsschaling | Kosten bevestiging en veer vóór verdere voertuigberekening |
|---|---|---|
| HVSS | 259,2 kg per bogie met twee wielen: 129,6 kg per wiel | Circa 41,62 per wiel bij referentieschaal |
| Christie | 10 kg bevestiging + 25 kg arm + berekende veermassa M | `0,242996 × (10 + M) + 8,099862 × L`; L is gebouwde veerlengte in meter |
| Torsion bar | 10 kg bevestiging + 25 kg arm + berekende staafmassa M | `0,242996 × 10 + 0,607490 × M + 48,599174` |
| Hydro v0.2.4 | 10 + 25 + 70 = 105 kg | Circa 35,64 bij referentieschaal |
| Hydro v0.3.0 | 25 + 35 + 125 = 185 kg | Circa 316,07 bij referentieschaal |

HVSS heeft één bevestiging van 140 kg, twee armen van 25 kg en twee veren van 34,6 kg. Een HVSS-bogie vergelijken met één hydraulisch loopwiel zou de oude mod onterecht licht doen lijken. Ook per loopwiel was v0.2.4 echter goedkoper en lichter dan deze HVSS-referentie.

Christie en torsion bar hebben geen vaste volledige JSON-massa. De native Build-methoden bepalen de veermassa uit de gebouwde afmetingen. Torsion bar gebruikt `π × (diameter / 2)² × lengte × 16000` met meters. Dat is de aangetroffen gameformule, geen fysische staaldichtheid. Een voorbeeldstaaf van 60 mm bij 2 m levert 90,48 kg: samen circa 125,48 kg en 106,00 kosten bij de genoemde bevestigings- en armreferentie. Dit is een rekenvoorbeeld, geen standaardinstelling van ieder voertuig.

Hydropneumatic is bewust als duurste optie bij vergelijkbare normale afmetingen ingesteld. Extreem grote torsiestaven of afwijkende schalen kunnen de rangorde beïnvloeden. Mount en hydraulische adapter schalen met native geometrie; armen hebben hun eigen lengteschaling. Deze referentietabel vervangt geen vergelijking op hetzelfde tankontwerp.

De hogere massawaarden vertegenwoordigen een conventionele cilinder, accumulator, hydraulische regeling en versterkte bevestiging. De prijsopslag geeft precisieonderdelen en assemblage meer gewicht dan alleen materiaalgebruik. Er is geen openbare vergelijkbare modulemassa gebruikt voor een exact historisch getal. [Horstman InArm](https://www.horstman.horstmangroup.com/en/products/mobility-solutions/horstman-inarm-r) beschrijft juist massabesparing door integratie. [Horstman Hydrogas](https://horstmangroup.com/_Resources/Persistent/3/9/0/0/39001ed4ce57aa00fc244799abe0187f48917011/2024_Hydrogas.pdf) is de referentie voor gasveer en oliedemper, niet voor de gekozen 185 kg.

Native bewijs: VoluteSuspensionSpring.Build RVA 0x20736f0; WheelMount.TrackBuildInternal 0x207a880; TorsionBar.Build 0x20514c0; ChristieCoilSpring.Build 0x204b8b0. Geldig voor GameAssembly SHA256 `18A9A15B5E5F11898ED4DC34FC3E2D4C12950C3B37AC1FA499E8B00592DEDD56`.
