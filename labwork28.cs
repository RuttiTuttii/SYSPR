Отлично, переходим к лабораторной работе №28! Тут у нас тема gRPC (RPC-технологии). Задание просит раскидать CRUD-операции для заказов (Order) с товарами внутри, а также прикрутить фильтрацию по дате и цене.
Давай соберём всё решение в одном месте. Комментарии, как ты и просил, пишу с маленькой буквы в разговорном стиле.
## 1. Структура Proto-файла (orders.proto)
Этот файл закидываем и в проект сервера, и в проект клиента. Он описывает модели данных и методы нашего сервиса.
```protobuf
syntax = "proto3";

option csharp_namespace = "GrpcOrderService";

import "google/protobuf/timestamp.proto";

package orders;

// сервис для работы с заказами
service OrderManager {
  rpc CreateOrder (OrderRequest) returns (OrderReply);
  rpc GetOrder (OrderGetRequest) returns (OrderReply);
  rpc UpdateOrder (OrderRequest) returns (OrderReply);
  rpc DeleteOrder (OrderGetRequest) returns (DeleteReply);
  // метод для фильтрации по дате и прайсу
  rpc FilterOrders (FilterRequest) returns (OrderListReply);
}

// модель товара
message Product {
  string name = 1;
  double price = 2;
}

// модель заказа
message Order {
  int32 id = 1;
  google.protobuf.Timestamp order_date = 2;
  repeated Product products = 3; // repeated — это как List в c#
}

// запросы и ответы
message OrderRequest {
  Order order = 1;
}

message OrderGetRequest {
  int32 id = 1;
}

message OrderReply {
  bool success = 1;
  string message = 2;
  Order order = 3;
}

message DeleteReply {
  bool success = 1;
  string message = 2;
}

message FilterRequest {
  google.protobuf.Timestamp target_date = 1;
  double min_price = 2;
}

message OrderListReply {
  repeated Order orders = 1;
}

```
## 2. Серверная часть (Служба gRPC)
Создай проект по шаблону **gRPC Service**. В файле .csproj убедись, что для прото-файла прописана роль сервера: <Protobuf Include="Protos\orders.proto" GrpcServices="Server" />.
### Реализация сервиса Services/OrderService.cs
Тут мы храним заказы прямо в памяти и обрабатываем все CRUD-запросы.
```csharp
using Grpc.Core;
using Google.Protobuf.WellKnownTypes;
using System.Collections.Concurrent;

namespace GrpcOrderService.Services
{
    public class OrderService : OrderManager.OrderManagerBase
    {
        // фейковая бд в памяти для хранения заказов
        private static readonly ConcurrentDictionary<int32, Order> Orders = new();

        // создание заказа
        public override Task<OrderReply> CreateOrder(OrderRequest request, ServerCallContext context)
        {
            var order = request.Order;
            if (Orders.ContainsKey(order.Id))
            {
                return Task.FromResult(new OrderReply { Success = false, Message = $"заказ с id {order.Id} уже есть" });
            }

            Orders[order.Id] = order;
            return Task.FromResult(new OrderReply { Success = true, Message = "заказ создан", Order = order });
        }

        // чтение заказа
        public override Task<OrderReply> GetOrder(OrderGetRequest request, ServerCallContext context)
        {
            if (Orders.TryGetValue(request.Id, out var order))
            {
                return Task.FromResult(new OrderReply { Success = true, Message = "заказ найден", Order = order });
            }
            return Task.FromResult(new OrderReply { Success = false, Message = "заказ не найден" });
        }

        // обновление заказа
        public override Task<OrderReply> UpdateOrder(OrderRequest request, ServerCallContext context)
        {
            var order = request.Order;
            if (!Orders.ContainsKey(order.Id))
            {
                return Task.FromResult(new OrderReply { Success = false, Message = "нет такого заказа для обновления" });
            }

            Orders[order.Id] = order;
            return Task.FromResult(new OrderReply { Success = true, Message = "заказ обновлен", Order = order });
        }

        // удаление заказа
        public override Task<DeleteReply> DeleteOrder(OrderGetRequest request, ServerCallContext context)
        {
            if (Orders.TryRemove(request.Id, out _))
            {
                return Task.FromResult(new DeleteReply { Success = true, Message = "успешно удалили" });
            }
            return Task.FromResult(new DeleteReply { Success = false, Message = "не смогли удалить, видать нет такого id" });
        }

        // фильтрация по дате и минимальной стоимости
        public override Task<OrderListReply> FilterOrders(FilterRequest request, ServerCallContext context)
        {
            var response = new OrderListReply();
            
            // переводим proto-дату в обычный c# DateTime для сравнения
            var targetDate = request.TargetDate.ToDateTime().Date; 

            foreach (var order in Orders.Values)
            {
                var orderDate = order.OrderDate.ToDateTime().Date;
                
                // считаем общую сумму всех товаров в заказе
                double totalSum = 0;
                foreach (var p in order.Products)
                {
                    totalSum += p.Price;
                }

                // если дата совпадает и сумма заказа не меньше минималки — забираем
                if (orderDate == targetDate && totalSum >= request.MinPrice)
                {
                    response.Orders.Add(order);
                }
            }

            return Task.FromResult(response);
        }
    }
}

```
### Файл Program.cs (Сервер)
```csharp
using GrpcOrderService.Services;

var builder = WebApplication.CreateBuilder(args);

// подключаем сам grpc в систему
builder.Services.AddGrpc();

var app = builder.Build();

// маппим наш класс-сервис для обработки запросов
app.MapGrpcService<OrderService>();

app.MapGet("/", () => "grpc сервер работает! юзай grpc клиент для связи.");

app.Run();

```
## 3. Клиентская часть (Консольное приложение)
Создай обычное **Консольное приложение**. Добавь через NuGet пакеты: Google.Protobuf, Grpc.Net.Client, Grpc.Tools.
В .csproj пропиши роль клиента для прото-файла: <Protobuf Include="Protos\orders.proto" GrpcServices="Client" />.
### Файл Program.cs (Клиент)
```csharp
using System;
using System.Threading.Tasks;
using Grpc.Net.Client;
using Google.Protobuf.WellKnownTypes;
using GrpcOrderService;

namespace GrpcClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // подрубаемся к нашему grpc серверу (порт поставь свой из настроек сервера)
            using var channel = GrpcChannel.ForAddress("https://localhost:7123");
            var client = new OrderManager.OrderManagerClient(channel);

            while (true)
            {
                // простенькое консольное меню для тестов
                Console.WriteLine("\n--- МЕНЮ УПРАВЛЕНИЯ ЗАКАЗАМИ ---");
                Console.WriteLine("1. Создать заказ");
                Console.WriteLine("2. Посмотреть заказ по ID");
                Console.WriteLine("3. Обновить заказ");
                Console.WriteLine("4. Удалить заказ");
                Console.WriteLine("5. Фильтр заказов (Дата и Цена)");
                Console.WriteLine("0. Выход");
                Console.Write("Выбирай пункт: ");

                var choice = Console.ReadLine();
                if (choice == "0") break;

                try
                {
                    switch (choice)
                    {
                        case "1":
                            await CreateOrderWorkflow(client);
                            break;
                        case "2":
                            await GetOrderWorkflow(client);
                            break;
                        case "3":
                            await UpdateOrderWorkflow(client);
                            break;
                        case "4":
                            await DeleteOrderWorkflow(client);
                            break;
                        case "5":
                            await FilterOrdersWorkflow(client);
                            break;
                        default:
                            Console.WriteLine("какая-то левая команда, давай по новой");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"что-то пошло не так: {ex.Message}");
                }
            }
        }

        // сценарий создания заказа
        static async Task CreateOrderWorkflow(OrderManager.OrderManagerClient client)
        {
            Console.Write("введи ID заказа (число): ");
            int id = int.Parse(Console.ReadLine()!);

            var order = new Order { Id = id, OrderDate = Timestamp.FromDateTime(DateTime.UtcNow) };

            // собираем товары в заказ
            while (true)
            {
                Console.Write("название товара (или пустую строку для завершения): ");
                string name = Console.ReadLine()!;
                if (string.IsNullOrWhiteSpace(name)) break;

                Console.Write("цена товара: ");
                double price = double.Parse(Console.ReadLine()!);

                order.Products.Add(new Product { Name = name, Price = price });
            }

            var reply = await client.CreateOrderAsync(new OrderRequest { Order = order });
            Console.WriteLine($"ответ сервера: {reply.Message} (Успех: {reply.Success})");
        }

        // сценарий поиска заказа
        static async Task GetOrderWorkflow(OrderManager.OrderManagerClient client)
        {
            Console.Write("какой ID ищем?: ");
            int id = int.Parse(Console.ReadLine()!);

            var reply = await client.GetOrderAsync(new OrderGetRequest { Id = id });
            Console.WriteLine($"ответ сервера: {reply.Message}");
            if (reply.Success)
            {
                PrintOrder(reply.Order);
            }
        }

        // сценарий обновления заказа
        static async Task UpdateOrderWorkflow(OrderManager.OrderManagerClient client)
        {
            Console.Write("введи ID заказа, который хочешь обновить: ");
            int id = int.Parse(Console.ReadLine()!);

            var order = new Order { Id = id, OrderDate = Timestamp.FromDateTime(DateTime.UtcNow) };

            Console.WriteLine("набираем новый список товаров:");
            while (true)
            {
                Console.Write("название товара (или пусто для конца): ");
                string name = Console.ReadLine()!;
                if (string.IsNullOrWhiteSpace(name)) break;

                Console.Write("цена товара: ");
                double price = double.Parse(Console.ReadLine()!);

                order.Products.Add(new Product { Name = name, Price = price });
            }

            var reply = await client.UpdateOrderAsync(new OrderRequest { Order = order });
            Console.WriteLine($"ответ сервера: {reply.Message}");
        }

        // сценарий удаления
        static async Task DeleteOrderWorkflow(OrderManager.OrderManagerClient client)
        {
            Console.Write("какой ID удаляем?: ");
            int id = int.Parse(Console.ReadLine()!);

            var reply = await client.DeleteOrderAsync(new OrderGetRequest { Id = id });
            Console.WriteLine($"ответ сервера: {reply.Message}");
        }

        // сценарий фильтрации по дате и минимальной стоимости
        static async Task FilterOrdersWorkflow(OrderManager.OrderManagerClient client)
        {
            // для простоты фильтруем по сегодняшней дате
            Console.WriteLine("фильтруем по сегодняшней дате (UTC)...");
            var today = Timestamp.FromDateTime(DateTime.UtcNow);

            Console.Write("введи минимальную общую сумму заказа: ");
            double minPrice = double.Parse(Console.ReadLine()!);

            var reply = await client.FilterOrdersAsync(new FilterRequest { TargetDate = today, MinPrice = minPrice });
            
            Console.WriteLine($"найдено заказов: {reply.Orders.Count}");
            foreach (var order in reply.Orders)
            {
                PrintOrder(order);
            }
        }

        // вспомогательный метод для красивого вывода заказа в консоль
        static void PrintOrder(Order order)
        {
            Console.WriteLine($"-> Заказ №{order.Id} от {order.OrderDate.ToDateTime().ToShortDateString()}");
            foreach (var p in order.Products)
            {
                Console.WriteLine($"   - {p.Name}: {p.Price} руб.");
            }
        }
    }
}

```
## 4. Ответы на контрольные вопросы
### 8.1 В чем заключаются преимущества и недостатки gRPC?
 * **Плюсы (Преимущества):**
   * **Скорость и производительность:** Трафик жмется в бинарный формат через Protocol Buffers, поэтому пакеты весят копейки и летают в разы быстрее обычного JSON в REST API.
   * **Работа на HTTP/2:** Из коробки получаем мультиплексирование (куча запросов через один коннект) и двусторонний стриминг.
   * **Строгая типизация:** Контракт прописан в .proto, так что компилятор сразу надаёт по рукам, если ты передашь не те данные. Строгость — залог здоровья.
   * **Кроссплатформенность:** Написал один .proto файл и сгенерировал код под C#, Java, Python, Go и кучу других языков без костылей.
 * **Минусы (Недостатки):**
   * **Нечитаемость «глазами»:** Так как формат бинарный, просто так открыть вкладку Network в браузере или перехватить трафик и почитать данные не выйдет, нужны специальные тулзы.
   * **Сложнее тестировать:** Нельзя просто взять и закинуть обычный curl запрос или открыть эндпоинт в браузере.
   * **Проблемы с браузерами:** Напрямую из фронтенда (JS/TS в браузере) вызвать gRPC полноценно сложно, приходится городить прокси-прослойки типа gRPC-Web.
### 8.2 Для чего используются файлы .proto?
**Файлы .proto** — это сердце gRPC. Они нужны для:
 1. **Описания контракта взаимодействия:** В них мы на нейтральном языке описываем, какие у нас есть структуры данных (месседжи) и какие методы (сервисы) умеет обрабатывать сервер.
 2. **Генерации кода (Code generation):** Специальные утилиты (protoc или пакет Grpc.Tools в .NET) читают этот файл и автоматически создают базовые классы для сервера и готовые прокси-клиенты для отправки запросов. Нам не приходится вручную писать код сериализации и сетевых вызовов.
