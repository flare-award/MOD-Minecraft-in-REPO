# Minecraft in R.E.P.O. — инструкция на русском

> **Новичок? Сначала откройте пошаговый туториал: [TUTORIAL-RU.md](TUTORIAL-RU.md)** —
> там от установки до первого взрыва TNT: порядок запуска, калибровка F7,
> подготовка взрывчатки, чеклист и разбор типичных проблем.
> Здесь — справочник: режим соло, установка, сборка, настройки.

Настоящий Minecraft внутри R.E.P.O.: окно Minecraft захватывается и рисуется
поверх игры, камера Minecraft следует за камерой R.E.P.O., а взрывы TNT в
Minecraft подрывают врагов, предметы, ценности и игроков в R.E.P.O.

- `repo-mod/` — плагин BepInEx для R.E.P.O. (`MinecraftInRepo.dll`)
- `mc-mod/` — мод Fabric для Minecraft 1.21.1 (`mcrepo-1.1.0.jar`)
- Связь между ними: локальная петля `127.0.0.1:47621` (никакого интернета).

---

## 1. Режим игры: только одиночная игра (SOLO)

Это основной и единственный поддерживаемый режим, и он включён по умолчанию:

| Настройка | По умолчанию | Что делает |
|---|---|---|
| `General.SinglePlayerOnly` (R.E.P.O., `BepInEx\config\MinecraftInRepo.cfg`) | `true` | Как только R.E.P.O. сообщает, что вы в **сетевой** сессии, мод целиком уходит в режим ожидания |

В сетевой игре мод **ничего не делает**: не двигает камеру Minecraft, не
захватывает окно, не наносит урон. То есть чужая/совместная игра остаётся
чистой ванильной — ничего не рассинхронизируется и никого не «сломает». В
строке статуса слева внизу появится:
`solo-only: multiplayer session detected - mod idle`.

Если очень захотите запустить это в кооперативе — поставьте
`SinglePlayerOnly = false` (только на хосте, см. README, раздел «Multiplayer
notes»), но баги в сети возможны: именно поэтому по умолчанию стоит `true`.

В одиночке всё работает на полную: вы одновременно и хост, и единственный
клиент, поэтому урон от TNT применяется «авторитетно» (как в самой игре).

### Что мод делает для одиночки автоматически

1. **Не даёт Minecraft встать на паузу.** В одиночном мире Minecraft по
   умолчанию ставит паузу, когда окно теряет фокус, — а фокус во время игры
   держит R.E.P.O. Мод сам выключает `pauseOnLostFocus` (то же самое, что
   `F3 + P`) на время, пока ведёт камеру, и возвращает старое значение после.
2. **Двигает «серверного» игрока.** В одиночке Minecraft запускает внутренний
   сервер, и именно он решает, какие чанки тикают. Если он «думает», что вы
   стоите на точке спавна, то зажжённый вдали TNT просто никогда не тикнет и не
   взорвётся. Мод перемещает серверную копию игрока туда, где камера R.E.P.O.,
   — симуляция чанков и взрывы следуют за общим видом.
3. **Выдаёт полёт и бессмертие без читов.** Вместо команды `/gamemode creative`
   (она требует включённых читов) способности выставляются напрямую на
   серверном игроке, так что камера не «проваливается» на землю в выживании.

Отключается всё это в `config/mcrepo-bridge.json`:
`keepRunningWhenUnfocused`, `driveServerPlayerInSingleplayer`,
`requestCreativeMode`.

---

## 2. Установка (готовые файлы)

### R.E.P.O.

