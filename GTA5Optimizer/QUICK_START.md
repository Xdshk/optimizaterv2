# GTA5 Optimizer - Быстрый старт

## 🚀 Запуск приложения

### Вариант 1: Автоматическая сборка (рекомендуется)

1. Убедитесь, что установлен **.NET 8 Desktop Runtime**
   - Скачать: https://dotnet.microsoft.com/download/dotnet/8.0

2. Запустите от имени администратора:
```cmd
build-windows.bat
```

3. После сборки запустите:
```cmd
GTA5Optimizer\publish\GTA5Optimizer.UI.exe
```

### Вариант 2: Вручную

```cmd
REM Восстановить зависимости
dotnet restore GTA5Optimizer.sln

REM Собрать решение
dotnet build GTA5Optimizer.sln -c Release --no-restore

REM Опубликовать приложение
dotnet publish GTA5Optimizer\src\GTA5Optimizer.UI\GTA5Optimizer.UI.csproj -c Release -r win-x64 --self-contained true -o GTA5Optimizer\publish --no-restore
```

## ⚠️ Важные требования

1. **Запускайте от имени администратора** (обязательно!)
2. **.NET 8 Desktop Runtime** должен быть установлен
3. **Antivirus** может заблокировать изменение процессов - добавьте исключение

## 📋 Структура проекта

```
GTA5Optimizer/
├── src/
│   ├── GTA5Optimizer.Core/      # Интерфейсы
│   ├── GTA5Optimizer.Models/    # Модели данных
│   ├── GTA5Optimizer.Services/  # Сервисы
│   └── GTA5Optimizer.UI/        # WPF Интерфейс
├── GTA5Optimizer.sln            # Решение
├── build-windows.bat            # Скрипт сборки
├── README.md                    # Документация
└── ARCHITECTURE.md              # Архитектура
```

## 🎮 Использование

1. Выберите профиль оптимизации:
   - **Everyday Mode** - минимальные изменения
   - **RP Mode** - баланс для RP (по умолчанию)
   - **Massive Online** - для 100+ игроков
   - **Maximum FPS** - максимальный FPS

2. Нажмите **OPTIMIZE**

3. Следите за метриками в реальном времени

## 🛡️ Безопасность

- Все изменения создают точку восстановления
- Есть возможность отката (Restore Defaults)
- Защита системных процессов

## 🆘 Решение проблем

### Ошибка "Access Denied"
- Запустите от имени администратора

### Ошибка "dotnet not found"
- Установите .NET 8 Desktop Runtime

### Антивирус блокирует
- Добавьте папку в исключения
- Или временно отключите на время сборки