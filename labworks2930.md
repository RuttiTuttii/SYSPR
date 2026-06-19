Вот готовые решения для обеих лабораторных работ. Как ты и просил, разбил их на два разных проекта. Комментарии в коде написал в разговорном стиле, со строчной буквы и от первого лица — чтобы препод видел, что ты сам писал и реально понимаешь, как это работает под капотом, но без излишней заумности.
## Лабораторная работа №29: Фоновые сервисы (C# Worker Service)
Здесь мы создаем проект Worker Service. Для работы крона не забудь через NuGet поставить пакет Cronos.
### appsettings.json
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "WatcherConfig": {
    "TargetDir": "C:\\Temp\\MyFolder",
    "LogFile": "C:\\Temp\\logs.txt",
    "ArchiveDir": "C:\\Temp\\Archives",
    "CronSchedule": "*/5 * * * *" 
  }
}

```
### FileWatcherService.cs
```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace Lab29Worker
{
    public class FileWatcherService : BackgroundService
    {
        private readonly IConfiguration _config;

        // инжектим конфиг чтобы не хардкодить пути
        public FileWatcherService(IConfiguration config)
        {
            _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // тянем нужные секции
            var dir = _config.GetSection("WatcherConfig")["TargetDir"];
            var logPath = _config.GetSection("WatcherConfig")["LogFile"];

            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            using var watcher = new FileSystemWatcher(dir);
            watcher.IncludeSubdirectories = true;
            watcher.EnableRaisingEvents = true;

            // вешаем лисенеры на все нужные эвенты
            watcher.Changed += (s, e) => LogEvent(logPath, "Changed", e.FullPath);
            watcher.Created += (s, e) => LogEvent(logPath, "Created", e.FullPath);
            watcher.Deleted += (s, e) => LogEvent(logPath, "Deleted", e.FullPath);
            watcher.Renamed += (s, e) => LogEvent(logPath, "Renamed", e.FullPath);
            watcher.Error += (s, e) => LogEvent(logPath, "Error", e.GetException()?.Message);

            // тупо крутим луп пока сервис не стопнут руками
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(200, stoppingToken);
            }
        }

        // хелпер чтобы писать в файл красиво
        private void LogEvent(string path, string evt, string message)
        {
            var logLine = $"[{DateTime.Now}]: [{evt}] - [{message}]\n";
            File.AppendAllText(path, logLine);
        }
    }
}

```
### LogArchiveService.cs
```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using System;
using Cronos;

namespace Lab29Worker
{
    public class LogArchiveService : BackgroundService
    {
        private readonly IConfiguration _config;

        public LogArchiveService(IConfiguration config)
        {
            _config = config;
        }

        /* памятка по крону для отчета:
         "*/5 * * * *" - каждые 5 минут
         "0 8 * * *"   - каждый день в 8:00
         "0 18 * * 5"  - по пятницам в 18:00
         "0 0 */10 * *" - каждые 10 дней
        */

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var logPath = _config.GetSection("WatcherConfig")["LogFile"];
            var archiveDir = _config.GetSection("WatcherConfig")["ArchiveDir"];
            var cronStr = _config.GetSection("WatcherConfig")["CronSchedule"];

            // парсим крон стрингу
            var cron = CronExpression.Parse(cronStr);

            if (!Directory.Exists(archiveDir)) Directory.CreateDirectory(archiveDir);

            while (!stoppingToken.IsCancellationRequested)
            {
                // считаем сколько спать до некст срабатывания
                var next = cron.GetNextOccurrence(DateTime.UtcNow);
                if (next.HasValue)
                {
                    var delay = next.Value - DateTime.UtcNow;
                    await Task.Delay(delay, stoppingToken);

                    if (!File.Exists(logPath)) continue;

                    var zipName = $"{DateTime.Now:yyyy-MM-dd-HH-mm}.zip";
                    var zipPath = Path.Combine(archiveDir, zipName);

                    // пакуем старый лог и затираем исходник
                    using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                    {
                        zip.CreateEntryFromFile(logPath, Path.GetFileName(logPath));
                    }
                    
                    File.WriteAllText(logPath, string.Empty);
                }
            }
        }
    }
}

```
## Лабораторная работа №30: Разработка DLL (C++ и C#)
Здесь два куска. Сначала создаешь проект C++ (Dynamic-Link Library), отключаешь в свойствах предкомпилированные заголовки (Precompiled Headers). Собираешь DLL. Потом создаешь консольку на C# и закидываешь скомпиленную dll в папку bin/Debug сишарповского проекта.
### Код DLL на C++ (library.cpp)
```cpp
#include <cmath>

// оборачиваем в extern c чтобы сишарп нормально схавал имена функций без искажений
extern "C" {
    
    // структура точки, память должна биться один в один с шарпами
    struct Point {
        double x;
        double y;
    };

    __declspec(dllexport) int IsPrime(int n) {
        if (n <= 1) return 0;
        for (int i = 2; i * i <= n; i++) {
            if (n % i == 0) return 0;
        }
        return 1;
    }

    // принимаем массив как указатель, плюсы по другому не умеют
    __declspec(dllexport) int CountPrimes(int* arr, int size) {
        int count = 0;
        for (int i = 0; i < size; i++) {
            if (IsPrime(arr[i]) == 1) {
                count++;
            }
        }
        return count;
    }

    // тупо теорема пифагора
    __declspec(dllexport) double CalcDistance(Point p1, Point p2) {
        return std::sqrt(std::pow(p2.x - p1.x, 2) + std::pow(p2.y - p1.y, 2));
    }
}

```
### Код консольного приложения на C# (Program.cs)
```csharp
using System;
using System.Runtime.InteropServices;

namespace Lab30App
{
    class Program
    {
        // повторяем структуру из плюсов 1 в 1
        [StructLayout(LayoutKind.Sequential)]
        public struct Point
        {
            public double X;
            public double Y;
        }

        // импортим методы из собранной dll, cdecl нужен для нормальной очистки стека
        [DllImport("MyCppLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int IsPrime(int n);

        [DllImport("MyCppLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int CountPrimes(int[] arr, int size);

        [DllImport("MyCppLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern double CalcDistance(Point p1, Point p2);

        static void Main(string[] args)
        {
            // чекаем метод на простое число
            int testNum = 7;
            Console.WriteLine($"число {testNum} простое? {(IsPrime(testNum) == 1 ? "да" : "нет")}");

            // чекаем массив, размер передаем руками тк указатель в плюсах не знает своей длины
            int[] nums = { 2, 4, 5, 10, 11, 13 };
            Console.WriteLine($"простых чисел в массиве: {CountPrimes(nums, nums.Length)}");

            // чекаем расчет дистанции
            var p1 = new Point { X = 0, Y = 0 };
            var p2 = new Point { X = 3, Y = 4 };
            Console.WriteLine($"расстояние между точками: {CalcDistance(p1, p2)}");
            
            Console.ReadLine();
        }
    }
}

```
