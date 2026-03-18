// LabWork16 - Полное решение лабораторной работы №16
// Проект: Console App (.NET 8 или выше)
// NuGet пакеты (добавьте через VS или dotnet add package):
// - System.CommandLine
// - Terminal.Gui
// - (System.Text.Json и System.Security.Cryptography уже встроены)

// Создайте папку LabWork16, файл Program.cs и скопируйте код ниже.
// Данные пользователей хранятся в users.json (рядом с exe) — только логины + SHA-256 хэши паролей (без соли для простоты лабораторной, но сравниваем ТОЛЬКО хэши, никогда не храним пароли в открытом виде).
// Архитектура чистая: отдельный класс UserStorage (работа с файлом), ReadPassword (маскировка ввода), обработчики CLI и TUI.
// При успешном входе — просто "Все окей!" и завершение (как просили).
// TUI запускается только по --ui, без лишних потоков/worker'ов — Terminal.Gui сама рисует в главном потоке (стандартно и стабильно).

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.CommandLine;
using Terminal.Gui;

namespace LabWork16
{
    // =============================================
    // 1. Модель пользователя + хранение в файле
    // =============================================
    public class User
    {
        public string Login { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
    }

    public static class UserStorage
    {
        private static readonly string FilePath = "users.json";
        private static List<User> _users = LoadUsers();

        private static List<User> LoadUsers()
        {
            if (!File.Exists(FilePath))
                return new List<User>();

            string json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<List<User>>(json) ?? new List<User>();
        }

        private static void SaveUsers()
        {
            string json = JsonSerializer.Serialize(_users, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }

        public static bool UserExists(string login) =>
            _users.Exists(u => u.Login.Equals(login, StringComparison.OrdinalIgnoreCase));

        public static bool Authenticate(string login, string password)
        {
            string hash = HashPassword(password);
            return _users.Exists(u => u.Login.Equals(login, StringComparison.OrdinalIgnoreCase) && u.PasswordHash == hash);
        }

        public static bool Register(string login, string password)
        {
            if (UserExists(login))
                return false;

            _users.Add(new User
            {
                Login = login,
                PasswordHash = HashPassword(password)
            });
            SaveUsers();
            return true;
        }

        private static string HashPassword(string password)
        {
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }

    // =============================================
    // 2. Маскированный ввод пароля (по заданию 5.4)
    // =============================================
    public static class PasswordInput
    {
        public static string ReadPassword()
        {
            StringBuilder sb = new StringBuilder();
            ConsoleKeyInfo keyInfo;

            while (true)
            {
                keyInfo = Console.ReadKey(true); // true = не отображать символы

                if (keyInfo.Key == ConsoleKey.Enter)
                    break;

                if (keyInfo.Key == ConsoleKey.Backspace && sb.Length > 0)
                {
                    sb.Length--;                 // удаляем из буфера
                    Console.Write("\b \b");      // стираем последний * на экране
                }
                else if (keyInfo.Key != ConsoleKey.Backspace && keyInfo.Key != ConsoleKey.Enter)
                {
                    sb.Append(keyInfo.KeyChar);
                    Console.Write("*");          // маскируем (символы НЕ отображаются в открытом виде)
                }
            }

            Console.WriteLine(); // перевод строки после Enter
            return sb.ToString();
        }
    }

    // =============================================
    // 3. Интерактивный CLI (без параметров)
    // =============================================
    public static class ConsoleHandler
    {
        public static void InteractiveLogin()
        {
            int attempts = 0;
            string? currentLogin = null;

            while (true)
            {
                attempts = 0;
                currentLogin = null;

                while (attempts < 3)
                {
                    attempts++;

                    if (string.IsNullOrEmpty(currentLogin))
                    {
                        Console.Write("Введите логин: ");
                        currentLogin = Console.ReadLine()?.Trim();
                        if (string.IsNullOrEmpty(currentLogin))
                        {
                            attempts--;
                            continue;
                        }
                    }

                    Console.Write("Введите пароль: ");
                    string pwd = PasswordInput.ReadPassword();

                    if (UserStorage.Authenticate(currentLogin, pwd))
                    {
                        Console.WriteLine("Все окей!");
                        return;
                    }

                    if (UserStorage.UserExists(currentLogin))
                    {
                        Console.WriteLine("Неверный пароль. Повторите ввод пароля.");
                        // оставляем currentLogin — повторяем только пароль
                    }
                    else
                    {
                        Console.WriteLine("Пользователь не существует. Повторите ввод логина и пароля.");
                        currentLogin = null; // сбрасываем — следующий раз спрашиваем логин заново
                    }
                }

                Console.WriteLine("Превышено 3 попытки. Начинаем заново.");
            }
        }

        public static void InteractiveRegister()
        {
            Console.Write("Введите логин: ");
            string? login = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(login) || UserStorage.UserExists(login))
            {
                Console.WriteLine("Логин уже существует или пустой.");
                return;
            }

            Console.Write("Введите пароль: ");
            string pwd = PasswordInput.ReadPassword();

            Console.Write("Подтвердите пароль: ");
            string confirm = PasswordInput.ReadPassword();

            if (pwd != confirm)
            {
                Console.WriteLine("Пароли не совпадают.");
                return;
            }

            if (UserStorage.Register(login, pwd))
                Console.WriteLine("Пользователь успешно зарегистрирован!");
            else
                Console.WriteLine("Ошибка регистрации.");
        }
    }

