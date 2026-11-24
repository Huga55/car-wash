# REFERENCE_SPECIFICATION

Дата: 2026-11-24  
Основной референс: Car Wash: Auto Repair Garage — HAPPY TAPPY STUDIO  
Google Play: https://play.google.com/store/apps/details?id=com.bbg.car.wash.repair.garage.games

## 1. Доказательная база и ограничения

| Источник | Использование | Статус |
|---|---|---|
| Google Play page text/metadata (web fetch) | Название, издатель, описание, заявленные механики, платформы, рейтинг, ads/IAP, update note. | Подтверждено |
| Локальные скриншоты `/tmp/opencode/ref/shot1.png`…`shot8.png` | Файлы обнаружены в `/tmp/opencode/ref/` ранее в сессии, но **не просмотрены**: модель не поддерживает image input. | Не подтверждено визуально |
| Видеозаписи/recordings в репозитории | Поиск по `*.mp4/*.mov/*.webm/*screen*record*` не дал результатов. | Не найдено |
| README/REPORT/docs проекта | Использованы только как контекст текущего проекта, не как доказательство референса. | Контекст |

**Правило маркировки:**
- `[CONFIRMED]` — прямо подтверждено Play Store text/metadata.
- `[INFERRED]` — разумный вывод, но не прямо подтверждённый доступными материалами.
- `[PROPOSED]` — наше предложение для Little Car Workshop, не факт референса.
- `[UNKNOWN]` — недостаточно данных.

## 2. Подтверждённые факты о референсе

| Факт | Значение | Метка |
|---|---|---|
| Название | Car Wash Repair Garage Games | [CONFIRMED] |
| Издатель | HAPPY TAPPY STUDIO | [CONFIRMED] |
| Позиционирование | Satisfying Car ASMR! Wash, Repair & Polish Vehicles | [CONFIRMED] |
| Core activities | Washing and polishing; repairing broken engines, windshields, tires; painting and makeover with colors and designs | [CONFIRMED] |
| Tools | Spray guns and polishers | [CONFIRMED] |
| Vehicles | Various car models from sports cars to trucks | [CONFIRMED] |
| Audio | ASMR sounds for relaxing experience | [CONFIRMED] |
| Visual claim | Beautiful graphics and smooth gameplay | [CONFIRMED] |
| Audience | Fun and easy-to-play for all ages | [CONFIRMED] |
| Platforms/metadata | Android, Windows (Intel-powered) listing; 50M+; Rated for 3+; ads and in-app purchases | [CONFIRMED] |
| Update note | “New & Exciting DJ Truck Level Added” | [CONFIRMED] |

## 3. Механики для реализации

### 3.1 Подтверждённые материалами референса

| Механика | Описание | Метка | Приоритет для нашего проекта |
|---|---|---|---|
| Car wash | Очистка загрязнённой машины | [CONFIRMED] | P0 |
| Polishing | Полировка/блеск после/в процессе ухода | [CONFIRMED] | P0 |
| Foam/spray/water-like tools | Spray guns упомянуты | [CONFIRMED] | P0 |
| Repair broken engines | Ремонт двигателя | [CONFIRMED] | P0 |
| Repair/replace windshields | Лобовое стекло | [CONFIRMED] | P0 |
| Repair/replace tires | Шины/колёса | [CONFIRMED] | P0 |
| Painting/makeover | Покраска и изменение внешнего вида, colors and designs | [CONFIRMED] | P0 |
| Multiple vehicle types | Sports cars to trucks | [CONFIRMED] | P0 |
| ASMR/satisfying feedback | Звуковой/тактильный feel | [CONFIRMED] | P1 |

### 3.2 Предлагаемые улучшения нашей игры

| Предложение | Почему | Метка |
|---|---|---|
| Data-driven JobDef pipeline | Позволит добавлять wash/repair/paint задачи без новых монолитных скриптов. | [PROPOSED] |
| Clear 3D camera focus per zone | Улучшит читаемость на mobile/desktop. | [PROPOSED] |
| Deterministic save/profile format | Надёжное сохранение прогресса и кастомизации. | [PROPOSED] |
| No ads/IAP/paywalls in our build | Бриф проекта: без рекламы, покупок, сети. | [PROPOSED] |
| Accessibility: large UI, Russian text, no timers/fail states | Подходит детской аудитории и текущему продукту. | [PROPOSED] |