1. Поставить [BepInEx 5.4.23.x](https://thunderstore.io/c/repo/p/BepInEx/BepInExPack/)
   (через r2modman/Gale или вручную).
2. Положить `MinecraftInRepo.dll` в `R.E.P.O.\BepInEx\plugins\`.

### Minecraft

1. В лаунчере создать установку **Fabric 1.21.1**.
2. Положить `mcrepo-1.1.0.jar` в папку `mods` этой установки (fabric-api **не**
   нужен).
3. Запустить, в логе увидеть `Bridge listening on 127.0.0.1:47621`.
4. Создать мир (суперплоский/плоский — отличная «воксельная копия» уровней
   R.E.P.O.). Читы включать **не нужно**.

### Порядок запуска

1. Запустить Minecraft, зайти в мир, свернуть (не закрывать!).
2. Запустить R.E.P.O. (моднутый). Слева внизу появится `[Minecraft] linked`.
3. В уровне встать туда, где миры должны совпасть, посмотреть в осмысленном
   направлении и один раз нажать **F7** — калибровка сохранится в
   `BepInEx\config\MinecraftInRepo.cfg`.
4. Поджечь TNT в Minecraft. Смотреть, как R.E.P.O. отвечает.

### Горячие клавиши

| Клавиша | Действие |
|---|---|
| F6 | Вкл/выкл следование камеры Minecraft за R.E.P.O. |
| F7 | Калибровка совмещения миров по текущей позиции/взгляду |
| F8 | Режим оверлея: FullScreen (призрак) → PiP → Off |
| F9 | Вкл/выкл урон от взрывов в R.E.P.O. |

---

## 3. Как собрать моды самому

### Вариант А. Скачать готовую сборку из GitHub Actions (рекомендую)

В репозитории на каждый пуш собираются оба мода. Качать их можно так:

```powershell
# Windows (нужен gh: winget install --id GitHub.cli ; затем gh auth login)
.\scripts\fetch-builds.ps1 -OutDir C:\Mods
# и сразу установить:
.\scripts\fetch-builds.ps1 -RepoGameDir "C:\Program Files (x86)\Steam\steamapps\common\REPO" `
                           -MinecraftDir "$env:APPDATA\.minecraft" -Install
```

```bash
# Linux / macOS
./scripts/fetch-builds.sh --out-dir ./dist
```

То же самое вручную: GitHub → вкладка **Actions** → workflow **build** →
последний успешный запуск → **Artifacts**:

| Артефакт | Что внутри |
|---|---|
| `MinecraftInRepo-gamelibs-build` | `MinecraftInRepo.dll` (собран против настоящих сборок R.E.P.O.) |
| `MinecraftInRepo-stubs-build` | `MinecraftInRepo.dll` (собран против офлайн-заглушек) |
| `mcrepo-fabric-mod` | `mcrepo-1.1.0.jar` |

### Вариант Б. Собрать локально

#### Нужен инструментарий

| Мод | Что нужно |
|---|---|
| R.E.P.O. (`MinecraftInRepo.dll`) | **.NET SDK 8+** — https://dotnet.microsoft.com/download (`dotnet` в PATH) |
| Minecraft (`mcrepo-1.1.0.jar`) | **JDK 25+** (не 21! Fabric Loom 1.18 отказывается запускать Gradle на Java 21; сам мод компилируется в байткод Java 21) — https://adoptium.net/temurin/releases/?version=25 |
| оба | Интернет (NuGet / Maven Fabric) и ~2 ГБ места под декомпиляцию Minecraft |

#### R.E.P.O. — `MinecraftInRepo.dll`

```powershell
# Windows (PowerShell)
.\scripts\build-repo-mod.ps1 -RepoGameDir "C:\Program Files (x86)\Steam\steamapps\common\REPO" -Install
```

```bash
# Linux / macOS
./scripts/build-repo-mod.sh "/path/to/REPO" --install
```

или вручную:

```bash
# 1) против вашей установки игры (рекомендуется)
dotnet build repo-mod/MinecraftInRepo.csproj -c Release -p:RepoGameDir="/path/to/REPO"

# 2) вообще без игры: сначала офлайн-заглушки, потом плагин
dotnet build repo-mod/Stubs/StubAssemblies.csproj -c Release
dotnet build repo-mod/MinecraftInRepo.csproj -c Release

# 3) против NuGet-пакета с боевыми сборками игры (нужен интернет)
dotnet build repo-mod/MinecraftInRepo.csproj -c Release -p:UseGameLibsNuGet=true
```

Результат: `repo-mod/bin/Release/MinecraftInRepo.dll` → скопировать в
`R.E.P.O.\BepInEx\plugins\`.

#### Minecraft — `mcrepo-1.1.0.jar`

```powershell
# Windows
.\scripts\build-mc-mod.ps1 -MinecraftDir "$env:APPDATA\.minecraft" -Install
# при необходимости указать JDK:
.\scripts\build-mc-mod.ps1 -JavaHome "C:\Program Files\Eclipse Adoptium\jdk-25"
```

```bash
# Linux / macOS
./scripts/build-mc-mod.sh --minecraft-dir ~/.minecraft --install
```

или вручную:

```bash
cd mc-mod
./gradlew build          # Windows: gradlew.bat build
# результат: mc-mod/build/libs/mcrepo-1.1.0.jar
```

Первый запуск долгий: Loom скачает Minecraft 1.21.1 и маппинги Mojang и
соберёт декомпилированный Minecraft. Если Gradle ругается на версию Java —
поставьте JDK 25 и укажите его через `JAVA_HOME` / `-JavaHome`.

Результат: `mc-mod/build/libs/mcrepo-1.1.0.jar` → скопировать в папку `mods`
вашей Fabric-установки 1.21.1.

#### Ошибка `Dependency requires at least JVM runtime version 25. This build uses a Java 21 JVM`

Это самый частый случай: Loom 1.18 не запускается на Java 21. Лечится установкой
JDK 25 (Java 21 при этом можно оставить — они не конфликтуют):

```powershell
winget install -e --id EclipseAdoptium.Temurin.25.JDK      # один раз
# дальше в том же окне PowerShell:
$jh = Get-ChildItem "C:\Program Files\Eclipse Adoptium" -Directory -Filter "jdk-25*" | Select-Object -First 1
$env:JAVA_HOME = $jh.FullName
$env:PATH = "$($jh.FullName)\bin;$env:PATH"
java -version        # должно показать 25 или новее
cd mc-mod
.\gradlew build
```

Или одним махом — скрипт сам найдёт JDK 25 (в `JAVA_HOME`, в PATH, в типичных
папках установки), а если не найдёт — предложит поставить Temurin 25 через
winget:

```powershell
..\scripts\build-mc-mod.ps1 -MinecraftDir "$env:APPDATA\.minecraft" -Install
```

Чтобы не прописывать `JAVA_HOME` каждый раз, добавьте строку
`org.gradle.java.home=C:\\Program Files\\Eclipse Adoptium\\jdk-25.0.4.101-hotspot`
в `C:\Users\<вас>\.gradle\gradle.properties`.

---

## 4. Если что-то не работает (одиночная игра)

| Симптом | Что делать |
|---|---|
| Статус `not connected` | Minecraft не запущен / не тот профиль (нужен Fabric 1.21.1 с модом) / не совпадает порт. В `latest.log` Minecraft должна быть строка `Bridge listening on 127.0.0.1:47621`. |
| Оверлей чёрный | Некоторые GPU/оконные менеджеры не отдают внеэкранный GDI-захват OpenGL-окна. Держите окно Minecraft видимым (второй монитор или рядом в оконном режиме) — сработает fallback на BitBlt. |
| Minecraft стоит на паузе | Проверьте, что `keepRunningWhenUnfocused = true` в `config/mcrepo-bridge.json`, или нажмите в Minecraft `F3 + P`, или выставьте `pauseOnLostFocus:false` в `options.txt`. |
| TNT не взрывается | TNT тикает только в симулируемых чанках. Убедитесь, что `driveServerPlayerInSingleplayer = true` (тогда симуляция идёт вокруг камеры), и/или поднимите «Simulation Distance» в настройках видео Minecraft. |
| Вид Minecraft дёргается / откатывается | Не применился необязательный миксин `IgnorePositionCorrectionMixin` (смотрите предупреждения миксинов в `latest.log`). Перекалибруйтесь по F7. |
| Миры совмещены криво | Калибруйтесь (F7), глядя вдоль ясного горизонтального направления, а не вверх/вниз. |
| Нет урона по врагам | Нажмите F9 (урон мог быть выключен) и проверьте, что уровень уже сгенерирован (в меню/загрузке взрывы игнорируются). |
| `Dependency requires at least JVM runtime version 25` при сборке | Нужен JDK 25 (не 21) для запуска Gradle — см. раздел 3, «Ошибка ...version 25». |
| Мод «молчит» в кооперативе | Это и задумано: `SinglePlayerOnly = true`. Для работы в сети поставьте `false` и будьте хостом. |

---

## 5. Настройки

- R.E.P.O.: `BepInEx\config\MinecraftInRepo.cfg` — разделы
  `[General]`, `[Net]`, `[Overlay]`, `[Camera]`, `[Map]`, `[Blast]`
  (полная таблица в английском `README.md`).
- Minecraft: `config/mcrepo-bridge.json` — `enabled`, `port`, `bind`,
  `applyCamera`, `broadcastExplosions`, `syncFov`, `hideHud`,
  `requestCreativeMode`, `keepRunningWhenUnfocused`,
  `driveServerPlayerInSingleplayer`.

Порт с двух сторон должен совпадать (`Net.Port` в R.E.P.O. = `port` в
Minecraft, по умолчанию `47621`).
