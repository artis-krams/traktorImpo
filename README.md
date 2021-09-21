# traktorImpo
    Programma izstrādāta lielu datu apjoma produktu sinhronizācijai e-komercijas sistēmā.
## Lietošanas pamācība:
    Ar powershell palīdzību atveram traktorImpo.exe failu un galā norādam vēlamās komandas.

## Pieejamās komandas:
    -u  datubāzes lietotājs
    -pw datubāzes parole
    -s  darubāzes ip adrese
    -dn datubāzes nosaukums
    -f  csv faila fialaNosaukums
    -p  vēlamā nobīde cenai procentos

## Piemērs:
    traktorImpo.exe -s 192.168.0.1 -dn traktors_db -u lietotajs -pw parole123 -f "cenas.csv" -p 5
    šajā piemērā programma atvērs failu cenas.csv, kas atrodas tajā pašā mapē, kur atrodas pati programma un palielinās visas cenas par 5%.

## Papildus pieraksti:
    Cenu failam jābūt .csv formātā.
    Csv faila pirmajā rindā jābūt norādītiem trīs kolonnu nosaukumiem:
        Part,Description,Price
    Gadījumā ja kolonnu skaits atšķiras, liekajām kolonnām arī jābut nosaukumiem pirmajā rindā.