    // =============================================
    // 4. TUI интерфейс (по заданию 5.5)
    // =============================================
    public static class TuiHandler
    {
        public static void Run()
        {
            Application.Init();
            var top = Application.Top;

            var win = new Window("Авторизация LabWork16")
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            var loginLabel = new Label("Логин:") { X = 1, Y = 1 };
            var loginField = new TextField("") { X = 15, Y = 1, Width = 40 };

            var passLabel = new Label("Пароль:") { X = 1, Y = 3 };
            var passField = new TextField("") { X = 15, Y = 3, Width = 40 };

            var btnLogin = new Button("Войти") { X = 15, Y = 5 };

            btnLogin.Accept += (sender, e) =>
            {
                string login = loginField.Text?.ToString() ?? "";
                string pwd = passField.Text?.ToString() ?? "";

                if (UserStorage.Authenticate(login, pwd))
                {
                    MessageBox.Query("Успех", "Все окей!", "OK");
                    Application.RequestStop();
                }
                else
                {
                    MessageBox.ErrorQuery("Ошибка", "Неверные учетные данные", "OK");
                }
            };

            win.Add(loginLabel, loginField, passLabel, passField, btnLogin);
            top.Add(win);

            Application.Run();
            Application.Shutdown();
        }
    }

    // =============================================
    // 5. Главный файл Program.cs
    // =============================================
    internal class Program
    {
        static void Main(string[] args)
        {
            // Глобальные опции
            var loginOption = new Option<string>("--login", "-l") { Description = "Логин пользователя" };
            var pwdOption = new Option<string>("--pwd", "-p") { Description = "Пароль пользователя" };
            var uiOption = new Option<bool>("--ui") { Description = "Запустить терминальный графический интерфейс (TUI)", Arity = ArgumentArity.ZeroOrOne };

            var rootCommand = new RootCommand("LabWork16 — CLI/TUI приложение авторизации");
            rootCommand.AddOption(loginOption);
            rootCommand.AddOption(pwdOption);
            rootCommand.AddOption(uiOption);

            // Подкоманда register
            var registerCommand = new Command("register", "Регистрация нового пользователя");
            registerCommand.AddOption(loginOption);
            registerCommand.AddOption(pwdOption);

            // Обработчик основной команды (логин)
            rootCommand.SetHandler((string? login, string? pwd, bool ui) =>
            {
                if (ui)
                {
                    TuiHandler.Run();
                    return;
                }

                // Параметры полностью указаны — проверка сразу (без повторов)
                if (!string.IsNullOrEmpty(login) && !string.IsNullOrEmpty(pwd))
                {
                    if (UserStorage.Authenticate(login, pwd))
                        Console.WriteLine("Все окей!");
                    else
                        Console.WriteLine("Неверные учетные данные.");
                    return;
                }

                // Указан только логин — спрашиваем только пароль (один раз)
                if (!string.IsNullOrEmpty(login))
                {
                    Console.Write("Введите пароль: ");
                    string inputPwd = PasswordInput.ReadPassword();
                    if (UserStorage.Authenticate(login, inputPwd))
                        Console.WriteLine("Все окей!");
                    else
                        Console.WriteLine("Неверный пароль.");
                    return;
                }

                // Без параметров — полный интерактив с повторами (по заданию)
                ConsoleHandler.InteractiveLogin();
            }, loginOption, pwdOption, uiOption);

            // Обработчик register
            registerCommand.SetHandler((string? login, string? pwd) =>
            {
                // Параметры указаны — регистрация сразу (без подтверждения и повторов)
                if (!string.IsNullOrEmpty(login) && !string.IsNullOrEmpty(pwd))
                {
                    if (UserStorage.Register(login, pwd))
                        Console.WriteLine("Пользователь успешно зарегистрирован!");
                    else
                        Console.WriteLine("Логин уже существует.");
                    return;
                }

                // Без параметров — интерактивная регистрация
                ConsoleHandler.InteractiveRegister();
            }, loginOption, pwdOption);

            rootCommand.AddCommand(registerCommand);

            // Запуск парсера
            new CommandLineBuilder(rootCommand)
                .UseDefaults()
                .Build()
                .Invoke(args);
        }
    }
}