### 3.3 Недостаточно информации

| Вопрос | Метка | Что нужно для подтверждения |
|---|---|---|
| Точная последовательность этапов в оригинале | [UNKNOWN] | Визуальный просмотр скриншотов/видео геймплея. |
| Точный HUD/layout/кнопки | [UNKNOWN] | Скриншоты UI. |
| Точная экономика/прогрессия/звёзды/валюты | [UNKNOWN] | Геймплей видео/разбор. |
| Точные категории кастомизации (decals/rims/spoilers/etc.) | [UNKNOWN] | Скриншоты customization. |
| Поведение камеры: fixed/orbit/auto zoom | [UNKNOWN] | Видео gameplay. |
| Наличие fail states/timers | [UNKNOWN] | Геймплей видео. |
| Конкретная реализация DJ Truck level | [UNKNOWN] | Видео/скриншоты обновления. |

## 4. Целевой player experience (для Little Car Workshop)

> Ниже — product target, не утверждение о точной копии референса.

### Core loop

1. Игрок выбирает задачу/машину в гараже. [PROPOSED]
2. Машина появляется в нужной зоне (wash/repair/paint). [PROPOSED]
3. Игрок использует инструмент/жест для выполнения текущего шага. [PROPOSED]
4. Система даёт немедленный визуальный/звуковой feedback. [PROPOSED]
5. По завершении этапа показывается прогресс/награда и открывается следующий шаг/машина. [PROPOSED]

### Mechanic breakdown

| Механика | Player action | Feedback | Completion |
|---|---|---|---|
| Wash | Выбрать струю/пену/губку/смыв/сушку, водить по загрязнённым зонам. | Грязь уходит, пена/вода/блеск появляются, SFX loop. | Все требуемые зоны очищены/высушены. [PROPOSED threshold] |
| Polish | Водить полировкой по чистой поверхности. | Блеск/shine mask растёт. | Shine threshold достигнут. [PROPOSED] |
| Repair engine | Клик/тап по проблеме, выполнить простые действия (открутить/заменить/закрепить). | Деталь меняет состояние, звук инструмента. | Engine part state Fixed. [PROPOSED] |
| Replace tire | Снять/поставить колесо через guided steps. | Колесо снимается/ставится, подсветка слотов. | Tire installed and fixed. [PROPOSED] |
| Replace windshield | Убрать повреждённое, поставить новое. | Разбитое стекло заменяется целым. | Windshield fixed. [PROPOSED] |
| Paint/makeover | Выбрать цвет/дизайн, применить. | Материал/цвет меняется сразу. | Выбор подтверждён. [PROPOSED] |

## 5. Content expectations

| Контент | Минимум для MVP | Метка |
|---|---|---|
| Vehicles | 6-10 разных 3D машин/типов | [PROPOSED] |
| Jobs | 10+ заданий с вариациями wash/repair/paint | [PROPOSED] |
| Tools | 5 wash tools + basic repair tools + paint selector | [PROPOSED] |
| Garage zones | Wash bay, repair bay, paint/custom bay (можно одна комната с фокусами) | [PROPOSED] |
| Languages | Русский UI | [PROPOSED, проектное требование] |

## 6. Non-goals / constraints

- Не копировать оригинальные ассеты, брендинг, тексты, audio, UI art. [PROPOSED/legal]
- Нет ads/IAP/network dependency в нашем build. [PROPOSED, проектное требование]
- Не утверждать неподтверждённые детали референса как факт. [PROPOSED/process]

## 7. Что нужно уточнить перед финальным polish

1. Просмотреть Play Store скриншоты/видео референса человеком или моделью с image input. [UNKNOWN]
2. Зафиксировать финальный список машин и деталей из доступных/приобретаемых 3D assets. [UNKNOWN]
3. Определить допустимую сложность repair interactions для целевой аудитории. [PROPOSED]
4. Подтвердить визуальный стиль: stylized realism vs low-poly cartoon. [PROPOSED]
