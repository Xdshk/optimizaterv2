# GTA5 Optimizer & Majestic RP

Профессиональное Windows-приложение для оптимизации GTA V и Majestic RP.

## 🚀 Быстрый запуск

### Вариант 1: Автоматическая сборка (Windows)
```cmd
build-windows.bat
```

### Вариант 2: Вручную
```bash
# Восстановить зависимости
dotnet restore GTA5Optimizer.sln

# Собрать решение
dotnet build GTA5Optimizer.sln -c Release -r win-x64

# Опубликовать приложение
dotnet publish GTA5Optimizer\src\GTA5Optimizer.UI\GTA5Optimizer.UI.csproj -c Release -r win-x64 --self-contained true -o publish
```

### Вариант 3: Через Visual Studio
1. Откройте `GTA5Optimizer.sln` в Visual Studio 2022
2. Выберите **Release** + **x64**
3. Правой кнопкой на проекте GTA5Optimizer.UI → **Publish**

## 📋 Требования

- **Windows 10/11 x64**
- **.NET 8 Desktop Runtime** (https://dotnet.microsoft.com/download)
- **Visual Studio 2022** (опционально)
- **Inno Setup 6** (для создания инсталлятора)

## 🛠 Инструкции

- **Минималистичный современный дизайн** с темной темой
- **Автоматическое обнаружение** GTA V и Majestic RP
- **Реальное время мониторинга** FPS, CPU, GPU, RAM, диска
- **Автооптимизация** каждые 30 секунд
- **4 режима оптимизации**:
  - Everyday Mode (повседневная работа)
  - RP Mode (баланс для RP)
  - Massive Online Mode (для массовых ивентов)
  - Maximum FPS Mode (максимальный FPS)

## Целевая аудитория

Игроки GTA V Role Play на серверах с высокой насыщенностью.

## Компоненты

### GTA5Optimizer.Core
Базовые интерфейсы и контракты сервисов.

### GTA5Optimizer.Models
Модели данных: профили, метрики, настройки, логи.

### GTA5Optimizer.Services
Сервисы:
- `SystemOptimizer` - основная оптимизация
- `ProcessManager` - управление процессами
- `MemoryManager` - оптимизация памяти
- `RegistryManager` - работа с реестром
- `GameDetector` - обнаружение игры
- `PerformanceMonitor` - мониторинг
- `AutoOptimizationService` - автооптимизация
- `LoggerService` - логирование

### GTA5Optimizer.UI
WPF интерфейс с плавными анимациями и современным дизайном.

## Требования

- Windows 10/11 (x64)
- .NET 8.0
- Intel Core i5-12400F или аналогичный
- RTX 3060 12GB или аналогичный
- 16 GB RAM
- GTA V на HDD/SSD
- Majestic RP на SSD (рекомендуется)

## Установка

1. Распаковать архив
2. Запустить от имени администратора
3. Выбрать профиль оптимизации
4. Нажать "OPTIMIZE"

## Безопасность

- Все изменения реестра создают точку восстановления
- Есть возможность отката ко всем изменениям
- Защита от закрытия системных процессов

## Лицензия

MIT