# Перестройка мода: Minecraft как персонаж внутри R.E.P.O. (архитектура host/guest)

Документ описывает, **что** и **в каком порядке** мы меняем. Образец — [PeakCraft](https://github.com/aeironnsarmiento/PeakCraft)
(тот же приём, что и SkyCraft: Minecraft внутри PEAK/Skyrim).

## 1. Что не так с текущей версией

Сейчас мод работает в режиме **«наблюдателя»**: R.E.P.O. владеет телом, а Minecraft — это
картинка на экране, камера которой следует за камерой R.E.P.O.

Нужно наоборот:

| | Сейчас (наблюдатель) | Нужно (host/guest) |
|---|---|---|
| Тело, физика, прыжок, спринт, крадущийся шаг | R.E.P.O. | **Minecraft** |
| Камера | R.E.P.O., Minecraft повторяет | **Minecraft** (1-е лицо и F5), R.E.P.O. повторяет |
| HUD, хотбар, инвентарь, чат, меню | R.E.P.O. | **Оба**: Minecraft рисует своё поверх картинки, R.E.P.O. — свои меню |
| Мир, предметы, враги, урон | R.E.P.O. | R.E.P.O. (без изменений) |
| Взаимодействие с объектами | клавиши R.E.P.O. | **Из тела Minecraft**, одной выделенной клавишей |

## 2. Термины

* **Guest (гость)** — скрытый Minecraft-клиент. Владеет физикой, инвентарём, HUD, экранами.
* **Host (хозяин)** — R.E.P.O. Владеет миром, картинкой, окном, клавиатурой, предметами, меню.
* **Host-плагин** — наш BepInEx-мод `MinecraftInRepo`.
* **Link (связь)** — канал между играми + понимание, что вторая сторона жива (heartbeat).
* **Ownership (владение)** — решение один раз за кадр, кто владеет телом/камерой/вводом/HUD.
* **Follower (ведомый)** — персонаж R.E.P.O., пока телом владеет Minecraft: скрыт, не симулируется,
  но каждый кадр переносится в позицию игрока Minecraft, чтобы триггеры, опасности и скрипты
  R.E.P.O. продолжали работать.
* **Overlay** — HUD и экраны Minecraft, наложенные поверх картинки R.E.P.O.
* **Mirror World (зеркальный мир)** — пустой мир Minecraft, в котором стоят только блоки игрока,
  а геометрия R.E.P.O. приходит как коллизии.

## 3. Таблица владения

| Состояние | Движение хоста | Ввод хоста | Камера хоста | HUD хоста | Персонаж хоста виден | Overlay Minecraft |
|---|---|---|---|---|---|---|
| **HostOwns** (нет связи / нет персонажа) | да | да | да | да | да | нет |
| **Handoff** (ждём подтверждения телепорта) | заморожено | нет | да | да | да | нет |
| **GuestOwns** (обычная игра) | нет | нет | нет | нет | нет (Follower) | да |
| **HostMenu** (меню R.E.P.O. открыто) | нет | да | нет | да | нет | да |
| **Cutscene** (загрузка, смерть, скриптовое падение, концовка) | да | нет | да | да | да | да |

Правила:

1. Состояние решается **один раз за кадр, в одной функции**; модули спрашивают «тело следует за
   гостем?» — а не «что сейчас делает хост?».
2. **Потеря связи = HostOwns.** Возврат всех систем в одном переходе и делает падение Minecraft
   безопасным: вы просто снова играете в R.E.P.O.
3. Рукопожатие телепорта: хост публикует позицию и увеличивает `teleportSeq`, ждёт `teleportAck`,
   и только потом скрывает персонажа. Без этого тело «прыгает» туда, где игрок Minecraft оказался.

## 4. Отличия от PeakCraft (наши упрощения)

| У PeakCraft | У нас | Почему |
|---|---|---|
| Общая память Windows (`Local\SkyCraft_v1`, ~191 МБ), seqlock-слоты, ring-буферы | Оставляем loopback TCP с построчным JSON | Канал уже работает и отлажен; объёмы на порядок меньше — мы не передаём альфа-кадры и геометрию |
| Overlay как RGBA-кадры через triple buffer | Захват окна Minecraft (как сейчас) + **хрома-маска по цвету неба** | Дешевле в реализации; платим отсутствием попиксельной прозрачности (см. ограничения) |
| Коллизии: точные треугольники (Havok) или перестроение поверхностей из рейкастов | **Воксельная сетка** (шаг 0.5 блока) → невидимые барьер-блоки в зеркальном мире | Даёт «честную» физику Minecraft (края, выступы, стены) ценой детализации; кода на порядок меньше |
| Minecraft 26.x, Java 25, FFM (`--enable-native-access`) | Остаёмся на **1.21.1 / Java 21** | Раз нет общей памяти, FFM не нужен; у пользователя всё уже установлено |
| Пять патч-классов на шесть методов | Ожидаем 4–5 патчей (см. §6) | R.E.P.O. — Unity+BepInEx, funnel-методы находим по `docs/REPO-NOTES.md` |

## 5. Что делает Minecraft (guest), что делает R.E.P.O. (host)

**Guest (Minecraft, Fabric-мод):**

* держит игрока в пустом зеркальном мире: физика, гравитация, прыжок, спринт, крадущийся шаг,
  полёт в креативе, урон, смерть, инвентарь, хотбар, чат, экраны;
* принимает от хоста: ввод (клавиши/мышь/текст/курсор), коллизии (воксели), урон, команды
  (телепорт, «отпустить всё», «открыто меню хоста»), предметы из R.E.P.O.;
* отдаёт хосту: позицию последних двух тиков + время (для интерполяции), yaw/pitch, высоту глаз,
  режим камеры и дистанцию (F5), FOV, здоровье, смерть/возрождение, состояние инвентаря,
  «экран открыт/закрыт»;
* применяет коллизии как барьер-блоки: хост говорит «тут твёрдо» — гость ставит невидимый блок
  (через интегрированный сервер, `setBlock`), и Minecraft сам считает физику.

**Host (R.E.P.O., плагин MinecraftInRepo):**

* рейкастит геометрию вокруг игрока и шлёт воксели;
* пишет позицию гостя в камеру и в Follower'а;
* перехватывает ввод игры и отключает её собственное управление, когда телом владеет гость;
* отдаёт гостю ввод и команды, принимает позу/здоровье/события;
* одна выделенная клавиша взаимодействия (G): включает нативное «interact» R.E.P.O. с камерой
  гостя, рисует свою подсказку, а подобранные ценности уходят в Minecraft как предметы;
* урон: перехватывает единую функцию опасности и превращает её в событие «hurt» для гостя
  (не чаще раза в 0.5 с); смерть гостя запускает смерть R.E.P.O.;
* рисует overlay и подсказку; скрывает HUD R.E.P.O., пока телом владеет гость.

## 6. Порядок работ (каждая фаза играбельна сама по себе)

| # | Фаза | Готово, когда |
|---|---|---|
| **0** | Разведка: `scripts/dump-repo-api.ps1` → `docs/REPO-NOTES.md`, таблица точек патча (ввод, движение, камера, опасность, смерть, порядок обновления кадров) | **Готово**: `docs/REPO-PATCH-POINTS.md` |
| **1** | Связь v2 + ownership: новые сообщения протокола, машина состояний, heartbeat, «фейковый гость» (`tools/fake_guest.py`) для проверки | **Готово**: `docs/PROTOCOL-V2.md`, 33 теста в `tests/HostTests` |
| **2** | Координаты + коллизии + Follower: масштаб, оси, yaw, высота ног; экспорт вокселей; персонаж R.E.P.O. скрыт и следует за гостем | **Код готов** (`CollisionExport`, `BodyFollower`): проверяется с фейковым гостем; точная высота ног калибруется по логу `feet=` |
| **3** | Камера + ввод: запись позы в камеру R.E.P.O. после её собственного обновления, проброс клавиш/мыши, отключение управления R.E.P.O. | **Хост готов** (`CameraDriver`, `InputGate`, `HealthBridge`); осталась гостевая часть (Fabric): приём ввода и барьер-блоки |
| **4** | Overlay + HUD: захват окна с маской по цвету неба, скрытие HUD R.E.P.O., курсор в экранах Minecraft | Видны хотбар, сердечки, инвентарь, чат, меню |
| **5** | Взаимодействие: клавиша G, подсказка, подбор ценностей → предметы в инвентаре Minecraft, меню R.E.P.O. (грузовик, терминал, магазин) из тела Minecraft | Можно подобрать ценность и купить улучшение, не выходя из Minecraft |
| **6** | Урон и смерть: опасности R.E.P.O. → сердечки Minecraft, смерть гостя → смерть R.E.P.O., автоподнятие по «Respawn» | Выживание согласовано; в креативе опасности просто выключены |
| **7** | Полировка: паузы (Esc/O), катсцены, логи по одной строке в секунду, установка в чистый профиль, честный README | Играется как игра |

## 7. Клавиши (итоговая раскладка)

| Клавиша | Действие |
|---|---|
| `W/A/S/D`, мышь, `Space`, `Shift`, `Ctrl`, `E`, `Q`, `1–9`, `T`, `F5` | Minecraft (всё, что не занято ниже) |
| `G` | Взаимодействие с R.E.P.O.: взять/использовать предмет, открыть дверь, терминал, магазин |
| `Esc` | Меню паузы R.E.P.O. (если открыт экран Minecraft — закрывает его) |
| `O` | Пауза/настройки Minecraft |
| `F6/F7/F8/F9` | Служебные: режим камеры, калибровка, overlay, маршрутизация TNT (остаются) |

## 8. Что нужно от игрока

1. Запустить `.\scripts\dump-repo-api.ps1` и прислать `docs/REPO-NOTES.md` (это список типов и
   методов `Assembly-CSharp.dll`, без кода игры) — без него патчить ввод и камеру нельзя.
2. Одобрить порядок фаз (и можно сразу сказать, что важнее — движение, взаимодействие или картинка).

## 9. Заранее известные ограничения

* Одиночная игра (singleplayer). Мультиплеер не поддерживается: мод бездействует в сетевой сессии.
* Воксельные коллизии: предметы тоньше шага сетки (0.5 блока) проходятся насквозь, острые углы
  скругляются, лестница из мелких выступов может быть проходимой.
* Overlay через захват окна: прозрачность получается маской по цвету неба (нужен ресурс-пак с
  однотонным небом), попиксельной альфы не будет; блоки Minecraft не освещаются светом R.E.P.O.
  и не перекрываются геометрией R.E.P.O. по глубине — это цена отказа от общей памяти.
* Пока телом владеет Minecraft, инвентарь и меню R.E.P.O. доступны только через клавишу `G` и `Esc`.
* Minecraft запускается скрытым окном; переключение окна мышью не требуется и не поддерживается.

## Session of 2026-10-05 (3): phases 0, 2 and 3 of the host side

The user ran the recon tool twice at the PC with the game installed, which
unblocked everything below.

### Phase 0: the notes file and the patch-point table

`docs/REPO-PATCH-POINTS.md` - the concern / class / member / why table the
porting guide asks for, written from the two dumps:

* update order: `FixedUpdate` (physics) → `Update` (input, camera aim) →
  `LateUpdate` (camera smoothing) → `onBeforeRender` (**our pump**). Writing the
  camera and zeroing the input from the render pump satisfies "after the host's
  own camera update" without a single Harmony patch.
* input gate: zero `PlayerController.InputDirection/InputDirectionRaw/sprinting/
  Crouching/Crawling/Sliding/moving/JumpInputBuffer` every frame;
* follower: `PlayerController.Kinematic(bool)`, move `PlayerAvatar.transform`,
  force `CollisionGrounded`, hide via `PlayerAvatarVisuals.localVisibility` +
  `ApplyLocalVisibilityBody()` (fallback: child renderers);
* camera: `Camera.main.transform` + `fieldOfView`;
* interact: `ToolController.InteractInput` from `G`;
* damage: mirror `PlayerHealth.health` instead of patching `Hurt/4` (arity is all
  the notes give us, and mirroring gets the same result);
* cutscene: `GameDirector.currentState`/`DisableInput`, the `RunManager` flags,
  `PlayerTumble.isTumbling`, `MenuManager.currentMenuPage`.

**Decision: no Harmony patches at all.** Every game member is reached through
`RepoApi` (reflection by name, cached, with a once-per-session "host API check"
line listing anything missing). That keeps the plugin compiling in stub mode,
keeps CI honest, and makes a game update degrade one feature instead of breaking
the game.

### Phases 2 and 3 on the host

* `Host/CollisionExport.cs` - half-block voxel regions streamed from ray casts.
  Per column: cast down, take the hit as the top of a solid span, then cast again
  from just inside it with `Physics.queriesHitBackfaces = true` to find the
  underside, and fill the cells between. Budgeted at 2.5 ms a frame, nearest
  region first, refreshed every 4 s near and 30 s far, and the epoch bumps
  whenever the region under the player changes. Scheduling lives in the pure
  `Host/RegionScheduler.cs` and is unit tested.
* `Host/BodyFollower.cs` - take/give back the body, remembering exactly which
  renderers it hid.
* `Host/CameraDriver.cs` - eye position from the interpolated feet plus the
  guest's eye height, yaw/pitch converted through the same alignment rotation the
  coordinate map uses, FOV copied.
* `Host/InputGate.cs` - the zeroing above, plus the reserved interact key.
* `Host/HealthBridge.cs` - host hazards become one `hurt` event per 0.5 s, the
  guest's health is written back so the host's death flow can only start when the
  guest dies, and a guest death calls `PlayerHealth.Death()`.
* `Host/GuestPose.cs` - `TickTracker` (interpolation alpha from the arrival time
  on the host's own clock, because the two processes do not share one) and
  `HealthScale`.

Green run: all five CI jobs green; `host-tests` covers ownership, the protocol
messages, the region scheduler, tick interpolation and health scaling.
