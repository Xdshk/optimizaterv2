# Инструкция по публикации GTA5 Optimizer

## Быстрый старт

### 1. Сборка portable версии

```bash
cd installer
build-portable.bat
```

Результат: `Output\GTA5Optimizer-v2.0.0-portable.zip`

### 2. Сборка инсталятора (требует Inno Setup 6)

```bash
cd installer
build-installer.bat
```

Результат:
- `Output\GTA5Optimizer-Setup-v2.0.0.exe` — полный инсталятор
- `Output\GTA5Optimizer-v2.0.0-portable.zip` — portable версия

---

## Системные требования для сборки

| Компонент | Версия | Скачать |
|-----------|--------|---------|
| .NET SDK | 8.0+ | https://dotnet.microsoft.com/download |
| Inno Setup | 6.x (опционально) | https://jrsoftware.org/isdl.php |
| 7-Zip | 23+ (опционально) | https://www.7-zip.org/ |

---

## Структура релиза

```
GTA5Optimizer-v2.0.0/
├── GTA5Optimizer-Setup-v2.0.0.exe    ← Инсталятор (для пользователей)
├── GTA5Optimizer-v2.0.0-portable.zip ← Portable (для продвинутых)
├── README.md                          ← Описание
└── CHANGELOG.md                       ← Список изменений
```

---

## Публикация на GitHub

### 1. Создайте релиз

```bash
git tag v2.0.0
git push origin v2.0.0
```

### 2. Создайте Release на GitHub

1. Перейдите в **Releases** → **Create a new release**
2. Tag: `v2.0.0`
3. Title: `GTA5 Optimizer v2.0.0`
4. Описание — см. шаблон ниже
5. Прикрепите файлы:
   - `GTA5Optimizer-Setup-v2.0.0.exe`
   - `GTA5Optimizer-v2.0.0-portable.zip`

### 3. Шаблон описания релиза

```markdown
## GTA5 Optimizer v2.0.0

### Что нового
- Полностью переработанный UI в стиле Fluent Design
- Реальный мониторинг железа через LibreHardwareMonitor
- Автоопределение CPU, GPU, RAM, экрана
- Диагностический центр (12+ проверок)
- Анализ настроек GTA V (settings.xml)
- PC Readiness Score (оценка готовности ПК)
- Бенчмарк с сравнением До/После
- 6 вкладок: Оптимизация, Мониторинг, Диагностика, Бенчмарк, Логи, Настройки

### Скачать
- **Инсталятор:** GTA5Optimizer-Setup-v2.0.0.exe (рекомендуется)
- **Portable:** GTA5Optimizer-v2.0.0-portable.zip

### Системные требования
- Windows 10/11 x64
- .NET 8 Desktop Runtime (входит в инсталятор)
- 4 GB RAM, 100 MB места
- GTA V (Steam/Rockstar/Epic)

### Установка
1. Скачайте инсталятор
2. Запустите от имени администратора
3. Следуйте инструкциям
4. Запустите программу и нажмите "Оптимизировать"
```

---

## Публикация на форумах / Discord

### Короткое описание

```
GTA5 Optimizer v2.0.0 — лучший инструмент для оптимизации GTA V / Majestic RP

Возможности:
• Реальный мониторинг FPS, CPU, GPU, RAM, температур
• Автооптимизация с 4 профилями
• Диагностика 12+ проблем
• Анализ настроек GTA V
• PC Readiness Score
• Бенчмарк с сравнением До/После

Скачать: [ссылка]
```

---

## Проверка перед публикацией

- [ ] Билд проходит без ошибок (`dotnet build -c Release`)
- [ ] Все тесты пройдены
- [ ] Версионирование обновлено (AssemblyInfo, csproj)
- [ ] README.md актуален
- [ ] CHANGELOG.md заполнен
- [ ] Иконка приложения установлена
- [ ] Цифровая подпись (опционально, для инсталятора)

---

## Цифровая подпись инсталятора (опционально)

Для избежания предупреждений Windows SmartScreen:

```bash
signtool sign /f certificate.pfx /p password /t http://timestamp.digicert.com Output\GTA5Optimizer-Setup-v2.0.0.exe
```

Без сертификата пользователи увидят "Неизвестный издатель" — это нормально для open-source проектов.
